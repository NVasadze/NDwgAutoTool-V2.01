# NDwgAutoTool V2.01

Open `NDwgAutoTool V2.01.sln` in Visual Studio.

## Project Layout

- `NDwgAutoTool.Domain/` - shared domain models and resource descriptors.
- `NDwgAutoTool.Application/` - repository/service contracts used by the app layer.
- `NDwgAutoTool.Infrastructure/` - cached file, workbook, note-block, BOM, notes, work-list, and resource repositories.
- `NDwgAutoTool.Presentation/` - WPF app, windows, shared theme, composition root, UI commands, and SolidWorks automation host code.

Dependency direction:

- `Presentation -> Application`
- `Presentation -> Infrastructure`
- `Infrastructure -> Application`
- `Application -> Domain`
- `Infrastructure -> Domain`

## Performance

V2.01 indexes the resource folder once and reuses the discovered file paths. WORK_LIST, N-DWG notes, BOM, and note-block lookups are cached and automatically refreshed when the source file timestamp changes.

## Resource Root

V2.01 does not read `NetworkRootPath.txt`. All shared resources are resolved under:

`U:\Vasadze\TEST`

## Publishing

`NDwgAutoTool.Presentation.csproj` is configured for `win-x64`, self-contained, single-file publish. Because the app uses SolidWorks and Office COM references, publish from Visual Studio or Visual Studio MSBuild instead of plain SDK `dotnet publish`.
