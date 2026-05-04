using SldWorksInterop = SldWorks;

namespace NDwgAutoTool.Models
{
    public class DrawingNoteInfo
    {
        public string Text { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsVertical { get; set; }

        public bool IsCallout { get; set; }
        public bool IsFlagNote { get; set; }
        public bool IsSelfPartCallout { get; set; }

        public string BasePartNo { get; set; } = "";
        public string FlagCode { get; set; } = "";

        public bool HasLeader { get; set; }

        public SldWorksInterop.Note? NoteObject { get; set; }
        public SldWorksInterop.Annotation? AnnotationObject { get; set; }

        public string ViewName { get; set; } = "";
    }
}