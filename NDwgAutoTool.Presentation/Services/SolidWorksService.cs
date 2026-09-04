using NDwgAutoTool.Helpers;
using NDwgAutoTool.Models;
using System.IO;
using System.Runtime.InteropServices;
using SldWorksInterop = SldWorks;
using SwConstInterop = SwConst;

namespace NDwgAutoTool.Services
{
    public class SolidWorksService
    {
        private const double TitleBlockNoteTextHeight = 0.00494;
        private const int TitleBlockNoteTextSizeInPoints = 14;

        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(
            ref Guid rclsid,
            IntPtr reserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void CLSIDFromProgID(
            string progID,
            out Guid clsid);

        public SldWorksInterop.ISldWorks? GetApplication()
        {
            try
            {
                CLSIDFromProgID("SldWorks.Application", out Guid clsid);
                GetActiveObject(ref clsid, IntPtr.Zero, out object swObject);
                return swObject as SldWorksInterop.ISldWorks;
            }
            catch
            {
                return null;
            }
        }

        public SldWorksInterop.ModelDoc2? GetActiveDocument(SldWorksInterop.ISldWorks swApp)
        {
            return swApp.IActiveDoc2;
        }

        public bool IsDrawing(SldWorksInterop.ModelDoc2 model)
        {
            return model.GetType() == (int)SwConstInterop.swDocumentTypes_e.swDocDRAWING;
        }

        public SldWorksInterop.ModelDoc2? OpenDrawing(
            SldWorksInterop.ISldWorks swApp,
            string filePath,
            out int errors,
            out int warnings)
        {
            errors = 0;
            warnings = 0;

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Drawing file path is empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Drawing file was not found.", filePath);

            return swApp.OpenDoc6(
                filePath,
                (int)SwConstInterop.swDocumentTypes_e.swDocDRAWING,
                (int)SwConstInterop.swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings) as SldWorksInterop.ModelDoc2;
        }

        public SldWorksInterop.ModelDoc2? OpenAssembly(
            SldWorksInterop.ISldWorks swApp,
            string filePath,
            out int errors,
            out int warnings)
        {
            errors = 0;
            warnings = 0;

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Assembly file path is empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Assembly file was not found.", filePath);

            return swApp.OpenDoc6(
                filePath,
                (int)SwConstInterop.swDocumentTypes_e.swDocASSEMBLY,
                (int)SwConstInterop.swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings) as SldWorksInterop.ModelDoc2;
        }

        public SldWorksInterop.ModelDoc2? OpenPart(
            SldWorksInterop.ISldWorks swApp,
            string filePath,
            out int errors,
            out int warnings)
        {
            errors = 0;
            warnings = 0;

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Part file path is empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Part file was not found.", filePath);

            return swApp.OpenDoc6(
                filePath,
                (int)SwConstInterop.swDocumentTypes_e.swDocPART,
                (int)SwConstInterop.swOpenDocOptions_e.swOpenDocOptions_Silent,
                "",
                ref errors,
                ref warnings) as SldWorksInterop.ModelDoc2;
        }

        public SldWorksInterop.ModelDoc2? FindOpenDocumentByPath(
            SldWorksInterop.ISldWorks swApp,
            string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            string targetPath = Path.GetFullPath(filePath);
            object[]? docs = swApp.GetDocuments() as object[];

            if (docs == null)
                return null;

            foreach (object obj in docs)
            {
                var model = obj as SldWorksInterop.ModelDoc2;
                if (model == null)
                    continue;

                try
                {
                    string modelPath = model.GetPathName();

                    if (!string.IsNullOrWhiteSpace(modelPath) &&
                        string.Equals(Path.GetFullPath(modelPath), targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return model;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public void CloseDocumentWithoutSaving(
            SldWorksInterop.ISldWorks swApp,
            SldWorksInterop.ModelDoc2 model)
        {
            string title = model.GetTitle() ?? "";

            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileName(model.GetPathName());

            if (string.IsNullOrWhiteSpace(title))
                throw new Exception("Could not determine SolidWorks document title for close.");

            swApp.CloseDoc(title);
        }

        public string GetActiveDrawingPartNo(SldWorksInterop.ModelDoc2 model)
        {
            string fullPath = model.GetPathName();

            if (string.IsNullOrWhiteSpace(fullPath))
                throw new Exception("Active drawing has no saved file path.");

            return Path.GetFileNameWithoutExtension(fullPath);
        }

        public List<DrawingNoteInfo> GetAllNotesOnActiveSheet(SldWorksInterop.ModelDoc2 model)
        {
            var result = new List<DrawingNoteInfo>();

            var drawing = model as SldWorksInterop.DrawingDoc;
            if (drawing == null)
                throw new Exception("Active document is not a drawing document.");

            object[]? viewGroups = drawing.GetViews() as object[];
            if (viewGroups == null)
                return result;

            foreach (object groupObj in viewGroups)
            {
                object[]? views = groupObj as object[];
                if (views == null)
                    continue;

                foreach (object viewObj in views)
                {
                    var view = viewObj as SldWorksInterop.View;
                    if (view == null)
                        continue;

                    var annotation = view.GetFirstAnnotation3();
                    while (annotation != null)
                    {
                        if (annotation.GetType() == (int)SwConstInterop.swAnnotationType_e.swNote)
                        {
                            var specificAnnotation = annotation.GetSpecificAnnotation();
                            var note = specificAnnotation as SldWorksInterop.Note;

                            if (note != null)
                            {
                                string text = note.GetText();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    double[] position = annotation.GetPosition() as double[] ?? new double[] { 0, 0, 0 };

                                    int leaderCount = 0;
                                    try
                                    {
                                        leaderCount = annotation.GetLeaderCount();
                                    }
                                    catch
                                    {
                                        leaderCount = 0;
                                    }

                                    result.Add(new DrawingNoteInfo
                                    {
                                        Text = text.Trim(),
                                        X = position.Length > 0 ? position[0] : 0,
                                        Y = position.Length > 1 ? position[1] : 0,
                                        ViewName = view.Name ?? "",
                                        IsVertical = false,
                                        HasLeader = leaderCount > 0,
                                        NoteObject = note,
                                        AnnotationObject = annotation
                                    });
                                }
                            }
                        }

                        annotation = annotation.GetNext3();
                    }
                }
            }

            return result;
        }

        public void UpdateNoteText(SldWorksInterop.Note note, string newText)
        {
            if (note == null)
                throw new Exception("Note object is null.");

            note.SetText(newText);
        }

        public void DeleteAnnotations(SldWorksInterop.ModelDoc2 model, IEnumerable<SldWorksInterop.Annotation> annotations)
        {
            if (model == null)
                throw new Exception("Model is null.");

            model.ClearSelection2(true);

            int selectedCount = 0;

            foreach (var annotation in annotations)
            {
                if (annotation == null)
                    continue;

                bool selected = annotation.Select3(true, null);
                if (selected)
                {
                    selectedCount++;
                }
            }

            if (selectedCount == 0)
                return;

            model.Extension.DeleteSelection2(
                (int)SwConstInterop.swDeleteSelectionOptions_e.swDelete_Absorbed);

            model.ClearSelection2(true);
            model.WindowRedraw();
        }

        public void DuplicateTemplateFlagNote(
            SldWorks.ModelDoc2 model,
            SldWorks.Annotation templateAnnotation,
            string newText,
            double newX,
            double newY,
            string? targetViewName,
            Action<string>? log = null)
        {
            if (model == null)
                throw new Exception("Model is null.");

            if (templateAnnotation == null)
                throw new Exception("Template annotation is null.");

            var beforeAllNotes = GetAllNotesOnActiveSheet(model).ToList();
            int beforeCount = beforeAllNotes.Count;

            model.ClearSelection2(true);

            bool selected = templateAnnotation.Select3(false, null);
            if (!selected)
                throw new Exception("Failed to select template annotation.");

            model.EditCopy();
            model.ClearSelection2(true);

            bool targetViewSelected = false;

            if (!string.IsNullOrWhiteSpace(targetViewName))
            {
                targetViewSelected = SelectDrawingViewByName(model, targetViewName);
                log?.Invoke($"Route A: target view '{targetViewName}' selected before paste -> {targetViewSelected}");
            }

            model.Paste();
            model.WindowRedraw();

            DrawingNoteInfo? pastedNoteInfo = null;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                System.Threading.Thread.Sleep(150);
                model.GraphicsRedraw2();
                model.WindowRedraw();

                var afterAllNotes = GetAllNotesOnActiveSheet(model).ToList();

                if (afterAllNotes.Count <= beforeCount)
                    continue;

                pastedNoteInfo = afterAllNotes
                    .Where(after =>
                        !beforeAllNotes.Any(before =>
                            before.Text.Equals(after.Text, StringComparison.OrdinalIgnoreCase) &&
                            Math.Abs(before.X - after.X) < 0.000001 &&
                            Math.Abs(before.Y - after.Y) < 0.000001))
                    .OrderByDescending(n => n.X + n.Y)
                    .FirstOrDefault();

                if (pastedNoteInfo != null)
                    break;
            }

            if (pastedNoteInfo == null || pastedNoteInfo.NoteObject == null || pastedNoteInfo.AnnotationObject == null)
                throw new Exception("Could not detect pasted template note.");

            pastedNoteInfo.NoteObject.SetText(newText);
            pastedNoteInfo.AnnotationObject.SetPosition(newX, newY, 0);

            model.ClearSelection2(true);
            model.GraphicsRedraw2();
            model.WindowRedraw();

            if (!string.IsNullOrWhiteSpace(targetViewName))
            {
                log?.Invoke($"Route A: pasted flagnote detected in view '{pastedNoteInfo.ViewName}' (target was '{targetViewName}')");
            }
        }

        public void UpdateNoteTextAtPosition(
            IEnumerable<DrawingNoteInfo> notes,
            double targetX,
            double targetY,
            string newText,
            double tolerance = 0.01)
        {
            var candidate = notes
                .Where(n => n.NoteObject != null)
                .OrderBy(n => Math.Abs(n.X - targetX) + Math.Abs(n.Y - targetY))
                .FirstOrDefault();

            if (candidate == null)
                throw new Exception($"No note found near X:{targetX:F4} Y:{targetY:F4}");

            double dx = Math.Abs(candidate.X - targetX);
            double dy = Math.Abs(candidate.Y - targetY);

            if (dx > tolerance || dy > tolerance)
                throw new Exception($"Nearest note is too far from target position X:{targetX:F4} Y:{targetY:F4}");

            candidate.NoteObject!.SetText(newText);
        }

        public void SetDrawingCustomProperty(SldWorksInterop.ModelDoc2 model, string propertyName, string value)
        {
            if (model == null)
                throw new Exception("Model is null.");

            var extension = model.Extension;
            if (extension == null)
                throw new Exception("Model extension is null.");

            var customPropertyManager = extension.CustomPropertyManager[""];
            if (customPropertyManager == null)
                throw new Exception("Could not access drawing custom property manager.");

            customPropertyManager.Add3(
                propertyName,
                (int)SwConstInterop.swCustomInfoType_e.swCustomInfoText,
                value,
                (int)SwConstInterop.swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
        }

        public void RebuildModel(SldWorksInterop.ModelDoc2 model)
        {
            if (model == null)
                throw new Exception("Model is null.");

            model.ForceRebuild3(false);
            model.GraphicsRedraw2();
            model.WindowRedraw();
        }

        public void UpdateOrCreateNoteAtPositionInSheetFormat(
            SldWorksInterop.ModelDoc2 model,
            IEnumerable<DrawingNoteInfo> notes,
            double targetX,
            double targetY,
            string newText,
            bool vertical = false,
            double tolerance = 0.01)
        {
            var candidate = notes
                .Where(n => n.NoteObject != null)
                .OrderBy(n => Math.Abs(n.X - targetX) + Math.Abs(n.Y - targetY))
                .FirstOrDefault();

            if (candidate != null)
            {
                double dx = Math.Abs(candidate.X - targetX);
                double dy = Math.Abs(candidate.Y - targetY);

                if (dx <= tolerance && dy <= tolerance)
                {
                    var noteObj = candidate.NoteObject!;
                    noteObj.SetText(newText);

                    noteObj.Angle = vertical ? Math.PI / 2.0 : 0;
                    ApplyTitleBlockNoteTextHeight(noteObj);

                    model.EditRebuild3();
                    model.GraphicsRedraw2();
                    model.WindowRedraw();

                    return;
                }
            }

            var drawing = model as SldWorksInterop.DrawingDoc;
            if (drawing == null)
                throw new Exception("Not a drawing.");

            drawing.EditTemplate();

            model.ClearSelection2(true);

            var note = model.InsertNote(newText);
            if (note == null)
                throw new Exception($"Failed to create note at X:{targetX:F4} Y:{targetY:F4}");

            note.LockPosition = false;
            note.Angle = vertical ? Math.PI / 2.0 : 0;
            ApplyTitleBlockNoteTextHeight(note);

            var annotation = note.GetAnnotation();
            if (annotation == null)
                throw new Exception($"Created note has no annotation.");

            annotation.SetPosition(targetX, targetY, 0);

            model.ClearSelection2(true);

            drawing.EditSheet();

            model.GraphicsRedraw2();
            model.WindowRedraw();
        }

        private static void ApplyTitleBlockNoteTextHeight(SldWorksInterop.Note note)
        {
            note.SetHeight(TitleBlockNoteTextHeight);

            var annotation = note.GetAnnotation();
            if (annotation == null)
                return;

            ApplyTitleBlockAnnotationTextHeight(annotation);
        }

        private static void ApplyTitleBlockAnnotationTextHeight(SldWorksInterop.Annotation annotation)
        {
            try
            {
                dynamic dynamicAnnotation = annotation;
                dynamic textFormat = dynamicAnnotation.GetTextFormat(0);

                if (textFormat != null)
                {
                    textFormat.CharHeight = TitleBlockNoteTextHeight;
                    textFormat.CharHeightInPts = TitleBlockNoteTextSizeInPoints;
                    dynamicAnnotation.SetTextFormat(0, false, textFormat);
                }
            }
            catch
            {
            }

            try
            {
                var paragraphs = annotation.GetParagraphs() as SldWorksInterop.Paragraphs;
                if (paragraphs == null)
                    return;

                int paragraphCount = Math.Max(1, paragraphs.Count);

                for (int paragraph = 0; paragraph < paragraphCount; paragraph++)
                {
                    paragraphs.CurrentParagraph = paragraph;
                    int segmentCount = paragraphs.GetTextSegmentCount();

                    for (int segment = 0; segment < segmentCount; segment++)
                    {
                        var textFormat = paragraphs.GetTextSegmentFormat(segment) as SldWorksInterop.TextFormat;
                        if (textFormat == null)
                            continue;

                        textFormat.CharHeight = TitleBlockNoteTextHeight;
                        textFormat.CharHeightInPts = TitleBlockNoteTextSizeInPoints;
                        paragraphs.SetTextSegmentFormat(segment, textFormat);
                    }

                    paragraphs.UpdateParagraph();
                }
            }
            catch
            {
            }
        }

        public void InsertNoteBlockAtOrigin(SldWorksInterop.ModelDoc2 model, string blockPath)
        {
            if (model == null)
                throw new Exception("Model is null.");

            if (!File.Exists(blockPath))
                throw new Exception($"Block file not found: {blockPath}");

            var drawing = model as SldWorksInterop.DrawingDoc;
            if (drawing == null)
                throw new Exception("Active document is not a drawing.");

            var sketchMgr = model.SketchManager as SldWorksInterop.SketchManager;
            if (sketchMgr == null)
                throw new Exception("Could not get SketchManager.");

            // Force SolidWorks out of any active drawing view and back to the sheet
            drawing.ActivateView("");
            drawing.EditSheet();

            model.ClearSelection2(true);
            model.GraphicsRedraw2();
            model.WindowRedraw();
            System.Threading.Thread.Sleep(150);

            sketchMgr.MakeSketchBlockFromFile(
                null,
                blockPath,
                false,
                0.1,
                0.0);

            model.ClearSelection2(true);
            model.GraphicsRedraw2();
            model.WindowRedraw();
            System.Threading.Thread.Sleep(200);
        }

        public bool DeleteExistingNoteBlock(SldWorks.ModelDoc2 model, Action<string>? log = null)
        {
            if (model == null)
                throw new Exception("Model is null.");

            var drawing = model as SldWorksInterop.DrawingDoc;
            if (drawing == null)
                throw new Exception("Active document is not a drawing.");

            drawing.ActivateView("");
            drawing.EditSheet();
            model.ClearSelection2(true);

            bool selected = model.Extension.SketchBoxSelect(
                0.180000,
                0.060000,
                0.000000,
                -0.010000,
                -0.010000,
                0.000000
            );

            if (!selected)
            {
                log?.Invoke("No existing note block found in note area.");
                model.ClearSelection2(true);
                return false;
            }

            var selMgr = model.SelectionManager;
            if (selMgr == null)
            {
                model.ClearSelection2(true);
                return false;
            }

            int count = selMgr.GetSelectedObjectCount2(-1);
            if (count == 0)
            {
                log?.Invoke("No existing note block found in note area.");
                model.ClearSelection2(true);
                return false;
            }

            bool deleted = model.Extension.DeleteSelection2(
                (int)SwConstInterop.swDeleteSelectionOptions_e.swDelete_Absorbed);

            model.ClearSelection2(true);
            model.GraphicsRedraw2();
            model.WindowRedraw();

            if (deleted)
                log?.Invoke("Existing note block deleted.");
            else
                log?.Invoke("Block objects were selected, but deletion failed.");

            return deleted;
        }

        public string? GetNearestNoteText(
            IEnumerable<NDwgAutoTool.Models.DrawingNoteInfo> notes,
            double targetX,
            double targetY,
            double tolerance = 0.01)
        {
            var candidate = notes
                .Where(n => !string.IsNullOrWhiteSpace(n.Text))
                .OrderBy(n => Math.Abs(n.X - targetX) + Math.Abs(n.Y - targetY))
                .FirstOrDefault();

            if (candidate == null)
                return null;

            double dx = Math.Abs(candidate.X - targetX);
            double dy = Math.Abs(candidate.Y - targetY);

            if (dx > tolerance || dy > tolerance)
                return null;

            return candidate.Text?.Trim();
        }

        public List<string> GetNoteBlockCodesInArea(SldWorks.ModelDoc2 model)
        {
            if (model == null)
                throw new Exception("Model is null.");

            var result = new List<string>();

            model.ClearSelection2(true);

            bool selected = model.Extension.SketchBoxSelect(
                0.180000,   // X1
                0.060000,   // Y1
                0.000000,   // Z1
                -0.010000,  // X2
                -0.010000,  // Y2
                0.000000    // Z2
            );

            if (!selected)
            {
                model.ClearSelection2(true);
                return result;
            }

            var selMgr = model.SelectionManager;
            if (selMgr == null)
            {
                model.ClearSelection2(true);
                return result;
            }

            int count = selMgr.GetSelectedObjectCount2(-1);

            for (int i = 1; i <= count; i++)
            {
                object obj = selMgr.GetSelectedObject6(i, -1);
                if (obj == null)
                    continue;

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

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var codes = System.Text.RegularExpressions.Regex.Matches(
                    name.ToUpper(),
                    @"\b(?:G\d+|T\d+|B\d+|SE\d+)\b")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => m.Value)
                    .ToList();

                foreach (var code in codes)
                {
                    if (!result.Contains(code, StringComparer.OrdinalIgnoreCase))
                        result.Add(code);
                }
            }

            model.ClearSelection2(true);
            return result;
        }

        private bool SelectDrawingViewByName(SldWorks.ModelDoc2 model, string viewName)
        {
            if (model == null)
                return false;

            if (string.IsNullOrWhiteSpace(viewName))
                return false;

            try
            {
                model.ClearSelection2(true);

                bool selected = model.Extension.SelectByID2(
                    viewName,
                    "DRAWINGVIEW",
                    0, 0, 0,
                    false,
                    0,
                    null,
                    0);

                return selected;
            }
            catch
            {
                return false;
            }
        }

        public DrawingNoteInfo? PasteCopiedFlagNoteInView(
            SldWorks.ModelDoc2 model,
            string newText,
            double newX,
            double newY,
            string targetViewName,
            Action<string>? log = null)
        {
            if (model == null)
                throw new Exception("Model is null.");

            var beforeAllNotes = GetAllNotesOnActiveSheet(model).ToList();
            int beforeCount = beforeAllNotes.Count;

            model.ClearSelection2(true);

            bool targetViewSelected = false;

            if (!string.IsNullOrWhiteSpace(targetViewName))
            {
                targetViewSelected = model.Extension.SelectByID2(
                    targetViewName,
                    "DRAWINGVIEW",
                    0, 0, 0,
                    false,
                    0,
                    null,
                    0);

                log?.Invoke($"Hybrid: target view '{targetViewName}' selected before paste -> {targetViewSelected}");
            }

            model.Paste();

            DrawingNoteInfo? pastedNoteInfo = null;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                System.Threading.Thread.Sleep(50);

                var afterAllNotes = GetAllNotesOnActiveSheet(model).ToList();

                if (afterAllNotes.Count <= beforeCount)
                    continue;

                foreach (var note in afterAllNotes)
                    DrawingNoteHelper.Classify(note);

                pastedNoteInfo = afterAllNotes
                    .Where(after =>
                        !beforeAllNotes.Any(before =>
                            before.Text.Equals(after.Text, StringComparison.OrdinalIgnoreCase) &&
                            Math.Abs(before.X - after.X) < 0.000001 &&
                            Math.Abs(before.Y - after.Y) < 0.000001))
                    .OrderByDescending(n => n.X + n.Y)
                    .FirstOrDefault();

                if (pastedNoteInfo != null)
                    break;
            }

            if (pastedNoteInfo == null || pastedNoteInfo.NoteObject == null || pastedNoteInfo.AnnotationObject == null)
                throw new Exception("Could not detect pasted flag note.");

            pastedNoteInfo.NoteObject.SetText(newText);
            pastedNoteInfo.AnnotationObject.SetPosition(newX, newY, 0);
            pastedNoteInfo.Text = newText;
            pastedNoteInfo.X = newX;
            pastedNoteInfo.Y = newY;
            DrawingNoteHelper.Classify(pastedNoteInfo);

            try
            {
                pastedNoteInfo.AnnotationObject.SetLeader3(
                    (int)SwConst.swLeaderStyle_e.swNO_LEADER,
                    0,
                    false,
                    false,
                    false,
                    false);
                log?.Invoke("Hybrid: leader forced to NO LEADER.");
            }
            catch (Exception ex)
            {
                log?.Invoke("WARNING: Could not force no leader: " + ex.Message);
            }

            model.ClearSelection2(true);

            return pastedNoteInfo;
        }
    }
}
