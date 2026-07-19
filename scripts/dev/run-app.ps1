param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot

Write-Host "Starting GpxCut.App ($Configuration)..."
dotnet run --project "src/GpxCut.App/GpxCut.App.csproj" -c $Configuration