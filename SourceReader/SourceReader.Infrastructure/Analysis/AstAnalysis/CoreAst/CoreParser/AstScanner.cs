using Microsoft.Extensions.Logging;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreLanguage;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreQuery;
using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser
{
    public sealed class AstScanner
    {
        //2 atribute này khả năng cho singleton chứ nhỉ? nếu vậy thì 
        //private readonly ParserPool _parserPool;
        //private readonly QueryRegistry _queries;
        private readonly FileParser _parser;
        private const int BATCH_SIZE = 50;

        // cache results of parssing process
        private readonly ConcurrentDictionary<int, AstFileResult> _results = new();
        // track files is parsing , avoid double parsing 1 file .
        private readonly ConcurrentDictionary<int, Task<AstFileResult?>> _inFlight = new();
        private readonly object _globalLock = new();

        public AstScanner(FileParser parser)
        {
            _parser = parser;
        }

        public async Task ScanOnDemand(
            ProjectIndex index,
            List<int> fileIds,
            int resumePoint,
            CancellationToken ct = default
            )
        {
            var fileOnDemands = index.Files.Values
               .Where(f => LanguageResolver.IsSupported(f.FilePath) &&
               fileIds.Contains(f.FileId))
               .Skip(resumePoint)
               .OrderByDescending(f => f.PriorityScore)
               .ToList();

            await RunBatchAsync(fileOnDemands, ct);
        }

        public async Task ScanAllAsync(
            ProjectIndex index,
            int resumePoint = 0,
            CancellationToken ct = default)
        {
            var ordered = index.Files.Values
                .Where(f => LanguageResolver.IsSupported(f.FilePath))
                .OrderByDescending(f => f.PriorityScore)
                .Skip(resumePoint)
                .ToList();

            var total = ordered.Count;

            for (var i = 0; i < total; i += BATCH_SIZE)
            {
                ct.ThrowIfCancellationRequested();

                var batch = ordered.Skip(i).Take(BATCH_SIZE).ToList();
                await RunBatchAsync(batch, ct);

                Console.WriteLine($"[ast]: {Math.Min(i + BATCH_SIZE, total)}/{total}");

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

            await Parallel.ForEachAsync(
                files,
                new ParallelOptions
                {
                    // Limit number of parrallel thread , Math.Max(1,..) to avoid case Cumputer have 1 core.
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
                    CancellationToken = ct
                },
                async (file, token) =>
                {
                    try
                    {
                        // if file is parsed so skip it
                        if (_results.ContainsKey(file.FileId)) return;

                        var task = _parser.ParseAsync(file, token);
                        _inFlight[file.FileId] = task;

                        var result = await task;
                        if (result is not null) _results[file.FileId] = result;

                    }
                    catch (OperationCanceledException ex)
                    {
                        throw;
                    }
                    //catch exception in each task to avoid stop other parallel task when it failed
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Parse failed: {file.FilePath} - {ex.Message}");
                    }
                    finally
                    {
                        //in case result error _inFlight still can remove the error task instead of keep hold it.
                        _inFlight.TryRemove(file.FileId, out _);
                    }

                });
        }

    }
}
