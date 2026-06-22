using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreLanguage;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreQuery;
using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using TreeSitter;
using Microsoft.Extensions.Logging;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser
{
    public class FileParser
    {
        private readonly ParserPool _pool;
        private readonly QueryRegistry _queries;
        private readonly ILogger<FileParser> _logger; 

        public FileParser(
            ParserPool pool,
            QueryRegistry queryRegistry,
            ILogger<FileParser> logger)
        {
            _pool = pool;
            _logger = logger;
            _queries = queryRegistry;
        }

        /// <summary>
        /// this method parse file by two way :
        /// 1. if this language have pattern it will run query and extract symbol by pattern
        /// 2. if this language don't have pattern it will fallback to NodeWalker
        /// </summary>
        /// <param name="file"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<AstFileResult> ParseAsync(
           SRFileRecord file, CancellationToken ct)
        {
            var langName = LanguageResolver.Resolve(file.FilePath);
            if (langName is null) return null;

            try
            {
                var poolParser = _pool.Rent(langName);
                var source = await File.ReadAllTextAsync(file.FilePath, ct);

                // tree phải còn sống trong suốt quá trình extract
                using var tree = poolParser.Parser.Parse(source);
                if (tree is null)
                {
                    Console.WriteLine($"[parse] null tree: {file.FileName}");
                    return null;
                }

                var pattern = QueryRunner.GetPattern(langName);
                List<SymbolRecord> symbols;

                //run query if this language have pattern(like oh with c# i just want to take class , method , etc so i set for it a pattern and run query so whenever i extract symbol i get name method ..etc)
                if (pattern is not null)
                {
                    var query = _queries.GetOrCompile(poolParser.Language, langName, pattern);
                    symbols = QueryRunner.Run(query, tree.RootNode, file.FileId, langName);
                }
                else
                {
                    symbols = NodeWalker.Walk(tree.RootNode, file.FileId, langName);
                }

                return new AstFileResult(file.FileId, langName, symbols);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            // this method will be run in parrallel so we need to catch exception each of this task so when 1 of this fail , it will not end the whole parallel process
            catch (Exception ex)
            {
                // log detail error :stacktrace, message..vv 
                _logger.LogError(ex, "Error parsing file {FileId} with language {Language}", file.FileId, langName);
                //create ast file result with error message.
                return AstFileResult.Fail(file.FileId, langName, ex.Message);
            }
        }
    }

}
