using LiteDB;
using SourceReader.Core.Models.MetaData.NewFolder;
using SourceReader.Core.Models.MetaData.PartOfFile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Infrastructure.LiteDb
{
    public class MetaDataStore
    {
        private readonly LiteDatabase _liteDB;
        public MetaDataStore(string path)
        {
            _liteDB = new LiteDatabase(path);
        }

        public ILiteCollection<Repository> Repositories=> _liteDB.GetCollection<Repository>("repositories");
        public ILiteCollection<CodeChunk> CodeChunks => _liteDB.GetCollection<CodeChunk>("chunks");
        public ILiteCollection<CallEdge> CallEdges=> _liteDB.GetCollection<CallEdge>("callEdges");
        public ILiteCollection<Symbol> Symbols=> _liteDB.GetCollection<Symbol>("synbols");
        public ILiteCollection<FileIndex> FileIndexs=> _liteDB.GetCollection<FileIndex>("fileIndexs");
        public ILiteCollection<DependencyEdge> Dependencies=> _liteDB.GetCollection<DependencyEdge>("dependencies");
    }
}
