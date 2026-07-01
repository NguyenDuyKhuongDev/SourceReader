using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.DataModel
{
    [MessagePackObject]
    public class ProjectIndex
    {
        public ProjectIndex(string cachedPath)
        {
            CachedPath= cachedPath;
        }

        [property: Key(0)] public string CachedPath{ get; set; }
        [property: Key(1)] public Dictionary<int, SRFileRecord> Files { get; set; } = new();
        [property: Key(2)] public Dictionary<int, SRImportRecord> Imports { get; set; } = new();
        /// <summary>
        /// src => target[] , key: sourceId - value : list of targertId that source file imports
        /// </summary>
        [property: Key(3)] public Dictionary<int, List<int>> OutEdge { get; set; } = new();
        /// <summary>
        /// target => src[] , key : targetId(file that is be imported) - value: list of sourceId (file that import target file)
        /// </summary>
        [property: Key(4)] public Dictionary<int, List<int>> InEdge { get; set; } = new();
        /// <summary>
        /// key: File Path - value: File Id
        /// </summary>
        [property: Key(5)] public Dictionary<string, int> PathToId { get; set; } = new();
    }

    [MessagePackObject]
    public partial record SRFileRecord
    {
        [property: Key(0)] public int FileId { get; set; }
        [property: Key(1)] public string FilePath { get; set; }
        [property: Key(2)] public string FileName { get; set; }
        [property: Key(3)] public long FileSize { get; set; }
        [property: Key(4)] public int Depth { get; set; }
        /// <summary>
        /// number of import of that depend on this file
        /// equal to InEdge[Id].Count
        /// </summary>
        [property: Key(5)] public int InDegree { get; set; }
        [property: Key(6)] public double PriorityScore { get; set; }
        [property: Key(7)] public long ModifiedAt { get; set; }

        // status of the file, i use this field some case like resume after crash , shutdown cumputer ..etc
        // and i use binary search to  find the unscanned file to resume at but it have some problem when user
        //demand to scan some file that not follow by order of priority score , so i add field isondemand to indicate with normal scanned case
        [property: Key(8)] public bool IsScanned { get; set; }
        //indicate if the file is scanned by demand or by order of priority score
        [property: Key(9)] public bool IsOnDemand { get; set; }
    }

    [MessagePackObject]
    public partial record SRImportRecord
    {
        // not the id of the imported file , but the id of the import statement
        [property: Key(0)] public int ImportId { get; set; }
        /// <summary>
        /// the file that the import statement is in
        /// </summary>
        [property: Key(1)] public int SourceFileId { get; set; }
        /// <summary>
        /// the file that is imported, if this id is null this import file is external , not from the project
        /// </summary>
        [property: Key(2)] public int? TargetFileId { get; set; }
        [property: Key(3)] public string RawFilePath { get; set; }
        [property: Key(4)] public bool IsExternal { get; set; }
    }

    public record ProjectDiff(
        List<int> Deleted,
        // because new file may not have id yet, so i store file path here
        List<string> Added,
        List<int> Modified
        )
    {
        public bool IsEmpty =>
                (Deleted == null || Deleted.Count == 0) &&
                (Added == null || Added.Count == 0) &&
                (Modified == null || Modified.Count == 0);

        /// <summary>
        /// just a readonly property that show the list of file ids that are affected by change.
        /// </summary>
        public IEnumerable<int> AffectedFileIds => (Deleted ?? Enumerable.Empty<int>()).Union(Modified ?? Enumerable.Empty<int>());
    }


}
