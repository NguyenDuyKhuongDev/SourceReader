using SourceReader.Core.Models;
using SourceReader.Core.Models.MetaData.PartOfFile;
using SourceReader.Infrastructure.Analysis.SymbolAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.SemanticChunk
{
    public class SemanticChunkExtractor
    {
        public static List<CodeChunk> Extract(Node root, string content, string file)
        {
            var result = new List<CodeChunk>();
            Walk(root, content, file,result, null);
            return result;
        }

    private static void Walk(Node node, string content, string file, List<CodeChunk> result, string? currentNamespace)
        {
            if (node.Kind == "namespace_declaration")
            {
                currentNamespace = SymbolResolver.GetSymbolName(node, content);
            }

            if (NodeClassifier.IsSymbol(node)) {
                var name = SymbolResolver.GetSymbolName(node, content);

                var text = content.Substring(node.StartByte, node.EndByte - node.StartByte);

                result.Add(new CodeChunk{
                    FilePath = file,
                    SymbolType = node.Kind,
                    SymbolName = name,
                    NameSpace = currentNamespace,
                    Content = text,
                    StartLine = node.StartPosition.Row.ToString(),
                    EndLine = node.EndPosition.Row.ToString(),
                });
            }
            for (int i = 0; i < node.ChildCount; i++) {
                Walk(node.Child(i), content, file, result, currentNamespace);
            }
        }
    } 
}

