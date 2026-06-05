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

        public AstFileResult(int fileId, string language, List<SymbolRecord> symbols)
        {
            FileId = fileId;
            Language = language;
            this.symbols = symbols;
        }
    }
}
