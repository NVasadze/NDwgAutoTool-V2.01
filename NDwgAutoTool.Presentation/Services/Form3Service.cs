using Microsoft.Win32;
using NDwgAutoTool.Form3;
using System.IO;
using System.Windows;
using Excel = Microsoft.Office.Interop.Excel;

namespace NDwgAutoTool.Services
{
    public class Form3Service
    {
        private readonly Form3SolidWorksService _swService;
        private readonly Action<string>? _log;

        public Form3Service(Action<string>? log = null)
        {
            _swService = new Form3SolidWorksService(log);
            _log = log;
        }

        private void Log(string message)
        {
            _log?.Invoke(message);
        }

        public int GetRealDrawingViewCount()
        {
            return _swService.GetRealDrawingViewCount();
        }

        public string GetDefaultOutputFileName()
        {
            return _swService.GetOutputFileName();
        }

        public string GetActiveDrawingFolderPath()
        {
            var model = _swService.GetActiveModel();
            if (model == null)
                throw new Exception("No active SolidWorks document.");

            string modelPath = model.GetPathName();
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new Exception("Drawing must be saved before creating Form 3.");

            string? folder = Path.GetDirectoryName(modelPath);
            if (string.IsNullOrWhiteSpace(folder))
                throw new Exception("Could not determine drawing folder.");

            return folder;
        }

        private bool ConfirmOverwrite(string path)
        {
            if (!File.Exists(path))
                return true;

            return NDwgAutoTool.StyledConfirmWindow.ShowConfirm(
                title: "File Already Exists",
                question: "Would you like to replace the existing file?",
                details: path,
                owner: System.Windows.Application.Current.MainWindow);
        }

        public void CreateForm3(List<string>? additionalNoteNumbers = null)
        {
            string defaultName = GetDefaultOutputFileName();
            Log($"Form3 default output name: {defaultName}");

            var dlg = new SaveFileDialog
            {
                Filter = "Excel file (*.xls)|*.xls",
                Title = "Save Form 3",
                FileName = defaultName,
                AddExtension = true,
                DefaultExt = "xls",
                OverwritePrompt = false
            };

            if (dlg.ShowDialog() != true)
            {
                Log("Form3 save was cancelled.");
                return;
            }

            string outputPath = dlg.FileName;
            if (!outputPath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                outputPath += ".xls";

            CreateForm3ToPath(outputPath, additionalNoteNumbers, showSuccessPopup: true);
        }

        public void CreateForm3ToPath(
            string outputPath,
            List<string>? additionalNoteNumbers = null,
            bool showSuccessPopup = false)
        {
            Excel.Application? excel = null;
            Excel.Workbook? wb = null;

            try
            {
                Log("Form3 started.");

                var rows = _swService.BuildRowsForExport(additionalNoteNumbers);
                Log($"Form3 rows built: {rows.Count}");

                string templatePath = new ResourceLocator().FindForm3Template();

                Log($"Form3 template path: {templatePath}");

                if (!File.Exists(templatePath))
                    throw new Exception("Template not found:\n" + templatePath);

                if (!outputPath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                    outputPath += ".xls";

                Log($"Form3 output path: {outputPath}");

                if (!ConfirmOverwrite(outputPath))
                {
                    Log("Form3: user chose not to overwrite existing file.");
                    throw new OperationCanceledException("User chose not to overwrite existing file.");
                }

                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xls");
                File.Copy(templatePath, tempFile, true);
                Log($"Form3 temp file created: {tempFile}");

                excel = new Excel.Application();
                excel.Visible = false;
                excel.DisplayAlerts = false;
                excel.ScreenUpdating = false;
                excel.EnableEvents = false;

                Log("Excel application opened.");

                wb = excel.Workbooks.Open(tempFile);
                Log("Workbook opened.");

                string partNo = _swService.GetPartNumber().Trim();
                string partName = _swService.GetNomenclature().Trim();

                Log($"Form3 Part Number: {partNo}");
                Log($"Form3 Part Name: {partName}");

                Excel.Worksheet? wsTemplate = null;
                Excel.Worksheet? wsNoData = null;

                foreach (Excel.Worksheet ws in wb.Worksheets)
                {
                    if (string.Equals(ws.Name, "Template", StringComparison.OrdinalIgnoreCase))
                        wsTemplate = ws;
                    else if (string.Equals(ws.Name, "Template_NoData", StringComparison.OrdinalIgnoreCase))
                        wsNoData = ws;
                }

                if (wsTemplate == null)
                    throw new Exception("Worksheet 'Template' not found.");

                Log("Template sheet found.");

                Excel.Worksheet wsMain = wsTemplate;

                if (wsNoData != null)
                {
                    Log("Deleting Template_NoData sheet.");
                    wsNoData.Delete();
                }

                while (wb.Worksheets.Count > 1)
                {
                    Excel.Worksheet extra = (Excel.Worksheet)wb.Worksheets[wb.Worksheets.Count];
                    if (!ReferenceEquals(extra, wsMain))
                    {
                        Log($"Deleting extra sheet: {extra.Name}");
                        extra.Delete();
                    }
                    else
                    {
                        break;
                    }
                }

                const int rowsPerPage = 14;
                const int pageBlockHeight = 27;
                const int firstDataRowInPage = 9;
                const int lastDataRowInPage = 22;

                int totalPages = Math.Max(1, (int)Math.Ceiling((double)rows.Count / rowsPerPage));
                Log($"Form3 total pages: {totalPages}");

                if (totalPages > 1)
                {
                    Excel.Range sourcePageRange = wsMain.Rows["1:27"];

                    for (int page = 2; page <= totalPages; page++)
                    {
                        int targetStartRow = ((page - 1) * pageBlockHeight) + 1;
                        string targetRangeAddress = $"{targetStartRow}:{targetStartRow + pageBlockHeight - 1}";

                        Log($"Copying page block to rows {targetRangeAddress}");

                        Excel.Range targetPageRange = wsMain.Rows[targetRangeAddress];
                        sourcePageRange.Copy(targetPageRange);
                    }

                    excel.CutCopyMode = 0;
                    Log("Additional page blocks copied.");
                }

                for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
                {
                    int pageBaseRow = pageIndex * pageBlockHeight;
                    int dataStartRow = pageBaseRow + firstDataRowInPage;
                    int dataEndRow = pageBaseRow + lastDataRowInPage;

                    Log($"Filling page {pageIndex + 1}: rows {dataStartRow}-{dataEndRow}");

                    for (int r = dataStartRow; r <= dataEndRow; r++)
                    {
                        wsMain.Cells[r, 2] = "";
                        wsMain.Cells[r, 3] = "";
                        wsMain.Cells[r, 4] = "";
                        wsMain.Cells[r, 5] = "";
                    }

                    Excel.Range? pnHeader = wsMain.Cells.Find(
                        "1.部品番号/ Part Number",
                        wsMain.Cells[pageBaseRow + 1, 1]);

                    if (pnHeader != null)
                        wsMain.Cells[pnHeader.Row + 1, pnHeader.Column] = partNo;
                    else
                        wsMain.Cells[pageBaseRow + 5, 2] = partNo;

                    Excel.Range? nameHeader = wsMain.Cells.Find(
                        "2.部品名称/ Part Name",
                        wsMain.Cells[pageBaseRow + 1, 1]);

                    if (nameHeader != null)
                        wsMain.Cells[nameHeader.Row + 1, nameHeader.Column] = partName;
                    else
                        wsMain.Cells[pageBaseRow + 5, 5] = partName;

                    wsMain.Cells[pageBaseRow + 2, 10] = $"Sheet {pageIndex + 1} of _{totalPages}";

                    int startIndex = pageIndex * rowsPerPage;
                    int endIndex = Math.Min(startIndex + rowsPerPage, rows.Count);

                    int excelRow = dataStartRow;

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        var row = rows[i];

                        wsMain.Cells[excelRow, 2] = row.CharNo;
                        wsMain.Cells[excelRow, 3] = row.ReferenceLocation;
                        wsMain.Cells[excelRow, 4] = row.Designator;
                        wsMain.Cells[excelRow, 5] = row.Requirement;

                        excelRow++;
                    }
                }

                wsMain.Name = "Form3";

                wb.Save();
                wb.SaveCopyAs(outputPath);

                wb.Close(false);
                excel.Quit();

                wb = null;
                excel = null;

                try
                {
                    File.Delete(tempFile);
                }
                catch
                {
                    Log("Temp file could not be deleted.");
                }

                Log($"Form3 completed successfully -> {outputPath}");

                if (showSuccessPopup)
                    StyledMessageWindow.ShowMessage("info", "Form 3 created successfully.");
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (excel != null)
                    {
                        excel.ScreenUpdating = true;
                        excel.EnableEvents = true;
                    }

                    wb?.Close(false);
                    excel?.Quit();
                }
                catch
                {
                }

                Log("Form3 cancelled.");
            }
            catch (Exception ex)
            {
                try
                {
                    if (excel != null)
                    {
                        excel.ScreenUpdating = true;
                        excel.EnableEvents = true;
                    }

                    wb?.Close(false);
                    excel?.Quit();
                }
                catch
                {
                }

                Log("Form3 failed: " + ex);
                throw;
            }
        }

        public string FindForm3OutputFolder()
        {
            var fileFinder = new FileFinderService();
            string? folder = fileFinder.FindForm3Folder();

            if (string.IsNullOrWhiteSpace(folder))
                throw new Exception("Could not find a folder named FORM3 or FORM 3 under the configured root folder.");

            Log($"Form3 output folder found: {folder}");
            return folder;
        }
    }
}
