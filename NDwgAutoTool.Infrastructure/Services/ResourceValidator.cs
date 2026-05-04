using NDwgAutoTool.Application.Abstractions;
using NDwgAutoTool.Infrastructure.Repositories;

namespace NDwgAutoTool.Services
{
    public sealed class ResourceValidator
    {
        private readonly IResourceRepository _resources;

        public ResourceValidator()
            : this(ResourceRepository.Shared)
        {
        }

        public ResourceValidator(IResourceRepository resources)
        {
            _resources = resources;
        }

        public ResourceAvailabilityReport Validate()
        {
            var report = new ResourceAvailabilityReport();

            Check(report, "Root", () => _resources.RootPath);
            Check(report, "WORK_LIST workbook", () => _resources.WorkListFile);
            Check(report, "N-DWG notes workbook", () => _resources.NotesFile);
            Check(report, "BOM workbook", () => _resources.BomFile);
            Check(report, "FORM 3 folder", () => _resources.Form3Folder);
            Check(report, "Form 3 template", () => _resources.Form3Template);
            Check(report, "Note block folder", () => _resources.NoteBlockFolder);
            Check(report, "Note blocks", () => _resources.NoteBlockFiles.Count + " file(s)");

            return report;
        }

        private static void Check(ResourceAvailabilityReport report, string label, Func<string> find)
        {
            try
            {
                string value = find();
                report.FoundItems.Add($"{label}: {value}");
            }
            catch (Exception ex)
            {
                report.MissingItems.Add($"{label}: {ex.Message}");
            }
        }
    }
}
