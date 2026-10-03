param(
    [switch]$Deploy,
    [string]$GameRoot = "",
    [string]$Compiler = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = $env:STRANDED_DEEP_GAME_ROOT
}

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    throw "GameRoot is required. Pass -GameRoot '<Stranded Deep directory>' or set STRANDED_DEEP_GAME_ROOT."
}

$GameRoot = [System.IO.Path]::GetFullPath($GameRoot)
$Managed = Join-Path $GameRoot "Stranded_Deep_Data\Managed"
$BepInExCore = Join-Path $GameRoot "BepInEx\core"
$Plugins = Join-Path $GameRoot "BepInEx\plugins"

if (-not (Test-Path (Join-Path $Managed "Assembly-CSharp.dll"))) {
    throw "Invalid Stranded Deep game root; Assembly-CSharp.dll not found under: $Managed"
}
if (-not (Test-Path (Join-Path $BepInExCore "BepInEx.dll"))) {
    throw "BepInEx core not found under: $BepInExCore"
}

if ([string]::IsNullOrWhiteSpace($Compiler)) {
    $Framework64 = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    $Framework32 = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
    if (Test-Path $Framework64) {
        $Compiler = $Framework64
    }
    elseif (Test-Path $Framework32) {
        $Compiler = $Framework32
    }
}

if ([string]::IsNullOrWhiteSpace($Compiler) -or -not (Test-Path $Compiler)) {
    throw "C# compiler not found. Pass -Compiler '<path to csc.exe>'."
}

$BuildDir = Join-Path $PSScriptRoot "build"
$BuildOutput = Join-Path $BuildDir "StrandedDeepDiagnostics.dll"
$PluginDir = Join-Path $Plugins "StrandedDeepDiagnostics"
$RuntimeOutput = Join-Path $PluginDir "StrandedDeepDiagnostics.dll"

New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

$Sources = @(
    Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -Filter "*.cs" |
        Where-Object { $_.FullName -notlike "$BuildDir*" } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName }
)

if ($Sources.Count -eq 0) {
    throw "No C# sources found under $PSScriptRoot"
}

$References = New-Object System.Collections.Generic.List[string]

$HarmonyDll = Join-Path $BepInExCore "0Harmony.dll"
if (-not (Test-Path $HarmonyDll)) {
    $HarmonyCandidate = Get-ChildItem -Path (Join-Path $GameRoot "BepInEx") -Filter "0Harmony.dll" -File -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($HarmonyCandidate) {
        $HarmonyDll = $HarmonyCandidate.FullName
    }
}

$RequiredReferences = @(
    (Join-Path $BepInExCore "BepInEx.dll"),
    $HarmonyDll,
    (Join-Path $Managed "Assembly-CSharp.dll"),
    (Join-Path $Managed "bolt.dll"),
    (Join-Path $Managed "bolt.user.dll"),
    (Join-Path $Managed "Rewired_Core.dll")
)

foreach ($Required in $RequiredReferences) {
    if (-not (Test-Path $Required)) {
        throw "Required assembly not found: $Required"
    }
    $References.Add($Required)
}

Get-ChildItem -Path $Managed -Filter "UnityEngine*.dll" |
    Sort-Object Name |
    ForEach-Object { $References.Add($_.FullName) }

$Args = New-Object System.Collections.Generic.List[string]
$Args.Add("/nologo")
$Args.Add("/target:library")
$Args.Add("/optimize+")
$Args.Add("/debug:pdbonly")
$Args.Add("/langversion:5")
$Args.Add("/out:$BuildOutput")

foreach ($Reference in $References) { $Args.Add("/reference:$Reference") }
foreach ($Source in $Sources) { $Args.Add($Source) }

Write-Host "StrandedDeepDiagnostics v1.0.0-rc1 Public RC"
Write-Host "Compiler:   $Compiler"
Write-Host "Game root:  $GameRoot"
Write-Host "Workspace:  $PSScriptRoot"
Write-Host "Output:     $BuildOutput"
Write-Host "Sources:    $($Sources.Count)"
Write-Host "References: $($References.Count)"
Write-Host ""

& $Compiler $Args.ToArray()
if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $BuildOutput)) {
    throw "Build reported success but DLL was not created: $BuildOutput"
}

$BuildHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $BuildOutput).Hash
Write-Host "Build OK"
Write-Host "SHA256: $BuildHash"
Write-Host ""

if ($Deploy) {
    New-Item -ItemType Directory -Force -Path $PluginDir | Out-Null
    Copy-Item -LiteralPath $BuildOutput -Destination $RuntimeOutput -Force

    # Keep the installed runtime self-documenting. Distribution packages are
    # built from this known-good runtime folder, so these public documents
    # are included in the GitHub release install ZIP as well.
    $RuntimeDocs = @(
        "README.md",
        "USER_GUIDE.md",
        "USER_GUIDE_RU.md",
        "LICENSE"
    )
    foreach ($DocName in $RuntimeDocs) {
        $DocSource = Join-Path $PSScriptRoot $DocName
        if (-not (Test-Path -LiteralPath $DocSource -PathType Leaf)) {
            throw "Required runtime documentation missing: $DocSource"
        }
        Copy-Item -LiteralPath $DocSource -Destination (Join-Path $PluginDir $DocName) -Force
    }

    if (-not (Test-Path $RuntimeOutput)) {
        throw "Deploy failed: runtime DLL missing: $RuntimeOutput"
    }

    $RuntimeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $RuntimeOutput).Hash
    if ($RuntimeHash -ne $BuildHash) {
        throw "Deploy hash mismatch. Build=$BuildHash Runtime=$RuntimeHash"
    }

    Write-Host "Deploy OK"
    Write-Host "Runtime: $RuntimeOutput"
    Write-Host "Runtime SHA256: $RuntimeHash"
    Write-Host ""
    Write-Host "v1.0.0-rc1 public-RC focused check:"
    Write-Host "  1. F8 opens Diagnostics and module cycling remains functional."
    Write-Host "  2. Overlay shows CAPS AVAILABLE/PARTIAL/UNAVAILABLE for the active module."
    Write-Host "  3. F12 creates reports under BepInEx\config\StrandedDeepDiagnostics\Reports by default."
    Write-Host "  4. AUDIO/RAFT optional BamEx adapters may be unavailable without breaking the core."
    Write-Host "  5. F8 OFF and scene/world unload disable active traces/recorders."
    Write-Host "  6. No gameplay/save/physics mutation is introduced by the RC metadata pass."
    Write-Host "  7. Run tools\verify-rc-source.ps1 and Distribution verify before publication."
}
else {
    Write-Host "Not deployed. Re-run with -Deploy to copy the DLL into BepInEx\plugins."
}
