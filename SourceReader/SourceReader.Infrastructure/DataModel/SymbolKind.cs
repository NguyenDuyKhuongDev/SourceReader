using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.DataModel
{
         public enum SymbolKind 
        {
            Namespace,
            Class,
            Interface,
            Struct, 
            Enum,
            Method, //là fucntion đi liền với class hoặc obj
            Function, // là nói chung về 1 đoaọn code thực hiện được 1 tác vụ nào đó  , có thể độc lập hoặc không với class.
            Constructor,
            Property,
            Field,
            Record
        }
}
