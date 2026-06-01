using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreLanguage
{
    /// <summary>
    /// convert file path to language of file bassed on file extension
    /// </summary>
    public class LanguageResolver
    {
        private static readonly Dictionary<string, string> ExtMap =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    [".cs"] = "C#",
                    [".py"] = "Python",
                    [".js"] = "JavaScript",
                    [".ts"] = "TypeScript",
                    [".tsx"] = "TypeScript",
                    [".jsx"] = "JavaScript",
                    [".go"] = "Go",
                    [".rs"] = "Rust",
                    [".java"] = "Java",
                    [".cpp"] = "C++",
                    [".c"] = "C",
                    [".rb"] = "Ruby",
                    [".php"] = "PHP",
                };

        public static string? Resolve(string path)
        {
            ExtMap.TryGetValue(Path.GetExtension(path), out var lang);
            return lang;
        }

        public static bool IsSupported(string path) {
            return Resolve(path) is not null;
        }
    }
}
