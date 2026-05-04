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
                    matches.Add(new DrawingFileMatch(drawingNumber, filePath));
                else
                    missing.Add(drawingNumber);
            }

            missingDrawingNumbers = missing;
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
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            };

            return Directory
                .EnumerateFiles(rootFolder, "*.slddrw", options)
                .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
                .GroupBy(path => NormalizeDrawingNumber(Path.GetFileNameWithoutExtension(path)), StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(path => path.Length).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).First(),
                    StringComparer.OrdinalIgnoreCase);
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
    }

    public sealed record DrawingFileMatch(string DrawingNumber, string FilePath);
}
