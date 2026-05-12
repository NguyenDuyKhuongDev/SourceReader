using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst
{
    public class AstWalker
    {
        public static void Walk(Node node, List<Node> result)
        {
            result.Add(node);

            for (int i = 0; i < node.ChildCount; i++)
            {
                Walk(node.Child(i), result);
            }

        }
    }
}
