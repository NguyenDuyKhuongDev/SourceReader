using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Core.Models.MetaData.NewFolder
{
    public class FileIndex
    {
        public string Id { get; set; }
        public string RepositoryId{ get; set; }
        public string FilePath{ get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Hash{ get; set; }
        public DateTime LastIndex{ get; set; }

    }
}
