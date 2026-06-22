using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.DataModel
{
    public record AstFileResult
    {
        public int FileId { get; set; }
        public string Language { get; set; }
        public List<SymbolRecord>? symbols { get; set; }
        public bool IsSuccessed { get; set; }
        public string? Error { get; set; }

        public AstFileResult() { }
        public AstFileResult(int fileId, string language, List<SymbolRecord> symbols)
        {
            FileId = fileId;
            Language = language;
            this.symbols = symbols;
        }
        public static AstFileResult Fail(int fileId, string language, string? error)
        {
            return new AstFileResult
            {
                FileId = fileId,
                Language = language,
                symbols = null,
                IsSuccessed = false,
                Error = error,
            };
        }
    }
}
