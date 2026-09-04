namespace NDwgAutoTool.Infrastructure.Settings
{
    public sealed class AppSettings
    {
        public string? LastRootPath { get; set; }
    }

    public sealed class UserSettings
    {
        public OpenAllPreferences OpenAll { get; set; } = new();
        public WindowLocationPreferences WindowLocation { get; set; } = new();
        public CompactViewPreferences CompactView { get; set; } = new();
        public BatchGroupPreferences BatchGroups { get; set; } = new();
    }

    public sealed class OpenAllPreferences
    {
        public bool Drawings { get; set; } = true;
        public bool Containers { get; set; }
        public bool Models { get; set; }
    }

    public sealed class WindowLocationPreferences
    {
        public bool HasValue { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
    }

    public sealed class CompactViewPreferences
    {
        public bool ButtonsHorizontal { get; set; }
    }

    public sealed class BatchGroupPreferences
    {
        public bool DrawingBatchExpanded { get; set; }
        public bool DrawingToolsExpanded { get; set; }
    }
}
