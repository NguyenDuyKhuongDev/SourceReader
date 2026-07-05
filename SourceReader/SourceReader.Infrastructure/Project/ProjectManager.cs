using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreLanguage;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser;
using SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile;
using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Core.Services.Project
{
    public class ProjectManager
    {
        private readonly AstScanner _scanner;
        private readonly string _rootPath;
        private ProjectIndexManager _cacheManager;
        public ProjectIndex? _index = null; // need set this readonly or private 

        public ProjectManager(AstScanner scanner, string rootPath)
        {
            _scanner = scanner;
            _rootPath = rootPath;
            _cacheManager = new ProjectIndexManager(_rootPath);
        }
        /// <summary>
        /// Load IndexProject for this instance of projectManager , if it have cached index then load it, if file is exist create a new index have no data, other case throw exception.
        ///Project Index - is a data structure of project that contain all file record , import, rootpath ..etc that can be used to manage project..etc
        /// </summary>
        /// <param name="ct"></param>
        public async Task LoadIndexAsync(CancellationToken ct)
        {
            _cacheManager = new ProjectIndexManager(_rootPath);
            try
            {
                _index = await _cacheManager.LoadAsync(ct);
            }
            catch (FileNotFoundException)
            {
                _index = new ProjectIndex(_cacheManager.GetCachePath());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Load Index failed] {ex}");
                throw ;
            }
        }
        /// <summary>
        /// load project from cached or if it's not scanned so scann it (scan in here mean create an index project of project with data of structure like FileRecord, Index Record ..vv) 
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task LoadCachedManagerAsync(CancellationToken ct) => await _cacheManager.LoadOrScanningAsync(ct);

        /// <summary>
        /// Main flow scan files of project in order priority score , skip the file that already scanned. start scann at resumepoint - the closest file to the root that is not scanned yet.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public void StartScanAsync(CancellationToken ct)
        {
            if (_index.Files.Count == 0) throw new InvalidOperationException("Project is empty");

            var orderedFiles = _index.Files.Values
                .Where(f => LanguageResolver.IsSupported(f.FilePath))
                .OrderByDescending(f => f.PriorityScore)
                .ToList();

            var resumePoint = GetResumePoint(orderedFiles);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _scanner.ScanAllAsync(_index, resumePoint, ct);
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

        public Task<AstFileResult?> ParseFileToAst(SRFileRecord file, CancellationToken ct)
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

    }
}
