using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.DataModel
{
    public record AstFileResult
    {
        public int FileId { get; set; }
        public string Language { get; set; }
        public List<SymbolRecord> symbols { get; set; }

    }
}
