namespace NDwgAutoTool.Application.Abstractions
{
    public interface INoteBlockRepository
    {
        IReadOnlyList<string> GetOptionalBlockFileNames();
        IReadOnlyList<string> GetCharacteristicBlockFileNames();
        string GetBlockFileByName(string blockFileName);
        string FindMatchingBlockFile(string projectCode, IReadOnlyList<string> requiredCodes);
        IReadOnlyList<string> ExpandExcelNoteTokens(IEnumerable<string> rawTokens);
        void Refresh();
    }
}
