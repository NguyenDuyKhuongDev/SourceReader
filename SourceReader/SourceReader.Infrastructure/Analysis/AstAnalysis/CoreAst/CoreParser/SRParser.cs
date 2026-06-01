using System;
using System.Collections.Generic;
using System.Text;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser
{
    public sealed class SRParser
    {
        public Language Language{ get; }
        public Parser Parser { get; set; }
        internal SRParser(
            Parser parser,
            Language language)
        {
            Language = language;
            Parser = parser;

        }

    }
}
