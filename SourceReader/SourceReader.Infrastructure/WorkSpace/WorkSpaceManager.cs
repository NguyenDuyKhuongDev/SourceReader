using SourceReader.Core.Services.Project;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser;
using SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile;
using SourceReader.Infrastructure.DataModel;
using SourceReader.Infrastructure.Project;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace SourceReader.Infrastructure.WorkSpace
{
    /// <summary>
    /// The hightest level manager of this project, manager other projects 
    /// </summary>
    public sealed class WorkSpaceManager
    {
        private readonly ProjectManagerFactory _factory;
        // Key: rootPath ->  Value: ProjectManager
        public readonly ConcurrentDictionary<string, ProjectManager> _projectManagers = new();
        // Key: RoothPath -> Value: project Cached path
        public List<Tuple<string, string>> _projectCacheds = new();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly string CACHED_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceReader", "cache");


        public WorkSpaceManager(ProjectManagerFactory factory)
        {
            _factory = factory;
            LoadCachedProject();
        }

        /// <summary>
        ///this load a list project has cached , this can be use to show the list of project that
        /// workspace manager can open (not create an object of project manager yet)
        /// </summary>
        public async void LoadCachedProject()
        {
            string[] files= Directory.GetFiles(CACHED_DIR);

            foreach (var file in files)
            {
                _projectCacheds.Add(Tuple.Create(Path.GetFileNameWithoutExtension(file), file));
            }
        }

        /// <summary>
        ///Create an instance of project manager for the project that had scanned or 1st time scan , after create instance add it into a list key(rootpath) - value(instance project manager) in workspace manager - it's like this project is opened in workpace maanager
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public async Task<ProjectManager> GetOrCreateAsync(string root)
        {
            // Normalize path — tránh "C:/proj" vs "C:/proj/" tạo 2 instance
            var normalized = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);

            // ConcurrentDictionary.GetOrAdd không đảm bảo factory chỉ chạy 1 lần
            // → dùng lock để tránh tạo 2 instance đồng thời
            if (_projectManagers.TryGetValue(normalized, out var existing))
                return existing;

            await _lock.WaitAsync();
            try
            {
                // Double-check sau khi có lock
                if (_projectManagers.TryGetValue(normalized, out existing))
                    return existing;

                //use factory to create instance of ProjectManager
                var manager = _factory.Create(root);
                _projectManagers[normalized] = manager;
                return manager;
            }
            finally
            {
                _lock.Release();
            }
        }


    }
}

