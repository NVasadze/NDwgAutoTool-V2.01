using NDwgAutoTool.Infrastructure.Repositories;
using NDwgAutoTool.Infrastructure.Settings;

namespace NDwgAutoTool.Services
{
    public sealed class ResourceLocator
    {
        private static readonly object Sync = new();

        // Loaded from last saved session (or empty if none)
        private static string _rootPath = LoadLastRootPath();

        private readonly ResourceRepository _resources;

        public ResourceLocator()
            : this(ResourceRepository.Shared)
        {
        }

        public ResourceLocator(ResourceRepository resources)
        {
            _resources = resources;
        }

        private static string LoadLastRootPath()
        {
            var settings = SettingsStore.Load();

            return string.IsNullOrWhiteSpace(settings.LastRootPath)
                ? string.Empty
                : settings.LastRootPath;
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
            string cleaned = (rootPath ?? string.Empty)
                .Trim()
                .Trim('"');

            if (string.IsNullOrWhiteSpace(cleaned))
                return;

            lock (Sync)
                _rootPath = cleaned;

            // persist last used path
            SettingsStore.Save(new AppSettings
            {
                LastRootPath = cleaned
            });

            // refresh repository cache
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