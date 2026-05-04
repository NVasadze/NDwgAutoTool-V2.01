using NDwgAutoTool.Application.Abstractions;
using NDwgAutoTool.Infrastructure.Caching;
using NDwgAutoTool.Models;
using OfficeOpenXml;
using System.IO;

namespace NDwgAutoTool.Services
{
    public class BomReaderService : IBomRepository
    {
        private static readonly FileBackedCache<Dictionary<string, List<BomRow>>> Cache =
            new(LoadBomRowsByDrawing);

        public List<BomRow> ReadBom(string filePath, string drawingPartNo)
        {
            return GetRowsForDrawing(filePath, drawingPartNo).ToList();
        }

        public IReadOnlyList<BomRow> GetRowsForDrawing(string filePath, string drawingPartNo)
        {
            var rowsByDrawing = Cache.Get(filePath);

            if (!rowsByDrawing.TryGetValue(drawingPartNo, out var rows))
                return new List<BomRow>();

            return rows.Select(Clone).ToList();
        }

        private static Dictionary<string, List<BomRow>> LoadBomRowsByDrawing(string filePath)
        {
            ExcelPackage.License.SetNonCommercialPersonal("NDwgAutoTool");

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets["PARTS_LIST"];

            if (worksheet == null)
                throw new Exception("Sheet PARTS_LIST not found.");

            var result = new Dictionary<string, List<BomRow>>(StringComparer.OrdinalIgnoreCase);
            int row = 2;

            while (true)
            {
                var partNo = worksheet.Cells[row, 2].Text.Trim();

                if (string.IsNullOrWhiteSpace(partNo))
                    break;

                var subPart = worksheet.Cells[row, 4].Text.Trim();
                var nomenclature = worksheet.Cells[row, 5].Text.Trim();
                var qtyText = worksheet.Cells[row, 6].Text.Trim();
                var flagYesNo = worksheet.Cells[row, 9].Text.Trim();
                var noteCode = worksheet.Cells[row, 10].Text.Trim();

                int.TryParse(qtyText, out int qty);

                if (!result.TryGetValue(partNo, out var rows))
                {
                    rows = new List<BomRow>();
                    result[partNo] = rows;
                }

                rows.Add(new BomRow
                {
                    PartNo = partNo,
                    SubPartNo = subPart,
                    Nomenclature = nomenclature,
                    Quantity = qty,
                    FlagYesNo = flagYesNo,
                    NoteCode = noteCode,
                    FlagNote = flagYesNo,
                    Note = noteCode
                });

                row++;
            }

            return result;
        }

        private static BomRow Clone(BomRow row)
        {
            return new BomRow
            {
                PartNo = row.PartNo,
                SubPartNo = row.SubPartNo,
                Nomenclature = row.Nomenclature,
                Quantity = row.Quantity,
                FlagYesNo = row.FlagYesNo,
                NoteCode = row.NoteCode,
                FlagNote = row.FlagNote,
                Note = row.Note
            };
        }
    }
}
