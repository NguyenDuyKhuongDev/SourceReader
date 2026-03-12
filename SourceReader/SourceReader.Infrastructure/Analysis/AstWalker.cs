using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis
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
