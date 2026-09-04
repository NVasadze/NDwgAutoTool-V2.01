using SldWorks;
using SwConst;

namespace NDwgAutoTool.Services
{
    public class SaveAllService
    {
        private readonly Action<string>? _log;

        public SaveAllService(Action<string>? log = null)
        {
            _log = log;
        }

        private void Log(string message)
        {
            _log?.Invoke(message);
        }

        public List<ModelDoc2> GetOpenDrawingDocuments()
        {
            var result = new List<ModelDoc2>();

            var solidWorksService = new SolidWorksService();
            var swApp = solidWorksService.GetApplication();

            if (swApp == null)
                throw new Exception("Could not connect to SolidWorks.");

            object[]? docs = swApp.GetDocuments() as object[];
            if (docs == null || docs.Length == 0)
                return result;

            foreach (object obj in docs)
            {
                var model = obj as ModelDoc2;
                if (model == null)
                    continue;

                try
                {
                    if (model.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                        result.Add(model);
                }
                catch
                {
                }
            }

            return result;
        }

        public string SaveDrawing(ModelDoc2 drawing)
        {
            if (drawing == null)
                throw new Exception("Drawing is null.");

            if (drawing.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
                throw new Exception("Document is not a drawing.");

            string drawingName = drawing.GetTitle() ?? "Unknown Drawing";

            string path = drawing.GetPathName();
            if (string.IsNullOrWhiteSpace(path))
                throw new Exception("Drawing must be saved once before Save All can save it.");

            TurnOffGridIfVisible(drawing, drawingName);

            Log($"Save All: zoom to fit -> {drawingName}");

            try
            {
                drawing.ViewZoomtofit2();
                drawing.GraphicsRedraw2();
                drawing.WindowRedraw();
            }
            catch
            {
            }

            int errors = 0;
            int warnings = 0;

            bool ok = drawing.Save3(
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                ref errors,
                ref warnings);

            if (!ok || errors != 0)
                throw new Exception($"Save failed. errors={errors}, warnings={warnings}");

            Log($"Save All: saved -> {drawingName}");

            return drawingName;
        }

        private void TurnOffGridIfVisible(ModelDoc2 drawing, string drawingName)
        {
            try
            {
                var extension = drawing.Extension;

                bool gridVisible = extension.GetUserPreferenceToggle(
                    (int)swUserPreferenceToggle_e.swGridDisplay,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified);

                if (!gridVisible)
                    return;

                bool changed = extension.SetUserPreferenceToggle(
                    (int)swUserPreferenceToggle_e.swGridDisplay,
                    (int)swUserPreferenceOption_e.swDetailingNoOptionSpecified,
                    false);

                Log(changed
                    ? $"Save All: grid display turned off -> {drawingName}"
                    : $"Save All: grid display was visible but could not be turned off -> {drawingName}");
            }
            catch (Exception ex)
            {
                Log($"Save All: could not check grid display for {drawingName} | {ex.Message}");
            }
        }
    }
}
