# PSARC Importer

A standalone add-on for [The String Theory](https://github.com/AnthonySf/TheStringTheory) that converts Rocksmith 2014 `.psarc` files into `.theory` packages. Built with .NET 8 and [Rocksmith2014.NET](https://github.com/iminashi/Rocksmith2014.NET).

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

## CLI Contract

The add-on accepts these arguments (passed by the host game):

| Argument | Description |
|----------|-------------|
| `--source {path}` | Path to `.psarc` file (required) |
| `--output {path}` | Path to write the `.theory` package (required) |
| `--work {path}` | Temporary working directory (optional, defaults to `%TEMP%\PsarcImporter_<GUID>`) |

Exit codes: `0` on success, non-zero on failure.

## Project Structure

```
PsarcImporter/
├── Directory.Build.props       # Disables NuGet audit warnings
├── lib/
│   └── Rocksmith2014.NET/      # Git submodule (v3.5.0)
├── src/
│   └── PsarcImporter/
│       ├── importer.json        # Add-on manifest
│       ├── Program.cs           # Entry point & CLI parsing
│       ├── PsarcConverter.cs    # Orchestrator
│       ├── TheoryPackageWriter.cs # .theory ZIP writer + models
│       ├── Conversion/
│       │   ├── ArtworkConverter.cs   # DDS → PNG (raw encoder)
│       │   ├── AudioConverter.cs     # WEM → OGG
│       │   ├── MacWemAudioConverter.cs # macOS WEM decoder
│       │   └── ToneExtractor.cs      # Tone definition extraction
│       └── Models/
│           ├── ArrangementBuilder.cs # Variant building
│           ├── CachedModels.cs       # Cached song format models
│           ├── NoteBuilder.cs        # Note conversion logic
│           ├── ParsedArrangement.cs  # Arrangement context models
│           └── TimingExporter.cs     # Timing data extraction
└── README.md
```

## License

Subject to the same license terms as Rocksmith2014.NET (LGPL-3.0).