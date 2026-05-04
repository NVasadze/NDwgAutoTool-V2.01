using Microsoft.Win32;
using NDwgAutoTool.Helpers;
using NDwgAutoTool.Models;
using NDwgAutoTool.Services;
using SwConst;
using System.Windows;
using System.Windows.Input;

namespace NDwgAutoTool
{
    public partial class MainWindow : Window
    {
        private const double CompactWindowWidth = 210;
        private const double CompactWindowHeight = 430;
        private const double ExpandedWindowWidth = 820;
        private const double ExpandedWindowHeight = 430;
        private const double LeftPanelWidth = 190;
        private const double RightPanelWidth = 190;
        private const double PanelGap = 10;

        private readonly NDwgAutoTool.Composition.AppServices _services = NDwgAutoTool.Composition.AppServices.Current;
        private UiCommandRunner _commandRunner = null!;

        private void WindowDragArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                WindowMoveResizeHelper.Move(this);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaxRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaxRestoreButton.Content = "□";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaxRestoreButton.Content = "❐";
            }
        }

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaxRestoreButton_Click(sender, new RoutedEventArgs());
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (MaxRestoreButton != null)
                MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        private void ApplySelected23Button_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.RunWithResult(
                "Apply Selected 2/3",
                () =>
                {
                    var blockLibraryService = new NoteBlockLibraryService();
                    var blockFiles = blockLibraryService.GetCharacteristicBlockFileNames();

                    if (blockFiles.Count == 0)
                        return "No { blocks were found in the note block folder.";

                    string? selectedBlockFile = StyledBlockChoiceWindow.ShowSingleChoice(
                        blockFiles,
                        "Apply Selected 2/3",
                        "Select one block to insert at every dimension in the selected view.",
                        this);

                    if (string.IsNullOrWhiteSpace(selectedBlockFile))
                    {
                        AddLog("Apply Selected 2/3: no block selected.");
                        return "No block was inserted.";
                    }

                    string blockPath = blockLibraryService.GetBlockFileByName(selectedBlockFile);
                    return new NDwgAutoTool.Dim4.Dim4Service(AddLog)
                        .ApplySelectedCharacteristicBlockToView(blockPath, selectedBlockFile);
                });
        }

        private bool _isCompactMode = true;
        private const string ShowMorePassword = "1234"; // Change this password
        private bool _showMorePasswordAccepted = false;


        public MainWindow()
        {
            InitializeComponent();
            _commandRunner = new UiCommandRunner(this, AddLog, SetLastAction, SetStatus);
            MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
            LoadNetworkRootPathIntoUi();
            ApplyCompactLayout();
            AddLog("NDwgAutoTool V2.01 started.");
            AddLog($"Resource root: {NDwgAutoTool.Services.ResourceLocator.RequiredRootPath}");
            LogResourceAvailability();
        }


        private bool FlagNoteExistsNear(List<DrawingNoteInfo> notes, string text, double x, double y, double tolerance = 0.01)
        {
            return notes.Any(n =>
                n.IsFlagNote &&
                n.FlagCode.Equals(text, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(n.X - x) <= tolerance &&
                Math.Abs(n.Y - y) <= tolerance);
        }

        private void CreateFlagnotesButton_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.RunWithResult(
                "Create Flagnotes",
                () =>
                {
                    var (activeDoc, solidWorksService) = _services.ActiveDrawingProvider.GetActiveDrawing();
                    return CreateFlagnotesForDrawing(activeDoc, solidWorksService);
                },
                errorMessage: ex => $"Failed to create flagnotes.\n\n{ex.Message}");
        }

        private void GenerateNotesButton_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.RunWithResult(
                "Generate Notes",
                () =>
                {
                    var (activeDoc, solidWorksService) = _services.ActiveDrawingProvider.GetActiveDrawing();
                    string result = GenerateNotesForDrawing(activeDoc, solidWorksService);
                    string optionalBlockResult = InsertOptionalBlocksAfterNotes(activeDoc, solidWorksService);
                    return result + "\n\n" + optionalBlockResult;
                },
                errorMessage: ex => $"Failed to generate notes.\n\n{ex.Message}");
        }

        private string InsertOptionalBlocksAfterNotes(SldWorks.ModelDoc2 activeDoc, SolidWorksService solidWorksService)
        {
            var blockLibraryService = new NoteBlockLibraryService();
            var optionalBlockFiles = blockLibraryService.GetOptionalBlockFileNames();

            if (optionalBlockFiles.Count == 0)
            {
                AddLog("Optional blocks: none found.");
                return "No optional blocks were available.";
            }

            var selectedBlockFiles = StyledBlockChoiceWindow.ShowChoice(optionalBlockFiles, this);

            if (selectedBlockFiles.Count == 0)
            {
                AddLog("Optional blocks: none selected.");
                return "No optional blocks were inserted.";
            }

            var insertedBlocks = new List<string>();

            foreach (string blockFile in selectedBlockFiles)
            {
                string blockPath = blockLibraryService.GetBlockFileByName(blockFile);
                solidWorksService.InsertNoteBlockAtOrigin(activeDoc, blockPath);
                insertedBlocks.Add(blockFile);
                AddLog($"Inserted optional block: {blockFile}");
            }

            return $"Inserted optional block(s): {string.Join(", ", insertedBlocks)}.";
        }



        private void UpdateSignatureButton_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.RunWithResult(
                "Update Signature",
                () =>
                {
                    var (activeDoc, solidWorksService) = _services.ActiveDrawingProvider.GetActiveDrawing();
                    return UpdateSignatureForDrawing(activeDoc, solidWorksService);
                },
                errorMessage: ex => $"Failed to update signature.\n\n{ex.Message}");
        }

        private void FillTitleBlockButton_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.RunWithResult(
                "Fill Title Block",
                () =>
                {
                    var (activeDoc, solidWorksService) = _services.ActiveDrawingProvider.GetActiveDrawing();
                    return FillTitleBlockForDrawing(activeDoc, solidWorksService);
                },
                errorMessage: ex => $"Failed to fill title block.\n\n{ex.Message}");
        }

        private void CheckDwgButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("CheckDWG");
            SetStatus("Working...");

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

                string drawingPartNo = GetCurrentDrawingPartNo();
                AddLog($"Current drawing: {drawingPartNo}");

                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var activeDoc = solidWorksService.GetActiveDocument(swApp);

                if (activeDoc is null)
                    throw new Exception("No active SolidWorks document.");

                if (!solidWorksService.IsDrawing(activeDoc))
                    throw new Exception("Active document is not a drawing.");

                var notes = solidWorksService.GetAllNotesOnActiveSheet(activeDoc);

                var notesReader = new NotesReaderService();
                var rawTokens = notesReader.GetRequiredNoteTokens(notesFile, drawingPartNo);
                AddLog($"Raw Excel note tokens -> {string.Join(", ", rawTokens)}");

                var workListProjectReader = new WorkListProjectReaderService();
                string projectCode = workListProjectReader.GetProjectCode(workListFile);
                AddLog($"Project code -> {projectCode}");

                var blockLibraryService = new NoteBlockLibraryService();
                var requiredCodes = blockLibraryService.ExpandExcelNoteTokens(rawTokens);
                AddLog($"Required note codes -> {string.Join(", ", requiredCodes)}");

                string drawingNumber = drawingPartNo;
                string sheetNumber = "";

                int aIndex = drawingPartNo.IndexOf('A');
                if (aIndex > 0 && aIndex < drawingPartNo.Length - 1)
                {
                    drawingNumber = drawingPartNo.Substring(0, aIndex);
                    string suffix = drawingPartNo.Substring(aIndex + 1);
                    sheetNumber = "71" + suffix;
                }

                var workListReader = new WorkListReaderService();
                string title = workListReader.GetTitleFromWorkList(workListFile, drawingPartNo);

                var bomReader = new BomReaderService();
                var bomRows = bomReader.ReadBom(bomFile, drawingPartNo);

                var expectedSubparts = GetExpectedSubpartQuantities(bomRows);
                var actualSubparts = GetActualSubpartQuantities(notes);
                int holeCount = CountHoleCallouts(notes);

                var reportRows = new List<NDwgAutoTool.Models.CheckDwgReportRow>();

                reportRows.AddRange(BuildTitleBlockReportRows(
                    notes,
                    drawingNumber,
                    sheetNumber,
                    title,
                    solidWorksService));

                reportRows.AddRange(BuildRedReportRows(
                    requiredCodes,
                    notes,
                    notesFile,
                    activeDoc,
                    bomRows));

                reportRows.AddRange(BuildHoleReportRows(holeCount));

                reportRows.AddRange(BuildSubpartReportRows(
                    expectedSubparts,
                    actualSubparts));

                var saveDialog = new SaveFileDialog
                {
                    Title = "Save CheckDWG Report",
                    FileName = $"{drawingPartNo}_Checked.xlsx",
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    DefaultExt = ".xlsx",
                    AddExtension = true
                };

                bool? saveResult = saveDialog.ShowDialog();

                if (saveResult != true || string.IsNullOrWhiteSpace(saveDialog.FileName))
                {
                    AddLog("CheckDWG report save was cancelled.");
                    SetStatus("Ready");
                    return;
                }

                string outputPath = saveDialog.FileName;

                WriteCheckDwgExcelReport(outputPath, reportRows, drawingPartNo);

                AddLog($"CheckDWG report created -> {outputPath}");
                StyledMessageWindow.ShowMessage("CheckDWG completed successfully.", "CheckDWG");
            }
            catch (Exception ex)
            {
                AddLog($"ERROR: {ex.Message}");
                StyledMessageWindow.ShowMessage(
                    $"CheckDWG failed.\n\n{ex.Message}",
                    "CheckDWG Error");
            }

            SetStatus("Ready");
        }

        private void CreateForm3Button_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.Run(
                "Create FORM 3",
                () => new NDwgAutoTool.Services.Form3Service(AddLog).CreateForm3(null),
                "Form 3 Error",
                ex => "Error running Form 3:\n\n" + ex);
        }


        private void SaveNetworkPathButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyNetworkRootPathFromUi();
            AddLog($"Resource root set to: {NDwgAutoTool.Services.ResourceLocator.RequiredRootPath}");
            StyledMessageWindow.ShowMessage(
                "Resource Root",
                $"Resource root set to:\n{NDwgAutoTool.Services.ResourceLocator.RequiredRootPath}",
                this);
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            AddLog("Log cleared.");
        }

        private void AttachDim4Button_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.RunWithResult(
                "Attach Dim4",
                () => new NDwgAutoTool.Dim4.Dim4Service(AddLog).ProcessCharacteristic4(false));
        }

        private void ReverseDim4Button_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.RunWithResult(
                "Reverse Dim4",
                () => new NDwgAutoTool.Dim4.Dim4Service(AddLog).ProcessCharacteristic4(true));
        }

        private void CreatePdfButton_Click(object sender, RoutedEventArgs e)
        {
            _commandRunner.RunWithResult(
                "Create PDF",
                () => new NDwgAutoTool.Services.PdfService(AddLog).CreatePdfFromActiveDrawing());
        }

        private async void OpenAllButton_Click(object sender, RoutedEventArgs e)
        {
            var drawingNumbers = OpenAllDrawingsWindow.Ask(this);

            if (drawingNumbers is null || drawingNumbers.Count == 0)
                return;

            SetLastAction("Open All");
            SetStatus("Working...");
            AddLog($"Open All: requested {drawingNumbers.Count} drawing(s).");

            StyledProgressWindow? progress = null;
            var failed = new List<string>();
            IReadOnlyList<string> missing = Array.Empty<string>();
            int opened = 0;

            try
            {
                var fileFinder = new FileFinderService();
                var matches = fileFinder.FindDrawingFiles(drawingNumbers, out missing);

                if (matches.Count == 0)
                {
                    string noMatchResult = BuildOpenAllResult(opened, drawingNumbers.Count, missing, failed);
                    AddLog(noMatchResult);
                    StyledMessageWindow.ShowMessage("Open All", noMatchResult, this);
                    return;
                }

                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                progress = new StyledProgressWindow("Open All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    string fileName = System.IO.Path.GetFileName(match.FilePath);

                    progress.UpdateProgress(
                        "Opening drawings...",
                        fileName,
                        i + 1,
                        matches.Count);

                    try
                    {
                        var drawing = solidWorksService.OpenDrawing(
                            swApp,
                            match.FilePath,
                            out int errors,
                            out int warnings);

                        if (drawing is null)
                            throw new Exception($"SolidWorks did not return a document. Errors: {errors}; Warnings: {warnings}");

                        opened++;
                        AddLog($"Open All: opened {match.DrawingNumber} -> {match.FilePath}");

                        if (errors != 0 || warnings != 0)
                            AddLog($"Open All: SolidWorks reported errors={errors}, warnings={warnings} for {match.DrawingNumber}.");
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{match.DrawingNumber}: {ex.Message}");
                        AddLog($"Open All ERROR: {match.DrawingNumber}: {ex.Message}");
                    }

                    await System.Threading.Tasks.Task.Delay(25);
                }

                string result = BuildOpenAllResult(opened, drawingNumbers.Count, missing, failed);
                AddLog(result);
                StyledMessageWindow.ShowMessage("Open All", result, this);
            }
            catch (Exception ex)
            {
                AddLog("Open All ERROR: " + ex);
                StyledMessageWindow.ShowMessage("Open All Error", ex.Message, this);
            }
            finally
            {
                progress?.Close();
                SetStatus("Ready");
            }
        }

        private static string BuildOpenAllResult(
            int opened,
            int requested,
            IReadOnlyList<string> missing,
            IReadOnlyList<string> failed)
        {
            var result = new System.Text.StringBuilder();
            result.AppendLine($"Requested: {requested}");
            result.AppendLine($"Opened: {opened}");

            if (missing.Count > 0)
            {
                result.AppendLine();
                result.AppendLine("Not found:");
                foreach (string drawingNumber in missing.Take(20))
                    result.AppendLine(drawingNumber);

                if (missing.Count > 20)
                    result.AppendLine($"...and {missing.Count - 20} more");
            }

            if (failed.Count > 0)
            {
                result.AppendLine();
                result.AppendLine("Failed:");
                foreach (string failure in failed.Take(10))
                    result.AppendLine(failure);

                if (failed.Count > 10)
                    result.AppendLine($"...and {failed.Count - 10} more");
            }

            return result.ToString().TrimEnd();
        }

        private async void SaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            // First confirmation
            bool confirm1 = StyledConfirmWindow.ShowConfirm(
                "Save All",
                "Are you sure you want to save all open drawings?",
                "This will save every currently open drawing document.",
                this);

            if (!confirm1)
            {
                AddLog("Save All: cancelled at first confirmation.");
                return;
            }

            // Second confirmation (strong warning)
            bool confirm2 = StyledConfirmWindow.ShowConfirm(
                "⚠ Final Confirmation",
                "ARE YOU REALLY SURE?!!!",
                "This action will overwrite ALL open drawings and cannot be undone.",
                this);

            if (!confirm2)
            {
                AddLog("Save All: cancelled at second confirmation.");
                return;
            }

            SetLastAction("Save All");
            SetStatus("Working...");

            SetLastAction("Save All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var service = new NDwgAutoTool.Services.SaveAllService(AddLog);
                var drawings = service.GetOpenDrawingDocuments();

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Save All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Save All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("Save All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];

                    string drawingName = "";
                    try
                    {
                        drawingName = System.IO.Path.GetFileNameWithoutExtension(drawing.GetPathName());
                        if (string.IsNullOrWhiteSpace(drawingName))
                            drawingName = drawing.GetTitle() ?? $"Drawing_{i + 1}";
                    }
                    catch
                    {
                        drawingName = $"Drawing_{i + 1}";
                    }

                    progress.UpdateProgress(
                        "Saving drawings...",
                        drawingName,
                        i + 1,
                        drawings.Count);

                    AddLog($"Save All: processing {drawingName}");

                    try
                    {
                        int errors = 0;

                        try
                        {
                            swApp.ActivateDoc3(
                                drawing.GetTitle(),
                                true,
                                (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref errors);
                        }
                        catch
                        {
                        }

                        service.SaveDrawing(drawing);

                        succeeded++;
                        AddLog($"Save All: success -> {drawingName}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingName);
                        AddLog($"Save All: FAILED -> {drawingName} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Save All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult(
                    "Save All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "Save All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private void CloseAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Close All");
            SetStatus("Working...");

            try
            {
                var service = new NDwgAutoTool.Services.CloseAllService(AddLog);

                int remaining = service.CloseNormally();

                if (remaining == 0)
                {
                    AddLog("Close All: all documents closed normally.");
                    StyledMessageWindow.ShowMessage(
                        "Close All",
                        "All documents were closed successfully.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Close All: {remaining} document(s) still open.");

                bool forceClose = StyledConfirmWindow.ShowConfirm(
                    title: "Close All",
                    question: $"{remaining} unsaved or protected document(s) are still open. \n\nDo you want to close them without saving?",
                    details: "",
                    owner: System.Windows.Application.Current.MainWindow);                

                if (!forceClose)
                {
                    AddLog("Close All: user chose not to force close unsaved documents.");
                    StyledMessageWindow.ShowMessage(
                        "Close All",
                        $"{remaining} document(s) remain open.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                int finalRemaining = service.ForceCloseRemainingWithoutSaving();

                if (finalRemaining == 0)
                {
                    AddLog("Close All: all remaining documents were force-closed.");
                    StyledMessageWindow.ShowMessage(
                        "Close All",
                        "All remaining documents were closed without saving.",
                        this);
                }
                else
                {
                    AddLog($"Close All: {finalRemaining} document(s) still remain open.");
                    StyledMessageWindow.ShowMessage(
                        "Close All",
                        $"{finalRemaining} document(s) still remain open.",
                        this);
                }
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex.Message);
                StyledMessageWindow.ShowMessage(
                    "Close All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private async void CreateFlagnotesForAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Create Flagnotes For All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var drawings = GetOpenDrawingDocumentsForBatch(swApp);

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Create Flagnotes For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Create Flagnotes For All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("Create Flagnotes For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];
                    string drawingPartNo = GetNormalizedDrawingPartNoFromDoc(drawing, solidWorksService);

                    progress.UpdateProgress(
                        "Creating flagnotes...",
                        drawingPartNo,
                        i + 1,
                        drawings.Count);

                    AddLog($"Create Flagnotes For All: processing {drawingPartNo}");

                    try
                    {
                        int errors = 0;

                        try
                        {
                            swApp.ActivateDoc3(
                                drawing.GetTitle(),
                                true,
                                (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref errors);
                        }
                        catch
                        {
                        }

                        CreateFlagnotesForDrawing(drawing, solidWorksService);

                        succeeded++;
                        AddLog($"Create Flagnotes For All: success -> {drawingPartNo}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingPartNo);
                        AddLog($"Create Flagnotes For All: FAILED -> {drawingPartNo} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Create Flagnotes For All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult(
                    "Create Flagnotes For All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "Create Flagnotes For All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private async void AttachDim4ForAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Attach Dim4 For All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var drawings = GetOpenDrawingDocumentsForBatch(swApp);

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Attach Dim4 For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Attach Dim4 For All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("Attach Dim4 For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];
                    string drawingPartNo = GetDrawingPartNoFromModel(drawing);

                    progress.UpdateProgress(
                        "Attaching Dim4...",
                        drawingPartNo,
                        i + 1,
                        drawings.Count);

                    AddLog($"Attach Dim4 For All: processing {drawingPartNo}");

                    try
                    {
                        int errors = 0;

                        try
                        {
                            swApp.ActivateDoc3(
                                drawing.GetTitle(),
                                true,
                                (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref errors);
                        }
                        catch
                        {
                        }

                        var service = new NDwgAutoTool.Dim4.Dim4Service(AddLog);
                        string result = service.ProcessCharacteristic4(false);

                        AddLog($"Attach Dim4 For All result -> {drawingPartNo} | {result}");

                        if (result.StartsWith("Done", StringComparison.OrdinalIgnoreCase))
                        {
                            succeeded++;
                            AddLog($"Attach Dim4 For All: success -> {drawingPartNo}");
                        }
                        else
                        {
                            failed++;
                            failedDrawings.Add(drawingPartNo);
                            AddLog($"Attach Dim4 For All: FAILED -> {drawingPartNo} | {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingPartNo);
                        AddLog($"Attach Dim4 For All: FAILED -> {drawingPartNo} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Attach Dim4 For All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult(
                    "Attach Dim4 For All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "Attach Dim4 For All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private async void CreateForm3ForAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Create FORM 3 For All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var drawings = GetOpenDrawingDocumentsForBatch(swApp);

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Create FORM 3 For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Create FORM 3 For All: found {drawings.Count} open drawing(s).");

                var service = new NDwgAutoTool.Services.Form3Service(AddLog);
                string outputFolder = service.FindForm3OutputFolder();
                AddLog($"Create FORM 3 For All: output folder -> {outputFolder}");

                progress = new StyledProgressWindow("Create FORM 3 For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];
                    string drawingPartNo = GetDrawingPartNoFromModel(drawing);

                    progress.UpdateProgress(
                        "Creating Form 3 files...",
                        drawingPartNo,
                        i + 1,
                        drawings.Count);

                    AddLog($"Create FORM 3 For All: processing {drawingPartNo}");

                    try
                    {
                        int errors = 0;

                        try
                        {
                            swApp.ActivateDoc3(
                                drawing.GetTitle(),
                                true,
                                (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref errors);
                        }
                        catch
                        {
                        }

                        string fileName = service.GetDefaultOutputFileName();
                        string outputPath = System.IO.Path.Combine(outputFolder, fileName);

                        service.CreateForm3ToPath(outputPath, null, showSuccessPopup: false);

                        succeeded++;
                        AddLog($"Create FORM 3 For All: success -> {drawingPartNo}");
                    }
                    catch (OperationCanceledException)
                    {
                        failed++;
                        failedDrawings.Add(drawingPartNo);
                        AddLog($"Create FORM 3 For All: skipped/cancelled -> {drawingPartNo}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingPartNo);
                        AddLog($"Create FORM 3 For All: FAILED -> {drawingPartNo} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Create FORM 3 For All completed.", processed);

                var resultWindow = new BatchResultWindow(
                    "Create FORM 3 For All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);

                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "Create FORM 3 For All Error",
                    "Error running Create FORM 3 For All:\n\n" + ex,
                    this);
            }

            SetStatus("Ready");
        }


        private async void CreatePdfForAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Create PDF For All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            int succeeded = 0;
            int failed = 0;
            int processed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var pdfService = new NDwgAutoTool.Services.PdfService(AddLog);
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp == null)
                    throw new Exception("Could not connect to SolidWorks.");

                var drawings = pdfService.GetOpenDrawingDocuments();

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Create PDF For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Create PDF For All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("Create PDF For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];

                    string drawingName = "";
                    try
                    {
                        drawingName = System.IO.Path.GetFileNameWithoutExtension(drawing.GetPathName());
                        if (string.IsNullOrWhiteSpace(drawingName))
                            drawingName = drawing.GetTitle() ?? $"Drawing_{i + 1}";
                    }
                    catch
                    {
                        drawingName = $"Drawing_{i + 1}";
                    }

                    progress.UpdateProgress(
                        "Creating PDFs...",
                        drawingName,
                        i + 1,
                        drawings.Count);

                    AddLog($"Create PDF For All: processing {drawingName}");

                    try
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

                        pdfService.CreatePdfFromDrawing(drawing);

                        succeeded++;
                        AddLog($"Create PDF For All: success -> {drawingName}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingName);
                        AddLog($"Create PDF For All: FAILED -> {drawingName} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Create PDF For All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult(
                    "Create PDF For All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "Create PDF For All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private async void CheckDwgForAllButton_Click(object sender, RoutedEventArgs e)
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

                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var drawings = GetOpenDrawingDocumentsForBatch(swApp);

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "CheckDWG For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"CheckDWG For All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("CheckDWG For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];
                    string drawingPartNo = GetDrawingPartNoFromModel(drawing);

                    progress.UpdateProgress(
                        "Checking drawings...",
                        drawingPartNo,
                        i + 1,
                        drawings.Count);

                    AddLog($"CheckDWG For All: processing {drawingPartNo}");

                    try
                    {
                        int errors = 0;

                        try
                        {
                            swApp.ActivateDoc3(
                                drawing.GetTitle(),
                                true,
                                (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref errors);
                        }
                        catch
                        {
                        }

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
                        failedDrawings.Add(drawingPartNo);
                        AddLog($"CheckDWG For All: FAILED -> {drawingPartNo} | {ex.Message}");
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
                    SetStatus("Ready");
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
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "CheckDWG For All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private async void GenerateNotesForAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Generate Notes For All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var drawings = GetOpenDrawingDocumentsForBatch(swApp);

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Generate Notes For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Generate Notes For All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("Generate Notes For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];
                    string drawingPartNo = GetDrawingPartNoFromModel(drawing);

                    progress.UpdateProgress(
                        "Generating notes...",
                        drawingPartNo,
                        i + 1,
                        drawings.Count);

                    AddLog($"Generate Notes For All: processing {drawingPartNo}");

                    try
                    {
                        int errors = 0;

                        try
                        {
                            swApp.ActivateDoc3(
                                drawing.GetTitle(),
                                true,
                                (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref errors);
                        }
                        catch
                        {
                        }

                        GenerateNotesForDrawing(drawing, solidWorksService);

                        succeeded++;
                        AddLog($"Generate Notes For All: success -> {drawingPartNo}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingPartNo);
                        AddLog($"Generate Notes For All: FAILED -> {drawingPartNo} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Generate Notes For All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult(
                    "Generate Notes For All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "Generate Notes For All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private async void UpdateSignatureForAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Update Signature For All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var drawings = GetOpenDrawingDocumentsForBatch(swApp);

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Update Signature For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Update Signature For All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("Update Signature For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];
                    string drawingPartNo = GetDrawingPartNoFromModel(drawing);

                    progress.UpdateProgress(
                        "Updating signatures...",
                        drawingPartNo,
                        i + 1,
                        drawings.Count);

                    AddLog($"Update Signature For All: processing {drawingPartNo}");

                    try
                    {
                        int errors = 0;

                        try
                        {
                            swApp.ActivateDoc3(
                                drawing.GetTitle(),
                                true,
                                (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref errors);
                        }
                        catch
                        {
                        }

                        UpdateSignatureForDrawing(drawing, solidWorksService);

                        succeeded++;
                        AddLog($"Update Signature For All: success -> {drawingPartNo}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingPartNo);
                        AddLog($"Update Signature For All: FAILED -> {drawingPartNo} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Update Signature For All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult(
                    "Update Signature For All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "Update Signature For All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private async void FillTitleBlockForAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Fill Title Block For All");
            SetStatus("Working...");

            var failedDrawings = new List<string>();
            int processed = 0;
            int succeeded = 0;
            int failed = 0;

            StyledProgressWindow? progress = null;

            try
            {
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp is null)
                    throw new Exception("Could not connect to SolidWorks.");

                var drawings = GetOpenDrawingDocumentsForBatch(swApp);

                if (drawings.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Fill Title Block For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Fill Title Block For All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("Fill Title Block For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawings.Count; i++)
                {
                    var drawing = drawings[i];
                    string drawingPartNo = GetDrawingPartNoFromModel(drawing);

                    progress.UpdateProgress(
                        "Filling title blocks...",
                        drawingPartNo,
                        i + 1,
                        drawings.Count);

                    AddLog($"Fill Title Block For All: processing {drawingPartNo}");

                    try
                    {
                        int errors = 0;

                        try
                        {
                            swApp.ActivateDoc3(
                                drawing.GetTitle(),
                                true,
                                (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                                ref errors);
                        }
                        catch
                        {
                        }

                        FillTitleBlockForDrawing(drawing, solidWorksService);

                        succeeded++;
                        AddLog($"Fill Title Block For All: success -> {drawingPartNo}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingPartNo);
                        AddLog($"Fill Title Block For All: FAILED -> {drawingPartNo} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Fill Title Block For All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult(
                    "Fill Title Block For All",
                    processed,
                    succeeded,
                    failed,
                    failedDrawings,
                    this);
            }
            catch (Exception ex)
            {
                AddLog("ERROR: " + ex);

                if (progress != null)
                {
                    try { progress.Close(); } catch { }
                }

                StyledMessageWindow.ShowMessage(
                    "Fill Title Block For All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            LogTextBox.AppendText($"[{timestamp}] {message}{System.Environment.NewLine}");
            LogTextBox.ScrollToEnd();
        }

        private void SetLastAction(string action)
        {
            AddLog($"Last action set to: {action}");
        }

        private void SetStatus(string status)
        {
            AddLog($"Status: {status}");
        }

        private void ApplyCompactLayout()
        {
            _isCompactMode = true;
            SizeToContent = SizeToContent.Manual;

            MiddlePanel.Visibility = Visibility.Collapsed;
            RightPanel.Visibility = Visibility.Collapsed;

            LeftSpacerColumn.Width = new GridLength(0);
            MiddleColumn.Width = new GridLength(0);
            RightSpacerColumn.Width = new GridLength(0);
            RightColumn.Width = new GridLength(0);

            LeftColumn.Width = new GridLength(LeftPanelWidth);

            ToggleLayoutButton.Content = "SHOW MORE";

            Width = CompactWindowWidth;
            Height = CompactWindowHeight;
        }

        private void ApplyExpandedLayout()
        {
            _isCompactMode = false;

            MiddlePanel.Visibility = Visibility.Visible;
            RightPanel.Visibility = Visibility.Visible;

            LeftColumn.Width = new GridLength(LeftPanelWidth);
            LeftSpacerColumn.Width = new GridLength(PanelGap);
            MiddleColumn.Width = new GridLength(1, GridUnitType.Star);
            RightSpacerColumn.Width = new GridLength(PanelGap);
            RightColumn.Width = new GridLength(RightPanelWidth);

            ToggleLayoutButton.Content = "SHOW LESS";

            SizeToContent = SizeToContent.Manual;
            Width = ExpandedWindowWidth;
            Height = ExpandedWindowHeight;
        }

        private void ToggleLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompactMode)
            {
                if (!_showMorePasswordAccepted)
                {
                    if (!AskForShowMorePassword())
                        return;

                    _showMorePasswordAccepted = true;
                }

                ApplyExpandedLayout();
            }
            else
            {
                ApplyCompactLayout();
            }
        }

        private bool AskForShowMorePassword()
        {
            return StyledPasswordWindow.Ask(ShowMorePassword, this);
        }

        private string GetCurrentDrawingPartNo()
        {
            var solidWorksService = new SolidWorksService();
            var swApp = solidWorksService.GetApplication();

            if (swApp is null)
                throw new Exception("Could not connect to SolidWorks.");

            var activeDoc = solidWorksService.GetActiveDocument(swApp);

            if (activeDoc is null)
                throw new Exception("No active SolidWorks document.");

            if (!solidWorksService.IsDrawing(activeDoc))
                throw new Exception("Active SolidWorks document is not a drawing.");

            string rawFileName = solidWorksService.GetActiveDrawingPartNo(activeDoc);

            string normalized = System.Text.RegularExpressions.Regex.Match(
                rawFileName,
                @"^[A-Z]{4}\d{6}[A-Z]\d{4}"
            ).Value;

            if (string.IsNullOrWhiteSpace(normalized))
                throw new Exception($"Could not extract drawing part number from file name: {rawFileName}");

            return normalized;
        }

        private string GetDrawingNumberFromPartNo(string drawingPartNo)
        {
            int indexOfA = drawingPartNo.IndexOf('A');

            if (indexOfA <= 0)
                throw new Exception($"Could not derive Drawing Number from part number: {drawingPartNo}");

            return drawingPartNo.Substring(0, indexOfA);
        }

        private string GetSheetNumberFromPartNo(string drawingPartNo)
        {
            if (drawingPartNo.Length < 4)
                throw new Exception($"Could not derive Sheet Number from part number: {drawingPartNo}");

            string last4 = drawingPartNo.Substring(drawingPartNo.Length - 4);
            return "71" + last4;
        }

        private List<string> ParseCodesFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new List<string>();

            return System.Text.RegularExpressions.Regex.Matches(
                    name.ToUpper(),
                    @"(G\d+|T\d+|B\d+|SE\d+)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<string> ExtractExpectedFlagCodes(List<string> requiredCodes)
        {
            return requiredCodes
                .Where(c => System.Text.RegularExpressions.Regex.IsMatch(
                    c,
                    @"^(B\d+|SE\d+)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .Select(c => c.ToUpper())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private Dictionary<string, int> GetExpectedSubpartQuantities(List<NDwgAutoTool.Models.BomRow> rows)
        {
            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.SubPartNo))
                .GroupBy(r => r.SubPartNo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.Quantity),
                    StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, int> GetActualSubpartQuantities(List<NDwgAutoTool.Models.DrawingNoteInfo> notes)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var note in notes)
            {
                NDwgAutoTool.Helpers.DrawingNoteHelper.Classify(note);

                if (string.IsNullOrWhiteSpace(note.Text))
                    continue;

                string text = note.Text.Trim();

                // Ignore HOLE xP callouts completely for subpart comparison
                if (System.Text.RegularExpressions.Regex.IsMatch(
                    text,
                    @"^HOLE\s+\d+P$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    continue;
                }

                // Normal callout: PARTNO 3P
                if (note.IsCallout)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        text,
                        @"^(?<part>[A-Z0-9\-]+)\s+(?<qty>\d+)P$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        string part = match.Groups["part"].Value.Trim();
                        int qty = int.Parse(match.Groups["qty"].Value);

                        if (result.ContainsKey(part))
                            result[part] += qty;
                        else
                            result[part] = qty;
                    }

                    continue;
                }

                // Self-part style callout:
                // PPNR101010A0001      -> qty = 1
                // PPNR101010A0001 2P   -> qty = 2
                if (NDwgAutoTool.Helpers.PartNumberHelper.IsSelfPart(text))
                {
                    if (result.ContainsKey(text))
                        result[text] += 1;
                    else
                        result[text] = 1;

                    continue;
                }

                var selfPartWithQtyMatch = System.Text.RegularExpressions.Regex.Match(
                    text,
                    @"^(?<part>P[A-Z0-9\-]+)\s+(?<qty>\d+)P$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (selfPartWithQtyMatch.Success)
                {
                    string part = selfPartWithQtyMatch.Groups["part"].Value.Trim();
                    int qty = int.Parse(selfPartWithQtyMatch.Groups["qty"].Value);

                    if (result.ContainsKey(part))
                        result[part] += qty;
                    else
                        result[part] = qty;
                }
            }

            return result;
        }

        private List<string> GetActualFlagCodes(List<NDwgAutoTool.Models.DrawingNoteInfo> notes)
        {
            var result = new List<string>();

            foreach (var note in notes)
            {
                // Ignore notes outside the actual drawing sheet area
                if (note.X < 0 || note.Y < 0)
                    continue;

                NDwgAutoTool.Helpers.DrawingNoteHelper.Classify(note);

                if (!note.IsFlagNote || string.IsNullOrWhiteSpace(note.FlagCode))
                    continue;

                string code = note.FlagCode.Trim().ToUpper();

                if (System.Text.RegularExpressions.Regex.IsMatch(
                    code,
                    @"^(B\d+|SE\d+)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    result.Add(code);
                }
            }

            return result
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string? GetActualNoteBlockNameFromDrawing(SldWorks.ModelDoc2 activeDoc)
        {
            var pickPoints = new List<(double X, double Y)>
                {
                    (0.0598867427928131, 0.0224488902434242),
                    (0.0600, 0.0225),
                    (0.0550, 0.0200),
                    (0.0700, 0.0250)
                };

            foreach (var pt in pickPoints)
            {
                activeDoc.ClearSelection2(true);

                bool selected = activeDoc.Extension.SelectByID2(
                    "",
                    "SUBSKETCHINST",
                    pt.X,
                    pt.Y,
                    0.0,
                    false,
                    0,
                    null,
                    0);

                AddLog($"Trying note block select at X:{pt.X:F4} Y:{pt.Y:F4} -> {selected}");

                if (!selected)
                    continue;

                var selMgr = activeDoc.SelectionManager;
                if (selMgr == null)
                {
                    activeDoc.ClearSelection2(true);
                    continue;
                }

                object obj = selMgr.GetSelectedObject6(1, -1);
                if (obj == null)
                {
                    activeDoc.ClearSelection2(true);
                    continue;
                }

                string name = "";

                try
                {
                    dynamic dynObj = obj;
                    name = dynObj.Name ?? "";
                }
                catch
                {
                    name = "";
                }

                AddLog($"Selected note block name -> {name}");

                activeDoc.ClearSelection2(true);

                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }

            activeDoc.ClearSelection2(true);
            return null;
        }

        private List<NDwgAutoTool.Models.CheckDwgReportRow> BuildTitleBlockReportRows(
            List<NDwgAutoTool.Models.DrawingNoteInfo> notes,
            string expectedDrawingNumber,
            string expectedSheetNumber,
            string expectedTitle,
            NDwgAutoTool.Services.SolidWorksService solidWorksService)
        {
            var rows = new List<NDwgAutoTool.Models.CheckDwgReportRow>();

            string? bottomDrawing = solidWorksService.GetNearestNoteText(notes, 0.4728, 0.0170);
            string? bottomSheet = solidWorksService.GetNearestNoteText(notes, 0.5182, 0.0165);
            string? bottomTitle = solidWorksService.GetNearestNoteText(notes, 0.4927, 0.0311);

            string? sideDrawing = solidWorksService.GetNearestNoteText(notes, 0.5805, 0.3080);
            string? sideSheet = solidWorksService.GetNearestNoteText(notes, 0.5808, 0.3525);

            bool drawingOk =
                string.Equals(bottomDrawing, expectedDrawingNumber, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(sideDrawing, expectedDrawingNumber, StringComparison.OrdinalIgnoreCase);

            bool sheetOk =
                string.Equals(bottomSheet, expectedSheetNumber, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(sideSheet, expectedSheetNumber, StringComparison.OrdinalIgnoreCase);

            bool titleOk =
                string.Equals(bottomTitle, expectedTitle, StringComparison.OrdinalIgnoreCase);

            rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
            {
                CheckItem = "Drawing Number",
                Result = drawingOk ? "Correct" : "Issue Found",
                Details = drawingOk
                    ? $"Drawing number matches WORK_LIST in both title block locations ({expectedDrawingNumber})."
                    : $"Drawing number does not match WORK_LIST. Expected {expectedDrawingNumber}. Bottom found: {bottomDrawing ?? "<not found>"}. Side found: {sideDrawing ?? "<not found>"}."
            });

            rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
            {
                CheckItem = "Sheet Number",
                Result = sheetOk ? "Correct" : "Issue Found",
                Details = sheetOk
                    ? $"Sheet number matches WORK_LIST in both title block locations ({expectedSheetNumber})."
                    : $"Sheet number does not match WORK_LIST. Expected {expectedSheetNumber}. Bottom found: {bottomSheet ?? "<not found>"}. Side found: {sideSheet ?? "<not found>"}."
            });

            rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
            {
                CheckItem = "Title",
                Result = titleOk ? "Correct" : "Issue Found",
                Details = titleOk
                    ? $"Title matches WORK_LIST ({expectedTitle})."
                    : $"Title does not match WORK_LIST. Expected {expectedTitle}. Found: {bottomTitle ?? "<not found>"}."
            });

            return rows;
        }

        private List<NDwgAutoTool.Models.CheckDwgReportRow> BuildRedReportRows(
    List<string> requiredCodes,
    List<NDwgAutoTool.Models.DrawingNoteInfo> notes,
    string notesFilePath,
    SldWorks.ModelDoc2 activeDoc,
    List<NDwgAutoTool.Models.BomRow> bomRows)
        {
            var rows = new List<NDwgAutoTool.Models.CheckDwgReportRow>();

            string notesFileName = System.IO.Path.GetFileName(notesFilePath);

            // -----------------------------
            // 1) NOTE BLOCK CHECK
            // Leave this logic as-is
            // -----------------------------
            var expectedBlockCodes = requiredCodes
                .Select(x => x.ToUpper())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? actualBlockName = GetActualNoteBlockNameFromDrawing(activeDoc);

            var actualBlockCodes = ParseCodesFromName(actualBlockName ?? "")
                .Select(x => x.ToUpper())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var missingBlockCodes = expectedBlockCodes
                .Where(c => !actualBlockCodes.Contains(c, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var extraBlockCodes = actualBlockCodes
                .Where(c => !expectedBlockCodes.Contains(c, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (string.IsNullOrWhiteSpace(actualBlockName))
            {
                rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
                {
                    CheckItem = "Note Block",
                    Result = "Issue Found",
                    Details = $"Note block could not be identified on the drawing. Expected {string.Join(", ", expectedBlockCodes)} based on {notesFileName}."
                });
            }
            else if (missingBlockCodes.Count == 0 && extraBlockCodes.Count == 0)
            {
                rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
                {
                    CheckItem = "Note Block",
                    Result = "Correct",
                    Details = $"Note block matches {notesFileName}."
                });
            }
            else
            {
                rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
                {
                    CheckItem = "Note Block",
                    Result = "Issue Found",
                    Details = $"Note block does not match {notesFileName}. Expected {string.Join(", ", expectedBlockCodes)} but found {string.Join(", ", actualBlockCodes)}."
                });
            }

            // -----------------------------
            // 2) FLAGNOTE CHECK
            // NEW LOGIC:
            // Required flagnotes on drawing =
            // intersection of:
            //   - flag-style codes in ACTUAL note block
            //   - flag codes required by PARTS_LIST
            // -----------------------------

            // Flag-style codes that exist in the ACTUAL note block
            var blockFlagCodes = actualBlockCodes
                .Where(c => System.Text.RegularExpressions.Regex.IsMatch(
                    c,
                    @"^(B\d+|SE\d+)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .Select(c => c.ToUpper())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Flagnote codes required by PARTS_LIST
            var partListFlagCodes = bomRows
                .Where(r =>
                    !string.IsNullOrWhiteSpace(r.FlagYesNo) &&
                    r.FlagYesNo.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(r.NoteCode))
                .Select(r => r.NoteCode.Trim().ToUpper())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // FINAL expected flagnotes on drawing:
            // 1) codes common to ACTUAL note block and PARTS_LIST
            // 2) plus SE03 if ACTUAL note block contains SE03
            var expectedFlagCodesOnDrawing = blockFlagCodes
                .Where(c => partListFlagCodes.Contains(c, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (blockFlagCodes.Contains("SE03", StringComparer.OrdinalIgnoreCase) &&
                !expectedFlagCodesOnDrawing.Contains("SE03", StringComparer.OrdinalIgnoreCase))
            {
                expectedFlagCodesOnDrawing.Add("SE03");
            }

            // Actual flagnotes found on drawing
            var actualFlagCodes = GetActualFlagCodes(notes)
                .Select(c => c.ToUpper())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var missingOnDrawing = expectedFlagCodesOnDrawing
                .Where(c => !actualFlagCodes.Contains(c, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var extraOnDrawing = actualFlagCodes
                .Where(c => !expectedFlagCodesOnDrawing.Contains(c, StringComparer.OrdinalIgnoreCase))
                .ToList();

            string flagResult;
            string flagDetails;

            if (expectedFlagCodesOnDrawing.Count == 0 && actualFlagCodes.Count == 0)
            {
                flagResult = "Correct";
                flagDetails = "No flagnotes are required on the drawing based on the note block and 部品表(PARTS LIST), and none were found.";
            }
            else if (missingOnDrawing.Count == 0 && extraOnDrawing.Count == 0)
            {
                flagResult = "Correct";
                flagDetails = $"All required flagnotes are present on the drawing and match the common note codes between the note block and 部品表(PARTS LIST): {string.Join(", ", expectedFlagCodesOnDrawing)}.";
            }
            else
            {
                flagResult = "Issue Found";

                var messages = new List<string>();

                if (missingOnDrawing.Count > 0)
                    messages.Add($"Flagnotes missing on the drawing: {string.Join(", ", missingOnDrawing)}.");

                if (extraOnDrawing.Count > 0)
                    messages.Add($"Extra flagnotes found on the drawing: {string.Join(", ", extraOnDrawing)}.");

                if (expectedFlagCodesOnDrawing.Count == 0 && actualFlagCodes.Count > 0)
                    messages.Add("No flagnotes should exist on the drawing based on the common note codes between the note block and 部品表(PARTS LIST).");

                flagDetails = string.Join(" ", messages);
            }

            rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
            {
                CheckItem = "Flagnotes",
                Result = flagResult,
                Details = flagDetails
            });

            return rows;
        }

        private List<NDwgAutoTool.Models.CheckDwgReportRow> BuildSubpartReportRows(
            Dictionary<string, int> expected,
            Dictionary<string, int> actual)
        {
            var rows = new List<NDwgAutoTool.Models.CheckDwgReportRow>();

            var allParts = expected.Keys
                .Union(actual.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            foreach (var part in allParts)
            {
                int expectedQty = expected.ContainsKey(part) ? expected[part] : 0;
                int actualQty = actual.ContainsKey(part) ? actual[part] : 0;

                if (expectedQty == actualQty)
                {
                    rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
                    {
                        CheckItem = "Subpart Quantity",
                        Result = "Correct",
                        Details = $"Subpart {part} quantity matches PARTS_LIST. Expected {expectedQty} and found {actualQty} on the drawing."
                    });
                }
                else if (actualQty < expectedQty)
                {
                    rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
                    {
                        CheckItem = "Subpart Quantity",
                        Result = "Issue Found",
                        Details = $"Subpart {part} quantity is lower on the drawing than in PARTS_LIST. Expected {expectedQty} but found {actualQty}."
                    });
                }
                else
                {
                    rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
                    {
                        CheckItem = "Subpart Quantity",
                        Result = "Issue Found",
                        Details = $"Subpart {part} quantity is higher on the drawing than in PARTS_LIST. Expected {expectedQty} but found {actualQty}."
                    });
                }
            }

            return rows;
        }

        private void WriteCheckDwgExcelReport(
            string outputPath,
            List<NDwgAutoTool.Models.CheckDwgReportRow> rows,
            string drawingPartNo)
        {
            OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("NDwgAutoTool");

            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("CheckDWG");

            // Title (Drawing Number)
            ws.Cells[1, 1].Value = $"Drawing: {drawingPartNo}";
            ws.Cells[1, 1, 1, 3].Merge = true;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Font.Size = 14;

            // Header row
            ws.Cells[2, 1].Value = "Check Item";
            ws.Cells[2, 2].Value = "Result";
            ws.Cells[2, 3].Value = "Details";

            using (var range = ws.Cells[2, 1, 2, 3])
            {
                range.Style.Font.Bold = true;
            }

            // Data
            for (int i = 0; i < rows.Count; i++)
            {
                int r = i + 3;
                ws.Cells[r, 1].Value = rows[i].CheckItem;
                ws.Cells[r, 2].Value = rows[i].Result;
                ws.Cells[r, 3].Value = rows[i].Details;
            }

            ws.Column(1).Width = 26;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 110;
            ws.Cells[ws.Dimension.Address].Style.WrapText = true;

            package.SaveAs(new System.IO.FileInfo(outputPath));
        }

        private List<SldWorks.ModelDoc2> GetOpenDrawingDocumentsForBatch(SldWorks.ISldWorks swApp)
        {
            var result = new List<SldWorks.ModelDoc2>();

            object[]? docs = swApp.GetDocuments() as object[];
            if (docs == null || docs.Length == 0)
                return result;

            foreach (object obj in docs)
            {
                var model = obj as SldWorks.ModelDoc2;
                if (model == null)
                    continue;

                try
                {
                    if (model.GetType() == (int)SwConst.swDocumentTypes_e.swDocDRAWING)
                        result.Add(model);
                }
                catch
                {
                }
            }

            return result;
        }

        private string GetDrawingPartNoFromModel(SldWorks.ModelDoc2 model)
        {
            if (model == null)
                return "";

            try
            {
                string source = model.GetPathName();

                if (string.IsNullOrWhiteSpace(source))
                    source = model.GetTitle() ?? "";

                source = System.IO.Path.GetFileNameWithoutExtension(source).Trim().ToUpperInvariant();

                var match = System.Text.RegularExpressions.Regex.Match(
                    source,
                    @"[A-Z]{4}\d{6}[A-Z]\d{4}");

                if (match.Success)
                    return match.Value;

                return source;
            }
            catch
            {
                return "";
            }
        }

        private List<NDwgAutoTool.Models.CheckDwgReportRow> BuildCheckDwgReportRowsForDrawing(
            SldWorks.ModelDoc2 activeDoc,
            string notesFile,
            string workListFile,
            string bomFile,
            SolidWorksService solidWorksService)
        {
            if (activeDoc == null)
                throw new Exception("No active drawing document.");

            if (!solidWorksService.IsDrawing(activeDoc))
                throw new Exception("Document is not a drawing.");

            string drawingPartNo = GetDrawingPartNoFromModel(activeDoc);
            AddLog($"Current drawing: {drawingPartNo}");

            var notes = solidWorksService.GetAllNotesOnActiveSheet(activeDoc);

            var notesReader = new NotesReaderService();
            var rawTokens = notesReader.GetRequiredNoteTokens(notesFile, drawingPartNo);
            AddLog($"Raw Excel note tokens -> {string.Join(", ", rawTokens)}");

            var workListProjectReader = new WorkListProjectReaderService();
            string projectCode = workListProjectReader.GetProjectCode(workListFile);
            AddLog($"Project code -> {projectCode}");

            var blockLibraryService = new NoteBlockLibraryService();
            var requiredCodes = blockLibraryService.ExpandExcelNoteTokens(rawTokens);
            AddLog($"Required note codes -> {string.Join(", ", requiredCodes)}");

            string drawingNumber = drawingPartNo;
            string sheetNumber = "";

            int aIndex = drawingPartNo.IndexOf('A');
            if (aIndex > 0 && aIndex < drawingPartNo.Length - 1)
            {
                drawingNumber = drawingPartNo.Substring(0, aIndex);
                string suffix = drawingPartNo.Substring(aIndex + 1);
                sheetNumber = "71" + suffix;
            }

            var workListReader = new WorkListReaderService();
            string title = workListReader.GetTitleFromWorkList(workListFile, drawingPartNo);

            var bomReader = new BomReaderService();
            var bomRows = bomReader.ReadBom(bomFile, drawingPartNo);

            var expectedSubparts = GetExpectedSubpartQuantities(bomRows);            
            var actualSubparts = GetActualSubpartQuantities(notes);
            int holeCount = CountHoleCallouts(notes);

            var reportRows = new List<NDwgAutoTool.Models.CheckDwgReportRow>();

            reportRows.AddRange(BuildTitleBlockReportRows(
                notes,
                drawingNumber,
                sheetNumber,
                title,
                solidWorksService));

            reportRows.AddRange(BuildRedReportRows(
                requiredCodes,
                notes,
                notesFile,
                activeDoc,
                bomRows));

            reportRows.AddRange(BuildHoleReportRows(holeCount));

            reportRows.AddRange(BuildSubpartReportRows(
                expectedSubparts,
                actualSubparts));

            return reportRows;
        }

        private void WriteCheckDwgForAllExcelReport(
            string outputPath,
            List<(string DrawingPartNo, List<NDwgAutoTool.Models.CheckDwgReportRow> Rows)> allReports)
        {
            OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("NDwgAutoTool");

            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("CheckDWG For All");

            int currentRow = 1;

            if (allReports == null || allReports.Count == 0)
            {
                ws.Cells[currentRow, 1].Value = "No successful CheckDWG reports were generated.";
                ws.Cells[currentRow + 1, 1].Value = "All drawings failed during processing.";
                ws.Column(1).Width = 60;

                package.SaveAs(new System.IO.FileInfo(outputPath));
                return;
            }

            foreach (var report in allReports)
            {
                ws.Cells[currentRow, 1].Value = $"Drawing: {report.DrawingPartNo}";
                ws.Cells[currentRow, 1, currentRow, 3].Merge = true;
                ws.Cells[currentRow, 1].Style.Font.Bold = true;
                ws.Cells[currentRow, 1].Style.Font.Size = 14;
                currentRow++;

                ws.Cells[currentRow, 1].Value = "Check Item";
                ws.Cells[currentRow, 2].Value = "Result";
                ws.Cells[currentRow, 3].Value = "Details";

                using (var headerRange = ws.Cells[currentRow, 1, currentRow, 3])
                {
                    headerRange.Style.Font.Bold = true;
                }

                currentRow++;

                foreach (var row in report.Rows)
                {
                    ws.Cells[currentRow, 1].Value = row.CheckItem;
                    ws.Cells[currentRow, 2].Value = row.Result;
                    ws.Cells[currentRow, 3].Value = row.Details;
                    currentRow++;
                }

                currentRow++;
            }

            ws.Column(1).Width = 28;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 110;

            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].Style.WrapText = true;

            package.SaveAs(new System.IO.FileInfo(outputPath));
        }

        private string GenerateNotesForDrawing(SldWorks.ModelDoc2 activeDoc, SolidWorksService solidWorksService)
        {
            if (activeDoc is null)
                throw new Exception("No active SolidWorks document.");

            if (!solidWorksService.IsDrawing(activeDoc))
                throw new Exception("Active SolidWorks document is not a drawing.");

            var fileFinder = new FileFinderService();

            var notesFile = fileFinder.FindNotesFile();
            if (string.IsNullOrWhiteSpace(notesFile))
                throw new Exception("N-DWG Auto Tool notes file not found.");

            AddLog($"Notes file path: {notesFile}");

            var workListFile = fileFinder.FindWorkListFile();
            if (string.IsNullOrWhiteSpace(workListFile))
                throw new Exception("WORK_LIST file not found.");

            AddLog($"WORK_LIST file path: {workListFile}");

            string drawingPartNo = GetDrawingPartNoFromModel(activeDoc);
            AddLog($"Current drawing: {drawingPartNo}");

            var notesReader = new NotesReaderService();
            var rawTokens = notesReader.GetRequiredNoteTokens(notesFile, drawingPartNo);
            AddLog($"Raw Excel note tokens -> {string.Join(", ", rawTokens)}");

            var workListProjectReader = new WorkListProjectReaderService();
            string projectCode = workListProjectReader.GetProjectCode(workListFile);
            AddLog($"Project code -> {projectCode}");

            var blockLibraryService = new NoteBlockLibraryService();
            var requiredCodes = blockLibraryService.ExpandExcelNoteTokens(rawTokens);
            AddLog($"Required note codes -> {string.Join(", ", requiredCodes)}");

            string matchedBlockFile = blockLibraryService.FindMatchingBlockFile(projectCode, requiredCodes);
            AddLog($"Matched block file -> {matchedBlockFile}");

            bool deletedOld = solidWorksService.DeleteExistingNoteBlock(activeDoc, AddLog);

            if (deletedOld)
                AddLog("Existing note block deleted.");
            else
                AddLog("No existing note block found in note area.");

            solidWorksService.InsertNoteBlockAtOrigin(activeDoc, matchedBlockFile);
            AddLog("Note block inserted.");

            return $"Notes generated successfully for {drawingPartNo}.";
        }

        private string UpdateSignatureForDrawing(SldWorks.ModelDoc2 activeDoc, SolidWorksService solidWorksService)
        {
            if (activeDoc is null)
                throw new Exception("No active SolidWorks document.");

            if (!solidWorksService.IsDrawing(activeDoc))
                throw new Exception("Active document is not a drawing.");

            var fileFinder = new FileFinderService();
            var workListFile = fileFinder.FindWorkListFile();

            if (string.IsNullOrWhiteSpace(workListFile))
                throw new Exception("WORK_LIST file not found.");

            AddLog($"WORK_LIST file path: {workListFile}");

            string drawingPartNo = GetDrawingPartNoFromModel(activeDoc);
            AddLog($"Current drawing: {drawingPartNo}");

            var workListReader = new WorkListReaderService();
            var signature = workListReader.GetSignatureFromWorkList(workListFile);

            AddLog($"Signature Date: {signature.Date}");
            AddLog($"Signature Engineer: {signature.Engineer}");
            AddLog($"Signature Checker: {signature.Checker}");
            AddLog($"Signature Approver: {signature.Approver}");

            solidWorksService.SetDrawingCustomProperty(activeDoc, "DATE", signature.Date);
            solidWorksService.SetDrawingCustomProperty(activeDoc, "ENGR", signature.Engineer);
            solidWorksService.SetDrawingCustomProperty(activeDoc, "CHKD", signature.Checker);
            solidWorksService.SetDrawingCustomProperty(activeDoc, "APRD", signature.Approver);

            solidWorksService.RebuildModel(activeDoc);

            AddLog("Signature updated successfully.");

            return $"Signature updated successfully for {drawingPartNo}.";
        }

        private string FillTitleBlockForDrawing(SldWorks.ModelDoc2 activeDoc, SolidWorksService solidWorksService)
        {
            if (activeDoc is null)
                throw new Exception("No active SolidWorks document.");

            if (!solidWorksService.IsDrawing(activeDoc))
                throw new Exception("Active SolidWorks document is not a drawing.");

            var fileFinder = new FileFinderService();
            var workListFile = fileFinder.FindWorkListFile();

            if (string.IsNullOrWhiteSpace(workListFile))
                throw new Exception("WORK_LIST file not found.");

            AddLog($"WORK_LIST file path: {workListFile}");

            string drawingPartNo = GetDrawingPartNoFromModel(activeDoc);
            AddLog($"Current drawing: {drawingPartNo}");

            string drawingNumber = GetDrawingNumberFromPartNo(drawingPartNo);
            string sheetNumber = GetSheetNumberFromPartNo(drawingPartNo);

            var workListReader = new WorkListReaderService();
            string title = workListReader.GetTitleFromWorkList(workListFile, drawingPartNo);

            AddLog($"Target Drawing Number: {drawingNumber}");
            AddLog($"Target Sheet Number: {sheetNumber}");
            AddLog($"Target Title: {title}");

            var notes = solidWorksService.GetAllNotesOnActiveSheet(activeDoc);

            solidWorksService.UpdateOrCreateNoteAtPositionInSheetFormat(activeDoc, notes, 0.4728, 0.0170, drawingNumber, false);
            solidWorksService.UpdateOrCreateNoteAtPositionInSheetFormat(activeDoc, notes, 0.5182, 0.0165, sheetNumber, false);
            solidWorksService.UpdateOrCreateNoteAtPositionInSheetFormat(activeDoc, notes, 0.4927, 0.0311, title, false);

            solidWorksService.UpdateOrCreateNoteAtPositionInSheetFormat(activeDoc, notes, 0.5805, 0.3080, drawingNumber, true);
            solidWorksService.UpdateOrCreateNoteAtPositionInSheetFormat(activeDoc, notes, 0.5808, 0.3525, sheetNumber, true);

            activeDoc.GraphicsRedraw2();
            activeDoc.WindowRedraw();

            AddLog("Title block updated successfully.");

            return $"Title block filled successfully for {drawingPartNo}.";
        }

        private string CreateFlagnotesForDrawing(SldWorks.ModelDoc2 activeDoc, SolidWorksService solidWorksService)
        {
            if (activeDoc is null)
                throw new Exception("No active SolidWorks document.");

            if (!solidWorksService.IsDrawing(activeDoc))
                throw new Exception("Active SolidWorks document is not a drawing.");

            var fileFinder = new FileFinderService();

            var bomFile = fileFinder.FindBomFile();
            if (string.IsNullOrWhiteSpace(bomFile))
                throw new Exception("BOM file not found.");

            var notesFile = fileFinder.FindNotesFile();
            if (string.IsNullOrWhiteSpace(notesFile))
                throw new Exception("N-DWG Auto Tool notes file not found.");

            string drawingPartNo = GetNormalizedDrawingPartNoFromDoc(activeDoc, solidWorksService);
            AddLog($"Current drawing: {drawingPartNo}");

            var bomReader = new BomReaderService();
            var rows = bomReader.ReadBom(bomFile, drawingPartNo);

            AddLog($"BOM rows found: {rows.Count}");

            if (rows.Count == 0)
                throw new Exception($"No BOM rows found for drawing {drawingPartNo}.");

            var selfRow = rows.FirstOrDefault(r =>
                PartNumberHelper.IsSelfPart(r.SubPartNo) &&
                !string.IsNullOrWhiteSpace(r.Nomenclature) &&
                r.Nomenclature.Trim().Equals("H PANEL", StringComparison.OrdinalIgnoreCase));

            if (selfRow == null)
            {
                // fallback to old logic in case some older project does not have H PANEL filled as expected
                selfRow = rows.FirstOrDefault(r => PartNumberHelper.IsSelfPart(r.SubPartNo));
            }

            if (selfRow == null)
                throw new Exception("Self part row not found in BOM.");

            AddLog($"BOM self part: {selfRow.SubPartNo}");

            var flagRows = rows
                .Where(r =>
                    !string.IsNullOrWhiteSpace(r.FlagNote) &&
                    r.FlagNote.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(r.Note) &&
                    !r.Note.Trim().Equals("SE03", StringComparison.OrdinalIgnoreCase))
                .ToList();

            AddLog($"BOM auto-generated flag rows found: {flagRows.Count}");

            var autoFlagCodes = flagRows
                .Select(r => (r.Note ?? "").Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            AddLog($"Auto-generated flag codes: {string.Join(", ", autoFlagCodes)}");

            // Leadered flagnote code from N-DWG Auto Tool: A=drawing number, B=note code
            var notesReader = new NotesReaderService();
            string leaderedFlagCode = notesReader.GetLeaderedFlagCode(notesFile, drawingPartNo).Trim();
            AddLog($"Leadered flagnote code from N-DWG sheet: {leaderedFlagCode}");

            var notes = solidWorksService.GetAllNotesOnActiveSheet(activeDoc);
            foreach (var note in notes)
                DrawingNoteHelper.Classify(note);

            // Self callout handling
            var selfCandidates = notes
                .Where(n =>
                    !string.IsNullOrWhiteSpace(n.Text) &&
                    (n.Text.StartsWith(drawingPartNo, StringComparison.OrdinalIgnoreCase) ||
                     n.Text.StartsWith(selfRow.SubPartNo, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var selfCallout = selfCandidates.FirstOrDefault(n => n.HasLeader);

            if (selfCallout == null)
            {
                AddLog("WARNING: Self-part callout not found. Continuing.");
            }
            else if (selfCallout.NoteObject == null)
            {
                AddLog("WARNING: Self-part note object is missing. Continuing.");
            }
            else
            {
                AddLog($"Drawing self callout selected: {selfCallout.Text}");

                if (!selfCallout.Text.Equals(selfRow.SubPartNo, StringComparison.OrdinalIgnoreCase))
                {
                    AddLog($"Updating self callout to: {selfRow.SubPartNo}");
                    solidWorksService.UpdateNoteText(selfCallout.NoteObject, selfRow.SubPartNo);
                    AddLog("Self-part callout updated successfully.");
                }
                else
                {
                    AddLog("Self-part callout already correct.");
                }
            }
            // Refresh notes after self-part update so later logic sees the new text
            notes = solidWorksService.GetAllNotesOnActiveSheet(activeDoc);
            foreach (var note in notes)
                DrawingNoteHelper.Classify(note);

            var callouts = notes
                .Where(n => n.IsCallout)
                .ToList();

            // STEP 1: Update existing leadered flagnotes inside the sheet only
            int updatedLeaderedFlagnotes = 0;

            if (!string.IsNullOrWhiteSpace(leaderedFlagCode))
            {
                var leaderedFlagnotes = notes
                    .Where(n => IsLeaderedFlagnoteCandidate(n, activeDoc, selfRow.SubPartNo))
                    .ToList();

                AddLog($"Leadered flagnotes eligible for text update: {leaderedFlagnotes.Count}");

                foreach (var leaderedFlag in leaderedFlagnotes)
                {
                    if (leaderedFlag.NoteObject == null)
                        continue;

                    string currentText = (leaderedFlag.Text ?? "").Trim();

                    if (!currentText.Equals(leaderedFlagCode, StringComparison.OrdinalIgnoreCase))
                    {
                        solidWorksService.UpdateNoteText(leaderedFlag.NoteObject, leaderedFlagCode);
                        AddLog($"Updated leadered flagnote '{currentText}' -> '{leaderedFlagCode}'.");
                        updatedLeaderedFlagnotes++;
                    }
                }

                if (updatedLeaderedFlagnotes > 0)
                {
                    activeDoc.GraphicsRedraw2();
                    activeDoc.WindowRedraw();
                    System.Threading.Thread.Sleep(60);
                }
            }

            AddLog($"Leadered flagnotes updated from N-DWG sheet: {updatedLeaderedFlagnotes}");

            // STEP 2: Delete old autogenerated no-leader flags
            var oldAutoFlags = notes
                .Where(n =>
                    n.AnnotationObject != null &&
                    ShouldDeleteAutoGeneratedNoLeaderNote(n, activeDoc, callouts, selfRow.SubPartNo))
                .ToList();

            AddLog($"Existing auto-generated flagnotes found for deletion: {oldAutoFlags.Count}");

            if (oldAutoFlags.Count > 0)
            {
                solidWorksService.DeleteAnnotations(
                    activeDoc,
                    oldAutoFlags
                        .Where(n => n.AnnotationObject != null)
                        .Select(n => n.AnnotationObject!));

                AddLog("Old auto-generated flagnotes deleted successfully.");

                activeDoc.GraphicsRedraw2();
                activeDoc.WindowRedraw();
                System.Threading.Thread.Sleep(80);
            }

            // STEP 3: Refresh notes after deletion
            notes = solidWorksService.GetAllNotesOnActiveSheet(activeDoc);
            foreach (var note in notes)
                DrawingNoteHelper.Classify(note);

            callouts = notes.Where(n => n.IsCallout).ToList();
            AddLog($"Callouts available for new flagnotes: {callouts.Count}");

            var calloutLookup = callouts
                .Where(c => !string.IsNullOrWhiteSpace(c.BasePartNo))
                .GroupBy(c => c.BasePartNo, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // STEP 4: Copy template once
            var templateFlagForCopy = notes.FirstOrDefault(n =>
                n.IsFlagNote &&
                n.FlagCode.Equals("SE03", StringComparison.OrdinalIgnoreCase) &&
                !n.HasLeader &&
                n.AnnotationObject != null);

            if (templateFlagForCopy == null)
                throw new Exception("Template SE03 without leader not found.");

            AddLog($"Template SE03 selected at X: {templateFlagForCopy.X:F4} Y: {templateFlagForCopy.Y:F4}");

            activeDoc.ClearSelection2(true);

            bool templateSelected = templateFlagForCopy.AnnotationObject!.Select3(false, null);
            if (!templateSelected)
                throw new Exception("Failed to select template SE03 note for copy.");

            activeDoc.EditCopy();
            activeDoc.ClearSelection2(true);

            AddLog("Template SE03 copied once for reuse.");

            // STEP 5: Recreate autogenerated no-leader flags
            int createdCount = 0;
            int failedCount = 0;

            foreach (var bomRow in flagRows)
            {
                if (!calloutLookup.TryGetValue(bomRow.SubPartNo, out var matchingCallouts) || matchingCallouts.Count == 0)
                {
                    AddLog($"WARNING: No drawing callout found for BOM flag row: {bomRow.SubPartNo} -> {bomRow.Note}");
                    continue;
                }

                foreach (var callout in matchingCallouts)
                {
                    double newX = callout.X;
                    double newY = callout.Y + 0.0045;

                    bool created = false;

                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        try
                        {
                            var createdFlag = solidWorksService.PasteCopiedFlagNoteInView(
                                activeDoc,
                                bomRow.Note,
                                newX,
                                newY,
                                callout.ViewName,
                                AddLog);

                            if (createdFlag != null)
                            {
                                AddLog($"Created flagnote {bomRow.Note} detected in view '{createdFlag.ViewName}' near callout view '{callout.ViewName}'.");
                                created = true;
                                createdCount++;
                                break;
                            }
                        }
                        catch
                        {
                        }

                        if (attempt < 3)
                        {
                            activeDoc.GraphicsRedraw2();
                            System.Threading.Thread.Sleep(40);
                        }
                    }

                    if (!created)
                    {
                        failedCount++;
                        AddLog($"WARNING: Failed to create flagnote {bomRow.Note} for callout {callout.Text}");
                    }
                }
            }

            AddLog($"New auto-generated flagnotes created from template: {createdCount}");

            if (failedCount > 0)
                AddLog($"WARNING: Failed to create {failedCount} flagnote(s).");

            if (failedCount > 0)
                return $"Flagnotes completed with warnings for {drawingPartNo}.\n\nLeadered updated: {updatedLeaderedFlagnotes}\nCreated: {createdCount}\nFailed: {failedCount}";

            return $"Flagnotes created successfully for {drawingPartNo}.\n\nLeadered updated: {updatedLeaderedFlagnotes}\nCreated: {createdCount}";
        }

        private bool ShouldDeleteAutoGeneratedNoLeaderNote(
            NDwgAutoTool.Models.DrawingNoteInfo note,
            SldWorks.ModelDoc2 activeDoc)
        {
            if (note == null)
                return false;

            string text = (note.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Keep anything with a leader
            if (note.HasLeader)
                return false;

            // Keep characteristic notes like {4}, {5}, etc.
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\{\d+\}$"))
                return false;

            // Keep descriptive / view labels
            string upper = text.ToUpperInvariant();

            if (upper.Contains("VIEW") ||
                upper.Contains("SECTION") ||
                upper.Contains("DETAIL"))
            {
                return false;
            }

            bool insideRealView = IsNoteInsideRealDrawingView(note, activeDoc);
            bool insideSheet = IsNoteInsideSheetArea(note, activeDoc);

            // Delete if:
            // - note is in a real drawing view
            // - OR note is on the sheet and inside sheet boundaries
            // Do NOT delete notes outside the sheet
            if (!(insideRealView || insideSheet))
                return false;

            // Delete short code-like no-leader notes
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Z0-9\/\-]{2,12}$"))
                return true;

            return false;
        }

        private bool IsNoteInsideRealDrawingView(
            NDwgAutoTool.Models.DrawingNoteInfo note,
            SldWorks.ModelDoc2 activeDoc)
        {
            if (note == null)
                return false;

            string noteViewName = (note.ViewName ?? "").Trim();

            if (string.IsNullOrWhiteSpace(noteViewName))
                return false;

            try
            {
                var drawing = activeDoc as SldWorks.DrawingDoc;
                if (drawing == null)
                    return false;

                var sheetView = drawing.GetFirstView();
                if (sheetView == null)
                    return false;

                string sheetViewName = (sheetView.Name ?? "").Trim();

                return !noteViewName.Equals(sheetViewName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private bool IsNoteInsideSheetArea(
            NDwgAutoTool.Models.DrawingNoteInfo note,
            SldWorks.ModelDoc2 activeDoc)
        {
            if (note == null)
                return false;

            try
            {
                var drawing = activeDoc as SldWorks.DrawingDoc;
                if (drawing == null)
                    return false;

                var sheetView = drawing.GetFirstView();
                if (sheetView == null)
                    return false;

                double[] outline = (double[])sheetView.GetOutline();

                if (outline == null || outline.Length < 4)
                    return false;

                double minX = Math.Min(outline[0], outline[2]);
                double maxX = Math.Max(outline[0], outline[2]);
                double minY = Math.Min(outline[1], outline[3]);
                double maxY = Math.Max(outline[1], outline[3]);

                return note.X >= minX && note.X <= maxX &&
                       note.Y >= minY && note.Y <= maxY;
            }
            catch
            {
                return false;
            }
        }

        private bool IsHoleCalloutText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            if (text.Equals("HOLE", StringComparison.OrdinalIgnoreCase))
                return true;

            return System.Text.RegularExpressions.Regex.IsMatch(
                text,
                @"^HOLE\s+\d+P$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private bool IsCharacteristicOnlyText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(
                text.Trim(),
                @"^\{\d+\}$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private bool IsInsideSheetArea(
            NDwgAutoTool.Models.DrawingNoteInfo note,
            SldWorks.ModelDoc2 activeDoc)
        {
            if (note == null)
                return false;

            try
            {
                var drawing = activeDoc as SldWorks.DrawingDoc;
                if (drawing == null)
                    return false;

                var sheetView = drawing.GetFirstView();
                if (sheetView == null)
                    return false;

                double[] outline = (double[])sheetView.GetOutline();

                if (outline == null || outline.Length < 4)
                    return false;

                double minX = Math.Min(outline[0], outline[2]);
                double maxX = Math.Max(outline[0], outline[2]);
                double minY = Math.Min(outline[1], outline[3]);
                double maxY = Math.Max(outline[1], outline[3]);

                return note.X >= minX && note.X <= maxX &&
                       note.Y >= minY && note.Y <= maxY;
            }
            catch
            {
                return false;
            }
        }

        private bool IsNearAnyCallout(
            NDwgAutoTool.Models.DrawingNoteInfo note,
            List<NDwgAutoTool.Models.DrawingNoteInfo> callouts,
            double tolerance = 0.035)
        {
            foreach (var callout in callouts)
            {
                double dx = note.X - callout.X;
                double dy = note.Y - callout.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance <= tolerance)
                    return true;
            }

            return false;
        }

        private bool IsInRealView(NDwgAutoTool.Models.DrawingNoteInfo note, SldWorks.ModelDoc2 activeDoc)
        {
            if (note == null)
                return false;

            try
            {
                var drawing = activeDoc as SldWorks.DrawingDoc;
                if (drawing == null)
                    return false;

                var sheetView = drawing.GetFirstView();
                if (sheetView == null)
                    return false;

                string sheetViewName = (sheetView.Name ?? "").Trim();
                string noteViewName = (note.ViewName ?? "").Trim();

                if (string.IsNullOrWhiteSpace(noteViewName))
                    return false;

                return !noteViewName.Equals(sheetViewName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldDeleteAutoGeneratedNoLeaderNote(
            NDwgAutoTool.Models.DrawingNoteInfo note,
            SldWorks.ModelDoc2 activeDoc,
            List<NDwgAutoTool.Models.DrawingNoteInfo> callouts,
            string selfPartNo)
        {
            if (note == null)
                return false;

            string text = (note.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (note.HasLeader)
                return false;

            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\{\d+\}$"))
                return false;

            if (IsHoleCalloutText(text))
                return false;

            if (!string.IsNullOrWhiteSpace(selfPartNo) &&
                text.StartsWith(selfPartNo, StringComparison.OrdinalIgnoreCase))
                return false;

            string upper = text.ToUpperInvariant();

            if (upper.Contains("VIEW") ||
                upper.Contains("SECTION") ||
                upper.Contains("DETAIL"))
                return false;

            bool codeLike = System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Z0-9\/\-]{2,12}$");
            if (!codeLike)
                return false;

            // Delete if in a real drawing view
            if (IsInRealView(note, activeDoc))
                return true;

            // Or if on the sheet but close to a callout
            if (IsInsideSheetArea(note, activeDoc) && IsNearAnyCallout(note, callouts))
                return true;

            // Otherwise keep it (protects title block and outside template area)
            return false;
        }

        private bool IsLeaderedFlagnoteCandidate(
            NDwgAutoTool.Models.DrawingNoteInfo note,
            SldWorks.ModelDoc2 activeDoc,
            string selfPartNo)
        {
            if (note == null)
                return false;

            if (note.NoteObject == null || note.AnnotationObject == null)
                return false;

            if (!note.HasLeader)
                return false;

            // Must be either:
            // - inside a real drawing view
            // - or on the sheet but inside sheet boundaries
            // Not outside the sheet
            bool insideRealView = IsInRealView(note, activeDoc);
            bool insideSheet = IsInsideSheetArea(note, activeDoc);

            if (!(insideRealView || insideSheet))
                return false;

            string text = (note.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (IsCharacteristicOnlyText(text))
                return false;

            if (IsHoleCalloutText(text))
                return false;

            if (!string.IsNullOrWhiteSpace(selfPartNo) &&
                text.StartsWith(selfPartNo, StringComparison.OrdinalIgnoreCase))
                return false;

            string upper = text.ToUpperInvariant();

            if (upper.Contains("VIEW") ||
                upper.Contains("SECTION") ||
                upper.Contains("DETAIL"))
                return false;

            // Exclude normal callouts themselves
            if (note.IsCallout)
                return false;

            return true;
        }

        private double DistanceBetweenNotes(
            NDwgAutoTool.Models.DrawingNoteInfo a,
            NDwgAutoTool.Models.DrawingNoteInfo b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        
        private string GetNormalizedDrawingPartNoFromDoc(SldWorks.ModelDoc2 activeDoc, SolidWorksService solidWorksService)
        {
            if (activeDoc is null)
                throw new Exception("No active SolidWorks document.");

            if (!solidWorksService.IsDrawing(activeDoc))
                throw new Exception("Active SolidWorks document is not a drawing.");

            string rawFileName = solidWorksService.GetActiveDrawingPartNo(activeDoc);

            string normalized = System.Text.RegularExpressions.Regex.Match(
                rawFileName,
                @"^[A-Z]{4}\d{6}[A-Z]\d{4}"
            ).Value;

            if (string.IsNullOrWhiteSpace(normalized))
                throw new Exception($"Could not extract drawing part number from file name: {rawFileName}");

            return normalized;
        }

        private int CountHoleCallouts(List<NDwgAutoTool.Models.DrawingNoteInfo> notes)
        {
            int total = 0;

            foreach (var note in notes)
            {
                NDwgAutoTool.Helpers.DrawingNoteHelper.Classify(note);

                if (string.IsNullOrWhiteSpace(note.Text))
                    continue;

                string text = note.Text.Trim();

                var match = System.Text.RegularExpressions.Regex.Match(
                    text,
                    @"^HOLE\s+(?<qty>\d+)P$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    total += int.Parse(match.Groups["qty"].Value);
                }
            }

            return total;
        }

        private List<NDwgAutoTool.Models.CheckDwgReportRow> BuildHoleReportRows(int totalHoleCount)
        {
            var rows = new List<NDwgAutoTool.Models.CheckDwgReportRow>();

            rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
            {
                CheckItem = "HOLE Callouts",
                Result = "Correct",
                Details = totalHoleCount > 0
                    ? $"Total HOLE quantity found on the drawing: {totalHoleCount}."
                    : "No HOLE callouts were found on the drawing."
            });

            return rows;
        }

        private string GetNetworkRootConfigPath()
        {
            return NDwgAutoTool.Services.ResourceLocator.RequiredRootPath;
        }

        private void LoadNetworkRootPathIntoUi()
        {
            try
            {
                NetworkRootPathTextBox.Text = NDwgAutoTool.Services.ResourceLocator.RequiredRootPath;
            }
            catch (Exception ex)
            {
                AddLog("Failed to load resource root: " + ex.Message);
                NetworkRootPathTextBox.Text = NDwgAutoTool.Services.ResourceLocator.RequiredRootPath;
            }
        }

        private void NetworkRootPathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyNetworkRootPathFromUi();
        }

        private void ApplyNetworkRootPathFromUi()
        {
            if (NetworkRootPathTextBox == null)
                return;

            string root = (NetworkRootPathTextBox.Text ?? string.Empty).Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(root))
                return;

            NDwgAutoTool.Services.ResourceLocator.SetRootPath(root);
        }

        private void LogResourceAvailability()
        {
            var report = _services.ResourceValidator.Validate();
            AddLog(report.ToLogText());
        }
    }
}
