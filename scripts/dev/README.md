# Development Scripts

This directory contains PowerShell scripts for development and testing workflows.

## Available Scripts

### start-dev.ps1
Starts the development environment with build and tests.

```powershell
# Full development start (build + test + run)
.\scripts\dev\start-dev.ps1

# Skip build and tests
.\scripts\dev\start-dev.ps1 -SkipBuild -SkipTests

# Use Release configuration
.\scripts\dev\start-dev.ps1 -Configuration Release
```

### run-app.ps1
Runs the GpxCut app directly without rebuilding.

```powershell
.\scripts\dev\run-app.ps1
.\scripts\dev\run-app.ps1 -Configuration Release
```

### test-debug.ps1
Runs the test suite with debug configuration.

```powershell
.\scripts\dev\test-debug.ps1
```

### build-debug.ps1
Builds the entire solution in Debug configuration.

```powershell
.\scripts\dev\build-debug.ps1
```

### benchmark-performance.ps1
Generates test GPX files and runs performance benchmarks against different dataset sizes.

**Modes:**
- `quick` - Test only 100k points (fastest)
- `full` - Test 100k, 500k, and 1M points (default)
- `clean` - Run full benchmark and clean up test files

```powershell
# Full benchmark (generates test files and logs results)
.\scripts\dev\benchmark-performance.ps1

# Quick 100k test only
.\scripts\dev\benchmark-performance.ps1 -Mode quick

# Benchmark and clean up
.\scripts\dev\benchmark-performance.ps1 -Mode clean

# Custom output directory for results
.\scripts\dev\benchmark-performance.ps1 -OutputDir ./my-results
```

**Results:**
Benchmark results are logged to `perf-results/benchmark-YYYYMMDD-HHMMSS.log`

Test GPX files are retained in the project root for manual testing unless `-Mode clean` is used.

## Performance Test Generator

The performance test tool can also be run directly:

```powershell
# Generate a test file
dotnet run --project tests/GpxCut.Perf -- generate 500000 mytrack.gpx

# Benchmark an existing file
dotnet run --project tests/GpxCut.Perf -- benchmark mytrack.gpx

# Legacy usage (backward compatible)
dotnet run --project tests/GpxCut.Perf -- mytrack.gpx
```

### Generate Command
```
generate <point-count> <output-path> [segment-count]
```

Examples:
- `generate 100000 test-100k.gpx` - Single segment with 100k points
- `generate 500000 test-500k.gpx 5` - 5 segments with 500k total points
- `generate 1000000 test-1m.gpx` - Single segment with 1M points

### Benchmark Command
```
benchmark <path-to-gpx>
```

Outputs:
- Parse time
- Total points and segments
- Bounds (min/max lat/lon)
- Script generation time and chunk count
- Overall throughput (points/sec)

## Notes

- All scripts use PowerShell Core (pwsh)
- Release builds are faster for performance testing
- Test GPX files use deterministic random generation (seed=42) for reproducibility
- Large files (1M points) may take several seconds to generate and parse
