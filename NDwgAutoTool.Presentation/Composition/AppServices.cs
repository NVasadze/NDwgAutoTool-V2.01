using NDwgAutoTool.Application.Abstractions;
using NDwgAutoTool.Infrastructure.Repositories;
using NDwgAutoTool.Services;

namespace NDwgAutoTool.Composition
{
    public sealed class AppServices
    {
        public static AppServices Current { get; } = new();

        private AppServices()
        {
            Resources = ResourceRepository.Shared;
            ActiveDrawingProvider = new ActiveDrawingDocumentProvider();
            ResourceValidator = new ResourceValidator(Resources);
        }

        public IResourceRepository Resources { get; }
        public ActiveDrawingDocumentProvider ActiveDrawingProvider { get; }
        public ResourceValidator ResourceValidator { get; }
    }
}
