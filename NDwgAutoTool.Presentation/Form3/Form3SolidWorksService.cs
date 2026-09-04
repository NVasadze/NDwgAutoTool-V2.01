using SldWorks;
using SwConst;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NDwgAutoTool.Form3
{
    public class Form3Row
    {
        public string CharNo { get; set; } = "";
        public string ReferenceLocation { get; set; } = "";
        public string Designator { get; set; } = "";
        public string Requirement { get; set; } = "";
    }

    public class Form3SolidWorksService
    {
        private ModelDoc2? _targetModel;

        [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
        private static extern int CLSIDFromProgID(string progId, out Guid clsid);

        [DllImport("oleaut32.dll")]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        public SldWorks.SldWorks? GetApplication()
        {
            try
            {
                Guid clsid;
                int hr = CLSIDFromProgID("SldWorks.Application", out clsid);
                if (hr != 0)
                    return null;

                object obj;
                hr = GetActiveObject(ref clsid, IntPtr.Zero, out obj);
                if (hr != 0 || obj == null)
                    return null;

                return obj as SldWorks.SldWorks;
            }
            catch
            {
                return null;
            }
        }

        public void SetTargetModel(ModelDoc2? model)
        {
            _targetModel = model;
        }

        public ModelDoc2? GetActiveModel()
        {
            if (_targetModel != null)
                return _targetModel;

            var swApp = GetApplication();
            if (swApp == null)
                return null;

            return swApp.ActiveDoc as ModelDoc2;
        }

        public DrawingDoc? GetActiveDrawing()
        {
            var model = GetActiveModel();
            if (model == null)
                return null;

            if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
                return null;

            return model as DrawingDoc;
        }

        public ModelDoc2? GetReferencedModelFromActiveSheet()
        {
            var drawing = GetActiveDrawing();
            if (drawing == null)
                return null;

            var firstView = drawing.GetFirstView();
            if (firstView == null)
                return null;

            var modelView = firstView.GetNextView();
            while (modelView != null)
            {
                var refModel = modelView.ReferencedDocument as ModelDoc2;
                if (refModel != null)
                    return refModel;

                modelView = modelView.GetNextView();
            }

            return null;
        }

        public string GetCustomProperty(ModelDoc2? model, string propertyName)
        {
            if (model == null || string.IsNullOrWhiteSpace(propertyName))
                return string.Empty;

            try
            {
                var mgr = model.Extension.CustomPropertyManager[""];
                if (mgr == null)
                    return string.Empty;

                string valOut = "";
                string resolvedValOut = "";
                bool wasResolved = false;
                bool linkToProperty = false;

                mgr.Get6(
                    propertyName,
                    false,
                    out valOut,
                    out resolvedValOut,
                    out wasResolved,
                    out linkToProperty);

                if (!string.IsNullOrWhiteSpace(resolvedValOut))
                    return resolvedValOut.Trim();

                if (!string.IsNullOrWhiteSpace(valOut))
                    return valOut.Trim();

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public string GetPartNumber()
        {
            var drawingModel = GetActiveModel();
            var refModel = GetReferencedModelFromActiveSheet();

            string[] candidates =
            {
                "Part Number",
                "PART NUMBER",
                "PART_NO",
                "PARTNO",
                "NUMBER",
                "DWG_NO",
                "DRAWING NUMBER",
                "J_NUMBER"
            };

            foreach (string candidate in candidates)
            {
                string value = GetCustomProperty(drawingModel, candidate);
                if (!string.IsNullOrWhiteSpace(value))
                    return CleanPartNumber(value);
            }

            foreach (string candidate in candidates)
            {
                string value = GetCustomProperty(refModel, candidate);
                if (!string.IsNullOrWhiteSpace(value))
                    return CleanPartNumber(value);
            }

            if (refModel != null)
                return CleanPartNumber(System.IO.Path.GetFileNameWithoutExtension(refModel.GetTitle()));

            return string.Empty;
        }

        public string GetNomenclature()
        {
            var drawingModel = GetActiveModel();
            var refModel = GetReferencedModelFromActiveSheet();

            string[] candidates =
            {
                "Drawing Title",
                "TITLE",
                "J_PART_NAME",
                "Description",
                "DESCRIPTION",
                "NOMENCLATURE"
            };

            foreach (string candidate in candidates)
            {
                string value = GetCustomProperty(drawingModel, candidate);
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            foreach (string candidate in candidates)
            {
                string value = GetCustomProperty(refModel, candidate);
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return "";
        }

        public string GetSheetCode()
        {
            try
            {
                var drawing = GetActiveDrawing();
                if (drawing == null)
                    return "";

                var sheetView = drawing.GetFirstView();
                if (sheetView == null)
                    return "";

                var currentSheet = drawing.GetCurrentSheet();
                var props = currentSheet.GetProperties() as double[];
                if (props == null || props.Length < 7)
                    return "";

                double sheetWidth = props[5];
                double targetX = sheetWidth;
                double targetY = 0.0;

                var notes = sheetView.GetNotes() as object[];
                if (notes == null || notes.Length == 0)
                    return "";

                string bestText = "";
                double bestDist = double.MaxValue;

                foreach (object obj in notes)
                {
                    var swNote = obj as Note;
                    if (swNote == null)
                        continue;

                    string text;
                    double x = 0;
                    double y = 0;

                    try
                    {
                        text = swNote.GetText() ?? "";
                    }
                    catch
                    {
                        continue;
                    }

                    Match m = Regex.Match(text, @"\b\d{6}\b");
                    if (!m.Success)
                        continue;

                    try
                    {
                        var ann = swNote.GetAnnotation();
                        if (ann != null)
                        {
                            var pos = ann.GetPosition() as double[];
                            if (pos != null && pos.Length >= 2)
                            {
                                x = pos[0];
                                y = pos[1];
                            }
                        }
                    }
                    catch
                    {
                    }

                    double dx = targetX - x;
                    double dy = targetY - y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestText = m.Value;
                    }
                }

                return bestText;
            }
            catch
            {
                return "";
            }
        }

        public string GetOutputFileName()
        {
            string partNo = GetPartNumber();
            partNo = SanitizeFileNamePart(partNo);

            if (string.IsNullOrWhiteSpace(partNo))
                partNo = "Form3";

            string sheetCode = GetSheetCode();

            if (!string.IsNullOrWhiteSpace(sheetCode))
                return partNo + "-" + sheetCode + ".xls";

            return partNo + ".xls";
        }

        private string CleanPartNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Trim();

            Match basePartNumber = Regex.Match(
                text,
                @"^[A-Z]{4}\d{6}[A-Z]\d{4}",
                RegexOptions.IgnoreCase);

            if (basePartNumber.Success)
                return basePartNumber.Value.ToUpperInvariant();

            text = Regex.Replace(
                text,
                @"_(?:-)?(?:--|[A-Z]{1,2})$",
                "",
                RegexOptions.IgnoreCase).TrimEnd();

            while (text.EndsWith("-") || text.EndsWith("_") || text.EndsWith(".") || text.EndsWith(" "))
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
                if (text.Length == 0)
                    break;
            }

            return text;
        }

        private string SanitizeFileNamePart(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            string invalidChars = new string(Path.GetInvalidFileNameChars());

            foreach (char c in invalidChars)
                text = text.Replace(c.ToString(), "");

            text = text.Trim();

            while (text.EndsWith("-") || text.EndsWith("_") || text.EndsWith(".") || text.EndsWith(" "))
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
                if (text.Length == 0)
                    break;
            }

            return text;
        }

        public List<NoteInfo> GetCharacteristicNotesInDrawing()
        {
            List<NoteInfo> result = new List<NoteInfo>();

            var drawing = GetActiveDrawing();
            if (drawing == null)
                return result;

            var view = drawing.GetFirstView();
            while (view != null)
            {
                object annObj = view.GetFirstAnnotation3();

                while (annObj != null)
                {
                    var ann = annObj as Annotation;
                    if (ann == null)
                        break;

                    string text = "";
                    double x = 0;
                    double y = 0;

                    try
                    {
                        object specific = ann.GetSpecificAnnotation();
                        var swNote = specific as Note;

                        if (swNote != null)
                            text = swNote.GetText() ?? "";
                        else
                            text = ann.GetName() ?? "";
                    }
                    catch
                    {
                    }

                    int charNo = ExtractCharacteristicNumberFromNote(text);

                    if (charNo >= 0)
                    {
                        try
                        {
                            var pos = ann.GetPosition() as double[];
                            if (pos != null && pos.Length >= 2)
                            {
                                x = pos[0];
                                y = pos[1];
                            }
                        }
                        catch
                        {
                        }

                        result.Add(new NoteInfo
                        {
                            Text = text,
                            Name = ann.GetName() ?? "",
                            ViewName = view.Name,
                            X = x,
                            Y = y,
                            CharacteristicNumber = charNo
                        });
                    }

                    try
                    {
                        annObj = ann.GetNext3();
                    }
                    catch
                    {
                        break;
                    }
                }

                view = view.GetNextView();
            }

            return result;
        }

        public int ExtractCharacteristicNumberFromNote(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return -1;

            Match m = Regex.Match(text, @"\{(\d+)\}");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int result))
                return result;

            return -1;
        }

        public List<DimensionInfo> GetDimensionsInDrawing()
        {
            List<DimensionInfo> result = new List<DimensionInfo>();

            var drawing = GetActiveDrawing();
            if (drawing == null)
                return result;

            var sheetView = drawing.GetFirstView();
            if (sheetView == null)
                return result;

            var view = sheetView.GetNextView();

            while (view != null)
            {
                var dims = view.GetDisplayDimensions() as object[];

                if (dims != null)
                {
                    foreach (object obj in dims)
                    {
                        var dispDim = obj as DisplayDimension;
                        if (dispDim == null)
                            continue;

                        string fullName = "";
                        double x = 0;
                        double y = 0;
                        double nominalMm = 0;
                        bool isRef = false;
                        string displayText = "";

                        try
                        {
                            var ann = dispDim.GetAnnotation();
                            if (ann != null)
                            {
                                var pos = ann.GetPosition() as double[];
                                if (pos != null && pos.Length >= 2)
                                {
                                    x = pos[0];
                                    y = pos[1];
                                }
                            }
                        }
                        catch
                        {
                        }

                        Dimension? d = null;

                        try
                        {
                            d = dispDim.GetDimension2(0) as Dimension;
                            if (d != null)
                                fullName = d.FullName ?? "";
                        }
                        catch
                        {
                        }

                        try
                        {
                            displayText = dispDim.GetText(5) ?? "";
                        }
                        catch
                        {
                            displayText = "";
                        }

                        isRef = IsReferenceDimensionText(displayText);

                        // Primary: use actual dimension system value (meters -> mm)
                        try
                        {
                            if (d != null)
                            {
                                nominalMm = Math.Abs(d.SystemValue) * 1000.0;
                            }
                        }
                        catch
                        {
                            nominalMm = 0;
                        }

                        // Fallback: parse from display text if system value failed
                        if (nominalMm == 0)
                        {
                            nominalMm = ExtractNominalMmFromDisplayText(displayText);
                        }

                        string location = GetGridLocation(x, y);

                        result.Add(new DimensionInfo
                        {
                            ViewName = view.Name,
                            FullName = fullName,
                            X = x,
                            Y = y,
                            NominalMm = nominalMm,
                            IsRef = isRef,
                            DisplayText = displayText,
                            Location = location
                        });
                    }
                }

                view = view.GetNextView();
            }

            return result;
        }

        public DimensionInfo? FindNearestDimension(NoteInfo note, List<DimensionInfo> dims)
        {
            DimensionInfo? best = null;
            double bestDist = double.MaxValue;

            foreach (DimensionInfo d in dims)
            {
                if (!string.Equals(d.ViewName, note.ViewName, StringComparison.OrdinalIgnoreCase))
                    continue;

                double dist = GetDistance(note.X, note.Y, d.X, d.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = d;
                }
            }

            return best;
        }

        private double GetDistance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool IsReferenceDimensionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleaned = text.ToUpper().Replace(" ", "");
            return cleaned.Contains("REF");
        }

        public double ExtractNominalMmFromDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            string cleaned = text.ToUpper().Replace("REF", "").Trim();

            Match m = Regex.Match(cleaned, @"[-+]?\d+(\.\d+)?");
            if (m.Success && double.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return value;

            return 0;
        }

        public double GetGeneralToleranceMm(double nominalMm)
        {
            if (nominalMm >= 1 && nominalMm <= 4) return 0.1;
            if (nominalMm > 4 && nominalMm <= 16) return 0.2;
            if (nominalMm > 16 && nominalMm <= 63) return 0.3;
            if (nominalMm > 63 && nominalMm <= 250) return 0.5;
            if (nominalMm > 250 && nominalMm <= 1000) return 0.8;
            if (nominalMm > 1000 && nominalMm <= 2000) return 1.2;
            if (nominalMm > 2000 && nominalMm <= 4000) return 1.5;

            return 0;
        }

        public string FormatRequirement(string displayText, double nominalMm)
        {
            double tol = GetGeneralToleranceMm(nominalMm);

            if (nominalMm == 0)
                return "";

            string text = (displayText ?? "").Trim().ToUpperInvariant();

            // Remove REF if it somehow appears
            text = text.Replace("REF", "").Trim();

            // Take only the first numeric value from the displayed text
            Match m = Regex.Match(text, @"[-+]?\d+(\.\d+)?");
            if (!m.Success)
                return "";

            string nominalText = m.Value;

            // If drawing shows whole number like 901, write 901.0 in Form 3
            if (!nominalText.Contains("."))
                nominalText += ".0";

            return nominalText
                   + " ±"
                   + tol.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public string GetGridLocation(double x, double y)
        {
            try
            {
                var drawing = GetActiveDrawing();
                if (drawing == null)
                    return "";

                var sheet = drawing.GetCurrentSheet();
                if (sheet == null)
                    return "";

                var props = sheet.GetProperties() as double[];
                if (props == null || props.Length < 7)
                    return "";

                int columns = (int)props[1];
                int rows = GetRowCountFromSheet(sheet);

                double sheetWidth = props[5];
                double sheetHeight = props[6];

                double leftMargin = 0.01;
                double rightMargin = 0.01;
                double topMargin = 0.01;
                double bottomMargin = 0.01;

                double usableWidth = sheetWidth - leftMargin - rightMargin;
                double usableHeight = sheetHeight - topMargin - bottomMargin;

                double xInGrid = x - leftMargin;
                double yInGrid = y - bottomMargin;

                if (xInGrid < 0) xInGrid = 0;
                if (xInGrid > usableWidth) xInGrid = usableWidth;
                if (yInGrid < 0) yInGrid = 0;
                if (yInGrid > usableHeight) yInGrid = usableHeight;

                const double fixedColWidth = 0.0562; // 56.2 mm in meters
                double rowHeight = usableHeight / rows;
                const double epsilon = 0.000001;

                double xFromLeft = Math.Max(epsilon, Math.Min(usableWidth, xInGrid));
                double yFromTop = Math.Max(epsilon, Math.Min(usableHeight, usableHeight - yInGrid));

                int col = (int)Math.Ceiling((xFromLeft - epsilon) / fixedColWidth);
                int row = (int)Math.Ceiling((yFromTop - epsilon) / rowHeight);


                if (col < 1) col = 1;
                if (col > columns) col = columns;
                if (row < 1) row = 1;
                if (row > rows) row = rows;

                char rowLetter = (char)('A' + row - 1);
                return rowLetter + col.ToString();
            }
            catch
            {
                return "";
            }
        }

        public int GetRowCountFromSheet(Sheet sheet)
        {
            var props = sheet.GetProperties() as double[];
            if (props == null || props.Length < 7)
                return 8;

            double height = props[6];

            if (height < 0.25) return 6;
            if (height < 0.35) return 7;
            if (height < 0.5) return 8;
            if (height < 0.75) return 10;
            return 12;
        }

        public List<Form3Row> BuildRowsForExport(List<string>? additionalNoteNumbers = null)
        {
            List<Form3Row> result = new List<Form3Row>();

            var nearbyBlockNames = GetNearbyBlockNamesFromDrawing();

            if (nearbyBlockNames.Count == 0)
            {
                Log("Form3 nearby block scan: no nearby blocks found.");
            }
            else
            {
                Log("Form3 nearby block scan found:");
                foreach (string name in nearbyBlockNames)
                    Log("Form3 nearby block -> " + name);
            }

            result.AddRange(BuildNoteRowsFromActualNoteBlock());
            result.AddRange(BuildAdditionalRowsFromNearbyBlocks(nearbyBlockNames));
            result.AddRange(BuildDimensionRowsForExport());

            return result;
        }



        private List<Form3Row> BuildAdditionalManualNoteRows(List<string>? additionalNoteNumbers)
        {
            List<Form3Row> result = new List<Form3Row>();

            if (additionalNoteNumbers == null || additionalNoteNumbers.Count == 0)
                return result;

            string pageCode = GetCurrentPageCode();
            string fixedLocation = GetFixedNoteReferenceLocation();

            foreach (string number in additionalNoteNumbers)
            {
                string clean = (number ?? "").Trim();

                if (string.IsNullOrWhiteSpace(clean))
                    continue;

                result.Add(new Form3Row
                {
                    CharNo = pageCode + "-3",
                    ReferenceLocation = fixedLocation,
                    Designator = "注記",
                    Requirement = clean
                });
            }

            return result;
        }

        public string GetCurrentPageCode()
        {
            var drawing = GetActiveDrawing();
            if (drawing == null)
                return "P01";

            var sheetNamesObj = drawing.GetSheetNames() as object[];
            if (sheetNamesObj == null || sheetNamesObj.Length == 0)
                return "P01";

            string currentSheet = drawing.GetCurrentSheet().GetName();

            for (int i = 0; i < sheetNamesObj.Length; i++)
            {
                string? name = sheetNamesObj[i]?.ToString();
                if (string.Equals(name, currentSheet, StringComparison.OrdinalIgnoreCase))
                    return "P" + (i + 1).ToString("00");
            }

            return "P01";
        }

        private List<Form3Row> BuildNoteRowsFromActualNoteBlock()
        {
            List<Form3Row> result = new List<Form3Row>();

            string? blockName = GetActualNoteBlockNameFromDrawing();
            if (string.IsNullOrWhiteSpace(blockName))
                return result;

            List<string> codes = ParseCodesInOrderFromBlockName(blockName);

            string pageCode = GetCurrentPageCode();
            string fixedLocation = GetFixedNoteReferenceLocation();

            foreach (string code in codes)
            {
                result.Add(new Form3Row
                {
                    CharNo = pageCode + "-3",
                    ReferenceLocation = fixedLocation,
                    Designator = "注記",
                    Requirement = "1-" + code
                });
            }

            return result;
        }

        private string? GetActualNoteBlockNameFromDrawing()
        {
            var model = GetActiveModel();
            if (model == null)
                return null;

            var pickPoints = new List<(double X, double Y)>
            {
                (0.0598867427928131, 0.0224488902434242),
                (0.0600, 0.0225),
                (0.0550, 0.0200),
                (0.0700, 0.0250)
            };

            foreach (var pt in pickPoints)
            {
                model.ClearSelection2(true);

                bool selected = model.Extension.SelectByID2(
                    "",
                    "SUBSKETCHINST",
                    pt.X,
                    pt.Y,
                    0.0,
                    false,
                    0,
                    null,
                    0);

                if (!selected)
                    continue;

                var selMgr = model.SelectionManager;
                if (selMgr == null)
                {
                    model.ClearSelection2(true);
                    continue;
                }

                object obj = selMgr.GetSelectedObject6(1, -1);
                if (obj == null)
                {
                    model.ClearSelection2(true);
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

                model.ClearSelection2(true);

                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }

            model.ClearSelection2(true);
            return null;
        }

        private List<string> ParseCodesInOrderFromBlockName(string blockName)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrWhiteSpace(blockName))
                return result;

            MatchCollection matches = Regex.Matches(
                blockName.ToUpperInvariant(),
                @"(G\d+|T\d+|B\d+|SE\d+)");

            foreach (Match m in matches)
            {
                string code = m.Value.Trim();
                if (!string.IsNullOrWhiteSpace(code))
                    result.Add(code);
            }

            return result;
        }

        private List<Form3Row> BuildDimensionRowsForExport()
        {
            List<Form3Row> result = new List<Form3Row>();

            List<NoteInfo> characteristicNotes = GetCharacteristicNotesInDrawing();
            List<DimensionInfo> dims = GetDimensionsInDrawing();

            string pageCode = GetCurrentPageCode();

            foreach (NoteInfo characteristic in characteristicNotes.OrderBy(n => n.CharacteristicNumber))
            {
                if (characteristic.CharacteristicNumber == 3)
                    continue;

                DimensionInfo? nearestDim = FindNearestDimension(characteristic, dims);
                if (nearestDim != null && !nearestDim.IsRef)
                {
                    string requirement = FormatRequirement(nearestDim.DisplayText, nearestDim.NominalMm);

                    double locationX = characteristic.X - 0.0200;
                    double locationY = characteristic.Y;

                    string characteristicLocation = GetGridLocation(locationX, locationY);

                    result.Add(new Form3Row
                    {
                        CharNo = pageCode + "-" + characteristic.CharacteristicNumber.ToString(),
                        ReferenceLocation = characteristicLocation,
                        Designator = "寸法",
                        Requirement = requirement
                    });
                }
            }

            return result;
        }

        private string GetFixedNoteReferenceLocation()
        {
            try
            {
                DrawingDoc? drawing = GetActiveDrawing();
                if (drawing == null)
                    return "J1";

                Sheet? sheet = drawing.GetCurrentSheet();
                if (sheet == null)
                    return "J1";

                int rows = GetRowCountFromSheet(sheet);
                if (rows < 1)
                    return "J1";

                char lastRowLetter = (char)('A' + rows - 1);
                return lastRowLetter + "1";
            }
            catch
            {
                return "J1";
            }
        }

        public int GetRealDrawingViewCount()
        {
            var drawing = GetActiveDrawing();
            if (drawing == null)
                return 0;

            int count = 0;

            var sheetView = drawing.GetFirstView();
            if (sheetView == null)
                return 0;

            var view = sheetView.GetNextView(); // skip sheet itself

            while (view != null)
            {
                bool hasDimensions = false;
                bool hasAnnotations = false;

                try
                {
                    var dims = view.GetDisplayDimensions() as object[];
                    hasDimensions = dims != null && dims.Length > 0;
                }
                catch
                {
                }

                try
                {
                    var ann = view.GetFirstAnnotation3();
                    hasAnnotations = ann != null;
                }
                catch
                {
                }

                if (hasDimensions || hasAnnotations)
                    count++;

                view = view.GetNextView();
            }

            return count;
        }

        private readonly Action<string>? _log;

        public Form3SolidWorksService(Action<string>? log = null)
        {
            _log = log;
        }

        private void Log(string message)
        {
            _log?.Invoke(message);
        }

        private List<string> GetNearbyBlockNamesFromDrawing()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var model = GetActiveModel();
            if (model == null)
                return result.OrderBy(x => x).ToList();

            var pickPoints = new List<(double X, double Y)>
    {
        (0.0598867427928131, 0.0224488902434242),
        (0.0600, 0.0225),
        (0.0550, 0.0200),
        (0.0700, 0.0250),

        (0.0400, 0.0180),
        (0.0450, 0.0180),
        (0.0500, 0.0180),
        (0.0550, 0.0180),
        (0.0600, 0.0180),
        (0.0650, 0.0180),
        (0.0700, 0.0180),
        (0.0750, 0.0180),

        (0.0400, 0.0225),
        (0.0450, 0.0225),
        (0.0500, 0.0225),
        (0.0550, 0.0225),
        (0.0600, 0.0225),
        (0.0650, 0.0225),
        (0.0700, 0.0225),
        (0.0750, 0.0225),

        (0.0400, 0.0270),
        (0.0450, 0.0270),
        (0.0500, 0.0270),
        (0.0550, 0.0270),
        (0.0600, 0.0270),
        (0.0650, 0.0270),
        (0.0700, 0.0270),
        (0.0750, 0.0270),

        (0.0400, 0.0315),
        (0.0450, 0.0315),
        (0.0500, 0.0315),
        (0.0550, 0.0315),
        (0.0600, 0.0315),
        (0.0650, 0.0315),
        (0.0700, 0.0315),
        (0.0750, 0.0315)
    };

            foreach (var pt in pickPoints)
            {
                model.ClearSelection2(true);

                bool selected = model.Extension.SelectByID2(
                    "",
                    "SUBSKETCHINST",
                    pt.X,
                    pt.Y,
                    0.0,
                    false,
                    0,
                    null,
                    0);

                if (!selected)
                    continue;

                var selMgr = model.SelectionManager;
                if (selMgr == null)
                {
                    model.ClearSelection2(true);
                    continue;
                }

                object obj = selMgr.GetSelectedObject6(1, -1);
                if (obj == null)
                {
                    model.ClearSelection2(true);
                    continue;
                }

                string name = "";

                try
                {
                    dynamic inst = obj;

                    // Try definition/file path first
                    try
                    {
                        dynamic def = inst.GetDefinition();
                        if (def != null)
                        {
                            try
                            {
                                string fileName = System.IO.Path.GetFileNameWithoutExtension((string)(def.FileName ?? ""));
                                if (!string.IsNullOrWhiteSpace(fileName))
                                    name = fileName;
                            }
                            catch
                            {
                            }

                            if (string.IsNullOrWhiteSpace(name))
                            {
                                try
                                {
                                    name = def.Name ?? "";
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    catch
                    {
                    }

                    // Fallback to instance name
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        try
                        {
                            name = inst.Name ?? "";
                        }
                        catch
                        {
                            try
                            {
                                name = inst.GetName() ?? "";
                            }
                            catch
                            {
                                name = "";
                            }
                        }
                    }
                }
                catch
                {
                    name = "";
                }

                model.ClearSelection2(true);

                if (!string.IsNullOrWhiteSpace(name))
                    result.Add(name.Trim());
            }

            model.ClearSelection2(true);
            return result.OrderBy(x => x).ToList();
        }
        private List<Form3Row> BuildAdditionalRowsFromNearbyBlocks(List<string> nearbyBlockNames)
        {
            List<Form3Row> result = new List<Form3Row>();

            string pageCode = GetCurrentPageCode();
            string fixedLocation = GetFixedNoteReferenceLocation();

            bool add2 = false;
            bool add3 = false;

            foreach (string rawName in nearbyBlockNames)
            {
                string name = (rawName ?? "").Trim().ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (name.StartsWith("2 & 3", StringComparison.OrdinalIgnoreCase))
                {
                    add2 = true;
                    add3 = true;
                    continue;
                }

                if (name.StartsWith("2", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("2 & 3", StringComparison.OrdinalIgnoreCase))
                {
                    add2 = true;
                }
            }

            if (add2)
            {
                result.Add(new Form3Row
                {
                    CharNo = pageCode + "-3",
                    ReferenceLocation = fixedLocation,
                    Designator = "注記",
                    Requirement = "2"
                });
            }

            if (add3)
            {
                result.Add(new Form3Row
                {
                    CharNo = pageCode + "-3",
                    ReferenceLocation = fixedLocation,
                    Designator = "注記",
                    Requirement = "3"
                });
            }

            return result;
        }

    }
}
