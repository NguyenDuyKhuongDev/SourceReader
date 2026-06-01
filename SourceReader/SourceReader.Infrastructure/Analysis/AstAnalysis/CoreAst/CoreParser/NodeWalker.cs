using SourceReader.Infrastructure.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser
{
    public class NodeWalker
    {
        public static List<SymbolRecord> Walk(Node root, int fileId, string lang)
        {
            var symbols = new List<SymbolRecord>();
            var nextId = 1;
            WalkRecursive(root, fileId, lang, null, symbols, ref nextId);
            return symbols;
        }

        private static void WalkRecursive(
            Node node,
            int fileId,
            string lang,
            string? parentName,
            List<SymbolRecord> symbols,
            ref int nextId)
        {
            var kind = NodeKindMap.Resolve(node.Type, lang);
            if (kind.HasValue)
            {
                // TreeSitter.DotNet: ChildByFieldName("name") → node.Text
                var nameNode = node.ChildByFieldName("name");
                var name = nameNode?.Text;

                if (name is not null)
                {
                    symbols.Add(new SymbolRecord(
                        SymbolId: nextId++,
                        FileId: fileId,
                        Name: name,
                        Kind: kind.Value,
                        StartLine: (int)node.StartPoint.Row + 1,
                        EndLine: (int)node.EndPoint.Row + 1,
                        ParentName: parentName
                    ));

                    // Class/namespace/struct → làm parent cho các node con
                    if (kind.Value is SymbolKind.Class or SymbolKind.Namespace
                                   or SymbolKind.Interface or SymbolKind.Struct
                                   or SymbolKind.Record)
                        parentName = name;
                }
            }

            // TreeSitter.DotNet: node.ChildCount + node.Child(i)
            for (uint i = 0; i < node.ChildCount; i++)
                WalkRecursive(node.Child(i), fileId, lang, parentName, symbols, ref nextId);
        }
    }
}
