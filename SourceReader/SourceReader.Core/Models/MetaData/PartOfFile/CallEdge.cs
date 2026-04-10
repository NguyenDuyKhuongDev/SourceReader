using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Core.Models.MetaData.PartOfFile
{
    public class CallEdge
    {
        public string Id { get; set; }
        public string RepositoryId { get; set; }
        public string Caller { get; set; }
        public string Callee { get; set; }
        public string FilePath { get; set; }
        public int Line { get; set; }
    }
}
