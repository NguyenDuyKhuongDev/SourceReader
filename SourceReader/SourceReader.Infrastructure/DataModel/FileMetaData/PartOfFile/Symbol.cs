using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Core.Models.MetaData.PartOfFile
{
    public class Symbol
    {
        public string Id { get; set; }
        public string RepositoryId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string NameSpace { get; set; }
        public string FilePath { get; set; }
        public string StartLine { get; set; }
        public string EndLine { get; set; }

    }
}
