# Development & Release Guide

## Automated Release Process

### GitHub Actions Workflow

**Location:** `.github/workflows/release.yml`

**Trigger:** Automatically runs when a Git tag matching `v*` is pushed

```bash
git tag -a v0.1.17 -m "Release notes here..."
git push origin main --tags
```

### What Happens Automatically

1. **Checkout & Setup** - Repository checkout with .NET 8
2. **Extract Version** - Extracts version from tag name (e.g., `v0.1.17` → `0.1.17`)
3. **Run Tests** - `dotnet test --configuration Release`
4. **Publish Release Build** 
   - Output: `artifacts/publish/win-x64/`
   - Command: `dotnet publish -c Release -r win-x64 --self-contained true`
5. **Create Installer Layout** - Copies publish output to `artifacts/installer-layout/win-x64/`
6. **Install WiX Toolset** - Version 6.*
7. **Build MSI Installer** 
   - Input: `installer/wix/GpxCut.Setup.wxs`
   - Output: `GpxCut-{VERSION}-win-x64.msi`
   - Location: `artifacts/installer/win-x64/`
8. **Create GitHub Release** (Automated!)
   - Title: `GpxCut {VERSION}`
   - Body: Includes installation instructions
   - Attachment: MSI file
   - Draft: `false`
   - Prerelease: `true` if tag contains `-` (e.g., `v0.1.17-alpha`)

### Release Process Checklist

1. Update `VERSION_HISTORY.md` with new version entry
   - Type, Added, Changed, Fixed, Notes sections
   - Format: German for existing entries, English for new
   - Include performance metrics if applicable

2. Commit version history
   ```bash
   git commit -m "docs: update VERSION_HISTORY for vX.Y.Z"
   ```

3. Create annotated tag
   ```bash
   git tag -a vX.Y.Z -m "Release message with highlights"
   git push origin main --tags
   ```

4. **Done!** GitHub Actions handles everything:
   - Tests run (Release config)
   - Installer builds
   - Release published on GitHub
   - Check: https://github.com/shardt68/gpxcut/releases

### No Manual Steps Required
- ❌ No `gh` CLI needed
- ❌ No manual GitHub Release creation
- ❌ No MSI upload needed
- ✅ All automated via GitHub Actions

---

## Local Development Setup

### Prerequisites
- **OS:** Windows 10/11 x64
- **.NET:** 8.0 SDK
- **IDE:** Visual Studio Code or Visual Studio 2022+
- **WebView2:** Runtime (usually pre-installed)

### Initial Setup

```bash
# Clone repository
git clone https://github.com/shardt68/gpxcut.git
cd gpxcut

# Restore dependencies
dotnet restore

# Build (Debug)
dotnet build

# Run tests
dotnet test

# Start development app
dotnet run --project src/GpxCut.App/GpxCut.App.csproj -c Debug
```

### Using Build Scripts

```powershell
# Start full dev environment
./scripts/dev/start-dev.ps1

# Build debug
./scripts/dev/build-debug.ps1

# Run with debug
./scripts/dev/run-app.ps1

# Test with debug
./scripts/dev/test-debug.ps1
```

### Project Structure
- **src/GpxCut.App/** - WPF application (MVVM, main UI)
- **src/GpxCut.Core/** - Domain logic, GPX I/O, editing commands
- **src/GpxCut.MapBridge/** - WebView2 interop, MapLibre integration
- **tests/GpxCut.Core.Tests/** - Unit tests
- **tests/GpxCut.Perf/** - Performance benchmarks
- **.github/skills/** - Agent skill definitions
- **installer/wix/** - Windows installer (WiX configuration)

### Performance Testing

Generate and test large GPX datasets:

```bash
# Generate synthetic test file (100k, 500k, or 1M points)
dotnet run --project tests/GpxCut.Perf -- generate 1000000 test-1m.gpx

# Run benchmark suite
./scripts/dev/benchmark-performance.ps1 -Mode full
```

**Performance Baselines (all within MVP 10s target):**
- 100k points: 1.7s
- 500k points: 4.4s
- 1M points: 7.5s

---

## Architecture Notes

- **MVVM Pattern:** WPF UI layer cleanly separated from business logic
- **Streaming GPX I/O:** Large files parsed chunk-by-chunk to manage memory
- **Progressive Rendering:** JavaScript renders in batches (10 scripts, 10ms delays) for 1M+ points
- **Chunk Accumulation:** MapLibre updates batched to prevent O(n²) memory allocations
- **Tile Caching:** Local HTTP cache for OSM tiles with configurable limits

See `.github/skills/gpxcut-track-editing/references/software-architecture.md` for detailed architecture documentation.
