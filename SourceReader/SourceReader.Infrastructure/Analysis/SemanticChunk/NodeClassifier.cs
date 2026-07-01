using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.SemanticChunk
{
    public class NodeClassifier
    {
        private static readonly HashSet<string> Symbols = new()
    {
        "class_declaration",
        "method_declaration",
        "function_definition",
        "interface_declaration",
        "namespace_declaration"
    };

        public  static bool IsSymbol(Node node)
        {
            return Symbols.Contains(node.Type);
        }
    }
}
