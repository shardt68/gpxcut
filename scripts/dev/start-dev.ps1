param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot

if (-not $SkipBuild) {
    Write-Host "Building solution in $Configuration..."
    dotnet build "GpxCut.sln" -c $Configuration
}

if (-not $SkipTests) {
    Write-Host "Running tests in $Configuration..."
    if ($SkipBuild) {
        dotnet test "GpxCut.sln" -c $Configuration
    }
    else {
        dotnet test "GpxCut.sln" -c $Configuration --no-build
    }
}

Write-Host "Starting GpxCut.App ($Configuration)..."
dotnet run --project "src/GpxCut.App/GpxCut.App.csproj" -c $Configuration
