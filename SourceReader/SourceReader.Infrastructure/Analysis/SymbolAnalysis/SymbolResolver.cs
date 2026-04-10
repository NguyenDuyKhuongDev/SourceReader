using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.SymbolAnalysis
{
    public class SymbolResolver
    {
        public static string GetSymbolName(Node node, string content)
        {
            for (int i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i);
                if (child.Kind == "identifier")
                {
                    return content.Substring(child.StartByte, child.EndByte - child.StartByte);
                }
            }
            return "";
        }
    }
}
