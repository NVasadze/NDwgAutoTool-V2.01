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

        public SignatureInfo GetSignatureFromWorkList(string filePath)
        {
            return GetSignature(filePath);
        }

        public SignatureInfo GetSignature(string filePath)
        {
            return Clone(Cache.Get(filePath).Signature);
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

            var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int row = 2;

            while (true)
            {
                string partNo = partsList.Cells[row, 3].Text.Trim();
                if (string.IsNullOrWhiteSpace(partNo))
                    break;

                titles[partNo] = partsList.Cells[row, 4].Text.Trim();
                row++;
            }

            var data = package.Workbook.Worksheets["DATA"];
            if (data == null)
                throw new Exception("Sheet DATA not found in WORK_LIST file.");

            string projectCode = ReadProjectCode(data);
            SignatureInfo signature = ReadSignature(data);

            return new WorkListIndex(titles, projectCode, signature);
        }

        private static string ReadProjectCode(ExcelWorksheet worksheet)
        {
            for (int row = 1; row <= 50; row++)
            {
                string value = worksheet.Cells[row, 1].Text.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return NormalizeProjectCode(value);
            }

            throw new Exception("Project code not found in WORK_LIST DATA sheet column A.");
        }

        private static SignatureInfo ReadSignature(ExcelWorksheet worksheet)
        {
            var result = new SignatureInfo();
            var rawDate = worksheet.Cells[1, 4].Text.Trim();

            if (DateTime.TryParse(rawDate, out var parsedDate))
                result.Date = parsedDate.ToString("yyyy/MM/dd");
            else
                result.Date = rawDate;

            int row = 2;

            while (true)
            {
                string role = worksheet.Cells[row, 3].Text.Trim();
                string name = worksheet.Cells[row, 4].Text.Trim();

                if (string.IsNullOrWhiteSpace(role) && string.IsNullOrWhiteSpace(name))
                    break;

                if (role.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                    result.Approver = name;
                else if (role.Equals("Checked", StringComparison.OrdinalIgnoreCase))
                    result.Checker = name;
                else if (role.Equals("Engineered", StringComparison.OrdinalIgnoreCase))
                    result.Engineer = name;

                row++;
            }

            if (string.IsNullOrWhiteSpace(result.Date))
                throw new Exception("Signature date is empty in WORK_LIST DATA sheet.");

            return result;
        }

        private static string NormalizeProjectCode(string raw)
        {
            string value = raw.Trim().ToUpper();

            var digitsMatch = Regex.Match(value, @"^\d+");
            if (digitsMatch.Success)
                return digitsMatch.Value;

            var lettersMatch = Regex.Match(value, @"^[A-Z]+");
            if (lettersMatch.Success)
                return lettersMatch.Value;

            return value;
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

        private sealed record WorkListIndex(
            Dictionary<string, string> TitlesByDrawing,
            string ProjectCode,
            SignatureInfo Signature);
    }
}
