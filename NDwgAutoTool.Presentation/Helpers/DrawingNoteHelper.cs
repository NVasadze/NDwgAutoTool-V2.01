using System.Text.RegularExpressions;
using NDwgAutoTool.Models;

namespace NDwgAutoTool.Helpers
{
    public static class DrawingNoteHelper
    {
        private static readonly Regex CalloutPattern =
            new Regex(@"^(?<part>[A-Z0-9\-]+) (?<qty>\d+)P$");

        private static readonly Regex SelfPartPattern =
            new Regex(@"^[A-Z]{4}\d{6}[A-Z]\d{4}$");

        public static void Classify(DrawingNoteInfo note)
        {
            note.IsCallout = false;
            note.IsFlagNote = false;
            note.IsSelfPartCallout = false;
            note.BasePartNo = "";
            note.FlagCode = "";

            string text = note.Text.Trim();

            if (SelfPartPattern.IsMatch(text))
            {
                note.IsSelfPartCallout = true;
                note.BasePartNo = text;
                return;
            }

            var match = CalloutPattern.Match(text);
            if (match.Success)
            {
                note.IsCallout = true;
                note.BasePartNo = match.Groups["part"].Value;
                return;
            }

            // Treat short single-token codes like B20, B63, A12, SE03, etc. as flagnote candidates.
            // This is intentionally broad for your workflow.
            if (!text.Contains(" ") &&
                !text.Contains("\n") &&
                text.Length >= 2 &&
                text.Length <= 12)
            {
                note.IsFlagNote = true;
                note.FlagCode = text;
            }
        }
    }
}