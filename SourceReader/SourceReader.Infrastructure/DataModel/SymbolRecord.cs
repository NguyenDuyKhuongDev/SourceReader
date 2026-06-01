using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.DataModel
{
    public class SymbolRecord
    {
        public int SymbolId { get; set; }
        public int FileId { get; set; }
        public string Name { get; set; }
        public SymbolKind Kind{ get; set; }
        public int StartLine { get; set; }
        public int EndLine{ get; set; }
        /// <summary>
        /// là class chứa method hoặc namespace chứa class, interface ..vv ko b nên để là id không?
        /// </summary>
        public string ParentName{ get; set; }

        public SymbolRecord(int symbolId, int fileId, string name, SymbolKind kind, int startLine, int endLine, string parentName)
        {
            SymbolId = symbolId;
            FileId = fileId;
            Name = name;
            Kind = kind;
            StartLine = startLine;
            EndLine = endLine;
            ParentName = parentName;
        }

    }
}
