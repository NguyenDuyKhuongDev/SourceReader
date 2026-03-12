using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis
{
    public class LanguageLoader
    {
        private readonly Dictionary<string, Language> _languages = new();
        private readonly List<IntPtr> _dllHandles = new();
        public LanguageLoader()
        {
            LoadLanguage("csharp", "tree-sitter-csharp.dll", "tree_sitter_c_sharp");
            LoadLanguage("python", "tree-sitter-python.dll", "tree_sitter_python");
            LoadLanguage("javascript", "tree-sitter-javascript.dll", "tree_sitter_javascript");
        }

        public bool TryGet(string id, out Language language)
        {
            return _languages.TryGetValue(id, out language);
        }

        private unsafe void LoadLanguage(string langId, string dllName, string functionName)
        {
            if (!NativeLibrary.TryLoad(dllName, out IntPtr handle))
                return;

            _dllHandles.Add(handle);

            IntPtr symbol = NativeLibrary.GetExport(handle, functionName);

            delegate* unmanaged<IntPtr> getLang = (delegate* unmanaged<IntPtr>)symbol;

            IntPtr langPtr = getLang();

            _languages[langId] = new Language(langPtr);
        }
    }
}
