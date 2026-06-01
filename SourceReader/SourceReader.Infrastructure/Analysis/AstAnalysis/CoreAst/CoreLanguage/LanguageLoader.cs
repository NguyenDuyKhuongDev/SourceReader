using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreLanguage
{
    /// <summary>
    /// create language , parser and handle the exception when loading failed
    /// </summary>
    public static class LanguageLoader
    {

        /// <summary>
        ///  load native grammar DLL of language
        /// </summary>
        /// <param name="languageName"></param>
        /// <returns></returns>
        public static string? TryLoadLanguage(string languageName)
        {
            try
            {
                var lang = new Language(languageName);
                return languageName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lang] cannot load language {languageName}: Message: {ex.Message}");
                return null;
            }
        }
    }
}
