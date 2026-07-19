#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Performance testing script for GpxCut with various dataset sizes.
    
.DESCRIPTION
    Generates test GPX files and runs benchmarks for:
    - 100,000 points
    - 500,000 points
    - 1,000,000 points
    
    Results are logged and can be used to track performance regressions.

.EXAMPLE
    .\scripts\dev\benchmark-performance.ps1
    
.NOTES
    Requires: dotnet SDK 8+
#>

param(
    [ValidateSet('quick', 'full', 'clean')]
    [string]$Mode = 'full',
    
    [string]$OutputDir = 'perf-results'
)

$ErrorActionPreference = 'Stop'

# Ensure we're in the project root
if (!(Test-Path './GpxCut.sln')) {
    Write-Error "This script must be run from the project root directory"
    exit 1
}

# Create output directory
$null = New-Item -ItemType Directory -Path $OutputDir -Force

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logFile = Join-Path $OutputDir "benchmark-$timestamp.log"

function Write-Log {
    param([string]$Message)
    $msg = "[$(Get-Date -Format 'HH:mm:ss')] $Message"
    Write-Host $msg
    Add-Content -Path $logFile -Value $msg
}

Write-Log "=== GpxCut Performance Benchmark ==="
Write-Log "Mode: $Mode"
Write-Log ""

# Define test configurations
$tests = @(
    @{ Points = 100000; Name = '100k' }
    @{ Points = 500000; Name = '500k' }
    @{ Points = 1000000; Name = '1M' }
)

if ($Mode -eq 'quick') {
    $tests = @($tests[0])
}

try {
    # Build first
    Write-Log "Building GpxCut.Perf..."
    $buildOutput = dotnet build tests/GpxCut.Perf/GpxCut.Perf.csproj -c Release 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Log "❌ Build failed"
        Write-Log $buildOutput
        exit 1
    }
    Write-Log "✓ Build successful"
    Write-Log ""

    foreach ($test in $tests) {
        $gpxPath = "test-$($test.Name).gpx"
        
        Write-Log "Generating $($test.Name) test file ($($test.Points) points)..."
        $genOutput = dotnet run --project tests/GpxCut.Perf -c Release -- generate $test.Points $gpxPath 2>&1
        Write-Log $genOutput
        
        $fileSize = (Get-Item $gpxPath).Length / 1MB
        Write-Log "File size: $($fileSize.ToString('F2')) MB"
        Write-Log ""
        
        Write-Log "Running benchmark for $($test.Name)..."
        $benchOutput = dotnet run --project tests/GpxCut.Perf -c Release -- benchmark $gpxPath 2>&1
        Write-Log $benchOutput
        Write-Log ""
        
        if ($Mode -eq 'full') {
            # Keep the file for manual testing
            Write-Log "Test file retained: $gpxPath"
        } else {
            # Clean up in quick mode
            Remove-Item $gpxPath -Force
        }
        
        Write-Log "---"
        Write-Log ""
    }
    
    Write-Log "✓ All benchmarks completed"
    Write-Log "Results saved to: $logFile"
}
catch {
    Write-Log "❌ Error: $_"
    exit 1
}

if ($Mode -eq 'clean') {
    Write-Log "Cleaning up test files..."
    Get-Item test-*.gpx -ErrorAction SilentlyContinue | Remove-Item -Force
    Write-Log "✓ Cleanup complete"
}
