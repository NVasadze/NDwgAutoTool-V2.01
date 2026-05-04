using NDwgAutoTool.Models;

namespace NDwgAutoTool.Application.Abstractions
{
    public interface IBomRepository
    {
        IReadOnlyList<BomRow> GetRowsForDrawing(string filePath, string drawingPartNo);
    }
}
