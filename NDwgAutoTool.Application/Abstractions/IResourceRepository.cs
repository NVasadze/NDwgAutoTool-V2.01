namespace NDwgAutoTool.Application.Abstractions
{
    public interface IResourceRepository
    {
        string RootPath { get; }
        string WorkListFile { get; }
        string NotesFile { get; }
        string BomFile { get; }
        string Form3Folder { get; }
        string Form3Template { get; }
        string NoteBlockFolder { get; }
        IReadOnlyList<string> NoteBlockFiles { get; }
        void Refresh();
    }
}
