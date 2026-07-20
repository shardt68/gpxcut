param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "Building solution in $Configuration..."
dotnet build -c $Configuration

Write-Host "Running tests..."
dotnet test -c $Configuration --no-build

Write-Host "Validation completed."
