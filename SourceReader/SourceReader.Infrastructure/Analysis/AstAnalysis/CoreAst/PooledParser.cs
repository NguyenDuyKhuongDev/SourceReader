using System;
using System.Collections.Generic;
using System.Text;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst
{
    public sealed class PooledParser
    {
        public string LanguageName { get; }
        public Parser Parser { get; set; }
        internal PooledParser(
            string languageName,
            Parser parser)
        {
            LanguageName = languageName;
            Parser = parser;

        }

    }
}
