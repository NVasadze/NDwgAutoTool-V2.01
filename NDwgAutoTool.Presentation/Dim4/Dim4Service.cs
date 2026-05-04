using SldWorks;
using SwConst;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NDwgAutoTool.Dim4
{
    public class Dim4Service
    {
        private const double CharacteristicBlockScale = 0.1;
        private readonly Action<string>? _log;

        [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
        private static extern int CLSIDFromProgID(string progId, out Guid clsid);

        [DllImport("oleaut32.dll")]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        public Dim4Service(Action<string>? log = null)
        {
            _log = log;
        }

        private void Log(string message)
        {
            _log?.Invoke(message);
        }

        private class SelectedTemplateInfo
        {
            public string ViewName { get; set; } = "";
            public string Text { get; set; } = "";
            public int CharacteristicNumber { get; set; }
            public Annotation? Annotation { get; set; }
            public Note? NoteObject { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }

        private class NoteObjectInfo
        {
            public string Text { get; set; } = "";
            public string ViewName { get; set; } = "";
            public double X { get; set; }
            public double Y { get; set; }
            public Note? NoteObject { get; set; }
            public Annotation? AnnotationObject { get; set; }
        }

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

        public ModelDoc2? GetActiveModel()
        {
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

        public List<Dim4NoteInfo> GetCharacteristicNotesInDrawing()
        {
            List<Dim4NoteInfo> result = new List<Dim4NoteInfo>();

            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return result;

            View? view = drawing.GetFirstView();
            while (view != null)
            {
                object annObj = view.GetFirstAnnotation3();

                while (annObj != null)
                {
                    Annotation? ann = annObj as Annotation;
                    if (ann == null)
                        break;

                    string text = "";
                    double x = 0;
                    double y = 0;

                    try
                    {
                        object specific = ann.GetSpecificAnnotation();
                        Note? swNote = specific as Note;

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
                            double[]? pos = ann.GetPosition() as double[];
                            if (pos != null && pos.Length >= 2)
                            {
                                x = pos[0];
                                y = pos[1];
                            }
                        }
                        catch
                        {
                        }

                        result.Add(new Dim4NoteInfo
                        {
                            Text = text,
                            Name = ann.GetName() ?? "",
                            ViewName = view.Name ?? "",
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

        public List<Dim4DimensionInfo> GetDimensionsInDrawing()
        {
            List<Dim4DimensionInfo> result = new List<Dim4DimensionInfo>();

            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return result;

            View? sheetView = drawing.GetFirstView();
            if (sheetView == null)
                return result;

            View? view = sheetView.GetNextView();

            while (view != null)
            {
                object[]? dims = view.GetDisplayDimensions() as object[];

                if (dims != null)
                {
                    foreach (object obj in dims)
                    {
                        DisplayDimension? dispDim = obj as DisplayDimension;
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
                            try
                            {
                                bool gotTextPoint = TryGetDisplayDimensionTextPoint(dispDim, out x, out y);

                                if (!gotTextPoint)
                                {
                                    Annotation? ann = dispDim.GetAnnotation();
                                    if (ann != null)
                                    {
                                        double[]? pos = ann.GetPosition() as double[];
                                        if (pos != null && pos.Length >= 2)
                                        {
                                            x = pos[0];
                                            y = pos[1];
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                Annotation? ann = dispDim.GetAnnotation();
                                if (ann != null)
                                {
                                    double[]? pos = ann.GetPosition() as double[];
                                    if (pos != null && pos.Length >= 2)
                                    {
                                        x = pos[0];
                                        y = pos[1];
                                    }
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
                            displayText = GetFullDisplayDimensionText(dispDim);
                        }
                        catch
                        {
                            displayText = "";
                        }

                        isRef = IsReferenceDimensionText(displayText);

                        try
                        {
                            if (d != null)
                                nominalMm = Math.Abs(d.SystemValue) * 1000.0;
                        }
                        catch
                        {
                            nominalMm = 0;
                        }

                        if (nominalMm == 0)
                            nominalMm = ExtractNominalMmFromDisplayText(displayText);

                        result.Add(new Dim4DimensionInfo
                        {
                            ViewName = view.Name ?? "",
                            FullName = fullName,
                            X = x,
                            Y = y,
                            NominalMm = nominalMm,
                            IsRef = isRef,
                            DisplayText = displayText
                        });
                    }
                }

                view = view.GetNextView();
            }

            return result;
        }

        private string GetFullDisplayDimensionText(DisplayDimension dispDim)
        {
            List<string> parts = new List<string>();

            for (int i = 0; i <= 10; i++)
            {
                try
                {
                    string part = dispDim.GetText(i) ?? "";

                    if (!string.IsNullOrWhiteSpace(part))
                        parts.Add(part.Trim());
                }
                catch
                {
                }
            }

            string combined = string.Join("", parts)
                .Replace(" ", "")
                .Trim();

            return combined;
        }

        private bool TryGetDisplayDimensionTextPoint(DisplayDimension dispDim, out double x, out double y)
        {
            x = 0;
            y = 0;

            try
            {
                dynamic d = dispDim;

                object pointObj = d.GetTextPoint2();
                double[]? point = ToDoubleArray(pointObj);

                if (point != null && point.Length >= 2)
                {
                    x = point[0];
                    y = point[1];
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public bool IsReferenceDimensionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleaned = text.ToUpperInvariant().Replace(" ", "");
            return cleaned.Contains("REF");
        }

        public double ExtractNominalMmFromDisplayText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            string cleaned = text.ToUpperInvariant().Replace("REF", "").Trim();

            Match m = Regex.Match(cleaned, @"[-+]?\d+(\.\d+)?");
            if (m.Success && double.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                return value;

            return 0;
        }

        public string ProcessCharacteristic4(bool reverse)
        {
            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return "No active drawing.";

            ModelDoc2? model = GetActiveModel();
            if (model == null)
                return "No active drawing model.";

            List<Dim4NoteInfo> notes = GetCharacteristicNotesInDrawing();
            Dim4NoteInfo? char4 = notes.FirstOrDefault(n => n.CharacteristicNumber == 4);

            if (char4 == null)
                return "Characteristic {4} not found.";

            Log($"Dim4: found {{4}} at X={char4.X:F4}, Y={char4.Y:F4}, current view='{char4.ViewName}'");

            List<Dim4DimensionInfo> dimsBefore = GetDimensionsInDrawing();
            string? targetViewName = FindBestDim4TargetView(char4, dimsBefore);

            if (string.IsNullOrWhiteSpace(targetViewName))
                return "Could not find a valid target view with exactly 2 dimensions.";

            Log($"Dim4: chosen target view -> {targetViewName}");

            bool cleared = ClearRefFromDimensionsInView(targetViewName);

            try { model.GraphicsRedraw2(); } catch { }

            List<Dim4DimensionInfo> dimsAfterClear = GetDimensionsInDrawing();
            List<Dim4DimensionInfo> targetViewDims = dimsAfterClear
                .Where(d => string.Equals(d.ViewName, targetViewName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targetViewDims.Count != 2)
                return $"Expected exactly 2 dimensions in target view '{targetViewName}', but found {targetViewDims.Count}.";

            Dim4DimensionInfo smaller = targetViewDims[0].NominalMm <= targetViewDims[1].NominalMm ? targetViewDims[0] : targetViewDims[1];
            Dim4DimensionInfo larger = targetViewDims[0].NominalMm > targetViewDims[1].NominalMm ? targetViewDims[0] : targetViewDims[1];

            Log($"Dim4: smaller={smaller.NominalMm}, larger={larger.NominalMm}");

            Dim4DimensionInfo targetForChar4 = reverse ? larger : smaller;
            Dim4DimensionInfo targetForRef = reverse ? smaller : larger;

            bool refSet = SetDimensionTextToRef(targetForRef);

            try { model.GraphicsRedraw2(); } catch { }

            List<Dim4DimensionInfo> dimsAfterRef = GetDimensionsInDrawing();
            Dim4DimensionInfo? finalTargetForChar4 = dimsAfterRef.FirstOrDefault(d =>
                string.Equals(d.FullName, targetForChar4.FullName, StringComparison.OrdinalIgnoreCase));

            if (finalTargetForChar4 == null)
                return "REF was updated, but target dimension could not be found afterward.";

            bool moved = MoveCharacteristic4ToDimensionTopLeft(char4, finalTargetForChar4, targetViewName);

            try { model.GraphicsRedraw2(); } catch { }

            if (moved && refSet)
            {
                if (reverse)
                    return "Done. {4} attached to larger dimension, smaller dimension marked REF.";
                else
                    return "Done. {4} attached to smaller dimension, larger dimension marked REF.";
            }

            if (!cleared && !moved && !refSet)
                return "Failed to clear old REF, move {4}, and set new REF.";

            if (!moved && !refSet)
                return "Old REF may be cleared, but moving {4} and setting REF failed.";

            if (!moved)
                return "REF updated, but moving {4} failed.";

            if (!refSet)
                return "Moved {4}, but setting REF failed.";

            return "Done with warnings.";
        }

        private string? FindBestDim4TargetView(Dim4NoteInfo char4, List<Dim4DimensionInfo> allDims)
        {
            var candidates = allDims
                .GroupBy(d => d.ViewName)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() == 2)
                .Select(g => new
                {
                    ViewName = g.Key,
                    Dimensions = g.ToList(),
                    CenterX = g.Average(x => x.X),
                    CenterY = g.Average(x => x.Y)
                })
                .ToList();

            Log($"Dim4: candidate views with exactly 2 dimensions -> {candidates.Count}");

            foreach (var c in candidates)
            {
                Log($"Dim4: candidate view '{c.ViewName}' center=({c.CenterX:F4},{c.CenterY:F4})");
            }

            if (candidates.Count == 0)
                return null;

            if (candidates.Count == 1)
                return candidates[0].ViewName;

            return candidates
                .OrderBy(c => Distance(char4.X, char4.Y, c.CenterX, c.CenterY))
                .First()
                .ViewName;
        }

        private double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private bool ClearRefFromDimensionsInView(string viewName)
        {
            bool anyProcessed = false;

            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return false;

            View? sheetView = drawing.GetFirstView();
            if (sheetView == null)
                return false;

            View? view = sheetView.GetNextView();

            while (view != null)
            {
                if (!string.Equals(view.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView();
                    continue;
                }

                object[]? dims = view.GetDisplayDimensions() as object[];
                if (dims == null)
                    return false;

                foreach (object obj in dims)
                {
                    DisplayDimension? dispDim = obj as DisplayDimension;
                    if (dispDim == null)
                        continue;

                    try
                    {
                        string current = dispDim.GetText(5) ?? "";
                        string cleaned = RemoveRefSuffix(current);

                        if (!string.Equals(current, cleaned, StringComparison.Ordinal))
                            dispDim.SetText(5, cleaned);

                        anyProcessed = true;
                    }
                    catch
                    {
                    }
                }

                return anyProcessed;
            }

            return false;
        }

        private string RemoveRefSuffix(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            return Regex.Replace(text, @"\s+REF\s*$", "", RegexOptions.IgnoreCase).TrimEnd();
        }

        private bool MoveCharacteristic4ToDimensionTopLeft(Dim4NoteInfo char4, Dim4DimensionInfo targetDim, string targetViewName)
        {
            ModelDoc2? model = GetActiveModel();
            if (model == null)
                return false;

            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return false;

            View? targetView = FindViewByName(drawing, targetViewName);
            if (targetView == null)
                return false;

            Annotation? char4Annotation = FindCharacteristic4AnnotationAnywhere(drawing);
            if (char4Annotation == null)
                return false;

            try
            {
                model.ClearSelection2(true);

                bool viewSelected = model.Extension.SelectByID2(
                    targetViewName,
                    "DRAWINGVIEW",
                    0, 0, 0,
                    false,
                    0,
                    null,
                    0);

                Log($"Dim4: target view '{targetViewName}' selected before moving {{4}} -> {viewSelected}");

                string dimText = targetDim.DisplayText ?? "";
                string cleanText = RemoveRefSuffix(dimText).Trim();

                double xOffset = 0.0010 + (cleanText.Length * 0.0007);
                double yOffset = 0.0065;

                double newX = targetDim.X - xOffset;
                double newY = targetDim.Y + yOffset;

                char4Annotation.SetPosition(newX, newY, 0);

                try
                {
                    char4Annotation.SetLeader3(
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

                model.ClearSelection2(true);
                model.GraphicsRedraw2();
                model.WindowRedraw();

                Log($"Dim4: {{4}} moved to X={newX:F4}, Y={newY:F4} in target view '{targetViewName}'");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Annotation? FindCharacteristic4AnnotationAnywhere(DrawingDoc drawing)
        {
            View? view = drawing.GetFirstView();

            while (view != null)
            {
                object annObj = view.GetFirstAnnotation3();

                while (annObj != null)
                {
                    Annotation? ann = annObj as Annotation;
                    if (ann == null)
                        break;

                    string text = "";

                    try
                    {
                        object specific = ann.GetSpecificAnnotation();
                        Note? swNote = specific as Note;

                        if (swNote != null)
                            text = swNote.GetText() ?? "";
                        else
                            text = ann.GetName() ?? "";
                    }
                    catch
                    {
                    }

                    if (ExtractCharacteristicNumberFromNote(text) == 4)
                        return ann;

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

            return null;
        }

        private View? FindViewByName(DrawingDoc drawing, string viewName)
        {
            View? view = drawing.GetFirstView();

            while (view != null)
            {
                if (string.Equals(view.Name, viewName, StringComparison.OrdinalIgnoreCase))
                    return view;

                view = view.GetNextView();
            }

            return null;
        }

        private bool SetDimensionTextToRef(Dim4DimensionInfo targetDim)
        {
            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return false;

            View? sheetView = drawing.GetFirstView();
            if (sheetView == null)
                return false;

            View? view = sheetView.GetNextView();

            while (view != null)
            {
                if (!string.Equals(view.Name, targetDim.ViewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView();
                    continue;
                }

                object[]? dims = view.GetDisplayDimensions() as object[];
                if (dims == null)
                {
                    view = view.GetNextView();
                    continue;
                }

                foreach (object obj in dims)
                {
                    DisplayDimension? dispDim = obj as DisplayDimension;
                    if (dispDim == null)
                        continue;

                    string fullName = "";
                    try
                    {
                        Dimension? d = dispDim.GetDimension2(0) as Dimension;
                        if (d != null)
                            fullName = d.FullName ?? "";
                    }
                    catch
                    {
                    }

                    if (!string.Equals(fullName, targetDim.FullName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        string current = dispDim.GetText(5) ?? "";
                        string baseText = RemoveRefSuffix(current).Trim();
                        dispDim.SetText(5, baseText + " REF");
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }

                view = view.GetNextView();
            }

            return false;
        }

        private string NormalizeCharacteristicText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Trim();

            Match m = Regex.Match(text, @"\{(\d+)\}");
            if (m.Success)
                return m.Groups[1].Value;

            m = Regex.Match(text, @"^\d+$");
            if (m.Success)
                return m.Value;

            return text;
        }

        private int ExtractPlainOrBracketedCharacteristicNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return -1;

            text = text.Trim();

            Match m = Regex.Match(text, @"\{(\d+)\}");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int boxed))
                return boxed;

            if (int.TryParse(text, out int plain))
                return plain;

            return -1;
        }

        private SelectedTemplateInfo? GetSelectedTemplateAndView(ModelDoc2 model)
        {
            try
            {
                var selMgr = model.SelectionManager as SelectionMgr;
                if (selMgr == null)
                    return null;

                int count = selMgr.GetSelectedObjectCount2(-1);
                if (count < 2)
                    return null;

                View? selectedView = null;
                Annotation? selectedAnnotation = null;
                Note? selectedNote = null;
                string text = "";
                double x = 0;
                double y = 0;

                for (int i = 1; i <= count; i++)
                {
                    int selType = selMgr.GetSelectedObjectType3(i, -1);

                    if (selType == (int)swSelectType_e.swSelDRAWINGVIEWS)
                    {
                        selectedView = selMgr.GetSelectedObject6(i, -1) as View;
                        continue;
                    }

                    object obj = selMgr.GetSelectedObject6(i, -1);
                    if (obj == null)
                        continue;

                    Annotation? ann = null;

                    if (obj is Annotation directAnn)
                    {
                        ann = directAnn;
                    }
                    else if (obj is Note noteObj)
                    {
                        selectedNote = noteObj;
                        try { ann = noteObj.GetAnnotation(); } catch { }
                    }

                    if (ann == null)
                        continue;

                    try
                    {
                        if (ann.GetType() != (int)swAnnotationType_e.swNote)
                            continue;
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        object specific = ann.GetSpecificAnnotation();
                        Note? swNote = specific as Note;
                        if (swNote == null)
                            continue;

                        string candidateText = swNote.GetText() ?? "";
                        int charNo = ExtractPlainOrBracketedCharacteristicNumber(candidateText);

                        if (charNo == 2 || charNo == 3)
                        {
                            selectedAnnotation = ann;
                            selectedNote = swNote;
                            text = candidateText;

                            try
                            {
                                double[]? pos = ann.GetPosition() as double[];
                                if (pos != null && pos.Length >= 2)
                                {
                                    x = pos[0];
                                    y = pos[1];
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (selectedView == null || selectedAnnotation == null || selectedNote == null)
                    return null;

                int selectedNumber = ExtractPlainOrBracketedCharacteristicNumber(text);
                if (selectedNumber != 2 && selectedNumber != 3)
                    return null;

                return new SelectedTemplateInfo
                {
                    ViewName = selectedView.Name ?? "",
                    Text = text,
                    CharacteristicNumber = selectedNumber,
                    Annotation = selectedAnnotation,
                    NoteObject = selectedNote,
                    X = x,
                    Y = y
                };
            }
            catch
            {
                return null;
            }
        }

        private string? GetSelectedViewName(ModelDoc2 model)
        {
            try
            {
                var selMgr = model.SelectionManager as SelectionMgr;
                if (selMgr == null)
                    return null;

                int count = selMgr.GetSelectedObjectCount2(-1);
                string? selectedViewName = null;

                for (int i = 1; i <= count; i++)
                {
                    int selType = selMgr.GetSelectedObjectType3(i, -1);
                    if (selType != (int)swSelectType_e.swSelDRAWINGVIEWS)
                        continue;

                    View? selectedView = selMgr.GetSelectedObject6(i, -1) as View;
                    string? viewName = selectedView?.Name;

                    if (string.IsNullOrWhiteSpace(viewName))
                        continue;

                    if (!string.IsNullOrWhiteSpace(selectedViewName))
                        return null;

                    selectedViewName = viewName;
                }

                return selectedViewName;
            }
            catch
            {
                return null;
            }
        }

        private bool DeleteExistingCharacteristicNotesInView(string viewName, int characteristicNumber, Annotation? annotationToKeep)
        {
            ModelDoc2? model = GetActiveModel();
            if (model == null)
                return false;

            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return false;

            List<Annotation> toDelete = new List<Annotation>();

            View? view = drawing.GetFirstView();
            while (view != null)
            {
                if (!string.Equals(view.Name ?? "", viewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = view.GetNextView();
                    continue;
                }

                object annObj = view.GetFirstAnnotation3();

                while (annObj != null)
                {
                    Annotation? ann = annObj as Annotation;
                    if (ann == null)
                        break;

                    try
                    {
                        if (annotationToKeep != null && object.ReferenceEquals(ann, annotationToKeep))
                        {
                            annObj = ann.GetNext3();
                            continue;
                        }

                        if (ann.GetType() == (int)swAnnotationType_e.swNote)
                        {
                            object specific = ann.GetSpecificAnnotation();
                            Note? swNote = specific as Note;

                            if (swNote != null)
                            {
                                string text = swNote.GetText() ?? "";
                                int num = ExtractPlainOrBracketedCharacteristicNumber(text);

                                if (num == characteristicNumber)
                                    toDelete.Add(ann);
                            }
                        }
                    }
                    catch
                    {
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

                break;
            }

            if (toDelete.Count == 0)
            {
                Log($"Apply {characteristicNumber}: no existing {characteristicNumber} notes found in selected view.");
                return false;
            }

            model.ClearSelection2(true);

            int selectedCount = 0;
            foreach (var ann in toDelete)
            {
                try
                {
                    if (ann.Select3(true, null))
                        selectedCount++;
                }
                catch
                {
                }
            }

            if (selectedCount == 0)
            {
                model.ClearSelection2(true);
                return false;
            }

            model.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            model.ClearSelection2(true);
            model.GraphicsRedraw2();
            model.WindowRedraw();

            Log($"Apply {characteristicNumber}: deleted {selectedCount} existing note(s) in view '{viewName}'.");
            return true;
        }

        private (double X, double Y) GetSelectedCharacteristicPositionForDimension(Dim4DimensionInfo dim)
        {
            string dimText = RemoveRefSuffix(dim.DisplayText ?? "").Trim();
            int len = dimText.Length;

            double xOffset;

            if (len <= 1)
                xOffset = 0.0085;
            else if (len == 2)
                xOffset = 0.0095;
            else if (len == 3)
                xOffset = 0.0105;
            else if (len == 4)
                xOffset = 0.0115;
            else
                xOffset = 0.0125 + (len - 5) * 0.0004;

            double yOffset = 0.0070;

            return (dim.X - xOffset, dim.Y + yOffset);
        }

        private (double X, double Y) GetSelectedCharacteristicBlockPositionForDimension(Dim4DimensionInfo dim)
        {
            string fullText = RemoveRefSuffix(dim.DisplayText ?? "")
                .Replace(" ", "")
                .Trim();

            if (string.IsNullOrWhiteSpace(fullText))
                fullText = "0";

            string numericPart = Regex.Replace(fullText, @"TYP", "", RegexOptions.IgnoreCase);

            bool isTyp = fullText.Contains("TYP", StringComparison.OrdinalIgnoreCase);
            bool isFarRight = IsDimensionTextFarRightSideOfView(dim);

            int len = numericPart.Length;

            double charWidth = 0.00185;

            // shift from center to FIRST digit (ignore TYP!)
            double baseOffset = (len - 1) * charWidth / 2.0;

            double fineTuneX;

            if (Math.Abs(dim.NominalMm - 28.2) < 0.05 && isTyp)
            {
                fineTuneX = 0.0025;
            }
            else if (isTyp)
            {
                fineTuneX = -0.0090;
            }
            else if (isFarRight)
            {
                fineTuneX = -0.0055;
            }
            else
            {
                fineTuneX = -0.0050;
            }

            double yOffset = 0.0028;

            return (dim.X - baseOffset + fineTuneX, dim.Y + yOffset);
        }

        private bool IsDimensionTextFarRightSideOfView(Dim4DimensionInfo dim)
        {
            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return false;

            View? view = FindViewByName(drawing, dim.ViewName);
            if (view == null)
                return false;

            try
            {
                object? outlineObj = view.GetOutline();
                double[]? outline = ToDoubleArray(outlineObj);

                if (outline == null || outline.Length < 4)
                    return false;

                double left = Math.Min(outline[0], outline[2]);
                double right = Math.Max(outline[0], outline[2]);
                double width = right - left;

                if (width <= 0)
                    return false;

                // IMPORTANT:
                // Do NOT use center.
                // Only dimensions far to the right side should use +0.0040.
                double farRightThreshold = left + width * 0.80;

                return dim.X > farRightThreshold;
            }
            catch
            {
                return false;
            }
        }

        private List<NoteObjectInfo> GetAllNoteObjectsInDrawing()
        {
            List<NoteObjectInfo> result = new List<NoteObjectInfo>();

            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return result;

            View? view = drawing.GetFirstView();
            while (view != null)
            {
                object annObj = view.GetFirstAnnotation3();

                while (annObj != null)
                {
                    Annotation? ann = annObj as Annotation;
                    if (ann == null)
                        break;

                    try
                    {
                        if (ann.GetType() == (int)swAnnotationType_e.swNote)
                        {
                            object specific = ann.GetSpecificAnnotation();
                            Note? swNote = specific as Note;

                            if (swNote != null)
                            {
                                string text = swNote.GetText() ?? "";
                                double x = 0;
                                double y = 0;

                                try
                                {
                                    double[]? pos = ann.GetPosition() as double[];
                                    if (pos != null && pos.Length >= 2)
                                    {
                                        x = pos[0];
                                        y = pos[1];
                                    }
                                }
                                catch
                                {
                                }

                                result.Add(new NoteObjectInfo
                                {
                                    Text = text,
                                    ViewName = view.Name ?? "",
                                    X = x,
                                    Y = y,
                                    NoteObject = swNote,
                                    AnnotationObject = ann
                                });
                            }
                        }
                    }
                    catch
                    {
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

        private bool PasteCopiedSelectedCharacteristicInView(
                    ModelDoc2 model,
                    double newX,
                    double newY,
                    string targetViewName)
        {
            var beforeNotes = GetAllNoteObjectsInDrawing();
            int beforeCount = beforeNotes.Count;

            model.ClearSelection2(true);

            if (!string.IsNullOrWhiteSpace(targetViewName))
            {
                model.Extension.SelectByID2(
                    targetViewName,
                    "DRAWINGVIEW",
                    0, 0, 0,
                    false,
                    0,
                    null,
                    0);
            }

            model.Paste();

            NoteObjectInfo? pasted = null;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                System.Threading.Thread.Sleep(80);

                var afterNotes = GetAllNoteObjectsInDrawing();

                if (afterNotes.Count <= beforeCount)
                    continue;

                pasted = afterNotes
                    .Where(a =>
                        !beforeNotes.Any(b =>
                            string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase) &&
                            Math.Abs(a.X - b.X) < 0.000001 &&
                            Math.Abs(a.Y - b.Y) < 0.000001 &&
                            string.Equals(a.ViewName, b.ViewName, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(n => n.X + n.Y)
                    .FirstOrDefault();

                if (pasted != null)
                    break;
            }

            if (pasted == null || pasted.AnnotationObject == null)
                return false;

            try
            {
                pasted.AnnotationObject.SetPosition(newX, newY, 0);

                try
                {
                    pasted.AnnotationObject.SetLeader3(
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

                model.ClearSelection2(true);
                model.GraphicsRedraw2();
                model.WindowRedraw();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InsertBlockInViewAtPosition(
            ModelDoc2 model,
            string blockPath,
            double x,
            double y,
            string targetViewName,
            ref SketchBlockDefinition? cachedDefinition)
        {
            if (!File.Exists(blockPath))
                throw new FileNotFoundException($"Block file not found: {blockPath}", blockPath);

            DrawingDoc? drawing = model as DrawingDoc;
            if (drawing == null)
                return false;

            SketchManager? sketchMgr = model.SketchManager as SketchManager;
            if (sketchMgr == null)
                return false;

            View? targetView = FindViewByName(drawing, targetViewName);
            if (targetView == null)
                return false;

            Sketch? targetSketch = targetView.GetSketch() as Sketch;
            if (targetSketch == null)
                return false;

            int beforeCount = GetSketchBlockInstancesInView(targetView).Count;
            MathPoint? insertionPoint = CreateViewSketchPoint(targetView, x, y, 0);
            if (insertionPoint == null)
                return false;

            model.ClearSelection2(true);

            bool activated = false;
            try
            {
                activated = drawing.ActivateView(targetViewName);
            }
            catch
            {
            }

            if (!activated)
                return false;

            System.Threading.Thread.Sleep(50);

            SketchBlockInstance? insertedInstance = null;
            SketchBlockDefinition? definition = cachedDefinition ?? FindSketchBlockDefinition(sketchMgr, blockPath);

            try
            {
                if (definition == null)
                {
                    definition = sketchMgr.MakeSketchBlockFromFile(
                        insertionPoint,
                        blockPath,
                        false,
                        CharacteristicBlockScale,
                        0.0);
                }
                else
                {
                    insertedInstance = sketchMgr.InsertSketchBlockInstance(
                        definition,
                        insertionPoint,
                        CharacteristicBlockScale,
                        0.0);
                }
            }
            catch
            {
            }

            if (definition != null)
                cachedDefinition = definition;

            System.Threading.Thread.Sleep(80);

            int afterCount = GetSketchBlockInstancesInView(targetView).Count;

            model.ClearSelection2(true);
            model.GraphicsRedraw2();
            model.WindowRedraw();

            return insertedInstance != null || afterCount > beforeCount;
        }

        private MathPoint? CreateMathPoint(double x, double y, double z)
        {
            try
            {
                var swApp = GetApplication();
                if (swApp == null)
                    return null;

                MathUtility? mathUtility = swApp.GetMathUtility() as MathUtility;
                if (mathUtility == null)
                    return null;

                return mathUtility.CreatePoint(new double[] { x, y, z }) as MathPoint;
            }
            catch
            {
                return null;
            }
        }

        private MathPoint? CreateViewSketchPoint(View view, double sheetX, double sheetY, double sheetZ)
        {
            try
            {
                Sketch? sketch = view.GetSketch() as Sketch;
                if (sketch == null)
                    return null;

                MathPoint? sheetPoint = CreateMathPoint(sheetX, sheetY, sheetZ);
                MathTransform? modelToSketch = sketch.ModelToSketchTransform as MathTransform;

                if (sheetPoint == null || modelToSketch == null)
                    return null;

                return sheetPoint.MultiplyTransform(modelToSketch) as MathPoint;
            }
            catch
            {
                return null;
            }
        }

        private static SketchBlockDefinition? FindSketchBlockDefinition(SketchManager sketchMgr, string blockPath)
        {
            try
            {
                object[]? definitions = sketchMgr.GetSketchBlockDefinitions() as object[];
                if (definitions == null)
                    return null;

                string targetFileName = Path.GetFileName(blockPath);

                foreach (object definitionObj in definitions)
                {
                    SketchBlockDefinition? definition = definitionObj as SketchBlockDefinition;
                    if (definition == null)
                        continue;

                    string fileName = "";

                    try
                    {
                        fileName = definition.FileName ?? "";
                    }
                    catch
                    {
                    }

                    if (string.Equals(fileName, blockPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(fileName), targetFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return definition;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static List<object> GetSketchBlockInstancesInView(View view)
        {
            var result = new List<object>();

            try
            {
                Sketch? sketch = view.GetSketch() as Sketch;
                object[]? instances = sketch?.GetSketchBlockInstances() as object[];

                if (instances != null)
                    result.AddRange(instances.Where(instance => instance != null));
            }
            catch
            {
            }

            return result;
        }

        private bool DeleteExistingCharacteristicBlocksInView(ModelDoc2 model, DrawingDoc drawing, string viewName)
        {
            int deleted = 0;

            try
            {
                model.ClearSelection2(true);

                bool activated = drawing.ActivateView(viewName);
                if (!activated)
                {
                    Log($"Apply Selected 2/3: could not activate view '{viewName}'.");
                    return false;
                }

                model.EditSketch();

                SketchManager? sketchMgr = model.SketchManager as SketchManager;
                Sketch? activeSketch = sketchMgr?.ActiveSketch as Sketch;

                if (activeSketch == null)
                {
                    Log($"Apply Selected 2/3: active sketch is null after EditSketch.");
                    return false;
                }

                object[]? blocks = activeSketch.GetSketchBlockInstances() as object[];

                if (blocks == null || blocks.Length == 0)
                {
                    Log($"Apply Selected 2/3: active view sketch has 0 block instances.");
                    model.EditSketch();
                    return false;
                }

                foreach (object block in blocks)
                {
                    try
                    {
                        model.ClearSelection2(true);

                        bool selected = TrySelectSketchBlockInstance(block, false);

                        if (!selected)
                            continue;

                        model.EditDelete();
                        deleted++;
                    }
                    catch
                    {
                    }
                }

                model.ClearSelection2(true);

                try { model.EditSketch(); } catch { }

                model.GraphicsRedraw2();
                model.WindowRedraw();

                Log($"Apply Selected 2/3: deleted {deleted} block(s) from active view sketch '{viewName}'.");
                return deleted > 0;
            }
            catch (Exception ex)
            {
                try { model.EditSketch(); } catch { }
                model.ClearSelection2(true);
                Log($"Apply Selected 2/3: delete failed -> {ex.Message}");
                return false;
            }
        }

        private static bool TrySelectSketchBlockInstance(object instance, bool append)
        {
            try
            {
                SketchBlockInstance block = (SketchBlockInstance)instance;
                return block.Select(append, null);
            }
            catch { }

            try
            {
                dynamic d = instance;
                bool ok = d.Select(append, null);
                if (ok) return true;
            }
            catch { }

            try
            {
                dynamic d = instance;
                bool ok = d.Select4(append, null);
                if (ok) return true;
            }
            catch { }

            try
            {
                dynamic d = instance;
                bool ok = d.Select3(append, null);
                if (ok) return true;
            }
            catch { }

            return false;
        }

        private static bool TryDeleteSketchBlockInstanceDirect(object instance)
        {
            try
            {
                dynamic d = instance;
                d.Delete();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsCharacteristicBlockInstance(object instance)
        {
            foreach (string name in GetSketchBlockInstanceNames(instance))
            {
                if (IsBracePrefixedBlockName(name))
                    return true;
            }

            return false;
        }

        private static bool IsBracePrefixedBlockName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string trimmed = name.Trim();
            if (trimmed.StartsWith("{", StringComparison.OrdinalIgnoreCase))
                return true;

            string fileName = Path.GetFileName(trimmed);
            if (!string.IsNullOrWhiteSpace(fileName) &&
                fileName.StartsWith("{", StringComparison.OrdinalIgnoreCase))
                return true;

            int atIndex = trimmed.LastIndexOf('@');
            if (atIndex >= 0 && atIndex < trimmed.Length - 1)
            {
                string suffix = trimmed[(atIndex + 1)..].Trim();
                if (suffix.StartsWith("{", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        private static IEnumerable<string> GetSketchBlockInstanceNames(object instance)
        {
            var names = new List<string>();

            try
            {
                dynamic dynInstance = instance;
                string? instanceName = dynInstance.Name as string;
                if (!string.IsNullOrWhiteSpace(instanceName))
                    names.Add(instanceName);
            }
            catch
            {
            }

            try
            {
                dynamic dynInstance = instance;
                object definition = dynInstance.Definition;
                dynamic dynDefinition = definition;

                string? fileName = dynDefinition.FileName as string;
                if (!string.IsNullOrWhiteSpace(fileName))
                    names.Add(fileName);

                object feature = dynDefinition.GetFeature();
                dynamic dynFeature = feature;
                string? featureName = dynFeature.Name as string;
                if (!string.IsNullOrWhiteSpace(featureName))
                    names.Add(featureName);
            }
            catch
            {
            }

            return names;
        }

        private static double[]? ToDoubleArray(object? value)
        {
            if (value == null)
                return null;

            if (value is double[] doubles)
                return doubles;

            if (value is object[] objects)
            {
                return objects
                    .Select(Convert.ToDouble)
                    .ToArray();
            }

            return null;
        }

        public string ApplySelectedCharacteristicBlockToView(string blockPath, string blockFileName)
        {
            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return "No active drawing.";

            ModelDoc2? model = GetActiveModel();
            if (model == null)
                return "No active drawing model.";

            string? targetViewName = GetSelectedViewName(model);
            if (string.IsNullOrWhiteSpace(targetViewName))
                return "Please select exactly one drawing view, then click Apply Selected 2/3.";

            Log($"Apply Selected 2/3: selected view -> {targetViewName}");
            Log($"Apply Selected 2/3: selected block -> {blockFileName}");

            try
            {
                drawing.ActivateView(targetViewName);
            }
            catch
            {
            }

            DeleteExistingCharacteristicBlocksInView(model, drawing, targetViewName);

            DeleteExistingCharacteristicNotesInView(targetViewName, 2, null);
            DeleteExistingCharacteristicNotesInView(targetViewName, 3, null);

            List<Dim4DimensionInfo> dims = GetDimensionsInDrawing()
                .Where(d =>
                    string.Equals(d.ViewName, targetViewName, StringComparison.OrdinalIgnoreCase) &&
                    !d.IsRef)
                .ToList();

            if (dims.Count == 0)
                return $"No dimensions found in selected view '{targetViewName}'.";

            int created = 0;
            int failed = 0;
            SketchBlockDefinition? blockDefinition = null;

            foreach (Dim4DimensionInfo dim in dims)
            {
                try
                {
                    var pos = GetSelectedCharacteristicBlockPositionForDimension(dim);

                    bool ok = InsertBlockInViewAtPosition(
                        model,
                        blockPath,
                        pos.X,
                        pos.Y,
                        targetViewName,
                        ref blockDefinition);

                    if (ok)
                    {
                        created++;
                        Log($"Apply Selected 2/3: inserted '{blockFileName}' near dimension '{dim.FullName}' in view '{targetViewName}'.");
                    }
                    else
                    {
                        failed++;
                        Log($"Apply Selected 2/3: failed near dimension '{dim.FullName}'.");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"Apply Selected 2/3: failed near dimension '{dim.FullName}' -> {ex.Message}");
                }
            }

            List<NoteObjectInfo> holeNotes = GetAllNoteObjectsInDrawing()
                .Where(n =>
                    string.Equals(n.ViewName, targetViewName, StringComparison.OrdinalIgnoreCase) &&
                    (
                        n.Text.Contains("HOLE", StringComparison.OrdinalIgnoreCase) ||
                        n.Text.Contains("Ø", StringComparison.OrdinalIgnoreCase) ||
                        n.Text.Contains("%%C", StringComparison.OrdinalIgnoreCase)
                    ))
                .ToList();

            foreach (NoteObjectInfo note in holeNotes)
            {
                try
                {
                    double x = note.X + 0.0035;
                    double y = note.Y + 0.0010;

                    bool ok = InsertBlockInViewAtPosition(
                        model,
                        blockPath,
                        x,
                        y,
                        targetViewName,
                        ref blockDefinition);

                    if (ok)
                    {
                        created++;
                        Log($"Apply Selected 2/3: inserted '{blockFileName}' near hole callout '{note.Text}' in view '{targetViewName}'.");
                    }
                    else
                    {
                        failed++;
                        Log($"Apply Selected 2/3: failed near hole callout '{note.Text}'.");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"Apply Selected 2/3: failed near hole callout '{note.Text}' -> {ex.Message}");
                }
            }

            try
            {
                drawing.ActivateView("");
                drawing.EditSheet();
            }
            catch
            {
            }

            model.ClearSelection2(true);
            model.GraphicsRedraw2();
            model.WindowRedraw();

            if (failed == 0)
                return $"Inserted '{blockFileName}' at {created} item(s) in view '{targetViewName}'.";

            return $"Inserted '{blockFileName}' at {created} item(s) in view '{targetViewName}'. Failed: {failed}.";
        }

        public string ApplySelectedCharacteristicToView()
        {
            DrawingDoc? drawing = GetActiveDrawing();
            if (drawing == null)
                return "No active drawing.";

            ModelDoc2? model = GetActiveModel();
            if (model == null)
                return "No active drawing model.";

            SelectedTemplateInfo? selected = GetSelectedTemplateAndView(model);
            if (selected == null)
                return "Please select exactly one drawing view and one boxed 2 or 3 note inside that view.";

            int characteristicNumber = selected.CharacteristicNumber;
            string targetViewName = selected.ViewName;

            if (string.IsNullOrWhiteSpace(targetViewName))
                return "Selected view is invalid.";

            if (selected.Annotation == null)
                return "Selected note annotation is invalid.";

            Log($"Apply {characteristicNumber}: selected view -> {targetViewName}");
            Log($"Apply {characteristicNumber}: selected template text -> {selected.Text}");

            // Delete old same-number notes in the view, but keep the selected template
            DeleteExistingCharacteristicNotesInView(targetViewName, characteristicNumber, selected.Annotation);

            model.ClearSelection2(true);

            bool selectedTemplate = selected.Annotation.Select3(false, null);
            if (!selectedTemplate)
                return "Failed to select the boxed note template.";

            model.EditCopy();
            model.ClearSelection2(true);

            List<Dim4DimensionInfo> dims = GetDimensionsInDrawing()
                .Where(d =>
                    string.Equals(d.ViewName, targetViewName, StringComparison.OrdinalIgnoreCase) &&
                    !d.IsRef)
                .ToList();

            if (dims.Count == 0)
                return $"No dimensions found in selected view '{targetViewName}'.";

            int created = 0;
            int failed = 0;

            foreach (Dim4DimensionInfo dim in dims)
            {
                try
                {
                    var pos = GetSelectedCharacteristicPositionForDimension(dim);

                    bool ok = PasteCopiedSelectedCharacteristicInView(
                        model,
                        pos.X,
                        pos.Y,
                        targetViewName);

                    if (ok)
                    {
                        created++;
                        Log($"Apply {characteristicNumber}: created near dimension '{dim.FullName}' in view '{targetViewName}'.");
                    }
                    else
                    {
                        failed++;
                        Log($"Apply {characteristicNumber}: failed near dimension '{dim.FullName}'.");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"Apply {characteristicNumber}: failed -> {ex.Message}");
                }
            }

            model.ClearSelection2(true);
            model.GraphicsRedraw2();
            model.WindowRedraw();

            if (failed == 0)
                return $"Applied selected boxed {characteristicNumber} to {created} dimension(s) in view '{targetViewName}'.";

            return $"Applied selected boxed {characteristicNumber} to {created} dimension(s) in view '{targetViewName}'. Failed: {failed}.";
        }
    }
}