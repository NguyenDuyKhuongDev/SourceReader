using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Core.Services.Project
{
    //public class ProjectAnalysis
    //{
    //    public async Task RunAsync(ProjectIndex index, CancellationToken ct)
    //    {
    //        // Kick off background scan — không await, không block
    //        _ = Task.Run(() => _scanner.ScanAllAsync(index, ct: ct), ct);

    //        // User query bất kỳ lúc nào
    //        // File đã scan → trả về ngay
    //        // File chưa scan → parse on-demand rồi tiếp tục background
    //    }

    //    public Task<AstFileResult?> QueryFile(SRFileRecord file, CancellationToken ct)
    //        => _scanner.GetOrParseAsync(file, ct);
    //}
}
