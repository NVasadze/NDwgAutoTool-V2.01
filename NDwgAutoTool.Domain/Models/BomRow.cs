namespace NDwgAutoTool.Models
{
    public class BomRow
    {
        public string PartNo { get; set; } = "";
        public string SubPartNo { get; set; } = "";
        public string Nomenclature { get; set; } = "";
        public int Quantity { get; set; }
        public string FlagNote { get; set; } = "";
        public string Note { get; set; } = "";
        public string FlagYesNo { get; set; } = "";
        public string NoteCode { get; set; } = "";
    }
}