using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreLanguage;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser;
using SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile;
using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Core.Services.Project
{
    public class ProjectManager : IDisposable
    {
        private readonly AstScanner _scanner = new AstScanner();
        private readonly string _rootPath;
        private readonly ProjectCacheManager _cacheManager;

        public ProjectManager(string rootPath)
        {
            _rootPath = rootPath;
            _cacheManager = new ProjectCacheManager(rootPath);
        }

        /// <summary>
        /// Normal flow scan files of project in order priority score , skip the file that already scanned.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public void StartScanAsync(ProjectIndex index, CancellationToken ct)
        {
            if (index.Files.Count == 0) throw new InvalidOperationException("Project is empty");

            var orderedFiles = index.Files.Values
                .Where(f => LanguageResolver.IsSupported(f.FilePath))
                .OrderByDescending(f => f.PriorityScore)
                .ToList();

            var resumePoint = GetResumePoint(orderedFiles);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _scanner.ScanAllAsync(index, resumePoint, ct);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine(
                            $"[Scan Process Cancled]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                            $"[Scan failed] {ex}");
                }
            });
        }



        public Task<AstFileResult?> QueryFile(SRFileRecord file, CancellationToken ct)
            => _scanner.GetOrParseAsync(file, ct);

        //use binary search to fine the resume point , which is the closest file to the root that is not scanned yet.
        private static int GetResumePoint(List<SRFileRecord> ordered)
        {
            if (ordered.Count == 0) return 0;

            int low = 0, hight = ordered.Count - 1, result = 0;

            while (low <= hight)
            {
                var mid = (low + hight) / 2;
                var file = ordered[mid];

                // isOnDemand file isnot count as scanned because it be parse on demand of user , not folllow by 
                //seuqential so it can cause problem in binary search
                var scannedBackground = file.IsScanned && !file.IsOnDemand;

                if (scannedBackground)
                {
                    result = mid + 1;
                    low = mid + 1;

                }
                else
                {
                    hight = mid - 1;
                }
            }
            return result;
        }

        public void Dispose()
        {
            _scanner.Dispose();
            _cacheManager.Dispose();
        }
    }
}
