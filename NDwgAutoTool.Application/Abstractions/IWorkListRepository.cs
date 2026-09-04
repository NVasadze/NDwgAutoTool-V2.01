using NDwgAutoTool.Models;

namespace NDwgAutoTool.Application.Abstractions
{
    public interface IWorkListRepository
    {
        string GetTitle(string filePath, string drawingPartNo);
        string GetProjectCode(string filePath);
        SignatureInfo GetSignature(string filePath, string drawingPartNo);
    }
}
