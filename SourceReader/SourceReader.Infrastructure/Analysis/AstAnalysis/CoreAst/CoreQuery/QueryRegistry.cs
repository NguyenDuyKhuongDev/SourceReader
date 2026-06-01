using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreQuery
{
    public sealed class QueryRegistry : IDisposable
    {
        private readonly ConcurrentDictionary<string, Query> _cache = new();

        public Query GetOrCompile(Language lang, string langName, string pattern)
        {
            var key = $"{langName}::{pattern.GetHashCode()}";
            return _cache.GetOrAdd(key, _ => new Query(lang, pattern));
        }

        public void Dispose()
        {
            foreach (var q in _cache.Values) q.Dispose();
            _cache.Clear();
        }
    }
}
