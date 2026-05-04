using SldWorks;

namespace NDwgAutoTool.Services
{
    public sealed class ActiveDrawingDocumentProvider
    {
        public (ModelDoc2 Document, SolidWorksService SolidWorks) GetActiveDrawing()
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

            return (activeDoc, solidWorksService);
        }
    }
}
