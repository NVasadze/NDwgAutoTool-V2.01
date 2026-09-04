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
        public string WorkListFile => FindFile(Snapshot.Files, IsWorkList, "WORK_LIST workbook");
        public string NotesFile => FindFile(Snapshot.Files, IsNotesWorkbook, "N-DWG notes workbook");
        public string BomFile => FindFile(Snapshot.Files, IsBomWorkbook, "BOM workbook");
        public string Form3Folder => FindDirectory(Snapshot.Directories, IsForm3Folder, "FORM 3 folder");
        public string Form3Template => ResolveForm3Template();
        public string NoteBlockFolder => ResolveNoteBlockFolder();
        public IReadOnlyList<string> NoteBlockFiles => ResolveNoteBlockFiles(NoteBlockFolder);

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

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            };

            var files = Directory
                .EnumerateFiles(root, "*.*", options)
                .Where(path => !Path.GetFileName(path).Equals("Type3Output_Template.xls", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var directories = Directory
                .EnumerateDirectories(root, "*", options)
                .ToList();

            return new ResourceSnapshot(root, files, directories);
        }

        private static IReadOnlyList<string> ResolveNoteBlockFiles(string noteBlockFolder)
        {
            var noteBlockFiles = Directory
                .EnumerateFiles(noteBlockFolder, "*.SLDBLK", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName)
                .ToList();

            if (noteBlockFiles.Count == 0)
                throw new FileNotFoundException($"No .SLDBLK files were found in {noteBlockFolder}.");

            return noteBlockFiles;
        }

        private static string ResolveForm3Template()
        {
            string form3Template = Path.Combine(AppContext.BaseDirectory, "Type3Output_Template.xls");
            if (!File.Exists(form3Template))
                throw new FileNotFoundException($"Form 3 template was not found next to the program EXE: {form3Template}");

            return form3Template;
        }

        private static string ResolveNoteBlockFolder()
        {
            const string noteBlockFolder = @"L:\PROJECT\N-panel DWG-task\TOOLS\N-DWG Note block";
            if (Directory.Exists(noteBlockFolder))
                return noteBlockFolder;

            throw new DirectoryNotFoundException(
                $"N-DWG Note block folder was not found. Expected: {noteBlockFolder}");
        }

        private static string FindFile(IEnumerable<string> files, Func<string, bool> match, string label)
        {
            string? result = files
                .Where(match)
                .OrderByDescending(GetResourceRevisionRank)
                .ThenByDescending(IsNewResourceVersion)
                .ThenBy(path => path.Length)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

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
            string normalizedName = NormalizeResourceName(fileName);

            return IsExcel(path) && (
                fileName.EndsWith("WORK_LIST.xlsx", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("WORK_LIST.xlsm", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("WORK_LIST", StringComparison.OrdinalIgnoreCase) ||
                normalizedName.Contains("WORKLIST", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsNotesWorkbook(string path)
        {
            string fileName = Path.GetFileName(path);
            string normalizedName = NormalizeResourceName(fileName);

            return IsExcel(path) && (
                fileName.Contains("N-DWG\u81EA\u52D5\u30C4\u30FC\u30EB", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("N-DWG Auto Tool", StringComparison.OrdinalIgnoreCase) ||
                normalizedName.Contains("NDWG\u81EA\u52D5\u30C4\u30FC\u30EB", StringComparison.OrdinalIgnoreCase) ||
                normalizedName.Contains("NDWGAUTOTOOL", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBomWorkbook(string path)
        {
            string fileName = Path.GetFileName(path);
            string normalizedName = NormalizeResourceName(fileName);

            return IsExcel(path) && (
                fileName.Contains("\u90E8\u54C1\u8868", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("PARTS LIST", StringComparison.OrdinalIgnoreCase) ||
                normalizedName.Contains("PARTSLIST", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsForm3Folder(string path)
        {
            string name = Path.GetFileName(path);
            return name.Equals("FORM3", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("FORM 3", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExcel(string path)
        {
            return path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".xls", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetResourceRevisionRank(string path)
        {
            string fileName = StripNewResourceSuffix(Path.GetFileNameWithoutExtension(path).Trim());
            int revisionSeparator = fileName.LastIndexOf('_');

            if (revisionSeparator < 0 || revisionSeparator == fileName.Length - 1)
                return 0;

            string revision = NormalizeRevisionToken(fileName[(revisionSeparator + 1)..]);

            return GetRevisionRank(revision);
        }

        private static string NormalizeRevisionToken(string value)
        {
            string revision = value.Trim().ToUpperInvariant();

            if (revision == "--")
                return "--";

            if (revision.StartsWith("-", StringComparison.Ordinal))
                revision = revision[1..].Trim();

            return new string(revision.Where(c => c >= 'A' && c <= 'Z').ToArray());
        }

        private static int GetRevisionRank(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision) || revision == "--")
                return 0;

            if (revision.Length == 1)
                return (revision[0] - 'A') + 1;

            if (revision.Length == 2 && revision[0] == 'Z')
                return 26 + (revision[1] - 'A') + 1;

            return 26 + revision.Sum(c => (c - 'A') + 1);
        }

        private static bool IsNewResourceVersion(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path).Trim();
            return fileName.EndsWith("_NEW", StringComparison.OrdinalIgnoreCase);
        }

        private static string StripNewResourceSuffix(string fileName)
        {
            const string newSuffix = "_NEW";

            return fileName.EndsWith(newSuffix, StringComparison.OrdinalIgnoreCase)
                ? fileName[..^newSuffix.Length].Trim()
                : fileName;
        }

        private static string NormalizeResourceName(string fileName)
        {
            return fileName
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("_", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .Trim();
        }

        private sealed record ResourceSnapshot(
            string RootPath,
            IReadOnlyList<string> Files,
            IReadOnlyList<string> Directories);
    }
}
