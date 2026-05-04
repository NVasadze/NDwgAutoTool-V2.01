namespace NDwgAutoTool.Application.Abstractions
{
    public interface INotesRepository
    {
        IReadOnlyList<string> GetRequiredNoteTokens(string filePath, string drawingPartNo);
        string GetLeaderedFlagCode(string filePath, string drawingPartNo);
    }
}
