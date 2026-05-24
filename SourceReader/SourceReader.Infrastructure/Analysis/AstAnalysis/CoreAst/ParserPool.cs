using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using TreeSitter;

namespace SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst
{
    /// <summary>
    /// Language: shared (thread safe)
    /// Parser: pooled - to avoid race condition and state corruption when multiple threads use the same parser instance
    /// manage dispose of parser and language to avoid memory leak
    /// </summary>
    public sealed class ParserPool : IDisposable
    {
        /// <summary>
        /// Language usually imutable and thread safe more than Parser so we can share it
        /// but parser is not thread safe - can be corrupted or bug when many threds use it at the same time (
        /// parser state corruption,- state of parser can be overwite by onother thread when executing in current thread and cause wrong parsing result or even crash the application)
        ///Dictionary is just can protect dictionary access but not protect parser instance 
        /// </summary>

        //cache of language and parser pairs to avoid loading language and creating parser multiple times
        private readonly ConcurrentDictionary<string, Language> _languages = new();

        //lamnguage - queue of parsers for that language
        private readonly ConcurrentDictionary<string, ConcurrentBag<Parser>> _parserPools = new();

        //check object is disposed to avoid using disposed resource
        private int _disposed;

        private static readonly int MaxParsersPerLanguage = Environment.ProcessorCount;

        private readonly ConcurrentDictionary<Parser, byte> _activeParsers = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _languageSemaphore = new();

        private int _activeRentCount;
        private int _isShuttingDown;
        private readonly ManualResetEventSlim _allReturned = new(true);


        public PooledParser Rent(string languageName)
        {
            bool acquired = false;

            var semaphore = _languageSemaphore.GetOrAdd(languageName,
        _ => new SemaphoreSlim(MaxParsersPerLanguage));
            semaphore.Wait();
            acquired = true;

            try
            {
                if (Volatile.Read(ref _isShuttingDown) == 1) throw new ObjectDisposedException(nameof(ParserPool));
                Interlocked.Increment(ref _activeRentCount);
                _allReturned.Reset();
                ThrowIfDisposed();

                var bag = _parserPools.GetOrAdd(
                               languageName,
                               _ => new ConcurrentBag<Parser>()
                               );

                if (bag.TryTake(out var parser))
                {
                    _activeParsers.TryAdd(parser, 0);
                    return new PooledParser(languageName, parser);
                }

                var language = GetOrLoadLanguage(languageName);
                parser = new Parser();
                parser.Language = language;
                _activeParsers.TryAdd(parser, 0);

                return new PooledParser(languageName, parser);
            }
            catch {
            if(acquired) semaphore.Release();

                throw;
            } 

        }

        public void Return(PooledParser pooledParser)
        {
            var semaphore = _languageSemaphore.GetOrAdd(pooledParser.LanguageName,
                    _ => new SemaphoreSlim(MaxParsersPerLanguage));
            try
            {
                ThrowIfDisposed();

                var bag = _parserPools.GetOrAdd(
                   pooledParser.LanguageName,
                   _ => new ConcurrentBag<Parser>()
                   );

                if (!_activeParsers.TryRemove(pooledParser.Parser, out _))
                {
                    throw new InvalidOperationException("Parser already returned or not owned by pool");
                }

                ResetParser(pooledParser.Parser);
                bag.Add(pooledParser.Parser);

                if (Interlocked.Decrement(ref _activeRentCount) == 0) _allReturned.Set();
            }
            finally
            {
                semaphore.Release();
            }
        }

        private Language GetOrLoadLanguage(string languageName)
        {
            return _languages.GetOrAdd(
                languageName,
                name => LanguageLoader.TryLoadLanguage(name)
                ?? throw new InvalidOperationException($"Failed to load language {name}")
             );

        }

        //clean up parser and language resource to avoid memory leak and set disposed flag to acoid using disposed resource
        public void Dispose()
        {
            Interlocked.Exchange(ref _isShuttingDown, 1);
            _allReturned.Wait();

            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            foreach (var bag in _parserPools.Values)
            {
                while (bag.TryTake(out var parser))
                {
                    parser.Dispose();
                }
            }

            foreach (var language in _languages.Values) language.Dispose();
            foreach (var semaphore in _languageSemaphore.Values) semaphore.Dispose();
            //foreach (var activeParser in _activeParsers.Keys) activeParser.Dispose();

            //remove all reference to parser and language to allow gc to clean up
            //bc if keep ref to parser and language, even dispose them, they still occupy memory until gc clean up, but if remove ref, they can be collected immediately after dispose
            _parserPools.Clear();
            _languages.Clear();
            _activeParsers.Clear(); // taij sao ? 
            _languageSemaphore.Clear();
        }

        //avoid using disposed resource 
        private void ThrowIfDisposed()
        {
            // use volatile read to ensure that the latest value of _disposed is read , cpu will not cache old value of _diposed, and if it is 1, it means the object has been disposed, so throw ObjectDisposedException to prevent using disposed resource

            if (Volatile.Read(ref _disposed) == 1) throw new ObjectDisposedException(nameof(ParserPool));
        }

        private static void ResetParser(Parser parser)
        {
            parser.Reset();
        }

    }
}
