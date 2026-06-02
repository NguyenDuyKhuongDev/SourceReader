using MessagePack;
using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;
using File = System.IO.File;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile
{
    // This class have the main purpose is to light scan the project to Calculate the priority score for each file to decide which file should be scan first
    public class ProjectCacheManager
    {
        private readonly string CachePath;
        private readonly string root;
        // Ignore file that have much size and file have too small size
        private const int MaxFileSizeValid = 500000;
        private const int MinFileSizeValid = 500;
        private readonly HashSet<string> ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "bin","obj",".git","node_modules","dist","build",
              ".next","__pycache__",".cache" };
        private readonly Regex importRegex = new Regex(
                @"from\s+['""](.+?)['""]" +
                @"|import\s+['""](.+?)['""]" +
                @"|require\s*\(\s*['""](.+?)['""]" +
                @"|using\s+([\w.]+);" +
                @"|#include\s+[""<](.+?)[>""]",
                RegexOptions.Compiled | RegexOptions.Multiline
            );

        public ProjectCacheManager(string root)
        {
            CachePath = GetCachePath();
            this.root = root;
        }

        public async Task<ProjectIndex> LoadOrScanningAsync(CancellationToken ct)
        {
            if (File.Exists(CachePath))
            {
                var cached = await LoadAsync(ct);

                if (cached.Root != root) return await FullScanAsync(ct);

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
            return await FullScanAsync(ct);

        }

        private async Task<ProjectIndex> FullScanAsync(CancellationToken ct)
        {
            Console.WriteLine("Starting Full Scann...");
            var index = new ProjectIndex()
            {
                Root = root
            };

            // ignore all file contains  ignored pattern in directory and filter size file
            var file = GetFileValid();
            if (file.Count == 0) return index;

            //Phase 1: đọc tất cả các file, tạo FileRecord và RecordImport thô
            var tempImport = new List<(int sourceId, string rawPath)>();
            await CreateFileRecord(index, file, ct, tempImport);

            //Phase 2: tạo ImportRecord từ tempImport và index OutEdge, InEdge
            CreateImportRecord(tempImport, index);

            //Phase 3: tính degree, priority score cho mỗi file
            ScoringFile.CalculateFiles(index);

            await SaveAsync(index, ct);
            Console.WriteLine($"Scan Completed: {index.Files.Count} files with {index.Imports.Count} imports");

            return index;
        }
        #region Helpers method for Phase 1
        private async Task CreateFileRecord(
   ProjectIndex index,
   List<FileInfo> files,
   CancellationToken ct,
   List<(int sourceId, string rawPath)> tempImports
   )
        {
            int nextFileId = 1;
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                var fileId = nextFileId++;
                //remove root path and count the separator chat like"/" and "" to get depth
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
                    // có 1 vấn đề là nếu như để modified ngay lúc tạo raw thì 
                    //khi scan để hoàn thiện file lúc đầu vẫn được tính là đã
                    ////modified
                    ModifiedAt = file.LastWriteTimeUtc.Ticks

                };

                index.PathToId[file.FullName] = fileId;
                await CreateRawRecordImport(file, tempImports, fileId, ct);
            }
        }

        //read file content and find the import statement by regex then add to tempImports
        private async Task CreateRawRecordImport(
            FileInfo file,
            List<(int sourceId, string rawPath)> tempImports,
            int fileId,
            CancellationToken ct)
        {
            //!!! cần tối ưu vì đang đọc full file trong khi nên chỉ đọc khoảng n dòng đầu tiên
            var content = await ReadHeaderFile(file.FullName, ct);
            //match ~ 1 dòng import text trùng với regex
            //match[0] là bao gồm cả phần text impor nên skip
            // do regex có nhiều loại import mà match sẽ được ánh xạ tới chung tất các các group nên chỉ lấy group nào có sucess = true là được kết quả ra là group[1] hoặc group[2]..vv ~ nội dung đường dẫn raw của import.
            foreach (Match m in importRegex.Matches(content))
            {
                var raw = m.Groups.Cast<Group>()
                        .Skip(1)
                        .FirstOrDefault(g => g.Success)?.Value;
                if (raw is null) continue;

                tempImports.Add((fileId, raw));
            }
        }

        /// <summary>
        /// Read n lines of file from the top (n = param maxHeadLine)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="ct"></param>
        /// <param name="maxHeadLine"></param>
        /// <returns></returns>
        private static async Task<string> ReadHeaderFile(
            string path,
            CancellationToken ct,
            int maxHeadLine = 50)
        {
            string? line;
            var sb = new StringBuilder();
            using var reader = new StreamReader(path);

            for (int i = 0; i < maxHeadLine; i++)
            {
                line = await reader.ReadLineAsync(ct);
                if (line is null) break;
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }

        #endregion
        #region Helpers method for Phase 2

        private void CreateImportRecord(
            List<(int sourceId, string rawPath)> tempImports,
            ProjectIndex index,
            int nextImportId = 1
            )
        {
            // in here accept that import may be be wrong type determine external or internal but it doesnt need 100% accuracy , this limit can be acceptable at some point
            foreach (var (sourceId, rawPath) in tempImports)
            {
                //remove file name from file path to get source dir
                //exp: src/main/app.js -> src/main
                var sourceDir = Path.GetDirectoryName(index.Files[sourceId].FilePath)!;

                //đã bao quát được hết các trường hợp chưa?
                var isExternal = !rawPath.StartsWith("./") &&
                                 !rawPath.StartsWith("../");

                int? targetFileId = isExternal ? null : ResolveImport(sourceDir, rawPath, index.PathToId);

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
        private static void BuildOutEdge(
          ProjectIndex index,
          int sourceId,
          int? importFileId
            )
        {
            if (!index.OutEdge.TryGetValue(sourceId, out var listImportId))
                index.OutEdge[sourceId] = listImportId = [];
            if (importFileId.HasValue) listImportId.Add(importFileId.Value);
        }

        //InEdge xác định import A được import bởi các File nào (key: importId - value list file source id)
        private static void BuildInEdge(
            ProjectIndex index,
            int sourceId,
            int? importFileId
            )
        {
            if (!importFileId.HasValue) return;
            if (!index.InEdge.TryGetValue(importFileId.Value, out var listSourceId))
                index.InEdge[importFileId.Value] = listSourceId = [];
            listSourceId.Add(sourceId);
        }
        #endregion

        /// <summary>
        /// Update project index when detect changes
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

            var toReScan = diff.Modified
                .Select(id => index.Files[id].FilePath)
                .Concat(diff.Added)
                .ToList();

            foreach (var id in diff.Modified)
            {
                RemoveFileEdges(index, id);
            }

            await ReScanFileAsync(index, toReScan, ct);

            ScoringFile.RecalcScores(index);

            await SaveAsync(index, ct);
        }

        /// <summary>
        /// remove all in/out edges, import record related to fileId
        /// </summary>
        /// <param name="index"></param>
        /// <param name="fileId"></param> 
        private static void RemoveFileEdges(ProjectIndex index, int fileId)
        {
            // Remove out edge
            if (index.OutEdge.TryGetValue(fileId, out var target))
            {
                foreach (var t in target)
                    index.InEdge.GetValueOrDefault(t)?.Remove(fileId);
                index.OutEdge.Remove(fileId);
            }

            //Remove in edge
            if (index.InEdge.TryGetValue(fileId, out var sources))
            {
                foreach (var s in sources)
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
            var deleted = new ConcurrentBag<int>();
            var modified = new ConcurrentBag<int>();

            Parallel.ForEach(cached.Files, kvp =>
            {
                var (id, file) = kvp;
                if (!File.Exists(file.FilePath)) { deleted.Add(id); return; }
                var mTime = File.GetLastWriteTimeUtc(file.FilePath).Ticks;
                if (mTime != file.ModifiedAt) modified.Add(id);
            });

            var existingPath = cached.PathToId.Keys.ToHashSet();
            var allFilePathInDirValid = GetPathValid();
            var added = allFilePathInDirValid
               .Where(p => !existingPath.Contains(p))
               .ToList();

            return new ProjectDiff(deleted.ToList(), added, modified.ToList());
        }
        /// <summary>
        /// import usually dont have extension so we combine sourcedir + rawpath and popular extension to find this file in project by pathToId dictionary which is path-> fileId , method return fileId of import targetif found or null if not
        /// exp:
        /// import { Auth } from "./auth.service"
        /// rawPath = "./auth.service"
        /// sourceDir = "C:/project/src/controllers"
        /// try:
        /// C:/project/src/controllers/auth.service       → Dont exist
        /// C:/project/src/controllers/auth.service.cs    → exist → return id
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="rawPath"></param>
        /// <param name="pathToId"></param>
        /// <returns></returns>
        private static int? ResolveImport(string sourceDir, string rawPath, Dictionary<string, int> pathToId)
        {
            var extensions = new[] { "", ".cs", ".ts", ".js", ".py", ".go" };
            foreach (var ext in extensions)
            {
                var candidate = Path.GetFullPath(
                    Path.Combine(sourceDir, rawPath + ext)
                    );
                if (pathToId.TryGetValue(candidate, out var id)) return id;
            }
            return null;
        }

        private async Task SaveAsync(ProjectIndex index, CancellationToken ct)
        {
            Directory.CreateDirectory(".cache");
            await using var stream = File.Create(CachePath);
            await MessagePackSerializer.SerializeAsync(stream, index, cancellationToken: ct);
            Console.WriteLine($"[cached] saved - {new FileInfo(CachePath).Length / 1024}kb");
        }

        private async Task<ProjectIndex> LoadAsync(CancellationToken ct = default)
        {
            await using var stream = File.OpenRead(CachePath);
            return await MessagePackSerializer.DeserializeAsync<ProjectIndex>(stream, null, ct);
        }
        /// <summary>
        /// create unique cache path for each project by hash the root path to avoid conflict when multiple project have same name  but different path
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        private string GetCachePath()
        {
            //make an unique id for cache file by it root path and just take 12 char for short name 
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..12];

            //all the project cache file will be store in local app data user folder
            // exmp: C:\Users\Username\AppData\Local\SourceReader\cache
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceReader", "cache");

            Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, $"index-{hash}.msgpack");
        }

        /// <summary>
        /// get File that is not in ignored directory and have size that not too small and too big
        /// </summary>
        /// <returns></returns>
        private List<FileInfo> GetFileValid()
        {
            var file = Directory
                  .GetFiles(root, "*.*", SearchOption.AllDirectories)
                  .Where(f => !f.Split(Path.DirectorySeparatorChar)
                              .Any(p => ignored.Contains(p)))
                  .Select(f => new FileInfo(f))
                  .Where(f => f.Length is > MinFileSizeValid and < MaxFileSizeValid)
                  .ToList();
            return file;
        }

        /// <summary>
        /// get File path that is not in ignored directory and have size that not too small and too big
        /// </summary>
        /// <returns></returns>
        private List<string> GetPathValid()
        {
            var path = Directory
                  .GetFiles(root, "*.*", SearchOption.AllDirectories)
                  .Where(f => !f.Split(Path.DirectorySeparatorChar)
                              .Any(p => ignored.Contains(p)))
                  .ToList();
            return path;
        }
        /// <summary>
        /// Rescan file after detect modified or added, have the same execute logic with phase 1 in full scann but for specific file list add and updated
        /// </summary>
        /// <param name="index"></param>
        /// <param name="pathList"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task ReScanFileAsync(
            ProjectIndex index,
            List<string> pathList,
            CancellationToken ct)
        {
            var nextFileId = index.Files.Keys.DefaultIfEmpty(0).Max() + 1;
            var nextImportId = index.Imports.Keys.DefaultIfEmpty(0).Max() + 1;

            var tempImports = new List<(int sourceId, string rawPath)>();

            foreach (var path in pathList)
            {
                ct.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(path);

                if (!fileInfo.Exists ||
                    fileInfo.Length is
                    (> MaxFileSizeValid or < MinFileSizeValid)
                    ) continue;

                var fileId = index.PathToId.TryGetValue(path, out var exsitingId)
                    ? exsitingId : nextFileId++;

                var depth = path.Replace(root, "")
                    .Count(c => c == Path.DirectorySeparatorChar) - 1;

                index.Files[fileId] = new SRFileRecord
                {
                    FileId = fileId,
                    Depth = depth,
                    FileName = fileInfo.Name,
                    FilePath = path,
                    FileSize = fileInfo.Length,
                    InDegree = 0,
                    PriorityScore = 0,
                    ModifiedAt = fileInfo.LastWriteTimeUtc.Ticks
                };

                index.PathToId[path] = fileId;

                await CreateRawRecordImport(fileInfo, tempImports, fileId, ct);
            }
            CreateImportRecord(tempImports, index, nextImportId);
        }
    }
}
