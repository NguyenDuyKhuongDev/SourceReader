using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Core.Models.MetaData.PartOfFile
{
    public class CodeChunk
    {
        public string Id { get; set; }
        public string RepoId { get; set; }
        public string EmbeddingId { get; set; }
        public string FilePath { get; set; }
        /// <summary>
        /// Name of Symbol : Player (Class) , Run() (method)
        ///Created by khuongnd At 15/3/2026
        /// </summary>
        public string SymbolName { get; set; }
        /// <summary>
        /// SymbolType: Class, Method, Field..etc
        ///Created by khuongnd At 15/3/2026
        /// </summary>
        public string SymbolType { get; set; }
        public string NameSpace { get; set; }
        public string Content { get; set; }
        public string StartLine { get; set; }
        public string EndLine { get; set; }

    }
}
