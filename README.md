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

V2.01 indexes the resource folder once and reuses the discovered file paths.
