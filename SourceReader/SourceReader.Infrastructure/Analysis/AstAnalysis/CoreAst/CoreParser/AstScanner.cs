using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreLanguage;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreQuery;
using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser
{
    public sealed class AstScanner : IDisposable
    {
        private readonly ParserPool _parserPool;
        private readonly QueryRegistry _queries;
        private readonly FileParser _parser;

        public AstScanner()
        {
            _parserPool = new ParserPool();
            _queries = new QueryRegistry();
            _parser = new FileParser(_parserPool, _queries);
        }

        // Phase 2 — parse top-N file quan trọng nhất, block đến khi xong
        public async Task<List<AstFileResult>> ScanPriorityAsync(
            ProjectIndex index,
            int topN = 50,
            CancellationToken ct = default)
        {
            var files = index.Files.Values
                .Where(f => LanguageResolver.IsSupported(f.FilePath))
                .OrderByDescending(f => f.PriorityScore)
                .Take(topN)
                .ToList();

            Console.WriteLine($"[ast] priority scan: {files.Count} files");
            return await RunBatchAsync(files, ct);
        }

        // Phase 3 — background scan toàn bộ, không block
        public async Task<List<AstFileResult>> ScanAllAsync(
            ProjectIndex index,
            int batchSize = 50,
            CancellationToken ct = default)
        {
            var all = index.Files.Values
                .Where(f => LanguageResolver.IsSupported(f.FilePath))
                .OrderByDescending(f => f.PriorityScore)
                .ToList();

            var results = new List<AstFileResult>();
            var total = all.Count;

            for (var i = 0; i < total; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = all.Skip(i).Take(batchSize).ToList();
                results.AddRange(await RunBatchAsync(batch, ct));

                Console.WriteLine($"[ast] background: {Math.Min(i + batchSize, total)}/{total}");

                // Yield CPU — không block UI thread
                await Task.Delay(100, ct);
            }

            return results;
        }

        private async Task<List<AstFileResult>> RunBatchAsync(
            List<SRFileRecord> files,
            CancellationToken ct)
        {
            var results = new ConcurrentBag<AstFileResult>();

            // Parallel parse — mỗi thread có Parser riêng qua ThreadLocal
            await Parallel.ForEachAsync(
                files,
                new ParallelOptions
                {
                    // Giới hạn để không starve I/O thread
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
                    CancellationToken = ct
                },
                async (file, token) =>
                {
                    var result = await _parser.ParseAsync(file, token);
                    if (result is not null) results.Add(result);
                });

            return [.. results];
        }

        public void Dispose()
        {
            // Thứ tự quan trọng: Query trước, Language sau
            _queries.Dispose();
            _parserPool.Dispose();
        }
    }
}
