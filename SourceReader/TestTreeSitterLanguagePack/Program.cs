using TreeSitterLanguagePack;

internal class Program
{
    private static void Main(string[] args)
    {
        var code = """
import os

def hello(name):
    print(name)

class User:
    pass
""";

        var parser = new Treesitter.();

        parser.Language = LanguagePack.GetLanguage("python");

        var tree = parser.Parse(code);

        Console.WriteLine(tree.RootNode.Type);
    }
}