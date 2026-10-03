param(
    [string]$Root = "",
    [switch]$CanonicalWorkspace
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path $PSScriptRoot ".."
}
$Root = [System.IO.Path]::GetFullPath($Root)

$TextExtensions = @(".cs", ".ps1", ".md", ".txt", ".json", ".sh", ".xml", ".cfg")
$Self = [System.IO.Path]::GetFullPath($MyInvocation.MyCommand.Path)

# Construct local-only markers so this scanner does not report its own deny-list as findings.
$Patterns = @(
    "password" + "=",
    "passwd" + "=",
    "secret" + "=",
    "api" + "_key",
    "api" + "-key",
    "github" + "_pat_",
    "ghp" + "_",
    "Bearer" + " ",
    "ai" + "_share",
    "192" + ".168.",
    "deploy" + "-from-mac",
    "sync" + "-to-windows",
    "F:" + "\mod-work",
    "C:" + "\Users\",
    "ssh" + " ",
    "scp" + " "
)

$Findings = New-Object System.Collections.Generic.List[string]
$LocalOnlyFiles = @("install-to-canonical.ps1")

$Files = Get-ChildItem -LiteralPath $Root -Recurse -File | Where-Object {
    $FullName = [System.IO.Path]::GetFullPath($_.FullName)
    $Relative = $FullName.Substring($Root.Length).TrimStart([char[]]@('\','/'))
    $RelativeNormalized = $Relative.Replace('\','/')

    $IsGenerated =
        $RelativeNormalized -match '(^|/)(build|bin|obj|Logs|Reports)(/|$)' -or
        $RelativeNormalized -match '^dist/out(/|$)' -or
        $RelativeNormalized -match '(^|/)\.git(/|$)'

    $IsCanonicalLocalOnly = $CanonicalWorkspace -and ($LocalOnlyFiles -contains $RelativeNormalized)

    $TextExtensions -contains $_.Extension.ToLowerInvariant() -and
    $FullName -ne $Self -and
    -not $IsGenerated -and
    -not $IsCanonicalLocalOnly
}

foreach ($File in $Files) {
    $Lines = Get-Content -LiteralPath $File.FullName -ErrorAction Stop
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $Line = [string]$Lines[$i]
        foreach ($Pattern in $Patterns) {
            if ($Line.IndexOf($Pattern, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $Relative = $File.FullName.Substring($Root.Length).TrimStart([char[]]@('\','/'))
                $Findings.Add(("{0}:{1}: {2}" -f $Relative, ($i + 1), $Pattern))
            }
        }
    }
}

if (-not $CanonicalWorkspace) {
    $ForbiddenDirectories = @("build", "bin", "obj", "Logs", "Reports")
    foreach ($Name in $ForbiddenDirectories) {
        $Candidate = Join-Path $Root $Name
        if (Test-Path -LiteralPath $Candidate) {
            $Findings.Add("forbidden directory present: $Name")
        }
    }

    foreach ($Relative in $LocalOnlyFiles) {
        $Candidate = Join-Path $Root $Relative
        if (Test-Path -LiteralPath $Candidate -PathType Leaf) {
            $Findings.Add("local-only file present: $Relative")
        }
    }
}

if ($Findings.Count -gt 0) {
    Write-Host "PUBLIC SOURCE AUDIT: FAIL"
    $Findings | Sort-Object -Unique | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "PUBLIC SOURCE AUDIT: PASS"
Write-Host "Root: $Root"
if ($CanonicalWorkspace) {
    Write-Host "Mode: canonical workspace projection (generated/local-only files excluded)"
}
else {
    Write-Host "Mode: public source tree"
}
Write-Host "Text files checked: $($Files.Count)"
exit 0
