param(
    [string]$Root = "",
    [switch]$PublicSource
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path $PSScriptRoot ".."
}
$Root = [System.IO.Path]::GetFullPath($Root)

$ExpectedVersion = "1.0.0-rc1"
$ExpectedTag = "v1.0.0-rc1"
$ExpectedRepository = "stranded-deep-diagnostics"
$ExpectedGuid = "com.bamex.strandeddeep.diagnostics"

function Require-File([string]$Relative) {
    $Path = Join-Path $Root $Relative
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file missing: $Relative"
    }
    return $Path
}

$Constants = Require-File "Core\DiagnosticsConstants.cs"
$License = Require-File "LICENSE"
$Distribution = Require-File "dist\distribution.json"
$Readme = Require-File "README.md"
$UserGuide = Require-File "USER_GUIDE.md"
$UserGuideRu = Require-File "USER_GUIDE_RU.md"
$Audit = Require-File "tools\public-source-audit.ps1"

$ConstantsText = [System.IO.File]::ReadAllText($Constants)
if ($ConstantsText -notmatch ('PluginVersion\s*=\s*"' + [regex]::Escape($ExpectedVersion) + '"')) {
    throw "PluginVersion is not $ExpectedVersion"
}

$Dist = Get-Content -LiteralPath $Distribution -Raw | ConvertFrom-Json
if ([string]$Dist.version -ne $ExpectedVersion) { throw "distribution.json version mismatch" }
if ([string]$Dist.publishing.tag -ne $ExpectedTag) { throw "distribution.json tag mismatch" }
if ([string]$Dist.publishing.repository -ne $ExpectedRepository) { throw "distribution.json repository mismatch" }
if ([string]$Dist.publishing.releaseChannel -ne "prerelease") { throw "distribution.json releaseChannel must be prerelease for rc1" }
if ([string]$Dist.bepInExGuid -ne $ExpectedGuid) { throw "distribution.json bepInExGuid mismatch" }
if (@($Dist.packagePaths).Count -ne 1 -or [string]$Dist.packagePaths[0] -ne 'BepInEx/plugins/StrandedDeepDiagnostics') {
    throw "Unexpected packagePaths"
}
if (@($Dist.ownedPaths).Count -ne 1 -or [string]$Dist.ownedPaths[0] -ne 'BepInEx/plugins/StrandedDeepDiagnostics') {
    throw "Unexpected ownedPaths"
}

$ReadmeText = [System.IO.File]::ReadAllText($Readme)
if ($ReadmeText -notmatch 'USER_GUIDE\.md' -or $ReadmeText -notmatch 'USER_GUIDE_RU\.md') {
    throw "README does not link both usage guides"
}

$LicenseText = [System.IO.File]::ReadAllText($License)
if ($LicenseText -notmatch 'MIT License' -or $LicenseText -notmatch 'Copyright \(c\) 2026 BamEx') {
    throw "MIT license text/copyright not found"
}

if ($PublicSource) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Audit -Root $Root
}
else {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Audit -Root $Root -CanonicalWorkspace
}
if ($LASTEXITCODE -ne 0) {
    throw "Public source audit failed"
}

Write-Host "RC SOURCE VERIFY: PASS"
Write-Host "Version: $ExpectedVersion"
Write-Host "Repository: $ExpectedRepository"
Write-Host "License: MIT"
if ($PublicSource) {
    Write-Host "Audit mode: public source tree"
}
else {
    Write-Host "Audit mode: canonical workspace projection"
}
