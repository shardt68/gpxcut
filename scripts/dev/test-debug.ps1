param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot

Write-Host "Running tests in $Configuration..."
dotnet test "GpxCut.sln" -c $Configuration --no-build

Write-Host "Tests completed."