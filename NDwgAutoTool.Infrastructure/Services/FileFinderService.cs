namespace NDwgAutoTool.Services
{
    public class FileFinderService
    {
        private readonly ResourceLocator _resources = new ResourceLocator();

        public string GetRootFolder()
        {
            return _resources.GetRootPath();
        }

        public void RefreshCache()
        {
            _resources.Refresh();
        }

        public string? FindBomFile()
        {
            return FindOrNull(_resources.FindBomFile);
        }

        public string? FindWorkListFile()
        {
            return FindOrNull(_resources.FindWorkListFile);
        }

        public string? FindNotesFile()
        {
            return FindOrNull(_resources.FindNotesFile);
        }

        public string? FindForm3Folder()
        {
            return FindOrNull(_resources.FindForm3Folder);
        }

        public IReadOnlyList<DrawingFileMatch> FindDrawingFiles(
            IEnumerable<string> drawingNumbers,
            out IReadOnlyList<string> missingDrawingNumbers)
        {
            var requestedNumbers = drawingNumbers
                .Select(NormalizeDrawingNumber)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var drawingIndex = BuildDrawingIndex(GetRootFolder());
            var matches = new List<DrawingFileMatch>();
            var missing = new List<string>();

            foreach (string drawingNumber in requestedNumbers)
            {
                if (drawingIndex.TryGetValue(drawingNumber, out string? filePath))
                    matches.Add(new DrawingFileMatch(drawingNumber, filePath, GetRevisionLabel(filePath)));
                else
                    missing.Add(drawingNumber);
            }

            missingDrawingNumbers = missing;
            return matches;
        }

        public IReadOnlyList<ModelFileMatch> FindAssemblyFiles(
            IEnumerable<string> modelNumbers,
            out IReadOnlyList<string> missingModelNumbers)
        {
            var requestedNumbers = modelNumbers
                .Select(NormalizeDrawingNumber)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var assemblyIndex = BuildRevisionedFileIndex(GetRootFolder(), "*.sldasm");
            var matches = new List<ModelFileMatch>();
            var missing = new List<string>();

            foreach (string modelNumber in requestedNumbers)
            {
                if (assemblyIndex.TryGetValue(modelNumber, out string? filePath))
                    matches.Add(new ModelFileMatch(modelNumber, filePath, GetRevisionLabel(filePath)));
                else
                    missing.Add(modelNumber);
            }

            missingModelNumbers = missing;
            return matches;
        }

        public IReadOnlyList<ContainerFileMatch> FindContainerFiles(
            IEnumerable<string> containerNumbers,
            out IReadOnlyList<string> missingContainerNumbers)
        {
            var requestedNumbers = containerNumbers
                .Select(NormalizeDrawingNumber)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var containerIndex = BuildContainerIndex(GetRootFolder());
            var matches = new List<ContainerFileMatch>();
            var missing = new List<string>();

            foreach (string containerNumber in requestedNumbers)
            {
                if (containerIndex.TryGetValue(containerNumber, out string? filePath))
                    matches.Add(new ContainerFileMatch(containerNumber, filePath, GetRevisionLabel(filePath)));
                else
                    missing.Add(containerNumber);
            }

            missingContainerNumbers = missing;
            return matches;
        }

        private static string? FindOrNull(Func<string> find)
        {
            try
            {
                return find();
            }
            catch
            {
                return null;
            }
        }

        private static Dictionary<string, string> BuildDrawingIndex(string rootFolder)
        {
            return BuildRevisionedFileIndex(rootFolder, "*.slddrw");
        }

        private static Dictionary<string, string> BuildContainerIndex(string rootFolder)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            };

            var drawingDirectories = Directory
                .EnumerateFiles(rootFolder, "*.slddrw", options)
                .Where(IsSolidWorksUserFile)
                .Select(path => Path.GetDirectoryName(path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var assemblyDirectories = Directory
                .EnumerateFiles(rootFolder, "*.sldasm", options)
                .Where(IsSolidWorksUserFile)
                .Select(path => Path.GetDirectoryName(path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return Directory
                .EnumerateFiles(rootFolder, "*.sldprt", options)
                .Where(IsSolidWorksUserFile)
                .Where(path =>
                {
                    string? directory = Path.GetDirectoryName(path);

                    return !string.IsNullOrWhiteSpace(directory) &&
                           drawingDirectories.Contains(directory) &&
                           !assemblyDirectories.Contains(directory);
                })
                .GroupBy(path => NormalizeDrawingNumber(Path.GetFileNameWithoutExtension(path)), StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(GetRevisionRank)
                        .ThenBy(path => path.Length)
                        .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .First(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> BuildFileIndex(string rootFolder, string searchPattern)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            };

            return Directory
                .EnumerateFiles(rootFolder, searchPattern, options)
                .Where(IsSolidWorksUserFile)
                .GroupBy(path => NormalizeDrawingNumber(Path.GetFileNameWithoutExtension(path)), StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(path => path.Length).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).First(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, string> BuildRevisionedFileIndex(string rootFolder, string searchPattern)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            };

            return Directory
                .EnumerateFiles(rootFolder, searchPattern, options)
                .Where(IsSolidWorksUserFile)
                .GroupBy(path => NormalizeDrawingNumber(Path.GetFileNameWithoutExtension(path)), StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(GetRevisionRank)
                        .ThenBy(path => path.Length)
                        .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .First(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static int GetRevisionRank(string path)
        {
            string revision = GetRevisionLabel(path);

            if (revision == "--")
                return 0;

            if (revision.Length == 1)
                return (revision[0] - 'A') + 1;

            if (revision.Length == 2 && revision[0] == 'Z')
                return 26 + (revision[1] - 'A') + 1;

            return 26 + revision.Sum(c => (c - 'A') + 1);
        }

        public static string GetRevisionLabel(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path).Trim();
            int revisionSeparator = fileName.IndexOf('_');

            if (revisionSeparator < 0 || revisionSeparator == fileName.Length - 1)
                return "--";

            return NormalizeRevisionToken(fileName[(revisionSeparator + 1)..]);
        }

        private static string NormalizeRevisionToken(string value)
        {
            string revision = value.Trim().ToUpperInvariant();

            if (revision == "--")
                return "--";

            if (revision.StartsWith("-", StringComparison.Ordinal))
                revision = revision[1..].Trim();

            string letters = new string(revision.Where(c => c >= 'A' && c <= 'Z').ToArray());

            return string.IsNullOrWhiteSpace(letters) ? "--" : letters;
        }

        private static bool IsSolidWorksUserFile(string path)
        {
            return !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal);
        }

        private static string NormalizeDrawingNumber(string value)
        {
            string trimmed = value.Trim().Trim('"', '\'');

            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            string drawingNumber = Path.GetFileNameWithoutExtension(trimmed).Trim();
            int revisionSeparator = drawingNumber.IndexOf('_');

            if (revisionSeparator > 0)
                drawingNumber = drawingNumber[..revisionSeparator];

            return drawingNumber.Trim();
        }

        public static string NormalizeRequestedNumber(string value)
        {
            return NormalizeDrawingNumber(value);
        }
    }

    public sealed record DrawingFileMatch(string DrawingNumber, string FilePath, string Revision);
    public sealed record ModelFileMatch(string ModelNumber, string FilePath, string Revision);
    public sealed record ContainerFileMatch(string ContainerNumber, string FilePath, string Revision);
}
