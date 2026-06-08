using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Core.Models.MetaData.PartOfFile
{
    public class DependencyEdge
    {
        public string Id { get; set; }
        public string RepositoryId { get; set; }
        public string SourceFile { get; set; }
        public string TargetModule { get; set; }
    }
}
