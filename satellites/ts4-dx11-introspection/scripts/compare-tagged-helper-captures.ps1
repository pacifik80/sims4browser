$ErrorActionPreference = 'Stop'

param(
    [Parameter(Mandatory = $true)]
    [string[]]$Target,

    [Parameter(Mandatory = $true)]
    [string[]]$Control,

    [ValidateSet('shader-daynight', 'generated-light', 'projection-reveal')]
    [string]$FamilyFocus,

    [string]$Output
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$captureRoot = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\captures\live'
$reportRoot = Join-Path $captureRoot 'reports'
$companionProject = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj'

function Expand-SessionArgs {
    param([string[]]$Values)

    foreach ($value in $Values) {
        foreach ($part in ($value -split ',')) {
            $trimmed = $part.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $trimmed
            }
        }
    }
}

function Resolve-CapturePath {
    param([string]$Value)

    if ([System.IO.Path]::IsPathRooted($Value)) {
        return (Resolve-Path $Value).Path
    }

    return (Resolve-Path (Join-Path $captureRoot $Value)).Path
}

function Read-ContextTags {
    param([string]$SessionPath)

    $contextPath = Join-Path $SessionPath 'context-tags.json'
    if (-not (Test-Path $contextPath)) {
        throw "Tagged helper compare requires context-tags.json: $SessionPath"
    }

    return Get-Content -Path $contextPath -Raw | ConvertFrom-Json
}

$targetResolved = Expand-SessionArgs $Target | ForEach-Object { Resolve-CapturePath $_ }
$controlResolved = Expand-SessionArgs $Control | ForEach-Object { Resolve-CapturePath $_ }

$targetTags = @()
foreach ($session in $targetResolved) {
    $tags = Read-ContextTags $session
    if ($FamilyFocus -and ($tags.family_focus -notcontains $FamilyFocus)) {
        throw "Target session does not match requested FamilyFocus '$FamilyFocus': $session"
    }
    $targetTags += $tags
}

$controlTags = @()
foreach ($session in $controlResolved) {
    $tags = Read-ContextTags $session
    if ($FamilyFocus -and ($tags.family_focus -notcontains $FamilyFocus)) {
        throw "Control session does not match requested FamilyFocus '$FamilyFocus': $session"
    }
    $controlTags += $tags
}

if (-not $FamilyFocus) {
    $focuses = @($targetTags[0].family_focus)
    if ($focuses.Count -ne 1) {
        throw 'When Target sessions carry multiple family_focus values, specify -FamilyFocus explicitly.'
    }
    $FamilyFocus = [string]$focuses[0]
}

New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($Output)) {
    $targetName = [System.IO.Path]::GetFileName($targetResolved[0])
    $controlName = [System.IO.Path]::GetFileName($controlResolved[0])
    $Output = Join-Path $reportRoot ("tagged-{0}-{1}-vs-{2}.md" -f $FamilyFocus, $targetName, $controlName)
}
else {
    $Output = [System.IO.Path]::GetFullPath($Output)
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($Output)) -Force | Out-Null
}

$useGroupCompare = $targetResolved.Count -gt 1 -or $controlResolved.Count -gt 1
if ($useGroupCompare) {
    $arguments = @('run', '--project', $companionProject, '--', 'compare-groups')
    foreach ($path in $targetResolved) {
        $arguments += @('--left', $path)
    }
    foreach ($path in $controlResolved) {
        $arguments += @('--right', $path)
    }
    $arguments += @('--output', $Output)
}
else {
    $arguments = @('run', '--project', $companionProject, '--', 'compare', '--left', $targetResolved[0], '--right', $controlResolved[0], '--output', $Output)
}

Write-Host "Helper-family focus:"
Write-Host "  $FamilyFocus"
Write-Host "Target sessions:"
for ($i = 0; $i -lt $targetResolved.Count; $i++) {
    $tag = $targetTags[$i]
    Write-Host ("  {0} | {1} | {2}" -f $targetResolved[$i], $tag.world_mode, $tag.scene_label)
}
Write-Host "Control sessions:"
for ($i = 0; $i -lt $controlResolved.Count; $i++) {
    $tag = $controlTags[$i]
    Write-Host ("  {0} | {1} | {2}" -f $controlResolved[$i], $tag.world_mode, $tag.scene_label)
}
Write-Host "Writing report:"
Write-Host "  $Output"

dotnet @arguments

Write-Host ""
Write-Host "Done."
