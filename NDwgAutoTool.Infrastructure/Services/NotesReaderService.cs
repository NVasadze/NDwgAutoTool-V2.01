using NDwgAutoTool.Application.Abstractions;
using NDwgAutoTool.Infrastructure.Caching;
using OfficeOpenXml;
using System.IO;
using System.Text.RegularExpressions;

namespace NDwgAutoTool.Services
{
    public class NotesReaderService : INotesRepository
    {
        private const string WorksheetName = "Note,\u69D8\u5F0F3\u4F5C\u696DSheet";

        private static readonly FileBackedCache<NotesWorkbookIndex> Cache =
            new(LoadNotesWorkbook);

        public List<string> GetRequiredNoteTokens(string filePath, string drawingPartNo)
        {
            return GetRequiredNoteTokensCore(filePath, drawingPartNo).ToList();
        }

        IReadOnlyList<string> INotesRepository.GetRequiredNoteTokens(string filePath, string drawingPartNo)
        {
            return GetRequiredNoteTokensCore(filePath, drawingPartNo);
        }

        public string GetLeaderedFlagCode(string filePath, string drawingPartNo)
        {
            var index = Cache.Get(filePath);
            string normalizedTarget = NormalizeDrawingNumber(drawingPartNo);

            return index.LeaderedFlagByDrawing.TryGetValue(normalizedTarget, out string? value)
                ? value
                : "";
        }

        private static IReadOnlyList<string> GetRequiredNoteTokensCore(string filePath, string drawingPartNo)
        {
            var index = Cache.Get(filePath);
            string normalizedTarget = NormalizeDrawingNumber(drawingPartNo);

            if (!index.RequiredTokensByDrawing.TryGetValue(normalizedTarget, out var tokens) || tokens.Count == 0)
                throw new Exception($"Drawing {drawingPartNo} not found in sheet '{WorksheetName}'.");

            return tokens.ToList();
        }

        private static NotesWorkbookIndex LoadNotesWorkbook(string filePath)
        {
            ExcelPackage.License.SetNonCommercialPersonal("NDwgAutoTool");

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets[WorksheetName];

            if (worksheet == null)
                throw new Exception($"Sheet '{WorksheetName}' not found in N-DWG Auto Tool file.");

            var requiredTokens = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var leaderedFlags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            int lastRow = worksheet.Dimension?.End.Row ?? 0;

            for (int row = 1; row <= lastRow; row++)
            {
                string rawA = worksheet.Cells[row, 1].Text.Trim();
                string rawB = worksheet.Cells[row, 2].Text.Trim();

                if (!string.IsNullOrWhiteSpace(rawA))
                {
                    string normalizedA = NormalizeDrawingNumber(rawA);

                    if (!leaderedFlags.ContainsKey(normalizedA))
                        leaderedFlags[normalizedA] = rawB;

                    if (row >= 21 && rawB.Equals("SE03", StringComparison.OrdinalIgnoreCase))
                        AddToken(requiredTokens, normalizedA, "SE03");
                }

                if (row >= 21)
                {
                    string rawD = worksheet.Cells[row, 4].Text.Trim();
                    string rawF = worksheet.Cells[row, 6].Text.Trim();

                    if (!string.IsNullOrWhiteSpace(rawD) && !string.IsNullOrWhiteSpace(rawF))
                    {
                        string normalizedD = NormalizeDrawingNumber(rawD);
                        AddToken(requiredTokens, normalizedD, rawF);
                    }
                }
            }

            return new NotesWorkbookIndex(
                requiredTokens.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                    StringComparer.OrdinalIgnoreCase),
                leaderedFlags);
        }

        private static void AddToken(Dictionary<string, HashSet<string>> target, string drawingPartNo, string token)
        {
            if (string.IsNullOrWhiteSpace(drawingPartNo) || string.IsNullOrWhiteSpace(token))
                return;

            if (!target.TryGetValue(drawingPartNo, out var tokens))
            {
                tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                target[drawingPartNo] = tokens;
            }

            tokens.Add(token.Trim());
        }

        private static string NormalizeDrawingNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Trim().ToUpperInvariant();

            var match = Regex.Match(text, @"[A-Z]{4}\d{6}[A-Z]\d{4}");
            if (match.Success)
                return match.Value;

            return text.Replace(" ", "").Replace("\r", "").Replace("\n", "");
        }

        private sealed record NotesWorkbookIndex(
            Dictionary<string, List<string>> RequiredTokensByDrawing,
            Dictionary<string, string> LeaderedFlagByDrawing);
    }
}
