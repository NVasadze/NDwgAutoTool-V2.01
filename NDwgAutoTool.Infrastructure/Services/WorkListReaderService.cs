using NDwgAutoTool.Application.Abstractions;
using NDwgAutoTool.Infrastructure.Caching;
using NDwgAutoTool.Models;
using OfficeOpenXml;
using System.IO;
using System.Text.RegularExpressions;

namespace NDwgAutoTool.Services
{
    public class WorkListReaderService : IWorkListRepository
    {
        private static readonly FileBackedCache<WorkListIndex> Cache = new(LoadWorkList);

        public string GetTitleFromWorkList(string filePath, string drawingPartNo)
        {
            return GetTitle(filePath, drawingPartNo);
        }

        public string GetTitle(string filePath, string drawingPartNo)
        {
            var index = Cache.Get(filePath);

            if (!index.TitlesByDrawing.TryGetValue(drawingPartNo, out string? title))
                throw new Exception($"Drawing {drawingPartNo} not found in WORK_LIST PARTS_LIST sheet.");

            if (string.IsNullOrWhiteSpace(title))
                throw new Exception($"Title is empty for drawing {drawingPartNo} in WORK_LIST.");

            return title;
        }

        public SignatureInfo GetSignatureFromWorkList(string filePath, string drawingPartNo)
        {
            return GetSignature(filePath, drawingPartNo);
        }

        public SignatureInfo GetSignature(string filePath, string drawingPartNo)
        {
            var index = Cache.Get(filePath);
            string normalizedDrawing = NormalizeDrawingNumber(drawingPartNo);

            if (string.IsNullOrWhiteSpace(normalizedDrawing))
                throw new Exception("Drawing number is empty.");

            if (!index.SignaturesByDrawing.TryGetValue(normalizedDrawing, out var signature))
                throw new Exception($"Drawing {normalizedDrawing} not found in WORK_LIST DATA sheet column B.");

            if (string.IsNullOrWhiteSpace(signature.Date))
                throw new Exception("Signature date is empty in WORK_LIST DATA sheet.");

            if (!HasSignatureNames(signature))
                throw new Exception($"Signature information is incomplete for drawing {normalizedDrawing} in WORK_LIST DATA sheet.");

            return Clone(signature);
        }

        public string GetProjectCode(string filePath)
        {
            return Cache.Get(filePath).ProjectCode;
        }

        private static WorkListIndex LoadWorkList(string filePath)
        {
            ExcelPackage.License.SetNonCommercialPersonal("NDwgAutoTool");

            using var package = new ExcelPackage(new FileInfo(filePath));
            var partsList = package.Workbook.Worksheets["PARTS_LIST"];

            if (partsList == null)
                throw new Exception("Sheet PARTS_LIST not found in WORK_LIST file.");

            var titles = ReadTitles(partsList);

            var data = package.Workbook.Worksheets["DATA"];
            if (data == null)
                throw new Exception("Sheet DATA not found in WORK_LIST file.");

            string projectCode = ReadProjectCode(data);
            var signaturesByDrawing = ReadSignaturesByDrawing(data);

            return new WorkListIndex(titles, projectCode, signaturesByDrawing);
        }

        private static Dictionary<string, string> ReadTitles(ExcelWorksheet worksheet)
        {
            var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int partNumberColumn = FindPartsListColumn(worksheet, 2, "PARTNUMBER", "PARTNO", "PART");
            int titleColumn = FindPartsListColumn(worksheet, 3, "NOMENCLATURE", "NOMEN", "NAME", "TITLE");

            if (worksheet.Dimension == null)
                return titles;

            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                string partNo = NormalizeDrawingNumber(worksheet.Cells[row, partNumberColumn].Text);
                if (string.IsNullOrWhiteSpace(partNo))
                    continue;

                titles[partNo] = worksheet.Cells[row, titleColumn].Text.Trim();
            }

            return titles;
        }

        private static int FindPartsListColumn(ExcelWorksheet worksheet, int fallbackColumn, params string[] expectedHeaders)
        {
            if (worksheet.Dimension == null)
                return fallbackColumn;

            for (int column = 1; column <= worksheet.Dimension.End.Column; column++)
            {
                string header = NormalizeHeader(worksheet.Cells[1, column].Text);
                if (expectedHeaders.Any(expectedHeader =>
                    header.Equals(expectedHeader, StringComparison.OrdinalIgnoreCase) ||
                    header.Contains(expectedHeader, StringComparison.OrdinalIgnoreCase)))
                {
                    return column;
                }
            }

            return fallbackColumn;
        }

        private static string NormalizeHeader(string value)
        {
            return value
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("_", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .Trim()
                .ToUpperInvariant();
        }

        private static string ReadProjectCode(ExcelWorksheet worksheet)
        {
            string value = worksheet.Cells[1, 9].Text.Trim();

            if (TryNormalizeProjectCode(value, out string projectCode))
                return projectCode;

            throw new Exception("Project code is empty or invalid in WORK_LIST DATA sheet cell I1.");
        }

        private static Dictionary<string, SignatureInfo> ReadSignaturesByDrawing(ExcelWorksheet worksheet)
        {
            string date = ReadSignatureDate(worksheet);

            var signatures = new Dictionary<string, SignatureInfo>(StringComparer.OrdinalIgnoreCase);

            if (worksheet.Dimension == null)
                return signatures;

            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                string drawingNumber = NormalizeDrawingNumber(worksheet.Cells[row, 2].Text);
                if (!IsDrawingNumber(drawingNumber))
                    continue;

                signatures[drawingNumber] = new SignatureInfo
                {
                    Date = date,
                    Engineer = worksheet.Cells[row, 3].Text.Trim(),
                    Checker = worksheet.Cells[row, 4].Text.Trim(),
                    Approver = worksheet.Cells[row, 5].Text.Trim()
                };
            }

            return signatures;
        }

        private static string ReadSignatureDate(ExcelWorksheet worksheet)
        {
            string rawDate = FindSignatureDateCellText(worksheet);

            if (DateTime.TryParse(rawDate, out var parsedDate))
                return parsedDate.ToString("yyyy/MM/dd");

            return rawDate;
        }

        private static string FindSignatureDateCellText(ExcelWorksheet worksheet)
        {
            if (worksheet.Dimension != null)
            {
                int endRow = Math.Min(worksheet.Dimension.End.Row, 10);
                int endColumn = Math.Min(worksheet.Dimension.End.Column, 20);

                for (int row = 1; row <= endRow; row++)
                {
                    for (int column = 1; column <= endColumn; column++)
                    {
                        string label = worksheet.Cells[row, column].Text.Trim();
                        if (!label.Equals("DATE", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string valueToRight = worksheet.Cells[row, column + 1].Text.Trim();
                        if (!string.IsNullOrWhiteSpace(valueToRight))
                            return valueToRight;
                    }
                }
            }

            string h1 = worksheet.Cells[1, 8].Text.Trim();
            if (!h1.Equals("DATE", StringComparison.OrdinalIgnoreCase))
                return h1;

            return worksheet.Cells[1, 9].Text.Trim();
        }

        private static string NormalizeProjectCode(string raw)
        {
            string value = raw.Trim().ToUpper();

            var aircraftMatch = Regex.Match(value, @"^[A-Z]+\d+");
            if (aircraftMatch.Success)
                return aircraftMatch.Value;

            var digitsMatch = Regex.Match(value, @"^\d+");
            if (digitsMatch.Success)
                return digitsMatch.Value;

            var lettersMatch = Regex.Match(value, @"^[A-Z]+");
            if (lettersMatch.Success)
                return lettersMatch.Value;

            return value;
        }

        private static bool TryNormalizeProjectCode(string raw, out string projectCode)
        {
            projectCode = string.Empty;
            string value = raw.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(value) ||
                value == "NO" ||
                value == "\u2116" ||
                value == "DATE" ||
                value == "ENGINEER" ||
                value == "CHECKER" ||
                value == "APPROVER" ||
                Regex.IsMatch(value, @"^\d+$") ||
                IsDrawingNumber(value))
            {
                return false;
            }

            projectCode = NormalizeProjectCode(value);
            return !string.IsNullOrWhiteSpace(projectCode);
        }

        private static SignatureInfo Clone(SignatureInfo signature)
        {
            return new SignatureInfo
            {
                Date = signature.Date,
                Approver = signature.Approver,
                Checker = signature.Checker,
                Engineer = signature.Engineer
            };
        }

        private static bool HasSignatureNames(SignatureInfo signature)
        {
            return !string.IsNullOrWhiteSpace(signature.Approver) &&
                   !string.IsNullOrWhiteSpace(signature.Checker) &&
                   !string.IsNullOrWhiteSpace(signature.Engineer);
        }

        private static string NormalizeDrawingNumber(string value)
        {
            string source = (value ?? string.Empty).Trim().Trim('"', '\'');

            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

            string drawingNumber = Path.GetFileNameWithoutExtension(source).Trim().ToUpperInvariant();
            var match = Regex.Match(drawingNumber, @"[A-Z]{4}\d{6}[A-Z]\d{4}", RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Value.ToUpperInvariant();

            int revisionSeparator = drawingNumber.IndexOf('_');
            if (revisionSeparator > 0)
                drawingNumber = drawingNumber[..revisionSeparator];

            return drawingNumber.Trim();
        }

        private static bool IsDrawingNumber(string value)
        {
            return Regex.IsMatch(value, @"^[A-Z]{4}\d{6}[A-Z]\d{4}$", RegexOptions.IgnoreCase);
        }

        private sealed record WorkListIndex(
            Dictionary<string, string> TitlesByDrawing,
            string ProjectCode,
            Dictionary<string, SignatureInfo> SignaturesByDrawing);
    }
}
