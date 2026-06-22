using SourceReader.Core.Services.Project;
using SourceReader.Infrastructure.Analysis.AstAnalysis.PriorityProcessFile;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.WorkSpace
{
    public sealed class WorkSpaceManager : IDisposable
    {
        //in here i can just signup singeton in program.cs so it will automatically inject and disspose for me
        // singleton pattern , only use 1 instance of workspace manager in the application flow .
        private static readonly WorkSpaceManager _instance = new WorkSpaceManager();
        public static WorkSpaceManager Instance => _instance;

        public readonly ConcurrentDictionary<string, ProjectManager> _projectManagers = new();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        // Dùng:
        //var manager = await WorkspaceManager.Instance.GetOrCreateAsync("C:/projectA");
        //var index = await manager.LoadOrScanningAsync(ct);

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

                var manager = new ProjectManager(normalized);
                _projectManagers[normalized] = manager;
                return manager;
            }
            finally
            {
                _lock.Release();
            }
        }

        //ShutDown/CLose project
        public void Close(string root)
        {
            var normalized = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            if (_projectManagers.TryRemove(normalized, out var manager))
                manager.Dispose();
        }

        //SHut down workspace
        public void Dispose()
        {
            foreach (var project in _projectManagers.Values)
            {
                project.Dispose();
            }
            _instance.Dispose();
        }
    }
}

