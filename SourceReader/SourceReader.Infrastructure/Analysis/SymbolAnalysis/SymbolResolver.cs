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
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child.Type== "identifier")
                {
                    return content.Substring(child.StartIndex, child.EndIndex- child.StartIndex);
                }
            }
            return "";
        }
    }
}
