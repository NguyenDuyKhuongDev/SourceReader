using SourceReader.Core.Interfaces.IServices;
using SourceReader.Core.Models.MetaData.PartOfFile;
using SourceReader.Infrastructure.Analysis.SemanticChunk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TreeSitter;
using static System.Net.Mime.MediaTypeNames;

namespace SourceReader.Infrastructure.Analysis
{
    public class TreeSitterAnalyzer : ICodeAnalyzer
    {
        private readonly LanguageLoader _languageLoader = new();
        public async Task<List<CodeChunk>> AnalyzeFileAsync(string filePath, string content)
        {
            var ext = Path.GetExtension(filePath);
            var langId = ext switch
            {
                ".cs" => "csharp",
                ".py" => "python",
                ".js" => "javascript",
                _ => null
            };
            if (langId == null) return new();
            if (!_languageLoader.TryGet(langId, out Language language)) return new();


            using var parser = new Parser();
            parser.Language = language;
            using var tree = parser.Parse(content);
            var root = tree.Root;

            return SemanticChunkExtractor.Extract(root, content, filePath);
        }

        public async Task<List<CodeChunk>> AnalyzeProjectAsync(string projectPath)
        {
            var result = new List<CodeChunk>();

            var files = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file);
                var chunks = await AnalyzeFileAsync(file, content);

                result.AddRange(chunks);
            }
            return result;
        }
    }
}
