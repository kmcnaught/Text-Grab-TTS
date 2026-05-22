# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Windows-Only Application

This is a Windows-native .NET 10.0 WPF application. **Do not attempt to fully build or run on non-Windows platforms** — packaging tasks fail on Linux/macOS. Only dependency restoration and partial compilation validation are possible outside Windows.

## Build Commands

All commands run from the repo root. Build and test operations are slow — never cancel mid-run.

```bash
# Restore dependencies (~30s first run, ~2s cached)
dotnet restore Text-Grab.sln

# Build main project (~45s)
dotnet build Text-Grab/Text-Grab.csproj -c Release

# Run tests (~30s) — run before committing
dotnet test Tests/Tests.csproj

# Production multi-arch build (~3 min, Windows only)
.\build-unpackaged.ps1

# Run a single test class
dotnet test Tests/Tests.csproj --filter "ClassName=Text_Grab.Tests.OcrTests"
```

**WSL (full build via Windows dotnet.exe):**
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" restore Text-Grab.sln -p:EnableWindowsTargeting=true
"/mnt/c/Program Files/dotnet/dotnet.exe" build Text-Grab/Text-Grab.csproj -p:EnableWindowsTargeting=true 2>&1 | grep -E "error CS|^Build"
```

Note: when adding a new setting, also add it manually to `Properties/Settings.Designer.cs` — the code generator does not run on WSL.

**Non-Windows only** (validation, no full build):
```bash
dotnet restore Text-Grab.sln -p:EnableWindowsTargeting=true
```

## Architecture Overview

The solution has three projects:
- **Text-Grab/** — Main WPF application (`net10.0-windows10.0.22621.0`)
- **Text-Grab-Package/** — MSIX packaging project (`.wapproj`, Windows only)
- **Tests/** — xUnit test suite (references Text-Grab project directly)

### Application Modes

Four primary UI modes, each a separate WPF window in `Text-Grab/Views/`:
- `FullscreenGrab.xaml.cs` — overlays entire screen, user selects region for OCR
- `GrabFrame.xaml.cs` — transparent persistent frame positioned over live content
- `EditTextWindow.xaml.cs` — Notepad-like text editor with OCR capture and manipulation tools
- `QuickSimpleLookup.xaml.cs` — fast hotkey-activated custom text dictionary

### Key Layers

**`Text-Grab/Utilities/`** — Core logic, mostly static classes:
- `OcrUtilities.cs` — image-to-text pipeline; wraps Windows.Media.Ocr, Tesseract, and Windows AI
- `ImageMethods.cs` — screenshot capture, image preprocessing for OCR
- `StringMethods.cs` — text manipulation used by EditTextWindow
- `WindowUtilities.cs` — window management, launching modes
- `PostGrabActionManager.cs` — configurable post-capture actions pipeline
- `GrabTemplateExecutor/Manager.cs` — template-based automated capture sequences

**`Text-Grab/Services/`** — Singleton-style services:
- `SettingsService.cs` — hybrid settings: classic `.settings` file + `ApplicationDataContainer` (packaged) + JSON files for complex list-type settings (RegexList, ShortcutKeySets, PostGrabActions, etc.)
- `HistoryService.cs` — persists OCR capture history
- `LanguageService.cs` — resolves available OCR languages
- `CalculationService.cs` — math/unit/datetime expression evaluation (partial classes)

**`Text-Grab/Models/`** — Data transfer objects. Key ones:
- `OcrOutput.cs` / `OcrLinesWords.cs` — normalized OCR result structure
- `ResultTable.cs` — tabular OCR results
- `GrabTemplate.cs` / `TemplateRegion.cs` — template capture definitions
- `PostGrabContext.cs` — context passed through post-grab action pipeline

**`Text-Grab/Controls/`** — Reusable WPF controls including `WordBorder` (clickable OCR word overlay).

**`Text-Grab/UndoRedoOperations/`** — Undo/redo infrastructure for the GrabFrame word border editing.

### OCR Engine Abstraction

Multiple OCR engines are unified via `ILanguage` interface (`Text-Grab/Interfaces/ILanguage.cs`) with implementations:
- `WinRtOcrLinesWords` — Windows.Media.Ocr (built-in, primary)
- `WinAiOcrLinesWords` — Windows AI (newer, optional)
- `TessLang` — Tesseract (optional, downloaded separately)
- `UiAutomationLang` — UI Automation API (screen readers path)

### Settings Architecture

`SettingsService` reads/writes settings in two tiers:
1. **Classic settings** (`Properties.Settings`) — simple key-value pairs persisted to `user.config`
2. **Managed JSON files** — complex list settings stored as JSON files in a `settings-data/` folder alongside the executable (or in `ApplicationData` when packaged)

`AppUtilities.TextGrabSettings` provides the global `Properties.Settings.Default` instance used throughout the app.

## Code Style

Enforced via `.editorconfig`:
- 4-space indentation, CRLF line endings
- Explicit types preferred over `var`
- File-scoped namespaces (`namespace Foo.Bar;`)
- Allman brace style (`csharp_new_line_before_open_brace = all`)
- Private fields use `_camelCase` prefix
- Interfaces prefixed with `I`

## Known Expected Warnings

These are intentional and should not be "fixed":
- `CS0162` unreachable code in `WindowsAiUtilities.cs` — platform-conditional paths
- `WFO0003` high DPI manifest warning — legacy compatibility requirement
