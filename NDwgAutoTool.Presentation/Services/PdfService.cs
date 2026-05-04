using SldWorks;
using SwConst;
using System.IO;

namespace NDwgAutoTool.Services
{
    public class PdfService
    {
        private readonly Action<string>? _log;

        public PdfService(Action<string>? log = null)
        {
            _log = log;
        }

        private void Log(string message)
        {
            _log?.Invoke(message);
        }

        public string CreatePdfFromActiveDrawing()
        {
            var solidWorksService = new SolidWorksService();
            var swApp = solidWorksService.GetApplication();

            if (swApp == null)
                throw new Exception("Could not connect to SolidWorks.");

            var model = solidWorksService.GetActiveDocument(swApp);

            if (model == null)
                throw new Exception("No active SolidWorks document.");

            if (!solidWorksService.IsDrawing(model))
                throw new Exception("Active document is not a drawing.");

            return CreatePdfFromDrawing(model);
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
                ModelDoc2? model = obj as ModelDoc2;
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

        public string CreatePdfFromDrawing(ModelDoc2 model)
        {
            if (model == null)
                throw new Exception("Model is null.");

            if (model.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
                throw new Exception("Document is not a drawing.");

            string modelPath = model.GetPathName();

            if (string.IsNullOrWhiteSpace(modelPath))
                throw new Exception("Drawing must be saved before creating PDF.");

            string folder = Path.GetDirectoryName(modelPath) ?? "";
            string drawingName = Path.GetFileNameWithoutExtension(modelPath);

            string outputPath = GetNextCheckPdfPath(folder, drawingName);

            Log($"Create PDF drawing: {drawingName}");

            try
            {
                model.ViewZoomtofit2();
                model.GraphicsRedraw2();
            }
            catch
            {
            }

            Log($"Create PDF output path: {outputPath}");

            int errors = 0;
            int warnings = 0;

            bool ok = model.Extension.SaveAs(
                outputPath,
                (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                null,
                ref errors,
                ref warnings);

            if (!ok || errors != 0)
                throw new Exception($"Failed to save PDF. SaveAs errors={errors}, warnings={warnings}.");

            Log("Create PDF completed successfully.");

            return outputPath;
        }

        private string GetNextCheckPdfPath(string folder, string drawingName)
        {
            int checkNumber = 1;

            while (true)
            {
                string candidate = Path.Combine(folder, $"{drawingName}_Check{checkNumber}.pdf");

                if (!File.Exists(candidate))
                    return candidate;

                checkNumber++;
            }
        }
    }
}