namespace NDwgAutoTool.Services
{
    public sealed class ResourceAvailabilityReport
    {
        public bool IsComplete => MissingItems.Count == 0;
        public List<string> FoundItems { get; } = new();
        public List<string> MissingItems { get; } = new();

        public string ToLogText()
        {
            if (IsComplete)
                return "Resource check passed: " + string.Join("; ", FoundItems);

            return "Resource check failed. Missing: " + string.Join("; ", MissingItems);
        }
    }
}
