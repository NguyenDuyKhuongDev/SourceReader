using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Core.Models.MetaData.NewFolder
{
    public class Repository
    {
public string Id { get; set; }
public string Name{ get; set; }
public string Path{ get; set; }
public DateTime IndexedAt{ get; set; }
public string Branch { get; set; } //?
public string Commmit{ get; set; }
    }
}
