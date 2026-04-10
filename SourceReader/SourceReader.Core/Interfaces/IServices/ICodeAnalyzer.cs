using SourceReader.Core.Models.MetaData.PartOfFile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Core.Interfaces.IServices
{
    public interface ICodeAnalyzer 
    {
        public Task<List<CodeChunk>> AnalyzeProjectAsync(string projectPath);
    }
}
