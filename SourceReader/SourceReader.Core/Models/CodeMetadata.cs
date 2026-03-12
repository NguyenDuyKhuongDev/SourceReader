using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Core.Models
{
    public class CodeMetadata
    {
        public string FilePath { get; set; }
        public string SymbolName { get; set; }
        public string SymbolType { get; set; }
        public string Content { get; set; }
        public string NameSpace{ get; set; }
        public int StartLine{ get; set; }
        public int EndLine{ get; set; }

    }
}
