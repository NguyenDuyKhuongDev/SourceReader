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
        private const int BATCH_SIZE = 50;

        // cache results of parssing process
        private readonly ConcurrentDictionary<int, AstFileResult> _results = new();
        // track files is parsing , avoid double parsing 1 file .
        private readonly ConcurrentDictionary<int, Task<AstFileResult?>> _inFlight = new();
        private readonly object _globalLock = new();

        public AstScanner()
        {
            _parserPool = new ParserPool();
            _queries = new QueryRegistry();
            _parser = new FileParser(_parserPool, _queries);
        }

        // Phase 3 — background scan toàn bộ, không block
        public async Task ScanAllAsync(
            ProjectIndex index,
            int batchSize = BATCH_SIZE,
            CancellationToken ct = default)
        {
            var ordered = index.Files.Values
                .Where(f => LanguageResolver.IsSupported(f.FilePath))
                .OrderByDescending(f => f.PriorityScore)
                .ToList();

            var total = ordered.Count;

            for (var i = 0; i < total; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = ordered.Skip(i).Take(batchSize).ToList();
                await RunBatchAsync(batch, ct);

                Console.WriteLine($"[ast]: {Math.Min(i + batchSize, total)}/{total}");

                //yeild cpu avoud starve other task
                await Task.Delay(100, ct);
            }
        }

        // solve the problem when file that already parsed but user or other task reqest it again , so return
        // the cached result without parsing again
        public async Task<AstFileResult?> GetOrParseAsync(
            SRFileRecord file,
            CancellationToken ct = default)
        {
            //if file is already parsed return cached result 
            if (_results.TryGetValue(file.FileId, out var cached))
                return cached;

            // if file is parsing and exist in inFlight return await of the parsing task
            if (_inFlight.TryGetValue(file.FileId, out var existing))
                return await existing;

            //if file is not parsed an not in inFlight so parse it and add to inFilght
            Console.WriteLine($"[ast] on-demand: {file.FileName}");
            var task = _parser.ParseAsync(file, ct);
            _inFlight[file.FileId] = task;

            // File parsed and now cache the result and remove from iNfLIGHT
            var result = await task;
            if (result is not null) _results[file.FileId] = result;
            _inFlight.TryRemove(file.FileId, out _);

            return result;
        }

        private async Task RunBatchAsync(
            List<SRFileRecord> files,
            CancellationToken ct)
        {

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
                    // if file is parsed so skip it
                    if (_results.ContainsKey(file.FileId)) return;

                    var task = _parser.ParseAsync(file, token);
                    _inFlight[file.FileId] = task;

                    var result = await task;
                    if (result is not null) _results[file.FileId] = result;
                    _inFlight.TryRemove(file.FileId, out _);
                });
        }

        public int GetResumePoint() { 
        
        }
        public void Dispose()
        {
            // Thứ tự quan trọng: Query trước, Language sau
            _queries.Dispose();
            _parserPool.Dispose();
        }
    }
}
