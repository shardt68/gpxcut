param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot

Write-Host "Building solution in $Configuration..."
dotnet build "GpxCut.sln" -c $Configuration

Write-Host "Build completed."