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
        private readonly AstScanner _scanner = new AstScanner();
        private readonly string _rootPath;
        private readonly ProjectCacheManager _cacheManager;

        public ProjectManager(string rootPath)
        {
            _rootPath = rootPath;
            _cacheManager = new ProjectCacheManager(rootPath);
        }

        public async Task RunAsync(ProjectIndex index, CancellationToken ct)
        {
            var resumePoint = await GetResumePoint(ct);
            // Kick off background scan — không await, không block
            _ = Task.Run(() => _scanner.ScanAllAsync(index, resumePoint, ct: ct), ct);

            // User query bất kỳ lúc nào
            // File đã scan → trả về ngay
            // File chưa scan → parse on-demand rồi tiếp tục background
        }

        public Task<AstFileResult?> QueryFile(SRFileRecord file, CancellationToken ct)
            => _scanner.GetOrParseAsync(file, ct);

        //use binary search to fine the resume point , which is the closest file to the root that is not scanned yet.
        private static int GetResumePoint(List<SRFileRecord> ordered)
        {
            if (ordered.Count == 0) return 0;

            int low = 0 ,hight = ordered.Count - 1, result = 0;

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
