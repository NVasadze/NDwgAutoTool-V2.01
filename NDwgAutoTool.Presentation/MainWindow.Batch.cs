using Microsoft.Win32;
using NDwgAutoTool.Services;
using SldWorks;
using SwConst;
using System.Windows;

namespace NDwgAutoTool
{
    public partial class MainWindow
    {
        private static readonly char[] BatchDrawingNumberSeparators = ['\r', '\n', '\t', ' ', ',', ';'];

        private async void RunDrawingBatchSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var drawingNumbers = ParseInlineDrawingNumbers(DrawingBatchNumbersTextBox.Text);

            if (drawingNumbers.Count == 0)
            {
                StyledMessageWindow.ShowMessage("Drawing Batch", "Paste at least one drawing number.", this);
                DrawingBatchNumbersTextBox.Focus();
                return;
            }

            bool openSelected = DrawingBatchOpenAllCheckBox.IsChecked == true;
            bool saveSelected = DrawingBatchSaveAllCheckBox.IsChecked == true;
            bool closeSelected = DrawingBatchCloseAllCheckBox.IsChecked == true;

            if (!openSelected && !saveSelected && !closeSelected)
            {
                StyledMessageWindow.ShowMessage("Drawing Batch", "Select at least one operation.", this);
                return;
            }

            if (saveSelected && !ConfirmBatchSave("Drawing Batch"))
                return;

            SetLastAction("Drawing Batch");
            SetStatus("Working...");
            AddLog($"Drawing Batch: requested {drawingNumbers.Count} drawing number(s).");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var fileFinder = new FileFinderService();
                var drawingMatches = fileFinder.FindDrawingFiles(drawingNumbers, out var missingDrawings);

                foreach (string missingDrawing in missingDrawings)
                {
                    failedDrawings.Add($"{missingDrawing}: drawing file not found");
                    AddLog($"Drawing Batch: missing -> {missingDrawing}");
                }

                processed = missingDrawings.Count;
                failed = missingDrawings.Count;

                if (drawingMatches.Count == 0)
                {
                    StyledMessageWindow.ShowMessage("Drawing Batch", "No matching drawing files were found.", this);
                    return;
                }

                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var saveService = new SaveAllService(AddLog);

                progress = new StyledProgressWindow("Drawing Batch", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawingMatches.Count; i++)
                {
                    var match = drawingMatches[i];
                    string drawingName = System.IO.Path.GetFileNameWithoutExtension(match.FilePath);

                    progress.UpdateProgress(
                        "Running selected drawing operations...",
                        drawingName,
                        i + 1,
                        drawingMatches.Count);

                    AddLog($"Drawing Batch: processing {drawingName} -> {match.FilePath}");

                    try
                    {
                        ModelDoc2? drawing = solidWorksService.FindOpenDocumentByPath(swApp, match.FilePath);
                        bool openedByBatch = false;

                        if (drawing == null && (openSelected || saveSelected))
                        {
                            drawing = OpenBatchDrawing(swApp, solidWorksService, match.FilePath, out openedByBatch);
                            AddLog($"Drawing Batch: opened -> {drawingName}");
                        }

                        if (drawing == null)
                        {
                            AddLog($"Drawing Batch: not open, close skipped -> {drawingName}");
                            succeeded++;
                            processed++;
                            await System.Threading.Tasks.Task.Delay(50);
                            continue;
                        }

                        ActivateBatchDrawing(swApp, drawing);

                        if (openSelected && !openedByBatch)
                            AddLog($"Drawing Batch: already open -> {drawingName}");

                        if (saveSelected)
                        {
                            saveService.SaveDrawing(drawing);
                            AddLog($"Drawing Batch: saved -> {drawingName}");
                        }

                        if (closeSelected)
                        {
                            solidWorksService.CloseDocumentWithoutSaving(swApp, drawing);
                            AddLog($"Drawing Batch: closed without saving -> {drawingName}");
                        }

                        succeeded++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add($"{drawingName}: {ex.Message}");
                        AddLog($"Drawing Batch: FAILED -> {drawingName} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Drawing Batch completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult("Drawing Batch", processed, succeeded, failed, failedDrawings, this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);
                progress?.Close();
                StyledMessageWindow.ShowMessage("Drawing Batch Error", ex.Message, this);
            }
            finally
            {
                SetStatus("Ready");
            }
        }

        private async void RunToolBatchSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var drawingNumbers = ParseInlineDrawingNumbers(ToolBatchNumbersTextBox.Text);

            if (drawingNumbers.Count == 0)
            {
                StyledMessageWindow.ShowMessage("Drawing Tools", "Paste at least one drawing number.", this);
                ToolBatchNumbersTextBox.Focus();
                return;
            }

            var selection = GetToolBatchSelection();

            if (!selection.AnySelected)
            {
                StyledMessageWindow.ShowMessage("Drawing Tools", "Select at least one operation.", this);
                return;
            }

            string? form3OutputFolder = null;
            string? checkPdfOutputFolder = null;
            string? noCheckPdfOutputFolder = null;

            if (selection.CreateForm3)
            {
                form3OutputFolder = AskForm3ForAllOutputFolder();
                if (string.IsNullOrWhiteSpace(form3OutputFolder))
                    return;
            }

            if (selection.CreatePdf)
            {
                checkPdfOutputFolder = AskCreatePdfForAllOutputFolder();
                if (string.IsNullOrWhiteSpace(checkPdfOutputFolder))
                    return;
            }

            if (selection.PdfNoCheck)
            {
                noCheckPdfOutputFolder = AskPdfNoCheckOutputFolder();
                if (string.IsNullOrWhiteSpace(noCheckPdfOutputFolder))
                    return;
            }

            SetLastAction("Drawing Tools");
            SetStatus("Working...");
            AddLog($"Drawing Tools: requested {drawingNumbers.Count} drawing number(s).");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var fileFinder = new FileFinderService();
                var drawingMatches = fileFinder.FindDrawingFiles(drawingNumbers, out var missingDrawings);

                foreach (string missingDrawing in missingDrawings)
                {
                    failedDrawings.Add($"{missingDrawing}: drawing file not found");
                    AddLog($"Drawing Tools: missing -> {missingDrawing}");
                }

                processed = missingDrawings.Count;
                failed = missingDrawings.Count;

                if (drawingMatches.Count == 0)
                {
                    StyledMessageWindow.ShowMessage("Drawing Tools", "No matching drawing files were found.", this);
                    return;
                }

                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var pdfService = new PdfService(AddLog);
                var form3Service = new Form3Service(AddLog);

                bool hasDrawingChanges =
                    selection.CreateFlagnotes ||
                    selection.AttachDim4 ||
                    selection.ReverseDim4 ||
                    selection.GenerateNotes ||
                    selection.UpdateSignature ||
                    selection.FillTitleBlock;

                progress = new StyledProgressWindow("Drawing Tools", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawingMatches.Count; i++)
                {
                    var match = drawingMatches[i];
                    string drawingName = System.IO.Path.GetFileNameWithoutExtension(match.FilePath);
                    ModelDoc2? drawing = null;
                    bool openedByBatch = false;

                    progress.UpdateProgress(
                        "Running selected drawing tools...",
                        drawingName,
                        i + 1,
                        drawingMatches.Count);

                    AddLog($"Drawing Tools: processing {drawingName} -> {match.FilePath}");

                    try
                    {
                        drawing = solidWorksService.FindOpenDocumentByPath(swApp, match.FilePath);

                        if (drawing == null)
                            drawing = OpenBatchDrawing(swApp, solidWorksService, match.FilePath, out openedByBatch);
                        else
                            AddLog($"Drawing Tools: drawing already open -> {drawingName}");

                        ActivateBatchDrawing(swApp, drawing);

                        if (selection.CreateFlagnotes)
                            CreateFlagnotesForDrawing(drawing, solidWorksService);

                        if (selection.AttachDim4)
                            RunDim4ForBatchDrawing(drawingName, reverse: false);

                        if (selection.ReverseDim4)
                            RunDim4ForBatchDrawing(drawingName, reverse: true);

                        if (selection.GenerateNotes)
                            GenerateNotesForDrawing(drawing, solidWorksService);

                        if (selection.UpdateSignature)
                            UpdateSignatureForDrawing(drawing, solidWorksService);

                        if (selection.FillTitleBlock)
                            FillTitleBlockForDrawing(drawing, solidWorksService);

                        if (selection.CreateForm3)
                            CreateForm3ForBatchDrawing(form3Service, drawing, form3OutputFolder!);

                        if (selection.CreatePdf)
                            pdfService.CreatePdfFromDrawingToFolder(drawing, checkPdfOutputFolder!);

                        if (selection.PdfNoCheck)
                            pdfService.CreatePdfNoCheckFromDrawing(drawing, noCheckPdfOutputFolder!);

                        succeeded++;
                        AddLog($"Drawing Tools: success -> {drawingName}");
                    }
                    catch (OperationCanceledException)
                    {
                        failed++;
                        failedDrawings.Add(drawingName);
                        AddLog($"Drawing Tools: skipped/cancelled -> {drawingName}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add($"{drawingName}: {ex.Message}");
                        AddLog($"Drawing Tools: FAILED -> {drawingName} | {ex.Message}");
                    }
                    finally
                    {
                        form3Service.SetTargetDrawing(null);

                        if (openedByBatch && drawing != null && !hasDrawingChanges)
                        {
                            try
                            {
                                solidWorksService.CloseDocumentWithoutSaving(swApp, drawing);
                                AddLog($"Drawing Tools: closed without saving -> {drawingName}");
                            }
                            catch (Exception ex)
                            {
                                AddLog($"Drawing Tools: failed to close {drawingName} | {ex.Message}");
                            }
                        }
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Drawing Tools completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult("Drawing Tools", processed, succeeded, failed, failedDrawings, this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);
                progress?.Close();
                StyledMessageWindow.ShowMessage("Drawing Tools Error", ex.Message, this);
            }
            finally
            {
                SetStatus("Ready");
            }
        }

        private async Task RunCheckDwgForDrawingNumbers(IReadOnlyList<string> drawingNumbers)
        {
            SetLastAction("CheckDWG For All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            var allReports = new List<(string DrawingPartNo, List<NDwgAutoTool.Models.CheckDwgReportRow> Rows)>();

            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var fileFinder = new FileFinderService();

                var notesFile = fileFinder.FindNotesFile();
                if (string.IsNullOrWhiteSpace(notesFile))
                    throw new Exception("N-DWG Auto Tool notes file not found.");

                AddLog($"Notes file path: {notesFile}");

                var workListFile = fileFinder.FindWorkListFile();
                if (string.IsNullOrWhiteSpace(workListFile))
                    throw new Exception("WORK_LIST file not found.");

                AddLog($"WORK_LIST file path: {workListFile}");

                var bomFile = fileFinder.FindBomFile();
                if (string.IsNullOrWhiteSpace(bomFile))
                    throw new Exception("BOM file not found.");

                AddLog($"BOM file path: {bomFile}");

                var drawingMatches = fileFinder.FindDrawingFiles(drawingNumbers, out var missingDrawings);

                foreach (string missingDrawing in missingDrawings)
                {
                    failedDrawings.Add($"{missingDrawing}: drawing file not found");
                    AddLog($"CheckDWG For All: missing -> {missingDrawing}");
                }

                processed = missingDrawings.Count;
                failed = missingDrawings.Count;

                if (drawingMatches.Count == 0)
                {
                    StyledMessageWindow.ShowMessage("CheckDWG For All", "No matching drawing files were found.", this);
                    return;
                }

                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                AddLog($"CheckDWG For All: requested {drawingNumbers.Count} drawing number(s).");
                AddLog($"CheckDWG For All: found {drawingMatches.Count} drawing file(s).");

                progress = new StyledProgressWindow("CheckDWG For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawingMatches.Count; i++)
                {
                    var match = drawingMatches[i];
                    string drawingName = System.IO.Path.GetFileNameWithoutExtension(match.FilePath);
                    ModelDoc2? drawing = null;
                    bool openedByBatch = false;

                    progress.UpdateProgress(
                        "Checking drawings...",
                        drawingName,
                        i + 1,
                        drawingMatches.Count);

                    AddLog($"CheckDWG For All: processing {drawingName} -> {match.FilePath}");

                    try
                    {
                        drawing = solidWorksService.FindOpenDocumentByPath(swApp, match.FilePath);

                        if (drawing == null)
                            drawing = OpenBatchDrawing(swApp, solidWorksService, match.FilePath, out openedByBatch);
                        else
                            AddLog($"CheckDWG For All: drawing already open -> {drawingName}");

                        ActivateBatchDrawing(swApp, drawing);

                        string drawingPartNo = GetDrawingPartNoFromModel(drawing);
                        var reportRows = BuildCheckDwgReportRowsForDrawing(
                            drawing,
                            notesFile,
                            workListFile,
                            bomFile,
                            solidWorksService);

                        allReports.Add((drawingPartNo, reportRows));

                        succeeded++;
                        AddLog($"CheckDWG For All: success -> {drawingPartNo}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add($"{drawingName}: {ex.Message}");
                        AddLog($"CheckDWG For All: FAILED -> {drawingName} | {ex.Message}");
                    }
                    finally
                    {
                        if (openedByBatch && drawing != null)
                        {
                            try
                            {
                                solidWorksService.CloseDocumentWithoutSaving(swApp, drawing);
                                AddLog($"CheckDWG For All: closed without saving -> {drawingName}");
                            }
                            catch (Exception ex)
                            {
                                AddLog($"CheckDWG For All: failed to close {drawingName} | {ex.Message}");
                            }
                        }
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("CheckDWG For All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                var saveDialog = new SaveFileDialog
                {
                    Title = "Save CheckDWG For All Report",
                    FileName = "CheckDWG_ForAll.xlsx",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    DefaultExt = ".xlsx",
                    AddExtension = true
                };

                bool? saveResult = saveDialog.ShowDialog();

                if (saveResult != true || string.IsNullOrWhiteSpace(saveDialog.FileName))
                {
                    AddLog("CheckDWG For All report save was cancelled.");
                    return;
                }

                string outputPath = saveDialog.FileName;

                WriteCheckDwgForAllExcelReport(outputPath, allReports);
                AddLog($"CheckDWG For All report created -> {outputPath}");

                BatchResultWindow.ShowResult(
                    "CheckDWG For All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);

                PromptToOpenExcelReport("CheckDWG For All", outputPath, AllCheckReportsAreCorrect(allReports));
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);
                progress?.Close();
                StyledMessageWindow.ShowMessage("CheckDWG For All Error", ex.Message, this);
            }
            finally
            {
                SetStatus("Ready");
            }
        }

        private ToolBatchSelection GetToolBatchSelection()
        {
            return new ToolBatchSelection(
                ToolBatchCreateFlagnotesCheckBox.IsChecked == true,
                ToolBatchAttachDim4CheckBox.IsChecked == true,
                ToolBatchReverseDim4CheckBox.IsChecked == true,
                ToolBatchGenerateNotesCheckBox.IsChecked == true,
                ToolBatchUpdateSignatureCheckBox.IsChecked == true,
                ToolBatchFillTitleBlockCheckBox.IsChecked == true,
                ToolBatchCreateForm3CheckBox.IsChecked == true,
                ToolBatchCreatePdfCheckBox.IsChecked == true,
                ToolBatchPdfNoCheckCheckBox.IsChecked == true);
        }

        private static IReadOnlyList<string> ParseInlineDrawingNumbers(string text)
        {
            return text
                .Split(BatchDrawingNumberSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(FileFinderService.NormalizeRequestedNumber)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ModelDoc2 OpenBatchDrawing(
            ISldWorks swApp,
            SolidWorksService solidWorksService,
            string filePath,
            out bool openedByBatch)
        {
            openedByBatch = true;
            SetOpenAllDocumentTypeVisible(swApp, OpenAllFileKind.Drawing);

            var drawing = solidWorksService.OpenDrawing(
                swApp,
                filePath,
                out int errors,
                out int warnings);

            if (drawing == null)
                throw new Exception($"SolidWorks did not return a drawing. Errors: {errors}; Warnings: {warnings}");

            EnsureOpenAllDocumentVisible(swApp, drawing, filePath);

            return drawing;
        }

        private static void ActivateBatchDrawing(ISldWorks swApp, ModelDoc2 drawing)
        {
            int errors = 0;

            try
            {
                swApp.ActivateDoc3(
                    drawing.GetTitle(),
                    true,
                    (int)swRebuildOnActivation_e.swDontRebuildActiveDoc,
                    ref errors);
            }
            catch
            {
            }
        }

        private void RunDim4ForBatchDrawing(string drawingName, bool reverse)
        {
            string action = reverse ? "Reverse Dim4 For All" : "Attach Dim4 For All";
            string result = new NDwgAutoTool.Dim4.Dim4Service(AddLog).ProcessCharacteristic4(reverse);
            AddLog($"{action} result -> {drawingName} | {result}");

            if (!result.StartsWith("Done", StringComparison.OrdinalIgnoreCase))
                throw new Exception(result);
        }

        private void CreateForm3ForBatchDrawing(Form3Service service, ModelDoc2 drawing, string outputFolder)
        {
            service.SetTargetDrawing(drawing);
            string fileName = service.GetDefaultOutputFileName();
            string outputPath = System.IO.Path.Combine(outputFolder, fileName);

            service.CreateForm3ToPath(
                outputPath,
                null,
                showSuccessPopup: false,
                throwOnCancel: true);
        }

        private string? AskCreatePdfForAllOutputFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select folder for Check PDF output",
                Multiselect = false
            };

            return dialog.ShowDialog(this) == true
                ? dialog.FolderName
                : null;
        }

        private bool ConfirmBatchSave(string title)
        {
            bool confirm1 = StyledConfirmWindow.ShowConfirm(
                title,
                "Are you sure you want to save the selected drawings?",
                "This will save only the drawings listed in this batch.",
                this);

            if (!confirm1)
            {
                AddLog($"{title}: save cancelled at first confirmation.");
                return false;
            }

            bool confirm2 = StyledConfirmWindow.ShowConfirm(
                "Final Confirmation",
                "ARE YOU REALLY SURE?!!!",
                "This action will overwrite selected drawings and cannot be undone.",
                this);

            if (!confirm2)
                AddLog($"{title}: save cancelled at second confirmation.");

            return confirm2;
        }

        private sealed record ToolBatchSelection(
            bool CreateFlagnotes,
            bool AttachDim4,
            bool ReverseDim4,
            bool GenerateNotes,
            bool UpdateSignature,
            bool FillTitleBlock,
            bool CreateForm3,
            bool CreatePdf,
            bool PdfNoCheck)
        {
            public bool AnySelected =>
                CreateFlagnotes ||
                AttachDim4 ||
                ReverseDim4 ||
                GenerateNotes ||
                UpdateSignature ||
                FillTitleBlock ||
                CreateForm3 ||
                CreatePdf ||
                PdfNoCheck;
        }
    }
}



