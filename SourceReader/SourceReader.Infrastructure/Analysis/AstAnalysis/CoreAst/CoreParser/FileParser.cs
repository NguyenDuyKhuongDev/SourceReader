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
                using var tree = poolParser.Parser(source);
                if (tree is null)
                {
                    Console.WriteLine($"[parse] null tree: {file.FileName}");
                    return null;
                }

                var pattern = QueryRunner.GetPattern(langName);
                List<SymbolRecord> symbols;

                if (pattern is not null)
                {
                    // Query — chính xác, ưu tiên dùng
                    var query = _queries.GetOrCompile(lang, langName, pattern);
                    symbols = QueryRunner.Run(query, tree.RootNode, file.FileId, langName);
                }
                else
                {
                    // Fallback NodeWalker cho ngôn ngữ chưa có pattern
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
