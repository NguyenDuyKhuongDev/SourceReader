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

    /// the hardest part of this class is how to multithread access the resource , not dispose wrong time, not race condition, not memory leak and not overwrite other state.

    /// 5 main problem :
    /// 1.parser is not thread safe: state of parser can be overwite by onother thread when executing in current thread and cause wrong parsing result or even crash the application
    ///2.avoid to create too many parser instance ,create limit and reuse
    ///3. limit number of parser instance for each language to avoid memory leak and performance issue
    ///4.shut down safety: wait for all parser returned before dispose resouce , dont force dispose (can lead to lost data)
    ///5. ownership validation : ensure that parser is returned to the true owner poool(language)
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
        // Limits concurrent rented parsers per language.
        // Each Rent() acquires one permit.
        // Permit is released only when parser is returned.
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _languageSemaphore = new();

        //how many parser are currently rented out , used to wait for all parser returned before dispose resource 
        private int _activeRentCount;
        // is a flag to indicate that the pool is shutting down, when set to 1, it means the pool is shutting down and no new parser can be rented, and all returned parser will be waited until all parser are returned before dispose resource.
        private int _isShuttingDown;
        //can easyly imagine it like a switch  with 2 state on and off , when allReturned.set() - it's on 
        // and when allReturned.Reset() it's off  , and the method Wait() is waiting for those signal to work
        private readonly ManualResetEventSlim _allReturned = new(true);

        private  readonly object _lifeTimeLock = new();


        public PooledParser Rent(string languageName)
        {
            bool acquired = false;

            var semaphore = _languageSemaphore.GetOrAdd(languageName,
        _ => new SemaphoreSlim(MaxParsersPerLanguage));
            semaphore.Wait();
            acquired = true;

            try
            {
                lock (_lifeTimeLock)
                {
                    //check variable isshuttingdown if it's 1 ~ program is demand to shutdown, so it will avoid othe thread rent a parser.
                    //Volatile.Read is  used to Ensures memory visibility and acquire ordering semantics., and create a acquire barrier (the order of instruction near line volatile.read will not be reorder by cpu(to optimize performance but it can lead to wrong logic in a multi thread synchrolization) , and this is not lock thread, it's just a local synchronization boundary around this instruction )
                    //Acquire barrier mean that instruction behind will not be reorder to before this instruction(Volatile.Read)
                    if (Volatile.Read(ref _isShuttingDown) == 1) throw new ObjectDisposedException(nameof(ParserPool));
                    //create safe thread when update value of _activerentcount by zip it into a automic action (because increase is not just 1 step . it's like 3 step like read val of activerentcount then add 1 then write it new val so in multi thread this process can be overwrite by other thread like read or write val of activerentcount at the same time lead to the wrong val of activerentcount - it's call race condition )
                    Interlocked.Increment(ref _activeRentCount);
                    //in case allreturned had  been set() in the past mean there is no parser is renting, so when  the renting process is working again we have to reset it state . 
                    _allReturned.Reset();
                }
             
                var bag = _parserPools.GetOrAdd(
                               languageName,
                               _ => new ConcurrentBag<Parser>()
                               );

                if (bag.TryTake(out var parser))
                {
                    ResetParser(parser);
                    _activeParsers.TryAdd(parser, 0);
                    return new PooledParser(languageName, parser);
                }

                var language = GetOrLoadLanguage(languageName);
                parser = new Parser();
                parser.Language = language;
                _activeParsers.TryAdd(parser, 0);

                return new PooledParser(languageName, parser);
            }
            catch
            {
                //relese semaphore in case fail to rent, 
                if (acquired) semaphore.Release();

                // avoid to leak active rent count when fail to rent 
                if (Interlocked.Decrement(ref _activeRentCount) == 0)
                    _allReturned.Set();

                throw;
            }

        }

        public void Return(PooledParser pooledParser)
        {
            var semaphore = _languageSemaphore.GetOrAdd(pooledParser.LanguageName,
                    _ => new SemaphoreSlim(MaxParsersPerLanguage));
            try
            {
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

                //check if all  parser are return , allReturnned like a signal to indicate that it's a safe time to dispose , in dispose method have _allreturn.wait - it's waiting for allreturn.set() call. 
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
            lock (_lifeTimeLock)
            {
                //Interlocked make a actions into atomic action - like 3 step zip in 1 step => no other thread can interupt in the middle of this action
                //exchange vale ò shuttong down to 1 and return old value of isshutingdown.
                Interlocked.Exchange(ref _isShuttingDown, 1);
            }
            //waiting for allreturned.set()
            _allReturned.Wait();

            //the first thread call dispose will set dispose flag =1 , and get return is old value of _disposed ussually diferent to 1, so it will continuew , this line is used for  avoid other thread call dispose to , and make it double call.
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
            _activeParsers.Clear(); 
            _languageSemaphore.Clear();
        }

        //avoid using disposed resource 
        private void ThrowIfDisposed()
        {
            // Volatile.Read ensures visibility of the latest synchronized value
            // according to the .NET memory model.

            if (Volatile.Read(ref _disposed) == 1) throw new ObjectDisposedException(nameof(ParserPool));
        }

        private static void ResetParser(Parser parser)
        {
            parser.Reset();
        }

    }
}
