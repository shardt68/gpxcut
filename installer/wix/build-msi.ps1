param(
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0",
    [string]$LayoutRoot = "artifacts/installer-layout",
    [string]$OutputRoot = "artifacts/installer",
    [bool]$SignArtifacts = $false,
    [bool]$StrictRelease = $false,
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot

$layoutDir = Join-Path $repoRoot "$LayoutRoot/$Runtime"
if (!(Test-Path $layoutDir)) {
    throw "Installer layout not found: $layoutDir. Run scripts/release/create-installer-layout.ps1 first."
}

$outputDir = Join-Path $repoRoot "$OutputRoot/$Runtime"
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

$wxsPath = Join-Path $repoRoot "installer/wix/GpxCut.Setup.wxs"
$msiPath = Join-Path $outputDir "GpxCut-$Version-$Runtime.msi"

Write-Host "Building MSI..."
Write-Host "Input layout: $layoutDir"
Write-Host "Output MSI:  $msiPath"

if ($StrictRelease -and -not $SignArtifacts) {
    throw "StrictRelease=true requires signed artifacts. Re-run with -SignArtifacts `$true and valid signing settings."
}

# Requires WiX Toolset CLI (wix.exe) in PATH.
wix build $wxsPath -arch x64 -d PublishDir=$layoutDir -d Version=$Version -out $msiPath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed with exit code $LASTEXITCODE."
}

if ($SignArtifacts) {
    $signScriptPath = Join-Path $repoRoot "scripts/release/sign-artifacts.ps1"
    if (!(Test-Path $signScriptPath)) {
        throw "Sign script not found: $signScriptPath"
    }

    $filesToSign = @(
        (Join-Path $layoutDir "GpxCut.App.exe"),
        $msiPath
    )

    & $signScriptPath -Files $filesToSign -PfxPath $PfxPath -PfxPassword $PfxPassword -TimestampUrl $TimestampUrl
}
else {
    Write-Warning "Artifacts were not signed (SignArtifacts=false). This is expected for internal/test builds."
}

Write-Host "MSI build completed."