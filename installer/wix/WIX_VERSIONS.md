# WiX Toolset Version Notes

## Current Setup

**GpxCut uses WiX Toolset v4 (modern CLI)**, not the legacy v3.

### Version Overview

| Version | Tool | CLI | MSI Schema | Status |
|---------|------|-----|------------|--------|
| **v3** (EOL) | MSBuild integrated | `candle.exe`, `heat.exe` | v3 | Legacy, not supported here |
| **v4+** (Current) | Standalone CLI | `wix build`, `wix harvest` | v4 | **Active in GpxCut** |
| **v5-v7** | Standalone CLI | Same as v4 | v4 | Newer releases, compatible |

## Current Project Configuration

### WiX Source File
- **Schema**: `xmlns="http://wixtoolset.org/schemas/v4/wxs"`
- **File**: `installer/wix/GpxCut.Setup.wxs`

### Build Command
```powershell
wix build $wxsPath -arch x64 -d PublishDir=$layoutDir -d Version=$Version -out $msiPath
```

This is the **modern v4+ CLI**, not legacy v3 tools.

## Why You Saw "6 vs 7"

The release workflow in [.github/workflows/release.yml](../../.github/workflows/release.yml) installs WiX with:

```powershell
dotnet tool install --tool-path ./.tools wix --version 6.*
```

### Why CI Pins 6.*
- GitHub Actions runners are clean environments.
- Pinning `6.*` makes release builds reproducible.
- The MSI command used by this project is compatible with WiX 6 and WiX 7.

### Why Local Machines Can Show an Error
- If WiX 7 is already installed and you run a command that forces `6.*`, you can get a downgrade/version conflict error.
- Typical message: requested version 6.x is lower than installed 7.x.

### What To Do Locally
- Check version: `wix --version`
- If local build works with your current version, keep it (WiX 7 is fine for this project).
- Do not force a downgrade unless you explicitly need parity testing with CI.

### Optional: Strict CI Parity on Local Machine
- Use the repository-local tool path (`./.tools/wix.exe`) and install exactly 6.* there.
- Alternatively, keep system-wide WiX 7 and let CI enforce 6.* only in the pipeline.

## FAQ: What About "wix eula accept wix7"?

The current README mentions:
```powershell
wix eula accept wix7
```

### Context
- This command is used when installing WiX Toolset v7 (or later versions that require EULA acceptance).
- It accepts the license agreement for that specific version.
- **This is optional and one-time only** — not required on every build.

### When to Run It
- First time after installing WiX v7: `wix eula accept wix7`
- Subsequent builds: No need to run it again

### Why "v7"?
- The number (`v7`) is WiX Toolset's versioning scheme.
- If you have a different version installed (e.g., v5 or v6), adjust accordingly:
  - `wix eula accept wix5`
  - `wix eula accept wix6`
- To check your installed version: `wix --version`

## Migration Path (If Needed)

If you were previously on **WiX v3**, the key differences:

| Aspect | WiX v3 | WiX v4+ |
|--------|--------|---------|
| Installation | Visual Studio extension or standalone | Standalone CLI (`winget install wixtoolset`) |
| Build method | MSBuild integration (`.wixproj`) | CLI: `wix build` |
| Syntax | v3 XML schema | v4 XML schema (backward compatible for basics) |
| EULA | Automatic during install | `wix eula accept` command required |

GpxCut **only uses v4+ features** (modern schema, CLI build), so v3 is not an option.

## Setup Instructions

See [installer/wix/README.md](README.md) for build workflow and MSI generation steps.

For development environment setup (including WiX installation), see [DEVELOPMENT_SETUP.md](../../DEVELOPMENT_SETUP.md).
