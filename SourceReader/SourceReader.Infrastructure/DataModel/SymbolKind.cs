using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.DataModel
{
    public class SymbolKind
    {
        public enum SymKind
        {
            Namespace,
            Class,
            Interface,
            Struct, 
            Enum,
            Method,
            Constructor,
            Property,
            Field,
            Record
        }
    }
}
