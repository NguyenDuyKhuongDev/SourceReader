using MessagePack;
using SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile.DataModel;
using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile
{
    public class CacheManager
    {
        public const string CachePath = ".cache/project-index.msgpack";

        /// <summary>
        /// Entry point of cache manager
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public async Task<ProjectIndex> LoadOrScanningAsync(string root, CancellationToken ct)
        {
            if (System.IO.File.Exists(CachePath))
            {
                var cached = await LoadAsync(ct);

                if (cached.Root != root) return await FullScanAsync(root, ct);

                var diff = DetectsChanges(cached);
                if (diff.IsEmpty)
                {
                    Console.WriteLine("no changes in cache");
                    return cached;
                }

                Console.WriteLine($"[cache] patch -{diff.Modified.Count} modified,  {diff.Added.Count} added, {diff.Deleted.Count} deleted");


                await PatchAsync(cached, diff, ct);
                return cached;
            }
            return await FullScanAsync(root, ct);

        }

        private async Task<ProjectIndex> FullScanAsync(string root, CancellationToken ct)
        {
            Console.WriteLine("Starting Full Scann...");
            var index = new ProjectIndex()
            {
                Root = root
            };

            var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "bin","obj",".git","node_modules","dist","build",
              ".next","__pycache__",".cache" };

            var file = Directory
                .GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Split(Path.DirectorySeparatorChar)
                            .Any(p => ignored.Contains(p)))
                .Select(f => new FileInfo(f))
                .Where(f => f.Length is > 500 and < 500_000)
                .ToList();

            // Đọc metadata + regex scan song song
            var importRegex = new Regex(
                @"from\s+['""](.+?)['""]" +
                @"|import\s+['""](.+?)['""]" +
                @"|require\s*\(\s*['""](.+?)['""]" +
                @"|using\s+([\w.]+);" +
                @"|#include\s+[""<](.+?)[>""]",
                RegexOptions.Compiled | RegexOptions.Multiline
            );

            //Phase 1: đọc tất cả các file, tạo FileRecord và RecordImport thô
            var tempImport = new List<(int sourceId, string rawPath)>();
            await CreateFileRecord(index, file, ct, root, importRegex, tempImport);

            //Phase 2: tạo ImportRecord từ tempImport và index OutEdge, InEdge
            CreateImportRecord(tempImport, index);

            //Phase 3: tính degree, priority score cho mỗi file
            CalculateDegree(index);

            await SaveAsync(index, ct);
            Console.WriteLine($"Scan Completed: {index.Files.Count} files with {index.Imports.Count} imports");

            return index;
        }
        #region Helpers method for Phase 1
        private async Task CreateFileRecord(
   ProjectIndex index,
   List<FileInfo> files,
   CancellationToken ct,
   string root,
   Regex regex,
   List<(int sourceId, string rawPath)> tempImports
   )
        {
            int nextFileId = 1;
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                var fileId = nextFileId++;
                var depth = file.FullName.Replace(root, "")
                    .Count(c => c == Path.DirectorySeparatorChar) - 1;

                index.Files[fileId] = new SRFileRecord
                {
                    FileId = fileId,
                    Depth = depth,
                    FileName = file.Name,
                    FilePath = file.FullName,
                    FileSize = file.Length,
                    InDegree = 0,
                    PriorityScore = 0,
                    ModifiedAt = file.LastWriteTimeUtc.Ticks

                };

                index.PathToId[file.FullName] = fileId;
                await CreateRawRecordImport(file, regex, tempImports, fileId, ct);
            }
        }

        private async Task CreateRawRecordImport(
            FileInfo file,
            Regex regex,
            List<(int sourceId, string rawPath)> tempImports,
            int fileId,
            CancellationToken ct)
        {
            var content = await System.IO.File.ReadAllTextAsync(file.FullName, ct);
            //match ~ 1 dòng import text trùng với regex
            //match[0] là bao gồm cả phần text impor nên skip
            // do regex có nhiều loại import mà match sẽ được ánh xạ tới chung tất các các group nên chỉ lấy group nào có sucess = true là được kết quả ra là group[1] hoặc group[2]..vv ~ nội dung đường dẫn raw của import.
            foreach (Match m in regex.Matches(content))
            {
                var raw = m.Groups.Cast<Group>()
                        .Skip(1)
                        .FirstOrDefault(g => g.Success)?.Value;
                if (raw is null) continue;

                tempImports.Add((fileId, raw));
            }
        }

        #endregion
        #region Helpers method for Phase 2

        private void CreateImportRecord(
            List<(int sourceId, string rawPath)> tempImports,
            ProjectIndex index
            )
        {
            var nextImportId = 1;
            foreach (var (sourceId, rawPath) in tempImports)
            {
                var isExternal = !rawPath.StartsWith("./") &&
                                 !rawPath.StartsWith("../");

                int? targetFileId = null;
                if (!isExternal)
                {
                    var sourceDir = Path.GetDirectoryName(index.Files[sourceId].FilePath)!;
                    var resolved = ResolveImport(sourceDir, rawPath, index.PathToId);
                    targetFileId = resolved;
                }

                var importId = nextImportId++;
                index.Imports[importId] = new SRImportRecord
                {
                    ImportId = importId,
                    SourceFileId = sourceId,
                    TargetFileId = targetFileId,
                    RawFilePath = rawPath,
                    IsExternal = isExternal
                };

                BuildInEdge(index, sourceId, targetFileId);
                BuildOutEdge(index, sourceId, targetFileId);
            }
        }

        //OutEgde xác định file source A có các import nào (key:sourceId - value List import id)
        private void BuildOutEdge(
          ProjectIndex index,
          int sourceId,
          int? targetFileId
            )
        {
            if (!index.OutEdge.TryGetValue(sourceId, out var outEdges))
                index.OutEdge[sourceId] = outEdges = [];
            if (targetFileId.HasValue) outEdges.Add(targetFileId.Value);
        }

        //InEdge xác định import A được import bởi các File nào (key: importId - value list file source id)
        private void BuildInEdge(
            ProjectIndex index,
            int sourceId,
            int? targetFileId
            )
        {
            if (!index.InEdge.TryGetValue(targetFileId.Value, out var inEdges))
                index.InEdge[targetFileId.Value] = inEdges = [];
            inEdges.Add(sourceId);
        }
        #endregion
        #region Helpers method for Phase 3 - Calculate Score methods
        private void CalculateDegree(
            ProjectIndex index
            )
        {
            var sizes = index.Files.Values.Select(f => (double)f.FileSize).ToList();
            var mean = sizes.Average();
            var stdDev = Math.Sqrt(sizes.Select(s => Math.Pow(s - mean, 2)).Average());

            foreach (var (id, file) in index.Files)
            {
                var inDegree = index.InEdge.TryGetValue(id, out var inEdges) ? inEdges.Count : 0;
                var score = CalcScore(file, inDegree, mean, stdDev);

                index.Files[id] = file with
                {
                    InDegree = inDegree,
                    PriorityScore = score
                };
            }
        }

        private double CalcScore(SRFileRecord file, int inDegree, double mean, double stdDev)
        {
            var s1 = Math.Min(inDegree * 15, 80);
            var z = stdDev > 0 ? (file.FileSize - mean) / stdDev : 0;
            var s2 = Math.Min(z * 20, 60);
            var s4 = ConfigScore(file.FileName);
            var s3 = Math.Max(0.3, 1.0 - file.Depth * 0.15);
            var s5 = LayerMultiplier(file.FilePath);

            return (double)((s1 + s2 + s4) * s3 * s5);
        }

        private double ConfigScore(string fileName)
        {
            var lower = fileName.ToLower();
            if (lower.Contains("config") || lower.Contains("setting")) return 70;

            var known = new HashSet<string>
            { "package.json","cargo.toml","go.mod","pom.xml",
              "makefile","dockerfile","gemfile","composer.json" };
            if (known.Contains(lower)) return 70;
            return Path.GetExtension(lower) switch
            {
                ".env" => 65,
                ".toml" => 55,
                ".yaml" or ".yml" => 50,
                _ => 0
            };
        }

        private double LayerMultiplier(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path).ToLower();
            if (name is "main" or "index" or "app" or "program" or "server") return 1.5;

            return path.ToLower().Split(Path.DirectorySeparatorChar).Select(p => p switch
            {
                "core" or "domain" or "model" or "models" => 1.3,
                "api" or "controller" or "controllers" => 1.2,
                "service" or "services" or "usecase" => 1.2,
                "test" or "tests" or "spec" or "__tests__" => 0.2,
                "vendor" or "third_party" => 0.1,
                _ => 1.0
            }).FirstOrDefault(m => m != 1.0, 1.0);
        }

        private void RecalcScores(ProjectIndex index)
        {
            var sizes = index.Files.Values.Select(f => (double)f.FileSize).ToList();
            var mean = sizes.Average();
            var stdDev = Math.Sqrt(sizes.Select(s => Math.Pow(s - mean, 2)).Average());

            foreach (var (id, file) in index.Files)
            {
                var inDegree = index.InEdge.TryGetValue(id, out var ins)
                    ? ins.Count : 0;
                index.Files[id] = file with
                {
                    InDegree = inDegree,
                    PriorityScore = CalcScore(file, inDegree, mean, stdDev)
                };
            }
        }

        #endregion

        /// <summary>
        /// Update when detect changes
        /// </summary>
        /// <param name="index"></param>
        /// <param name="diff"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task PatchAsync(
            ProjectIndex index,
            ProjectDiff diff,
            CancellationToken ct)
        {
            foreach (var id in diff.Deleted)
            {
                var path = index.Files[id].FilePath;
                index.Files.Remove(id);
                index.PathToId.Remove(path);
                RemoveFileEdges(index, id);
            }

            var toRescan = diff.Modified
                .Select(id => index.Files[id].FilePath)
                .Concat(diff.Added.Select(id => index.Files[id].FilePath))
                .ToList();

            foreach (var id in diff.Modified) {
                RemoveFileEdges(index, id);
            }

            RecalcScores(index);

            await SaveAsync(index, ct);
        }

        private void RemoveFileEdges(ProjectIndex index, int fileId)
        {
            // Remove out edge
            if (index.OutEdge.TryGetValue(fileId, out var target)) {
                foreach (var t in target)
                    index.InEdge.GetValueOrDefault(t)?.Remove(fileId);
                index.OutEdge.Remove(fileId);
            }

            //Remove in edge
            if (index.InEdge.TryGetValue(fileId, out var sources)) { 
                foreach(var s in sources) 
                    index.OutEdge.GetValueOrDefault(s)?.Remove(fileId);
                index.InEdge.Remove(fileId);
            }

            //Remove import record related to fileId
            var toRemoveImport = index.Imports.Values
                .Where(i => i.SourceFileId == fileId || i.TargetFileId == fileId)
                .Select(i => i.ImportId)
                .ToList();
            foreach (var id in toRemoveImport) index.Imports.Remove(id);
        }

        /// <summary>
        /// detect changes by modified time of file
        /// </summary>
        /// <param name="cached"></param>
        /// <returns></returns>
        private ProjectDiff DetectsChanges(ProjectIndex cached)
        {

        }

        private static int? ResolveImport(string sourceDir, string rawPath, Dictionary<string, int> pathToId)
        {
        }

        private static async Task SaveAsync(ProjectIndex index, CancellationToken ct) { }

        private static async Task<ProjectIndex> LoadAsync(CancellationToken ct = default)
        {
            await using var stream = File.OpenRead(CachePath);
            return await MessagePackSerializer.DeserializeAsync<ProjectIndex>(stream, null, ct);
        }
    }
}
