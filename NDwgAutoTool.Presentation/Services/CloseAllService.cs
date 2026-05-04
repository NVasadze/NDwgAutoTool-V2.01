namespace NDwgAutoTool.Services
{
    public class CloseAllService
    {
        private readonly Action<string>? _log;

        public CloseAllService(Action<string>? log = null)
        {
            _log = log;
        }

        private void Log(string message)
        {
            _log?.Invoke(message);
        }

        public int GetOpenDocumentCount()
        {
            var solidWorksService = new SolidWorksService();
            var swApp = solidWorksService.GetApplication();

            if (swApp == null)
                throw new Exception("Could not connect to SolidWorks.");

            return swApp.GetDocumentCount();
        }

        public int CloseNormally()
        {
            var solidWorksService = new SolidWorksService();
            var swApp = solidWorksService.GetApplication();

            if (swApp == null)
                throw new Exception("Could not connect to SolidWorks.");

            int before = swApp.GetDocumentCount();
            Log($"Close All: {before} document(s) open before normal close.");

            if (before == 0)
                return 0;

            swApp.CloseAllDocuments(false);

            int after = swApp.GetDocumentCount();
            Log($"Close All: {after} document(s) remain after normal close.");

            return after;
        }

        public int ForceCloseRemainingWithoutSaving()
        {
            var solidWorksService = new SolidWorksService();
            var swApp = solidWorksService.GetApplication();

            if (swApp == null)
                throw new Exception("Could not connect to SolidWorks.");

            int before = swApp.GetDocumentCount();
            Log($"Close All: forcing close of remaining {before} document(s).");

            if (before == 0)
                return 0;

            var titles = GetOpenDocumentTitles(swApp);

            foreach (string title in titles)
            {
                try
                {
                    swApp.CloseDoc(title);
                    Log($"Close All: forced close -> {title}");
                }
                catch (Exception ex)
                {
                    Log($"Close All: failed to close {title} | {ex.Message}");
                }
            }

            int after = swApp.GetDocumentCount();
            Log($"Close All: {after} document(s) remain after force close.");

            return after;
        }

        private List<string> GetOpenDocumentTitles(SldWorks.ISldWorks swApp)
        {
            var titles = new List<string>();

            object[]? docs = swApp.GetDocuments() as object[];
            if (docs == null)
                return titles;

            foreach (object obj in docs)
            {
                var model = obj as SldWorks.ModelDoc2;
                if (model == null)
                    continue;

                try
                {
                    string title = model.GetTitle() ?? "";
                    if (!string.IsNullOrWhiteSpace(title))
                        titles.Add(title);
                }
                catch
                {
                }
            }

            return titles;
        }
    }
}