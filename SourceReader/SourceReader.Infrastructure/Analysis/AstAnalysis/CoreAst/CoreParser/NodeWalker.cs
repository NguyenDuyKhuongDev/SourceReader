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
                var nameNode = node.GetChildForField("name");
                var name = nameNode?.Text;

                if (name is not null)
                {
                    symbols.Add(new SymbolRecord(
                        symbolId: nextId++,
                        fileId: fileId,
                        name: name,
                        kind: kind.Value,
                        startLine: (int)node.StartPosition.Row + 1,
                        endLine: (int)node.EndPosition.Row + 1,
                        parentName: parentName
                    ));

                    // If in params have parentName so it will be add to the symbol by the line symbols.Add
                    // new(symbolrecord) above
                    //if the symbol is class, name space ..etc so it name will be pass to it child throught recursion
                    if (kind.Value is SymbolKind.Class or SymbolKind.Namespace
                                   or SymbolKind.Interface or SymbolKind.Struct
                                   or SymbolKind.Record)
                        parentName = name;
                }
            }

            // TreeSitter.DotNet: node.ChildCount + node.Child(i)
            for (int i = 0; i < node.Children.Count; i++)
                WalkRecursive(node.Children[i], fileId, lang, parentName, symbols, ref nextId);
        }
    }
}
