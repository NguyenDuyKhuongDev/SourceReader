using LiteDB;
using SourceReader.Core.Models.MetaData.PartOfFile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceReader.Infrastructure.LiteDb
{
    public static class LiteDbMapper
    {
        public static void Configure()
        {
            BsonMapper.Global.Entity<CodeChunk>()
                    .Id(x => x.Id);
        }

    }
}
