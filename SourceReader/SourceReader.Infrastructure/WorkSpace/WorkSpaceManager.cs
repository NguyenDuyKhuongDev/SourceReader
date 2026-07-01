using SourceReader.Core.Services.Project;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser;
using SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile;
using SourceReader.Infrastructure.DataModel;
using SourceReader.Infrastructure.Factory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace SourceReader.Infrastructure.WorkSpace
{
    public sealed class WorkSpaceManager
    {
        private readonly ProjectManagerFactory _factory;
        // Key: rootPath ->  Value: ProjectManager
        public readonly ConcurrentDictionary<string, ProjectManager> _projectManagers = new();
        // Key: RoothPath -> Value: 
        public List<(string name, string root)> _projectCacheds = new();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly string CACHED_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceReader", "cache");


        public WorkSpaceManager(ProjectManagerFactory factory)
        {
            _factory = factory;
            LoadCachedProject();
        }

     
        // Dùng:
        //var manager = await WorkspaceManager.Instance.GetOrCreateAsync("C:/projectA");
        //var index = await manager.LoadOrScanningAsync(ct);

        public async void LoadCachedProject()
        {
            string[] folders= Directory.GetDirectories(CACHED_DIR);

            foreach (var folder in folders)
            {
                _projectCacheds.Add((Path.GetFileNameWithoutExtension(folder), folder));
            }
        }

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

