using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreLanguage;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreQuery;
using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser
{
    public class FileParser
    {
        private readonly ParserPool _pool;
        private readonly QueryRegistry _queries;

        public FileParser(
            ParserPool pool,
            QueryRegistry queryRegistry)
        {
            _pool = pool;
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
        public async Task<AstFileResult?> ParseAsync(
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
                throw; // không swallow cancellation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[parse] skip {file.FileName}: {ex.Message}");
                return null;
            }
        }
    }

   }
