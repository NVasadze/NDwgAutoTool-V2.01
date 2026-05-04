using NDwgAutoTool.Application.Abstractions;
using NDwgAutoTool.Services;
using System.IO;

namespace NDwgAutoTool.Infrastructure.Repositories
{
    public sealed class ResourceRepository : IResourceRepository
    {
        private static readonly Lazy<ResourceRepository> SharedInstance = new(() => new ResourceRepository());
        private readonly object _sync = new();
        private ResourceSnapshot? _snapshot;

        public static ResourceRepository Shared => SharedInstance.Value;

        public string RootPath => Snapshot.RootPath;
        public string WorkListFile => Snapshot.WorkListFile;
        public string NotesFile => Snapshot.NotesFile;
        public string BomFile => Snapshot.BomFile;
        public string Form3Folder => Snapshot.Form3Folder;
        public string Form3Template => Snapshot.Form3Template;
        public string NoteBlockFolder => Snapshot.NoteBlockFolder;
        public IReadOnlyList<string> NoteBlockFiles => Snapshot.NoteBlockFiles;

        public void Refresh()
        {
            lock (_sync)
                _snapshot = null;
        }

        private ResourceSnapshot Snapshot
        {
            get
            {
                lock (_sync)
                    return _snapshot ??= BuildSnapshot();
            }
        }

        private static ResourceSnapshot BuildSnapshot()
        {
            string root = ResourceLocator.RequiredRootPath;
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Project folder not found: {root}");

            var files = Directory
                .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => !Path.GetFileName(path).Equals("Type3Output_Template.xls", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var directories = Directory
                .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                .ToList();

            string noteBlockFolder = ResolveNoteBlockFolder(root);

            var noteBlockFiles = Directory
                .EnumerateFiles(noteBlockFolder, "*.SLDBLK", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName)
                .ToList();

            if (noteBlockFiles.Count == 0)
                throw new FileNotFoundException($"No .SLDBLK files were found in {noteBlockFolder}.");

            string form3Template = Path.Combine(AppContext.BaseDirectory, "Type3Output_Template.xls");
            if (!File.Exists(form3Template))
                throw new FileNotFoundException($"Form 3 template was not found next to the program EXE: {form3Template}");

            return new ResourceSnapshot(
                RootPath: root,
                WorkListFile: FindFile(files, IsWorkList, "WORK_LIST workbook"),
                NotesFile: FindFile(files, IsNotesWorkbook, "N-DWG notes workbook"),
                BomFile: FindFile(files, IsBomWorkbook, "BOM workbook"),
                Form3Folder: FindDirectory(
                    directories,
                    path =>
                    {
                        string name = Path.GetFileName(path);
                        return name.Equals("FORM3", StringComparison.OrdinalIgnoreCase) ||
                               name.Equals("FORM 3", StringComparison.OrdinalIgnoreCase);
                    },
                    "FORM 3 folder"),
                Form3Template: form3Template,
                NoteBlockFolder: noteBlockFolder,
                NoteBlockFiles: noteBlockFiles);
        }

        private static string ResolveNoteBlockFolder(string projectRoot)
        {
            string? fromProject = TryBuildNoteBlockFolderFromProjectRoot(projectRoot);
            if (!string.IsNullOrWhiteSpace(fromProject) && Directory.Exists(fromProject))
                return fromProject;

            const string fallback = @"L:\PROJECT\N-panel DWG-task\TOOLS\N-DWG Note block";
            if (Directory.Exists(fallback))
                return fallback;

            throw new DirectoryNotFoundException(
                $"N-DWG Note block folder was not found. Expected: {fromProject ?? fallback}");
        }

        private static string? TryBuildNoteBlockFolderFromProjectRoot(string projectRoot)
        {
            try
            {
                DirectoryInfo? current = new DirectoryInfo(projectRoot);

                while (current != null)
                {
                    if (current.Name.Equals("N-panel DWG-task", StringComparison.OrdinalIgnoreCase))
                        return Path.Combine(current.FullName, "TOOLS", "N-DWG Note block");

                    current = current.Parent;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string FindFile(IEnumerable<string> files, Func<string, bool> match, string label)
        {
            string? result = files.FirstOrDefault(match);
            if (string.IsNullOrWhiteSpace(result))
                throw new FileNotFoundException($"{label} was not found under {ResourceLocator.RequiredRootPath}.");

            return result;
        }

        private static string FindDirectory(IEnumerable<string> directories, Func<string, bool> match, string label)
        {
            string? result = directories.FirstOrDefault(match);
            if (string.IsNullOrWhiteSpace(result))
                throw new DirectoryNotFoundException($"{label} was not found under {ResourceLocator.RequiredRootPath}.");

            return result;
        }

        private static bool IsWorkList(string path)
        {
            string fileName = Path.GetFileName(path);
            return IsExcel(path) && (
                fileName.EndsWith("WORK_LIST.xlsx", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("WORK_LIST.xlsm", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("WORK_LIST", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsNotesWorkbook(string path)
        {
            string fileName = Path.GetFileName(path);
            return IsExcel(path) && (
                fileName.Contains("N-DWG\u81EA\u52D5\u30C4\u30FC\u30EB", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("N-DWG Auto Tool", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBomWorkbook(string path)
        {
            string fileName = Path.GetFileName(path);
            return IsExcel(path) && (
                fileName.Contains("\u90E8\u54C1\u8868", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("PARTS LIST", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsExcel(string path)
        {
            return path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record ResourceSnapshot(
            string RootPath,
            string WorkListFile,
            string NotesFile,
            string BomFile,
            string Form3Folder,
            string Form3Template,
            string NoteBlockFolder,
            IReadOnlyList<string> NoteBlockFiles);
    }
}
