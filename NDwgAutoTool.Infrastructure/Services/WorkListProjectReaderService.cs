namespace NDwgAutoTool.Services
{
    public class WorkListProjectReaderService
    {
        private readonly WorkListReaderService _workList = new();

        public string GetProjectCode(string filePath)
        {
            return _workList.GetProjectCode(filePath);
        }
    }
}
