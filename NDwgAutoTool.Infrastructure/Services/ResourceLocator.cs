using NDwgAutoTool.Infrastructure.Repositories;
using System.IO;

namespace NDwgAutoTool.Services
{
    public sealed class ResourceLocator
    {
        private static readonly object Sync = new();
        private static string _rootPath = @"L:\PROJECT\N-panel DWG-task\003_AINB87-004_N-DWG";

        private readonly ResourceRepository _resources;

        public ResourceLocator()
            : this(ResourceRepository.Shared)
        {
        }

        public ResourceLocator(ResourceRepository resources)
        {
            _resources = resources;
        }

        public static string RequiredRootPath
        {
            get
            {
                lock (Sync)
                    return _rootPath;
            }
        }

        public static void SetRootPath(string? rootPath)
        {
            string cleaned = (rootPath ?? string.Empty).Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(cleaned))
                return;

            lock (Sync)
                _rootPath = cleaned;

            ResourceRepository.Shared.Refresh();
        }

        public string RootPath => _resources.RootPath;

        public string GetRootPath() => _resources.RootPath;
        public string FindWorkListFile() => _resources.WorkListFile;
        public string FindNotesFile() => _resources.NotesFile;
        public string FindBomFile() => _resources.BomFile;
        public string FindForm3Folder() => _resources.Form3Folder;
        public string FindNoteBlockFolder() => _resources.NoteBlockFolder;
        public string FindForm3Template() => _resources.Form3Template;
        public void Refresh() => _resources.Refresh();
    }
}
