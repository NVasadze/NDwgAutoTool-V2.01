using NDwgAutoTool.Application.Abstractions;
using NDwgAutoTool.Infrastructure.Repositories;
using System.IO;
using System.Text.RegularExpressions;

namespace NDwgAutoTool.Services
{
    public class NoteBlockLibraryService : INoteBlockRepository
    {
        private readonly IResourceRepository _resources;

        public NoteBlockLibraryService()
            : this(ResourceRepository.Shared)
        {
        }

        public NoteBlockLibraryService(IResourceRepository resources)
        {
            _resources = resources;
        }

        public List<string> GetOptionalBlockFileNames()
        {
            return _resources.NoteBlockFiles
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Where(name =>
                    !name.StartsWith("{", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("A305_", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("ICE_", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("787_", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .ToList();
        }

        IReadOnlyList<string> INoteBlockRepository.GetOptionalBlockFileNames()
        {
            return GetOptionalBlockFileNames();
        }

        public List<string> GetCharacteristicBlockFileNames()
        {
            return _resources.NoteBlockFiles
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Where(name => name.StartsWith("{", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name)
                .ToList();
        }

        IReadOnlyList<string> INoteBlockRepository.GetCharacteristicBlockFileNames()
        {
            return GetCharacteristicBlockFileNames();
        }

        public string GetBlockFileByName(string blockFileName)
        {
            if (string.IsNullOrWhiteSpace(blockFileName))
                throw new Exception("Block file name is empty.");

            var match = _resources.NoteBlockFiles
                .FirstOrDefault(f => string.Equals(Path.GetFileName(f), blockFileName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(match))
                throw new Exception($"Block file not found: {blockFileName}");

            return match;
        }

        public string FindMatchingBlockFile(string projectCode, List<string> requiredCodes)
        {
            return FindMatchingBlockFile(projectCode, (IReadOnlyList<string>)requiredCodes);
        }

        public string FindMatchingBlockFile(string projectCode, IReadOnlyList<string> requiredCodes)
        {
            if (requiredCodes == null || requiredCodes.Count == 0)
                throw new Exception("Required note codes are empty.");

            var requiredSet = new HashSet<string>(requiredCodes, StringComparer.OrdinalIgnoreCase);
            var files = string.IsNullOrWhiteSpace(projectCode)
                ? new List<string>()
                : _resources.NoteBlockFiles
                    .Where(f => Path.GetFileNameWithoutExtension(f)
                        .StartsWith(projectCode + "_", StringComparison.OrdinalIgnoreCase))
                    .ToList();

            string? projectMatch = FindExactCodeMatch(files, requiredSet);
            if (!string.IsNullOrWhiteSpace(projectMatch))
                return projectMatch;

            if (files.Count > 0)
                throw new Exception(
                    $"No exact matching .SLDBLK found for project '{projectCode}' and notes [{string.Join(", ", requiredCodes)}].");

            var fallbackMatches = FindExactCodeMatches(_resources.NoteBlockFiles, requiredSet);

            if (fallbackMatches.Count == 1)
                return fallbackMatches[0];

            if (fallbackMatches.Count > 1)
                throw new Exception(
                    $"Multiple .SLDBLK files match notes [{string.Join(", ", requiredCodes)}]. Add a project code to WORK_LIST DATA or make the note block code set unique.");

            if (string.IsNullOrWhiteSpace(projectCode))
                throw new Exception(
                    $"Project code was not found in WORK_LIST DATA and no .SLDBLK matched notes [{string.Join(", ", requiredCodes)}].");

            throw new Exception(
                $"No .SLDBLK files found for project '{projectCode}' in block folder.");
        }

        public List<string> ExpandExcelNoteTokens(List<string> rawTokens)
        {
            return ExpandExcelNoteTokens((IEnumerable<string>)rawTokens).ToList();
        }

        public IReadOnlyList<string> ExpandExcelNoteTokens(IEnumerable<string> rawTokens)
        {
            var result = new List<string>();

            foreach (var token in rawTokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                string t = token.Trim().ToUpper();

                if (t == "AAG01_T01")
                {
                    result.Add("G01");
                    result.Add("T01");
                    continue;
                }

                if (Regex.IsMatch(t, @"^(G\d+|T\d+|B\d+|SE\d+)$", RegexOptions.IgnoreCase))
                    result.Add(t);
            }

            return result
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void Refresh()
        {
            _resources.Refresh();
        }

        private static List<string> ParseCodesFromFileName(string fileNameWithoutExtension)
        {
            return fileNameWithoutExtension
                .Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToUpper())
                .Where(x => Regex.IsMatch(x, @"^(G\d+|T\d+|B\d+|SE\d+)$", RegexOptions.IgnoreCase))
                .ToList();
        }

        private static string? FindExactCodeMatch(IEnumerable<string> files, HashSet<string> requiredSet)
        {
            var matches = FindExactCodeMatches(files, requiredSet);
            return matches.Count == 1 ? matches[0] : null;
        }

        private static List<string> FindExactCodeMatches(IEnumerable<string> files, HashSet<string> requiredSet)
        {
            var matches = new List<string>();

            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                var fileCodes = ParseCodesFromFileName(fileName);
                var fileSet = new HashSet<string>(fileCodes, StringComparer.OrdinalIgnoreCase);

                if (requiredSet.SetEquals(fileSet))
                    matches.Add(file);
            }

            return matches;
        }
    }
}
