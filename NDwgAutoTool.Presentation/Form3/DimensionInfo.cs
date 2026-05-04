namespace NDwgAutoTool.Form3
{
    public class DimensionInfo
    {
        public string ViewName { get; set; } = "";
        public string FullName { get; set; } = "";

        public double X { get; set; }
        public double Y { get; set; }

        public double NominalMm { get; set; }
        public bool IsRef { get; set; }
        public string DisplayText { get; set; } = "";

        public string Location { get; set; } = "";
    }
}