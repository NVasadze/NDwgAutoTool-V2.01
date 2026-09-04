using Microsoft.Win32;
using NDwgAutoTool.Helpers;
using NDwgAutoTool.Infrastructure.Settings;
using NDwgAutoTool.Models;
using NDwgAutoTool.Services;
using SwConst;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NDwgAutoTool
{
    public partial class MainWindow : Window
    {
        private const double CompactWindowWidth = 143;
        private const double CompactWindowHeight = 500;
        private const double CompactHorizontalWindowWidth = 590;
        private const double CompactHorizontalWindowHeight = 95;
        private const double CompactHorizontalMinWidth = 590;
        private const double CompactHorizontalMinHeight = 95;
        private const double ExpandedWindowWidth = 995;
        private const double ExpandedWindowHeight = 515;
        private const double LeftPanelWidth = 125;
        private const double RightPanelWidth = 270;
        private const double MaxSidePanelWidth = 310;
        private const double PanelGap = 10;
        private const double CompactHorizontalButtonGap = 3;
        private const double CompactHorizontalButtonHeight = 20;
        private const double CompactHorizontalButtonFontSize = 8;
        private const double ActionButtonMinHeight = 26;
        private const double ActionButtonMaxHeight = 34;
        private const double ActionButtonMinFontSize = 11;
        private const double ActionButtonMaxFontSize = 13;
        private const string FarSideViewJapaneseText = "反対面を表示";
        private const string FarSideViewEnglishText = "(FAR SIDE VIEW)";
        private const string FarSideViewExpectedText = "<FONT effect=U>反対面を表示<FONT effect=RU>\r\n(FAR SIDE VIEW)";
        private const double FarSideViewTextHeight = 0.0036;
        private const double FarSideViewParagraphSpacing = 0.000001;
        private const double FarSideViewParagraphSpacingTolerance = 0.0000001;
        private const double FarSideViewYOffset = 0.0120;
        private const double FarSideViewSheetMargin = 0.0120;

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

            UpdateResponsiveLayout();
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
        private bool _compactButtonsHorizontal;
        private const string ShowMorePassword = "1234"; // Change this password
        private bool _showMorePasswordAccepted = false;


        public MainWindow()
        {
            InitializeComponent();
            _commandRunner = new UiCommandRunner(this, AddLog, SetLastAction, SetStatus);
            MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
            LoadNetworkRootPathIntoUi();
            LoadCompactViewPreferences();
            LoadBatchGroupPreferences();
            ApplyCompactLayout();
            RestoreUserWindowLocation();
            AddLog("NDwgAutoTool V2.04 started.");
            AddLog($"Resource root: {NDwgAutoTool.Services.ResourceLocator.RequiredRootPath}");
            LogResourceAvailability();
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveUserWindowLocation();
            base.OnClosed(e);
        }

        private void RestoreUserWindowLocation()
        {
            var settings = UserSettingsStore.Load();
            var location = settings.WindowLocation;

            if (location == null || !location.HasValue)
                return;

            if (!IsFiniteWindowCoordinate(location.Left) ||
                !IsFiniteWindowCoordinate(location.Top))
            {
                return;
            }

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = ClampWindowLeftToVirtualScreen(location.Left);
            Top = ClampWindowTopToVirtualScreen(location.Top);
        }

        private void LoadCompactViewPreferences()
        {
            var settings = UserSettingsStore.Load();
            _compactButtonsHorizontal = settings.CompactView?.ButtonsHorizontal == true;
        }

        private void LoadBatchGroupPreferences()
        {
            try
            {
                var settings = UserSettingsStore.Load();
                DrawingBatchExpander.IsExpanded = settings.BatchGroups?.DrawingBatchExpanded == true;
                DrawingToolsBatchExpander.IsExpanded = settings.BatchGroups?.DrawingToolsExpanded == true;
            }
            catch
            {
                DrawingBatchExpander.IsExpanded = false;
                DrawingToolsBatchExpander.IsExpanded = false;
            }
        }

        private void SaveUserWindowLocation()
        {
            try
            {
                var settings = UserSettingsStore.Load();

                settings.CompactView ??= new CompactViewPreferences();
                settings.CompactView.ButtonsHorizontal = _compactButtonsHorizontal;

                settings.BatchGroups ??= new BatchGroupPreferences();
                settings.BatchGroups.DrawingBatchExpanded = DrawingBatchExpander?.IsExpanded == true;
                settings.BatchGroups.DrawingToolsExpanded = DrawingToolsBatchExpander?.IsExpanded == true;

                Rect bounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, Width, Height)
                    : RestoreBounds;

                if (IsFiniteWindowCoordinate(bounds.Left) &&
                    IsFiniteWindowCoordinate(bounds.Top))
                {
                    settings.WindowLocation = new WindowLocationPreferences
                    {
                        HasValue = true,
                        Left = bounds.Left,
                        Top = bounds.Top
                    };
                }

                UserSettingsStore.Save(settings);
            }
            catch
            {
                // Window position is only a convenience preference.
            }
        }

        private void SaveCompactViewPreferences()
        {
            try
            {
                var settings = UserSettingsStore.Load();
                settings.CompactView ??= new CompactViewPreferences();
                settings.CompactView.ButtonsHorizontal = _compactButtonsHorizontal;
                UserSettingsStore.Save(settings);
            }
            catch
            {
                // Compact layout is only a convenience preference.
            }
        }

        private static bool IsFiniteWindowCoordinate(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private double ClampWindowLeftToVirtualScreen(double left)
        {
            double min = SystemParameters.VirtualScreenLeft;
            double max = SystemParameters.VirtualScreenLeft +
                         Math.Max(0, SystemParameters.VirtualScreenWidth - Math.Min(Width, CompactWindowWidth));

            return Math.Max(min, Math.Min(max, left));
        }

        private double ClampWindowTopToVirtualScreen(double top)
        {
            double min = SystemParameters.VirtualScreenTop;
            double max = SystemParameters.VirtualScreenTop +
                         Math.Max(0, SystemParameters.VirtualScreenHeight - Math.Min(Height, CompactWindowHeight));

            return Math.Max(min, Math.Min(max, top));
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

                reportRows.AddRange(BuildFarSideViewNoteReportRows(notes));

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
                PromptToOpenExcelReport("CheckDWG", outputPath, AllCheckRowsAreCorrect(reportRows));
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
            var request = OpenAllDrawingsWindow.Ask(this);

            if (request is null || request.Numbers.Count == 0)
                return;

            SetLastAction("Open All");
            SetStatus("Working...");
            AddLog($"Open All: requested {request.Numbers.Count} number(s); types: {request.Selection.Description}.");

            StyledProgressWindow? progress = null;
            var failed = new List<string>();
            var missing = new List<string>();
            var revisionMismatches = new List<string>();
            var matches = new List<OpenAllFileMatch>();
            int opened = 0;
            var requestedNumbers = request.Numbers
                .Select(FileFinderService.NormalizeRequestedNumber)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            int requested = requestedNumbers.Count * request.Selection.SelectedCount;

            try
            {
                var fileFinder = new FileFinderService();
                var drawingMatchesByNumber = new Dictionary<string, DrawingFileMatch>(StringComparer.OrdinalIgnoreCase);
                var containerMatchesByNumber = new Dictionary<string, ContainerFileMatch>(StringComparer.OrdinalIgnoreCase);
                var modelMatchesByNumber = new Dictionary<string, ModelFileMatch>(StringComparer.OrdinalIgnoreCase);

                if (request.Selection.Drawings)
                {
                    var drawingMatches = fileFinder.FindDrawingFiles(requestedNumbers, out var missingDrawings);
                    drawingMatchesByNumber = drawingMatches.ToDictionary(
                        match => match.DrawingNumber,
                        StringComparer.OrdinalIgnoreCase);
                    missing.AddRange(missingDrawings.Select(number => $"{number} (.slddrw)"));
                }

                if (request.Selection.Containers)
                {
                    var containerMatches = fileFinder.FindContainerFiles(requestedNumbers, out var missingContainers);
                    containerMatchesByNumber = containerMatches.ToDictionary(
                        match => match.ContainerNumber,
                        StringComparer.OrdinalIgnoreCase);
                    missing.AddRange(missingContainers.Select(number => $"{number} (.sldprt)"));
                }

                if (request.Selection.Models)
                {
                    var modelMatches = fileFinder.FindAssemblyFiles(requestedNumbers, out var missingModels);
                    modelMatchesByNumber = modelMatches.ToDictionary(
                        match => match.ModelNumber,
                        StringComparer.OrdinalIgnoreCase);
                    missing.AddRange(missingModels.Select(number => $"{number} (.sldasm)"));
                }

                foreach (string number in requestedNumbers)
                {
                    var matchesForNumber = new List<OpenAllFileMatch>();

                    if (request.Selection.Drawings &&
                        drawingMatchesByNumber.TryGetValue(number, out var drawingMatch))
                    {
                        matchesForNumber.Add(new OpenAllFileMatch(
                            OpenAllFileKind.Drawing,
                            drawingMatch.DrawingNumber,
                            drawingMatch.Revision,
                            drawingMatch.FilePath));
                    }

                    if (request.Selection.Containers &&
                        containerMatchesByNumber.TryGetValue(number, out var containerMatch))
                    {
                        matchesForNumber.Add(new OpenAllFileMatch(
                            OpenAllFileKind.Container,
                            containerMatch.ContainerNumber,
                            containerMatch.Revision,
                            containerMatch.FilePath));
                    }

                    if (request.Selection.Models &&
                        modelMatchesByNumber.TryGetValue(number, out var modelMatch))
                    {
                        matchesForNumber.Add(new OpenAllFileMatch(
                            OpenAllFileKind.Model,
                            modelMatch.ModelNumber,
                            modelMatch.Revision,
                            modelMatch.FilePath));
                    }

                    if (request.Selection.SelectedCount > 1 &&
                        matchesForNumber.Count != request.Selection.SelectedCount)
                    {
                        AddLog($"Open All: skipped {number} because one or more selected file types were not found.");
                        continue;
                    }

                    var revisions = matchesForNumber
                        .Select(match => match.Revision)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (revisions.Count > 1)
                    {
                        string mismatch = BuildOpenAllRevisionMismatch(number, matchesForNumber);
                        revisionMismatches.Add(mismatch);
                        AddLog($"Open All revision mismatch: {mismatch}");
                        continue;
                    }

                    matches.AddRange(matchesForNumber.OrderBy(GetOpenAllOpenOrder));
                }

                if (matches.Count == 0)
                {
                    string noMatchResult = BuildOpenAllResult(opened, requested, missing, revisionMismatches, failed);
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
                    string kind = GetOpenAllKindLabel(match.Kind);
                    string fileName = System.IO.Path.GetFileName(match.FilePath);

                    progress.UpdateProgress(
                        "Opening files...",
                        fileName,
                        i + 1,
                        matches.Count);

                    try
                    {
                        int errors = 0;
                        int warnings = 0;

                        SetOpenAllDocumentTypeVisible(swApp, match.Kind);

                        var document = match.Kind switch
                        {
                            OpenAllFileKind.Drawing => solidWorksService.OpenDrawing(
                                swApp,
                                match.FilePath,
                                out errors,
                                out warnings),
                            OpenAllFileKind.Container => solidWorksService.OpenPart(
                                swApp,
                                match.FilePath,
                                out errors,
                                out warnings),
                            OpenAllFileKind.Model => solidWorksService.OpenAssembly(
                                swApp,
                                match.FilePath,
                                out errors,
                                out warnings),
                            _ => throw new InvalidOperationException("Unsupported Open All file type.")
                        };

                        if (document is null)
                            throw new Exception($"SolidWorks did not return a document. Errors: {errors}; Warnings: {warnings}");

                        EnsureOpenAllDocumentVisible(swApp, document, match.FilePath);

                        opened++;
                        AddLog($"Open All: opened {kind} {match.Number} -> {match.FilePath}");

                        if (errors != 0 || warnings != 0)
                            AddLog($"Open All: SolidWorks reported errors={errors}, warnings={warnings} for {kind} {match.Number}.");
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{kind} {match.Number}: {ex.Message}");
                        AddLog($"Open All ERROR: {kind} {match.Number}: {ex.Message}");
                    }

                    await System.Threading.Tasks.Task.Delay(25);
                }

                string result = BuildOpenAllResult(opened, requested, missing, revisionMismatches, failed);
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

        private static string GetOpenAllKindLabel(OpenAllFileKind kind)
        {
            return kind switch
            {
                OpenAllFileKind.Drawing => "Drawing",
                OpenAllFileKind.Container => "Container",
                OpenAllFileKind.Model => "Model",
                _ => "File"
            };
        }

        private enum OpenAllFileKind
        {
            Drawing,
            Container,
            Model
        }

        private sealed record OpenAllFileMatch(
            OpenAllFileKind Kind,
            string Number,
            string Revision,
            string FilePath);

        private static int GetOpenAllOpenOrder(OpenAllFileMatch match)
        {
            return match.Kind switch
            {
                OpenAllFileKind.Container => 0,
                OpenAllFileKind.Drawing => 1,
                OpenAllFileKind.Model => 2,
                _ => 99
            };
        }

        private static void SetOpenAllDocumentTypeVisible(SldWorks.ISldWorks swApp, OpenAllFileKind kind)
        {
            try
            {
                int docType = kind switch
                {
                    OpenAllFileKind.Drawing => (int)SwConst.swDocumentTypes_e.swDocDRAWING,
                    OpenAllFileKind.Container => (int)SwConst.swDocumentTypes_e.swDocPART,
                    OpenAllFileKind.Model => (int)SwConst.swDocumentTypes_e.swDocASSEMBLY,
                    _ => 0
                };

                if (docType != 0)
                    swApp.DocumentVisible(true, docType);
            }
            catch
            {
            }
        }

        private static void EnsureOpenAllDocumentVisible(
            SldWorks.ISldWorks swApp,
            SldWorks.ModelDoc2 document,
            string filePath)
        {
            try
            {
                document.Visible = true;
            }
            catch
            {
            }

            try
            {
                int activationErrors = 0;
                string title = document.GetTitle();

                if (string.IsNullOrWhiteSpace(title))
                    title = System.IO.Path.GetFileName(filePath);

                swApp.ActivateDoc3(
                    title,
                    true,
                    (int)SwConst.swRebuildOnActivation_e.swDontRebuildActiveDoc,
                    ref activationErrors);
            }
            catch
            {
            }
        }

        private static string BuildOpenAllRevisionMismatch(
            string number,
            IReadOnlyList<OpenAllFileMatch> matches)
        {
            var details = matches
                .OrderBy(match => match.Kind)
                .Select(match =>
                    $"{GetOpenAllKindLabel(match.Kind)} rev {match.Revision} ({System.IO.Path.GetFileName(match.FilePath)})");

            return $"{number}: {string.Join("; ", details)}";
        }

        private static string BuildOpenAllResult(
            int opened,
            int requested,
            IReadOnlyList<string> missing,
            IReadOnlyList<string> revisionMismatches,
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

            if (revisionMismatches.Count > 0)
            {
                result.AppendLine();
                result.AppendLine("Revision mismatch:");
                foreach (string mismatch in revisionMismatches.Take(20))
                    result.AppendLine(mismatch);

                if (revisionMismatches.Count > 20)
                    result.AppendLine($"...and {revisionMismatches.Count - 20} more");
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
            var drawingNumbers = OpenAllDrawingsWindow.AskDrawingNumbersOnly(
                this,
                "Create FORM 3 For All",
                "Create");

            if (drawingNumbers is null || drawingNumbers.Count == 0)
                return;

            string? outputFolder = AskForm3ForAllOutputFolder();

            if (string.IsNullOrWhiteSpace(outputFolder))
                return;

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

                var fileFinder = new FileFinderService();
                var drawingMatches = fileFinder.FindDrawingFiles(drawingNumbers, out var missingDrawings);

                foreach (string missingDrawing in missingDrawings)
                {
                    failedDrawings.Add($"{missingDrawing}: drawing file not found");
                    AddLog($"Create FORM 3 For All: missing -> {missingDrawing}");
                }

                processed = missingDrawings.Count;
                failed = missingDrawings.Count;

                if (drawingMatches.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Create FORM 3 For All",
                        "No matching drawing files were found.",
                        this);
                    SetStatus("Ready");
                    return;
                }

                AddLog($"Create FORM 3 For All: requested {drawingNumbers.Count} drawing number(s).");
                AddLog($"Create FORM 3 For All: found {drawingMatches.Count} drawing file(s).");

                var service = new NDwgAutoTool.Services.Form3Service(AddLog);
                AddLog($"Create FORM 3 For All: output folder -> {outputFolder}");

                progress = new StyledProgressWindow("Create FORM 3 For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawingMatches.Count; i++)
                {
                    var match = drawingMatches[i];
                    string drawingPartNo = match.DrawingNumber;
                    SldWorks.ModelDoc2? drawing = null;
                    bool openedByBatch = false;

                    progress.UpdateProgress(
                        "Creating Form 3 files...",
                        drawingPartNo,
                        i + 1,
                        drawingMatches.Count);

                    AddLog($"Create FORM 3 For All: processing {drawingPartNo} -> {match.FilePath}");

                    try
                    {
                        drawing = solidWorksService.FindOpenDocumentByPath(swApp, match.FilePath);

                        if (drawing == null)
                        {
                            drawing = solidWorksService.OpenDrawing(
                                swApp,
                                match.FilePath,
                                out int openErrors,
                                out int openWarnings);

                            openedByBatch = true;

                            if (drawing == null)
                                throw new Exception($"SolidWorks did not return a drawing. Errors: {openErrors}; Warnings: {openWarnings}");

                            if (openErrors != 0 || openWarnings != 0)
                                AddLog($"Create FORM 3 For All: SolidWorks reported errors={openErrors}, warnings={openWarnings} for {drawingPartNo}.");
                        }
                        else
                        {
                            AddLog($"Create FORM 3 For All: drawing already open -> {drawingPartNo}");
                        }

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

                        service.SetTargetDrawing(drawing);
                        string fileName = service.GetDefaultOutputFileName();
                        string outputPath = System.IO.Path.Combine(outputFolder, fileName);

                        service.CreateForm3ToPath(
                            outputPath,
                            null,
                            showSuccessPopup: false,
                            throwOnCancel: true);

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
                    finally
                    {
                        service.SetTargetDrawing(null);

                        if (openedByBatch && drawing != null)
                        {
                            try
                            {
                                solidWorksService.CloseDocumentWithoutSaving(swApp, drawing);
                                AddLog($"Create FORM 3 For All: closed without saving -> {drawingPartNo}");
                            }
                            catch (Exception ex)
                            {
                                AddLog($"Create FORM 3 For All: failed to close {drawingPartNo} | {ex.Message}");
                            }
                        }
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

        private string? AskForm3ForAllOutputFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select folder for FORM 3 output",
                Multiselect = false
            };

            return dialog.ShowDialog(this) == true
                ? dialog.FolderName
                : null;
        }


        private async void CreatePdfForAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetLastAction("Create Check PDF For All");
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
                        "Create Check PDF For All",
                        "No open drawing documents were found.",
                        this);

                    SetStatus("Ready");
                    return;
                }

                AddLog($"Create Check PDF For All: found {drawings.Count} open drawing(s).");

                progress = new StyledProgressWindow("Create Check PDF For All", this);
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

                    AddLog($"Create Check PDF For All: processing {drawingName}");

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
                        AddLog($"Create Check PDF For All: success -> {drawingName}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingName);
                        AddLog($"Create Check PDF For All: FAILED -> {drawingName} | {ex.Message}");
                    }

                    processed++;
                    await System.Threading.Tasks.Task.Delay(50);
                }

                progress.MarkComplete("Create Check PDF For All completed.", processed);
                await System.Threading.Tasks.Task.Delay(250);
                progress.Close();

                BatchResultWindow.ShowResult(
                    "Create Check PDF For All",
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
                    "Create Check PDF For All Error",
                    ex.Message,
                    this);
            }

            SetStatus("Ready");
        }

        private async void PdfNoCheckButton_Click(object sender, RoutedEventArgs e)
        {
            var drawingNumbers = OpenAllDrawingsWindow.AskDrawingNumbersOnly(
                this,
                "Create PDF For All",
                "Create");

            if (drawingNumbers is null || drawingNumbers.Count == 0)
                return;

            string? outputFolder = AskPdfNoCheckOutputFolder();

            if (string.IsNullOrWhiteSpace(outputFolder))
                return;

            SetLastAction("Create PDF For All");
            SetStatus("Working...");

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
                    AddLog($"Create PDF For All: missing -> {missingDrawing}");
                }

                processed = missingDrawings.Count;
                failed = missingDrawings.Count;

                if (drawingMatches.Count == 0)
                {
                    StyledMessageWindow.ShowMessage(
                        "Create PDF For All",
                        "No matching drawing files were found.",
                        this);
                    SetStatus("Ready");
                    return;
                }

                var pdfService = new NDwgAutoTool.Services.PdfService(AddLog);
                var solidWorksService = new SolidWorksService();
                var swApp = solidWorksService.GetApplication();

                if (swApp == null)
                    throw new Exception("Could not connect to SolidWorks.");

                AddLog($"Create PDF For All: requested {drawingNumbers.Count} drawing number(s).");
                AddLog($"Create PDF For All: found {drawingMatches.Count} drawing file(s).");
                AddLog($"Create PDF For All: output folder -> {outputFolder}");

                progress = new StyledProgressWindow("Create PDF For All", this);
                progress.Show();
                await System.Threading.Tasks.Task.Delay(50);

                for (int i = 0; i < drawingMatches.Count; i++)
                {
                    var match = drawingMatches[i];
                    string drawingName = System.IO.Path.GetFileNameWithoutExtension(match.FilePath);
                    SldWorks.ModelDoc2? drawing = null;
                    bool openedByBatch = false;

                    progress.UpdateProgress(
                        "Creating PDFs...",
                        drawingName,
                        i + 1,
                        drawingMatches.Count);

                    AddLog($"Create PDF For All: processing {drawingName} -> {match.FilePath}");

                    try
                    {
                        drawing = solidWorksService.FindOpenDocumentByPath(swApp, match.FilePath);

                        if (drawing == null)
                        {
                            drawing = solidWorksService.OpenDrawing(
                                swApp,
                                match.FilePath,
                                out int openErrors,
                                out int openWarnings);

                            openedByBatch = true;

                            if (drawing == null)
                                throw new Exception($"SolidWorks did not return a drawing. Errors: {openErrors}; Warnings: {openWarnings}");

                            if (openErrors != 0 || openWarnings != 0)
                                AddLog($"Create PDF For All: SolidWorks reported errors={openErrors}, warnings={openWarnings} for {drawingName}.");
                        }
                        else
                        {
                            AddLog($"Create PDF For All: drawing already open -> {drawingName}");
                        }

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

                        string pdfPath = pdfService.CreatePdfNoCheckFromDrawing(drawing, outputFolder);

                        succeeded++;
                        AddLog($"Create PDF For All: success -> {pdfPath}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        failedDrawings.Add(drawingName);
                        AddLog($"Create PDF For All: FAILED -> {drawingName} | {ex.Message}");
                    }
                    finally
                    {
                        if (openedByBatch && drawing != null)
                        {
                            try
                            {
                                solidWorksService.CloseDocumentWithoutSaving(swApp, drawing);
                                AddLog($"Create PDF For All: closed without saving -> {drawingName}");
                            }
                            catch (Exception ex)
                            {
                                AddLog($"Create PDF For All: failed to close {drawingName} | {ex.Message}");
                            }
                        }
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

        private string? AskPdfNoCheckOutputFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select folder for PDF output",
                Multiselect = false
            };

            return dialog.ShowDialog(this) == true
                ? dialog.FolderName
                : null;
        }

        private async void CheckDwgForAllButton_Click(object sender, RoutedEventArgs e)
        {
            var drawingNumbers = OpenAllDrawingsWindow.AskDrawingNumbersOnly(
                this,
                "CheckDWG For All",
                "Check");

            if (drawingNumbers is null || drawingNumbers.Count == 0)
                return;

            await RunCheckDwgForDrawingNumbers(drawingNumbers);
        }
        private void PromptToOpenExcelReport(string title, string outputPath, bool allChecksCorrect)
        {
            string question = allChecksCorrect
                ? "All checks passed. Open the generated Excel report?"
                : "Some checks need attention. Open the generated Excel report?";

            bool openReport = StyledConfirmWindow.ShowConfirm(
                title,
                question,
                outputPath,
                this);

            if (!openReport)
                return;

            try
            {
                Process.Start(new ProcessStartInfo(outputPath)
                {
                    UseShellExecute = true
                });

                AddLog($"{title}: opened Excel report -> {outputPath}");
            }
            catch (Exception ex)
            {
                AddLog($"{title}: failed to open Excel report -> {ex.Message}");
                StyledMessageWindow.ShowMessage(
                    $"{title} Error",
                    $"Report was created, but could not be opened.\n\n{ex.Message}",
                    this);
            }
        }

        private static bool AllCheckReportsAreCorrect(
            IEnumerable<(string DrawingPartNo, List<NDwgAutoTool.Models.CheckDwgReportRow> Rows)> reports)
        {
            return reports.Any() && reports.All(report => AllCheckRowsAreCorrect(report.Rows));
        }

        private static bool AllCheckRowsAreCorrect(IEnumerable<NDwgAutoTool.Models.CheckDwgReportRow> rows)
        {
            return rows.Any() &&
                   rows.All(row => row.Result.Equals("Correct", StringComparison.OrdinalIgnoreCase));
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
            MinWidth = _compactButtonsHorizontal ? CompactHorizontalMinWidth : CompactWindowWidth;
            MinHeight = _compactButtonsHorizontal ? CompactHorizontalMinHeight : CompactWindowHeight;

            MiddlePanel.Visibility = Visibility.Collapsed;
            RightPanel.Visibility = Visibility.Collapsed;

            LeftSpacerColumn.Width = new GridLength(0);
            MiddleColumn.Width = new GridLength(0);
            RightSpacerColumn.Width = new GridLength(0);
            RightColumn.Width = new GridLength(0);

            LeftColumn.Width = new GridLength(_compactButtonsHorizontal
                ? CompactHorizontalWindowWidth - 16
                : LeftPanelWidth);

            ToggleLayoutButton.Content = "SHOW MORE";

            Width = _compactButtonsHorizontal ? CompactHorizontalWindowWidth : CompactWindowWidth;
            Height = _compactButtonsHorizontal ? CompactHorizontalWindowHeight : CompactWindowHeight;
            UpdateResponsiveLayout();
        }

        private void ApplyExpandedLayout()
        {
            _isCompactMode = false;
            MinWidth = ExpandedWindowWidth;
            MinHeight = ExpandedWindowHeight;

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
            UpdateResponsiveLayout();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout();
        }

        private void UpdateResponsiveLayout()
        {
            if (LeftColumn == null || RightColumn == null || ToggleLayoutButton == null)
                return;

            ApplyWindowDensity();

            double currentWidth = ActualWidth > 0 ? ActualWidth : Width;
            double currentHeight = ActualHeight > 0 ? ActualHeight : Height;

            if (_isCompactMode)
            {
                double compactWidthProgress = Clamp((currentWidth - CompactWindowWidth) / 320d, 0, 1);
                double compactHeightProgress = Clamp((currentHeight - CompactWindowHeight) / 420d, 0, 1);
                double compactScale = Math.Max(compactWidthProgress, compactHeightProgress * 0.85);

                double compactPanelWidth = _compactButtonsHorizontal
                    ? Math.Max(LeftPanelWidth, currentWidth - 16)
                    : LeftPanelWidth + ((MaxSidePanelWidth - LeftPanelWidth) * compactWidthProgress);
                LeftColumn.Width = new GridLength(compactPanelWidth);

                double compactButtonHeight = _compactButtonsHorizontal
                    ? CompactHorizontalButtonHeight
                    : ActionButtonMinHeight + ((ActionButtonMaxHeight - ActionButtonMinHeight) * compactScale);

                double compactButtonFontSize = _compactButtonsHorizontal
                    ? CompactHorizontalButtonFontSize
                    : ActionButtonMinFontSize + ((ActionButtonMaxFontSize - ActionButtonMinFontSize) * compactScale);

                SetToolButtonMetrics(compactButtonHeight, compactButtonFontSize);
                ApplyLeftCommandFlowLayout(compactPanelWidth);
                return;
            }

            double widthProgress = Clamp((currentWidth - ExpandedWindowWidth) / 1000d, 0, 1);
            double heightProgress = Clamp((currentHeight - ExpandedWindowHeight) / 520d, 0, 1);
            double scale = Math.Max(widthProgress, heightProgress * 0.85);

            double leftPanelWidth = LeftPanelWidth + ((MaxSidePanelWidth - LeftPanelWidth) * widthProgress);
            double rightPanelWidth = RightPanelWidth + ((MaxSidePanelWidth - RightPanelWidth) * widthProgress);

            LeftColumn.Width = new GridLength(leftPanelWidth);
            RightColumn.Width = new GridLength(rightPanelWidth);

            double buttonHeight = ActionButtonMinHeight + ((ActionButtonMaxHeight - ActionButtonMinHeight) * scale);
            double buttonFontSize = ActionButtonMinFontSize + ((ActionButtonMaxFontSize - ActionButtonMinFontSize) * scale);
            SetToolButtonMetrics(buttonHeight, buttonFontSize);
            ApplyLeftCommandFlowLayout(leftPanelWidth);

            if (NetworkRootPathTextBox != null)
            {
                NetworkRootPathTextBox.Height = 28 + (4 * scale);
                NetworkRootPathTextBox.FontSize = 13 + scale;
            }

            if (LogTextBox != null)
                LogTextBox.FontSize = 13 + scale;
        }

        private void ApplyWindowDensity()
        {
            bool compactHorizontal = _isCompactMode && _compactButtonsHorizontal;

            if (TitleBarRow != null)
                TitleBarRow.Height = new GridLength(compactHorizontal ? 28 : 40);

            if (TitleBarBorder != null)
                TitleBarBorder.Padding = compactHorizontal
                    ? new Thickness(8, 3, 8, 3)
                    : new Thickness(12, 8, 12, 8);

            if (MainContentGrid != null)
                MainContentGrid.Margin = compactHorizontal
                    ? new Thickness(4)
                    : new Thickness(8);

            if (LeftPanel != null)
                LeftPanel.Padding = compactHorizontal
                    ? new Thickness(5)
                    : new Thickness(9);

            if (LeftPanelScrollViewer != null)
                LeftPanelScrollViewer.VerticalScrollBarVisibility = compactHorizontal
                    ? ScrollBarVisibility.Disabled
                    : ScrollBarVisibility.Auto;

            if (AppTitleTextBlock != null)
                AppTitleTextBlock.FontSize = compactHorizontal ? 12 : 15;

            if (AppVersionTextBlock != null)
            {
                AppVersionTextBlock.FontSize = compactHorizontal ? 10 : 13;
                AppVersionTextBlock.Margin = compactHorizontal
                    ? new Thickness(3, 1, 0, 0)
                    : new Thickness(4, 2, 0, 0);
            }

            if (TitleBarButtonsPanel != null)
            {
                TitleBarButtonsPanel.Margin = compactHorizontal
                    ? new Thickness(0)
                    : new Thickness(0, -3, 0, 0);

                foreach (var button in TitleBarButtonsPanel.Children.OfType<Button>())
                {
                    button.Width = compactHorizontal ? 20 : 26;
                    button.Height = compactHorizontal ? 20 : 26;
                    button.FontSize = compactHorizontal ? 10 : 13;
                    button.Margin = compactHorizontal
                        ? new Thickness(3, 0, 0, 0)
                        : new Thickness(4, 0, 0, 0);
                }
            }
        }

        private void ApplyLeftCommandFlowLayout(double panelWidth)
        {
            if (LeftCommandFlowPanel == null ||
                ActiveDrawingHeaderTextBlock == null ||
                CompactOrientationToggleButton == null)
            {
                return;
            }

            bool useHorizontalFlow = _isCompactMode && _compactButtonsHorizontal;
            double contentWidth = Math.Max(
                90,
                panelWidth - LeftPanel.Padding.Left - LeftPanel.Padding.Right - 2);

            LeftCommandFlowPanel.Orientation = useHorizontalFlow
                ? Orientation.Horizontal
                : Orientation.Vertical;

            ActiveDrawingHeaderTextBlock.Visibility = _isCompactMode
                ? Visibility.Collapsed
                : Visibility.Visible;

            CompactOrientationToggleButton.Visibility = _isCompactMode
                ? Visibility.Visible
                : Visibility.Collapsed;

            CompactOrientationToggleButton.Content = _compactButtonsHorizontal
                ? "Vertical"
                : "Horizontal";

            CompactOrientationToggleButton.HorizontalContentAlignment = useHorizontalFlow
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Left;

            ActiveDrawingHeaderTextBlock.Width = contentWidth;
            ActiveDrawingHeaderTextBlock.Margin = new Thickness(0, 0, 0, 6);

            double buttonWidth = useHorizontalFlow
                ? CalculateCompactHorizontalButtonWidth(contentWidth)
                : contentWidth;

            foreach (var button in GetPanelButtons(LeftPanel))
            {
                button.Width = buttonWidth;
                button.MinWidth = useHorizontalFlow ? buttonWidth : 90;

                if (useHorizontalFlow)
                {
                    button.Margin = new Thickness(0, 0, CompactHorizontalButtonGap, CompactHorizontalButtonGap);
                    continue;
                }

                button.Margin = new Thickness(0, 0, 0, 3);

                if (ReferenceEquals(button, ToggleLayoutButton) ||
                    ReferenceEquals(button, OpenAllButton))
                {
                    button.Margin = new Thickness(0, 0, 0, 9);
                }
                else if (ReferenceEquals(button, CompactOrientationToggleButton))
                {
                    button.Margin = new Thickness(0, 0, 0, 6);
                }
                else if (string.Equals(button.Content?.ToString(), "Close All", StringComparison.Ordinal))
                {
                    button.Margin = new Thickness(0);
                }
            }
        }

        private static double CalculateCompactHorizontalButtonWidth(double contentWidth)
        {
            int columns =
                contentWidth >= 1080 ? 14 :
                contentWidth >= 820 ? 10 :
                contentWidth >= 680 ? 8 :
                7;

            double usableWidth = contentWidth - (CompactHorizontalButtonGap * (columns - 1));
            double buttonWidth = Math.Floor(usableWidth / columns);
            return Clamp(buttonWidth, 68, 90);
        }

        private void SetToolButtonMetrics(double height, double fontSize)
        {
            foreach (var button in GetPanelButtons(LeftPanel).Concat(GetPanelButtons(RightPanel)))
            {
                button.Height = height;
                button.FontSize = fontSize;
            }
        }

        private static IEnumerable<Button> GetPanelButtons(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is Button button)
                    yield return button;

                foreach (var nestedButton in GetPanelButtons(child))
                    yield return nestedButton;
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private void CompactOrientationToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _compactButtonsHorizontal = !_compactButtonsHorizontal;
            SaveCompactViewPreferences();

            if (_isCompactMode)
            {
                ApplyCompactLayout();
                return;
            }

            UpdateResponsiveLayout();
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

        private List<NDwgAutoTool.Models.CheckDwgReportRow> BuildFarSideViewNoteReportRows(
            List<NDwgAutoTool.Models.DrawingNoteInfo> notes)
        {
            var rows = new List<NDwgAutoTool.Models.CheckDwgReportRow>();

            var exactNote = FindExactFarSideViewNote(notes);

            if (exactNote != null)
            {
                var issues = GetFarSideViewNoteFormattingIssues(exactNote);

                rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
                {
                    CheckItem = "Far Side View Note",
                    Result = issues.Count == 0 ? "Correct" : "Issue Found",
                    Details = issues.Count == 0
                        ? $"Required note is present, centered, and Japanese text is underlined: {FormatFarSideViewExpectedText()}."
                        : $"Required note text is correct, but {string.Join(" ", issues)}"
                });

                return rows;
            }

            var relatedNote = FindRelatedFarSideViewNote(notes);

            if (relatedNote != null)
            {
                var messages = new List<string>
                {
                    $"A Far Side View note was found, but the text does not match. Expected: {FormatFarSideViewExpectedText()}. Found: {FormatNoteTextForReport(relatedNote.Text)}."
                };

                if (!IsNoteTextCentered(relatedNote))
                    messages.Add($"Text justification is not Center. Found: {GetNoteTextJustificationName(relatedNote)}.");

                if (!IsFarSideViewJapaneseTextUnderlined(relatedNote))
                    messages.Add("Japanese text is not underlined.");

                rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
                {
                    CheckItem = "Far Side View Note",
                    Result = "Issue Found",
                    Details = string.Join(" ", messages)
                });

                return rows;
            }

            rows.Add(new NDwgAutoTool.Models.CheckDwgReportRow
            {
                CheckItem = "Far Side View Note",
                Result = "Issue Found",
                Details = $"Required note was not found: {FormatFarSideViewExpectedText()}."
            });

            return rows;
        }

        private static NDwgAutoTool.Models.DrawingNoteInfo? FindExactFarSideViewNote(
            IEnumerable<NDwgAutoTool.Models.DrawingNoteInfo> notes)
        {
            return notes.FirstOrDefault(NoteHasFarSideViewText);
        }

        private static NDwgAutoTool.Models.DrawingNoteInfo? FindRelatedFarSideViewNote(
            IEnumerable<NDwgAutoTool.Models.DrawingNoteInfo> notes)
        {
            return notes.FirstOrDefault(note =>
                !NoteHasFarSideViewText(note) &&
                IsRelatedFarSideViewNoteText(note.Text));
        }

        private static bool NoteHasFarSideViewText(NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            var lines = NormalizeNoteLines(note.Text);

            for (int i = 0; i < lines.Count - 1; i++)
            {
                if (lines[i].Equals(FarSideViewJapaneseText, StringComparison.OrdinalIgnoreCase) &&
                    lines[i + 1].Equals(FarSideViewEnglishText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRelatedFarSideViewNoteText(string text)
        {
            string normalized = string.Join(" ", NormalizeNoteLines(text));

            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            string upper = normalized.ToUpperInvariant();

            return normalized.Contains(FarSideViewJapaneseText, StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("反対面", StringComparison.OrdinalIgnoreCase) ||
                   upper.Contains("FAR SIDE") ||
                   upper.Contains("FAR SIDE VIEW") ||
                   (upper.Contains("FAR") && upper.Contains("SIDE") && upper.Contains("VIEW"));
        }

        private static List<string> NormalizeNoteLines(string text)
        {
            return (text ?? "")
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => StripSolidWorksTextTags(line).Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
        }

        private static string StripSolidWorksTextTags(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            return System.Text.RegularExpressions.Regex.Replace(
                text,
                "<FONT[^>]*>",
                "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private static bool IsNoteTextCentered(NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            try
            {
                return note.NoteObject != null &&
                       note.NoteObject.GetTextJustification() == (int)swTextJustification_e.swTextJustificationCenter;
            }
            catch
            {
                return false;
            }
        }

        private static string GetNoteTextJustificationName(NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            try
            {
                if (note.NoteObject == null)
                    return "Unknown";

                int justification = note.NoteObject.GetTextJustification();

                return justification switch
                {
                    (int)swTextJustification_e.swTextJustificationCenter => "Center",
                    (int)swTextJustification_e.swTextJustificationLeft => "Left",
                    (int)swTextJustification_e.swTextJustificationRight => "Right",
                    (int)swTextJustification_e.swTextJustificationNone => "None",
                    _ => $"Unknown ({justification})"
                };
            }
            catch
            {
                return "Unknown";
            }
        }

        private static List<string> GetFarSideViewNoteFormattingIssues(
            NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            var issues = new List<string>();

            if (!IsNoteTextCentered(note))
                issues.Add($"Text justification is not Center. Found: {GetNoteTextJustificationName(note)}.");

            if (!IsFarSideViewJapaneseTextUnderlined(note))
                issues.Add("Japanese text is not underlined.");

            return issues;
        }

        private static bool IsFarSideViewNoteFormattingCorrect(
            NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            return IsFarSideViewJapaneseTextUnderlined(note);
        }

        private static bool IsFarSideViewParagraphSpacingCorrect(
            NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            try
            {
                var paragraphs = note.AnnotationObject?.GetParagraphs() as SldWorks.Paragraphs;
                if (paragraphs == null)
                    return false;

                int count = Math.Max(1, paragraphs.Count);

                for (int i = 0; i < count; i++)
                {
                    paragraphs.CurrentParagraph = i;

                    if (!paragraphs.GetFormatting(out double paragraphSpacing, out _))
                        return false;

                    if (Math.Abs(paragraphSpacing - FarSideViewParagraphSpacing) > FarSideViewParagraphSpacingTolerance)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsFarSideViewJapaneseTextUnderlined(
            NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            try
            {
                var paragraphs = note.AnnotationObject?.GetParagraphs() as SldWorks.Paragraphs;
                if (paragraphs == null)
                    return false;

                int count = Math.Max(1, paragraphs.Count);

                for (int i = 0; i < count; i++)
                {
                    paragraphs.CurrentParagraph = i;
                    int segmentCount = paragraphs.GetTextSegmentCount();

                    for (int segment = 0; segment < segmentCount; segment++)
                    {
                        string segmentText = StripSolidWorksTextTags(paragraphs.GetTextSegmentText(segment) ?? "");

                        if (!segmentText.Contains(FarSideViewJapaneseText, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var textFormat = paragraphs.GetTextSegmentFormat(segment) as SldWorks.TextFormat;
                        return textFormat?.Underline == true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static string FormatFarSideViewExpectedText()
        {
            return $"{FarSideViewJapaneseText} / {FarSideViewEnglishText}";
        }

        private static string FormatNoteTextForReport(string text)
        {
            var lines = NormalizeNoteLines(text);

            if (lines.Count == 0)
                return "<empty>";

            return string.Join(" / ", lines);
        }

        private string EnsureFarSideViewNoteForDrawing(
            SldWorks.ModelDoc2 activeDoc,
            SolidWorksService solidWorksService,
            SldWorks.View? activeTargetView)
        {
            var notes = solidWorksService.GetAllNotesOnActiveSheet(activeDoc);
            var drawing = activeDoc as SldWorks.DrawingDoc;

            if (drawing == null)
                throw new Exception("Active SolidWorks document is not a drawing.");

            var targetView = activeTargetView ?? GetActiveUserDrawingView(drawing);
            var targetPosition = GetFarSideViewNoteCreatePosition(drawing, targetView);
            var candidates = notes
                .Where(note => NoteHasFarSideViewText(note) || IsRelatedFarSideViewNoteText(note.Text))
                .ToList();

            if (targetView != null)
            {
                string targetViewName = targetView.Name ?? "";
                var notesOutsideTargetView = candidates
                    .Where(note => !NoteBelongsToView(note, targetViewName))
                    .ToList();

                foreach (var note in notesOutsideTargetView)
                    DeleteFarSideViewNote(activeDoc, note);

                candidates = candidates
                    .Where(note => NoteBelongsToView(note, targetViewName))
                    .ToList();
            }

            var targetNote = FindBestFarSideViewNote(candidates, targetPosition.X, targetPosition.Y);

            if (targetNote != null)
            {
                bool noteTextIsExact = NoteHasFarSideViewText(targetNote);
                bool noteIsCentered = IsNoteTextCentered(targetNote);
                bool noteFormattingIsCorrect = IsFarSideViewNoteFormattingCorrect(targetNote);

                if (noteTextIsExact && noteIsCentered && noteFormattingIsCorrect && IsFarSideViewNoteVisiblyUsable(targetNote))
                    return "Far Side View note is already correct.";

                if (noteTextIsExact && noteIsCentered && IsProbablyInvisibleSheetNote(targetNote, drawing))
                {
                    var createView = RequireActiveFarSideViewTarget(targetView);
                    var createPosition = GetFarSideViewNoteCreatePosition(drawing, createView);

                    DeleteFarSideViewNote(activeDoc, targetNote);
                    CreateFarSideViewNoteInView(activeDoc, drawing, createView, createPosition.X, createPosition.Y);
                    return $"Far Side View note was recreated in active view '{createView.Name}' because the existing note was not visible.";
                }

                FixExistingFarSideViewNote(activeDoc, targetNote);

                if (!noteTextIsExact && !noteIsCentered)
                    return "Far Side View note text and centering were fixed.";

                if (!noteTextIsExact)
                    return "Far Side View note text was fixed.";

                if (!noteIsCentered)
                    return "Far Side View note centering was fixed.";

                if (!noteFormattingIsCorrect)
                    return "Far Side View note formatting was fixed.";

                return "Far Side View note was refreshed.";
            }

            var requiredTargetView = RequireActiveFarSideViewTarget(targetView);
            var requiredTargetPosition = GetFarSideViewNoteCreatePosition(drawing, requiredTargetView);

            CreateFarSideViewNoteInView(activeDoc, drawing, requiredTargetView, requiredTargetPosition.X, requiredTargetPosition.Y);
            return $"Far Side View note was missing and has been created in active view '{requiredTargetView.Name}'.";
        }

        private static SldWorks.View? GetActiveUserDrawingView(SldWorks.DrawingDoc drawing)
        {
            try
            {
                var activeView = drawing.ActiveDrawingView as SldWorks.View;
                if (activeView == null)
                    return null;

                string activeViewName = activeView.Name ?? "";
                string sheetViewName = GetSheetViewName(drawing);

                if (string.IsNullOrWhiteSpace(activeViewName))
                    return null;

                if (!string.IsNullOrWhiteSpace(sheetViewName) &&
                    activeViewName.Equals(sheetViewName, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return activeView;
            }
            catch
            {
                return null;
            }
        }

        private static SldWorks.View RequireActiveFarSideViewTarget(SldWorks.View? targetView)
        {
            if (targetView != null)
                return targetView;

            throw new Exception("Far Side View note needs to be created, but no drawing view is active. Activate the view that should own the Far Side View note, then run Fill Title Block again.");
        }

        private static bool NoteBelongsToView(NDwgAutoTool.Models.DrawingNoteInfo note, string viewName)
        {
            return !string.IsNullOrWhiteSpace(viewName) &&
                   note.ViewName.Equals(viewName, StringComparison.OrdinalIgnoreCase);
        }

        private static NDwgAutoTool.Models.DrawingNoteInfo? FindBestFarSideViewNote(
            IEnumerable<NDwgAutoTool.Models.DrawingNoteInfo> candidates,
            double targetX,
            double targetY)
        {
            return candidates
                .OrderBy(note => Math.Abs(note.X - targetX) + Math.Abs(note.Y - targetY))
                .FirstOrDefault();
        }

        private static bool IsFarSideViewNoteVisiblyUsable(NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            try
            {
                if (note.NoteObject != null && !note.NoteObject.Visible)
                    return false;

                if (note.AnnotationObject != null && note.AnnotationObject.Visible == 0)
                    return false;
            }
            catch
            {
            }

            return true;
        }

        private static bool IsProbablyInvisibleSheetNote(
            NDwgAutoTool.Models.DrawingNoteInfo note,
            SldWorks.DrawingDoc drawing)
        {
            string sheetViewName = GetSheetViewName(drawing);

            return !string.IsNullOrWhiteSpace(sheetViewName) &&
                   note.ViewName.Equals(sheetViewName, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSheetViewName(SldWorks.DrawingDoc drawing)
        {
            try
            {
                var sheetView = drawing.GetFirstView() as SldWorks.View;
                return sheetView?.Name ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static void DeleteFarSideViewNote(
            SldWorks.ModelDoc2 activeDoc,
            NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            if (note.AnnotationObject == null)
                return;

            activeDoc.ClearSelection2(true);

            if (note.AnnotationObject.Select3(false, null))
            {
                activeDoc.Extension.DeleteSelection2(
                    (int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            }

            activeDoc.ClearSelection2(true);
            activeDoc.GraphicsRedraw2();
            activeDoc.WindowRedraw();
        }

        private static void FixExistingFarSideViewNote(
            SldWorks.ModelDoc2 activeDoc,
            NDwgAutoTool.Models.DrawingNoteInfo note)
        {
            if (note.NoteObject == null)
                throw new Exception("Far Side View note was found, but its SolidWorks note object is missing.");

            note.NoteObject.SetText(FarSideViewExpectedText);
            ApplyFarSideViewNoteFormatting(note.NoteObject);

            if (note.AnnotationObject != null)
                note.AnnotationObject.Visible = 1;

            note.Text = FarSideViewExpectedText;

            activeDoc.EditRebuild3();
            activeDoc.ForceRebuild3(false);
            activeDoc.GraphicsRedraw2();
            activeDoc.WindowRedraw();
        }

        private static void ApplyFarSideViewNoteFormatting(SldWorks.Note note)
        {
            note.SetHeight(FarSideViewTextHeight);
            note.SetTextJustification((int)swTextJustification_e.swTextJustificationCenter);

            var annotation = note.GetAnnotation();
            if (annotation != null)
                ApplyFarSideViewParagraphFormatting(annotation);
        }

        private static void ApplyFarSideViewParagraphFormatting(SldWorks.Annotation annotation)
        {
            try
            {
                var paragraphs = annotation.GetParagraphs() as SldWorks.Paragraphs;
                if (paragraphs == null)
                    return;

                int count = Math.Max(1, paragraphs.Count);

                for (int i = 0; i < count; i++)
                {
                    paragraphs.CurrentParagraph = i;

                    if (!paragraphs.GetFormatting(out _, out double lineSpacing) || lineSpacing <= 0)
                        lineSpacing = 1.0;

                    paragraphs.SetFormatting(FarSideViewParagraphSpacing, lineSpacing);
                    UnderlineFarSideViewJapaneseSegments(paragraphs);
                    paragraphs.UpdateParagraph();
                }
            }
            catch
            {
            }
        }

        private static void UnderlineFarSideViewJapaneseSegments(SldWorks.Paragraphs paragraphs)
        {
            try
            {
                int segmentCount = paragraphs.GetTextSegmentCount();

                for (int segment = 0; segment < segmentCount; segment++)
                {
                    string segmentText = StripSolidWorksTextTags(paragraphs.GetTextSegmentText(segment) ?? "");

                    if (!segmentText.Contains(FarSideViewJapaneseText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (segmentText.Contains(FarSideViewEnglishText, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var textFormat = paragraphs.GetTextSegmentFormat(segment) as SldWorks.TextFormat;
                    if (textFormat == null)
                        continue;

                    textFormat.Underline = true;
                    paragraphs.SetTextSegmentFormat(segment, textFormat);
                }
            }
            catch
            {
            }
        }

        private static void CreateFarSideViewNoteInView(
            SldWorks.ModelDoc2 activeDoc,
            SldWorks.DrawingDoc drawing,
            SldWorks.View targetView,
            double x,
            double y)
        {
            string targetViewName = targetView.Name ?? "";
            if (string.IsNullOrWhiteSpace(targetViewName))
                throw new Exception("Active drawing view has no name.");

            bool activated = drawing.ActivateView(targetViewName);
            if (!activated)
                throw new Exception($"Failed to activate drawing view '{targetViewName}'.");

            activeDoc.ClearSelection2(true);

            var note = activeDoc.InsertNote(FarSideViewExpectedText);
            if (note == null)
                throw new Exception("Failed to create Far Side View note.");

            note.LockPosition = false;
            note.Angle = 0;
            note.Visible = true;
            note.BehindSheet = false;
            note.SetTextPoint(x, y, 0);

            var annotation = note.GetAnnotation();
            if (annotation == null)
                throw new Exception("Created Far Side View note has no annotation.");

            annotation.Visible = 1;
            annotation.SetPosition(x, y, 0);
            ApplyFarSideViewNoteFormatting(note);

            try
            {
                annotation.SetLeader3(
                    (int)swLeaderStyle_e.swNO_LEADER,
                    0,
                    false,
                    false,
                    false,
                    false);
            }
            catch
            {
            }

            activeDoc.ClearSelection2(true);
            activeDoc.EditRebuild3();
            activeDoc.ForceRebuild3(false);
            activeDoc.GraphicsRedraw2();
            activeDoc.WindowRedraw();
        }

        private static SldWorks.View? FindRightmostDrawingView(SldWorks.DrawingDoc drawing)
        {
            var candidates = new List<(SldWorks.View View, double CenterX)>();

            try
            {
                var view = drawing.GetFirstView() as SldWorks.View;

                if (view != null)
                    view = view.GetNextView() as SldWorks.View;

                while (view != null)
                {
                    double[]? outline = ToDoubleArray((object?)view.GetOutline());

                    if (outline is { Length: >= 4 })
                    {
                        double centerX = (Math.Min(outline[0], outline[2]) + Math.Max(outline[0], outline[2])) / 2.0;
                        candidates.Add((view, centerX));
                    }

                    view = view.GetNextView() as SldWorks.View;
                }
            }
            catch
            {
            }

            return candidates
                .OrderByDescending(candidate => candidate.CenterX)
                .Select(candidate => candidate.View)
                .FirstOrDefault();
        }

        private static (double X, double Y) GetFarSideViewNoteCreatePosition(
            SldWorks.DrawingDoc drawing,
            SldWorks.View? targetView)
        {
            var (sheetWidth, sheetHeight) = GetActiveSheetSize(drawing);

            if (targetView != null)
            {
                double[]? outline = ToDoubleArray((object?)targetView.GetOutline());

                if (outline is { Length: >= 4 })
                {
                    double left = Math.Min(outline[0], outline[2]);
                    double right = Math.Max(outline[0], outline[2]);
                    double bottom = Math.Min(outline[1], outline[3]);

                    double x = (left + right) / 2.0;
                    double y = bottom + FarSideViewYOffset;

                    return (
                        ClampToSheet(x, sheetWidth),
                        ClampToSheet(y, sheetHeight));
                }
            }

            return (
                ClampToSheet(sheetWidth * 0.67, sheetWidth),
                ClampToSheet(sheetHeight * 0.25, sheetHeight));
        }

        private static (double Width, double Height) GetActiveSheetSize(SldWorks.DrawingDoc drawing)
        {
            try
            {
                var sheet = drawing.GetCurrentSheet();
                var props = sheet?.GetProperties() as double[];

                if (props != null && props.Length >= 7 && props[5] > 0 && props[6] > 0)
                    return (props[5], props[6]);
            }
            catch
            {
            }

            return (0.420, 0.297);
        }

        private static double ClampToSheet(double value, double sheetSize)
        {
            if (sheetSize <= FarSideViewSheetMargin * 2.0)
                return value;

            return Math.Max(
                FarSideViewSheetMargin,
                Math.Min(sheetSize - FarSideViewSheetMargin, value));
        }

        private static double[]? ToDoubleArray(object? value)
        {
            if (value is double[] doubles)
                return doubles;

            if (value is object[] objects)
            {
                var result = new double[objects.Length];

                for (int i = 0; i < objects.Length; i++)
                    result[i] = Convert.ToDouble(objects[i]);

                return result;
            }

            return null;
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

            ws.Cells[2, 2, Math.Max(rows.Count + 2, 2), 2].AutoFilter = true;

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

            reportRows.AddRange(BuildFarSideViewNoteReportRows(notes));

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

            ws.Cells[2, 2, Math.Max(currentRow - 1, 2), 2].AutoFilter = true;

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
            var signature = workListReader.GetSignatureFromWorkList(workListFile, drawingPartNo);

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

            var drawingDoc = activeDoc as SldWorks.DrawingDoc;
            var farSideViewTarget = drawingDoc != null
                ? GetActiveUserDrawingView(drawingDoc)
                : null;

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

            string farSideViewResult = EnsureFarSideViewNoteForDrawing(activeDoc, solidWorksService, farSideViewTarget);
            AddLog(farSideViewResult);

            activeDoc.GraphicsRedraw2();
            activeDoc.WindowRedraw();

            AddLog("Title block updated successfully.");

            return $"Title block filled successfully for {drawingPartNo}.\n\n{farSideViewResult}";
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

            string expectedSelfPartNo = BuildExpectedSelfPartFromDrawing(drawingPartNo);
            AddLog($"Expected self part from drawing number: {expectedSelfPartNo}");

            var hPanelSelfRows = rows
                .Where(r =>
                    PartNumberHelper.IsSelfPart(r.SubPartNo) &&
                    !string.IsNullOrWhiteSpace(r.Nomenclature) &&
                    r.Nomenclature.Trim().Equals("H PANEL", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var selfRow = hPanelSelfRows.FirstOrDefault(r =>
                NormalizeSelfPartCandidate(r.SubPartNo).Equals(expectedSelfPartNo, StringComparison.OrdinalIgnoreCase));

            if (selfRow == null)
            {
                selfRow = hPanelSelfRows.FirstOrDefault(r =>
                    NormalizeSelfPartCandidate(r.SubPartNo).StartsWith("P", StringComparison.OrdinalIgnoreCase));
            }

            if (selfRow == null)
            {
                selfRow = hPanelSelfRows.FirstOrDefault();
            }

            if (selfRow == null)
            {
                selfRow = rows.FirstOrDefault(r =>
                PartNumberHelper.IsSelfPart(r.SubPartNo) &&
                !string.IsNullOrWhiteSpace(r.Nomenclature) &&
                r.Nomenclature.Trim().Equals("H PANEL", StringComparison.OrdinalIgnoreCase));
            }

            if (selfRow == null)
            {
                // fallback to old logic in case some older project does not have H PANEL filled as expected
                selfRow = rows.FirstOrDefault(r =>
                    PartNumberHelper.IsSelfPart(r.SubPartNo) &&
                    NormalizeSelfPartCandidate(r.SubPartNo).StartsWith("P", StringComparison.OrdinalIgnoreCase));
            }

            if (selfRow == null)
            {
                selfRow = rows.FirstOrDefault(r => PartNumberHelper.IsSelfPart(r.SubPartNo));
            }

            if (selfRow == null)
                throw new Exception("Self part row not found in BOM.");

            AddLog($"BOM self part: {selfRow.SubPartNo}");

            if (!NormalizeSelfPartCandidate(selfRow.SubPartNo).Equals(expectedSelfPartNo, StringComparison.OrdinalIgnoreCase))
            {
                AddLog($"WARNING: BOM self part does not match expected self part from drawing. Expected {expectedSelfPartNo}; BOM selected {selfRow.SubPartNo}.");
            }

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
            selfCallout ??= FindLikelyMistypedSelfPartCallout(
                notes,
                drawingPartNo,
                selfRow.SubPartNo,
                AddLog);

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

        private static NDwgAutoTool.Models.DrawingNoteInfo? FindLikelyMistypedSelfPartCallout(
            IEnumerable<NDwgAutoTool.Models.DrawingNoteInfo> notes,
            string drawingPartNo,
            string bomSelfPartNo,
            Action<string>? log)
        {
            string expectedSelfPart = NormalizeSelfPartCandidate(bomSelfPartNo);
            string expectedFromDrawing = BuildExpectedSelfPartFromDrawing(drawingPartNo);
            var candidates = notes
                .Where(note =>
                    note.NoteObject != null &&
                    IsSelfPartTypoCandidate(note.Text, expectedSelfPart))
                .Select(note => new
                {
                    Note = note,
                    Text = NormalizeSelfPartCandidate(note.Text),
                    Distance = GetSelfPartTypoDistance(note.Text, expectedSelfPart),
                    DistanceToExpectedFromDrawing = GetSelfPartTypoDistance(note.Text, expectedFromDrawing)
                })
                .Where(candidate => candidate.Distance is >= 0 and <= 1)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.DistanceToExpectedFromDrawing)
                .ThenBy(candidate => candidate.Note.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0)
            {
                var seen = notes
                    .Where(note => note.NoteObject != null)
                    .Select(note => NormalizeSelfPartCandidate(note.Text))
                    .Where(text =>
                        !string.IsNullOrWhiteSpace(text) &&
                        Math.Abs(text.Length - expectedSelfPart.Length) <= 2 &&
                        text.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();

                if (seen.Count > 0)
                    log?.Invoke($"Self-part typo scan found no safe match for {expectedSelfPart}. Nearby bare note text seen: {string.Join(", ", seen)}");

                return null;
            }

            var best = candidates[0];

            if (candidates.Count > 1 && candidates[1].Distance == best.Distance)
            {
                log?.Invoke(
                    $"WARNING: Multiple possible mistyped self-part callouts found. Not auto-correcting: {string.Join(", ", candidates.Select(c => c.Note.Text).Take(5))}");
                return null;
            }

            log?.Invoke($"Likely mistyped self-part callout found: {best.Note.Text} (expected {expectedSelfPart}, HasLeader={best.Note.HasLeader}).");
            return best.Note;
        }

        private static bool IsSelfPartTypoCandidate(string text, string expectedSelfPart)
        {
            string normalized = NormalizeSelfPartCandidate(text);

            if (string.IsNullOrWhiteSpace(normalized) || string.IsNullOrWhiteSpace(expectedSelfPart))
                return false;

            if (!normalized.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                return false;

            if (Math.Abs(normalized.Length - expectedSelfPart.Length) > 1)
                return false;

            return normalized.Count(char.IsLetter) >= 4 &&
                   normalized.Count(char.IsDigit) >= 8 &&
                   GetSelfPartTypoDistance(normalized, expectedSelfPart) <= 1;
        }

        private static string NormalizeSelfPartCandidate(string text)
        {
            string normalized = (text ?? "")
                .Trim()
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal)
                .Replace("\n", "", StringComparison.Ordinal)
                .ToUpperInvariant();

            return new string(normalized
                .Where(c => (c >= 'A' && c <= 'Z') || char.IsDigit(c) || c == '-')
                .ToArray());
        }

        private static string BuildExpectedSelfPartFromDrawing(string drawingPartNo)
        {
            string normalizedDrawing = NormalizeSelfPartCandidate(drawingPartNo);

            if (normalizedDrawing.Length <= 1)
                return normalizedDrawing;

            return "P" + normalizedDrawing[1..];
        }

        private static int GetNearestOtherBomSubpartDistance(
            string candidate,
            string expectedSelfPart,
            IEnumerable<string> knownBomSubparts)
        {
            int nearest = int.MaxValue;
            string normalizedCandidate = NormalizeSelfPartCandidate(candidate);

            foreach (string subpart in knownBomSubparts)
            {
                if (string.IsNullOrWhiteSpace(subpart) ||
                    subpart.Equals(expectedSelfPart, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int distance = GetSelfPartEditDistance(normalizedCandidate, subpart);
                if (distance < nearest)
                    nearest = distance;
            }

            return nearest;
        }

        private static int GetSelfPartEditDistance(string candidate, string expected)
        {
            candidate = NormalizeSelfPartCandidate(candidate);
            expected = NormalizeSelfPartCandidate(expected);

            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(expected))
                return int.MaxValue;

            if (Math.Abs(candidate.Length - expected.Length) > 1)
                return int.MaxValue;

            int i = 0;
            int j = 0;
            int edits = 0;

            while (i < candidate.Length && j < expected.Length)
            {
                if (candidate[i] != expected[i])
                {
                    edits++;

                    if (edits > 1)
                        return edits;

                    if (candidate.Length > expected.Length)
                    {
                        i++;
                        continue;
                    }

                    if (candidate.Length < expected.Length)
                    {
                        j++;
                        continue;
                    }
                }

                i++;
                j++;
            }

            if (i < candidate.Length || j < expected.Length)
                edits++;

            return edits;
        }

        private static int GetSelfPartTypoDistance(string candidate, string expected)
        {
            candidate = NormalizeSelfPartCandidate(candidate);
            expected = NormalizeSelfPartCandidate(expected);

            if (candidate.Equals(expected, StringComparison.OrdinalIgnoreCase))
                return 0;

            if (Math.Abs(candidate.Length - expected.Length) > 1)
                return int.MaxValue;

            if (candidate.Length == expected.Length)
            {
                int differences = 0;

                for (int i = 0; i < candidate.Length; i++)
                {
                    if (candidate[i] != expected[i])
                        differences++;
                }

                return differences;
            }

            if (candidate.Length + 1 == expected.Length)
                return CanRemoveOneCharacterToMatch(expected, candidate) ? 1 : int.MaxValue;

            if (expected.Length + 1 == candidate.Length)
                return CanRemoveOneCharacterToMatch(candidate, expected) ? 1 : int.MaxValue;

            return int.MaxValue;
        }

        private static bool CanRemoveOneCharacterToMatch(string longer, string shorter)
        {
            for (int i = 0; i < longer.Length; i++)
            {
                string shortened = longer.Remove(i, 1);

                if (shortened.Equals(shorter, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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






