# PSARC Importer

A standalone add-on for [The String Theory](https://github.com/AnthonySf/TheStringTheory) that converts Rocksmith 2014 `.psarc` files into `.theory` packages. Built with .NET 8 and [Rocksmith2014.NET](https://github.com/iminashi/Rocksmith2014.NET).

## Features

- Convert single `.psarc` files or batch-convert entire directories
- Cross-platform GUI (Avalonia) with folder pickers and progress display
- CLI for automation and integration with The String Theory
- Automatic output naming from song metadata (`{Artist} - {Title}.theory`)
- Skip existing files and detect duplicates by metadata in batch mode

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (required to build the net8.0 target; includes net8.0 targeting packs)
- Git (with LFS support recommended for large .psarc test files)

## Clone & Build

```bash
# Clone with submodules
git clone --recurse-submodules https://github.com/odejesush/PsarcImporter.git
cd PsarcImporter

# If you already cloned without --recurse-submodules:
# git submodule update --init --recursive

# Restore and build (NuGet audit is disabled via Directory.Build.props)
dotnet restore src/PsarcImporter/PsarcImporter.csproj /p:NuGetAudit=false
dotnet build src/PsarcImporter/PsarcImporter.csproj /p:NuGetAudit=false
```

The submodule `lib/Rocksmith2014.NET` is pinned at tag `v3.5.0`.

## Publish

```bash
# Windows
dotnet publish src/PsarcImporter/PsarcImporter.csproj -c Release -r win-x64 --self-contained -o publish/win-x64 /p:NuGetAudit=false

# macOS (Intel)
dotnet publish src/PsarcImporter/PsarcImporter.csproj -c Release -r osx-x64 --self-contained -o publish/osx-x64 /p:NuGetAudit=false

# macOS (Apple Silicon)
dotnet publish src/PsarcImporter/PsarcImporter.csproj -c Release -r osx-arm64 --self-contained -o publish/osx-arm64 /p:NuGetAudit=false
```

## Installing in The String Theory

1. **Build** the importer for your platform (see [Publish](#publish) above).
2. **Copy** the published output folder into the game's importers directory:

   | Platform | Install Path |
   |----------|-------------|
   | Windows | `%LOCALAPPDATA%\StringTheory\StringTheory\Importers\psarc-importer\` |
   | macOS   | `~/Library/Application Support/StringTheory/Importers/psarc-importer/` |

3. Verify the folder contains:
   - `importer.json` (add-on manifest)
   - `PsarcImporter.exe` (Windows) or `PsarcImporter` (macOS)
   - All required `.dll` and runtime files

4. Launch The String Theory — the add-on is auto-discovered and registered for `.psarc` files.

## CLI Usage

### Single File Mode

```bash
PsarcImporter --source "D:\dlc\Song.psarc" --output "C:\Songs\Song.theory"
```

### Batch Directory Mode

Point `--source` at a directory to convert all `.psarc` files in it:

```bash
PsarcImporter --source "D:\Games\Rocksmith 2014 Edition - Remastered\dlc" --output "C:\Users\You\AppData\LocalLow\StringTheory\StringTheory\Songs"
```

Output filenames are derived from song metadata (`{Artist} - {Title}.theory`). Existing files are skipped.

### Arguments

| Argument | Description |
|----------|-------------|
| `--source {path}` | Path to `.psarc` file or directory containing `.psarc` files (required) |
| `--output {path}` | Path to write `.theory` package, or output directory in batch mode (required) |
| `--work {path}` | Temporary working directory (optional, defaults to `%TEMP%\PsarcImporter_<GUID>`) |
| `--force` | Skip metadata duplicate detection in batch mode (optional) |

Exit codes: `0` on success, `1` on failure, `2` for invalid arguments.

**Batch mode** automatically detects duplicate songs by reading `manifest.json` from existing `.theory` files in the output directory. Songs with matching title and artist are skipped. Use `--force` to bypass this check and re-import duplicates. Failed files are listed by name with error messages after the batch summary.

## GUI

A cross-platform GUI is available in the `src/PsarcImporter.Gui` project:

```bash
# Build and run the GUI
dotnet run --project src/PsarcImporter.Gui/PsarcImporter.Gui.csproj

# Publish GUI for Windows
dotnet publish src/PsarcImporter.Gui/PsarcImporter.Gui.csproj -c Release -r win-x64 --self-contained -o publish/gui-win-x64 /p:NuGetAudit=false
```

The GUI provides:
- Folder browser pickers for source and output directories
- Checkbox list of detected `.psarc` files
- Progress bar and status messages during conversion
- Select All / Deselect All controls

## Project Structure

```
PsarcImporter/
├── Directory.Build.props       # Disables NuGet audit warnings
├── lib/
│   └── Rocksmith2014.NET/      # Git submodule (v3.5.0)
├── src/
│   ├── PsarcImporter/
│   │   ├── importer.json        # Add-on manifest
│   │   ├── Program.cs           # Entry point & CLI parsing
│   │   ├── PsarcConverter.cs    # Orchestrator
│   │   ├── TheoryPackageWriter.cs # .theory ZIP writer + models
│   │   ├── Conversion/
│   │   │   ├── ArtworkConverter.cs   # DDS → PNG (raw encoder)
│   │   │   ├── AudioConverter.cs     # WEM → OGG
│   │   │   ├── MacWemAudioConverter.cs # macOS WEM decoder
│   │   │   └── ToneExtractor.cs      # Tone definition extraction
│   │   └── Models/
│   │       ├── ArrangementBuilder.cs # Variant building
│   │       ├── CachedModels.cs       # Cached song format models
│   │       ├── NoteBuilder.cs        # Note conversion logic
│   │       ├── ParsedArrangement.cs  # Arrangement context models
│   │       └── TimingExporter.cs     # Timing data extraction
│   └── PsarcImporter.Gui/       # Avalonia GUI front-end
│       ├── PsarcImporter.Gui.csproj
│       ├── Program.cs
│       ├── App.axaml
│       ├── ViewModels/
│       │   └── MainWindowViewModel.cs
│       └── Views/
│           ├── MainWindow.axaml
│           └── MainWindow.axaml.cs
├── tests/
│   └── PsarcImporter.Tests/     # xUnit tests
└── README.md
```

## License

Subject to the same license terms as Rocksmith2014.NET (LGPL-3.0).
