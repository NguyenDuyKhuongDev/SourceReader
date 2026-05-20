using TreeSitter;

internal class Program
{
    private static void Main(string[] args)
    {
        var tree = ParsePython();
        var node = tree?.RootNode;
        // Visit(tree.RootNode);
        //GetFunc(node);
        Console.WriteLine(node.GetNamedDescendantForRange(1,2));

    }

    private static Tree? ParsePython()
    {
        var code = """
def hello(name):
    print(name)
def hello(name):
    print(name)

class User:
    pass
""";
        using var language = new Language("python");
        using var parser = new Parser(language);
        var tree = parser.Parse(code);
        //if (tree != null) Console.WriteLine($"Root node: {tree.RootNode}");

        return tree;
    }

    private static void Visit(Node node, int depth = 0)
    {
        //Console.WriteLine(
        //    $"{new string(' ', depth * 2)}{node.Type}");

        var childCount = node.Children.Count;

        for (int i = 0; i < childCount; i++)
        {
            Visit(node.Children[i], depth + 1);
        }

    }

       private static void GetFunc(Node node)
    {
        var childCount = node.Children.Count;
        for (int i = 0; i < childCount; i++)
        {
            GetFunc(node.Children[i]);
            if (node.Children[i].Type == "function_definition")
            {
                var child = node.Children[i];
                Console.WriteLine(child.Text);
            }
        }
    }


    }



