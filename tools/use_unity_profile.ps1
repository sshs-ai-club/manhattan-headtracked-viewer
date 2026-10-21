param(
    [string]$Version,
    [switch]$List
)
# Switch the working tree between the committed default Unity project (6000.3.22f1)
# and an alternative editor profile in tools/unity-profiles/<version>/.
# A profile overlays the Unity-version-specific project files (packages, URP/Graphics settings,
# ProjectVersion). Switching back restores them from Git. Run this before setup/bake/build.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$profiles = @(Get-ChildItem -LiteralPath $ViewerProfileRoot -Directory | ForEach-Object Name)
$current = if ($ViewerProfile) { $ViewerProfile.editorVersion } else { $ViewerDefaultEditorVersion }
if ($List -or -not $Version) {
    Write-Host "Active Unity version: $current"
    Write-Host "Default: $ViewerDefaultEditorVersion"
    Write-Host "Profiles: $($profiles -join ', ')"
    Write-Host 'Usage: tools/use_unity_profile.ps1 -Version <version>'
    return
}
if ($Version -ne $ViewerDefaultEditorVersion -and $profiles -notcontains $Version) {
    throw "Unknown Unity version '$Version'. Choose $ViewerDefaultEditorVersion or one of: $($profiles -join ', ')."
}

function Get-OverlayFiles([string]$ProfileName) {
    $overlayRoot = Join-Path $ViewerProfileRoot "$ProfileName\overlay"
    Get-ChildItem -LiteralPath $overlayRoot -Recurse -File | ForEach-Object {
        $_.FullName.Substring($overlayRoot.Length + 1).Replace('\', '/')
    }
}

# Restore the committed project files first so profile switches never mix versions.
if ($ViewerProfile) {
    foreach ($relative in Get-OverlayFiles $ViewerProfile.editorVersion) {
        $gitPath = "ManhattanViewer/$relative"
        $tracked = @(& git -C $ViewerRepository ls-tree --name-only HEAD -- $gitPath)
        if ($tracked.Count -gt 0) { Invoke-Checked 'git' @('-C', $ViewerRepository, 'checkout', 'HEAD', '--', $gitPath) }
        else { Remove-Item -LiteralPath (Join-Path $ViewerRepository $gitPath) -Force -ErrorAction SilentlyContinue }
    }
    Remove-Item -LiteralPath $ViewerActiveProfileMarker -Force
}
if ($Version -ne $ViewerDefaultEditorVersion) {
    $overlayPaths = @(Get-OverlayFiles $Version | ForEach-Object { "ManhattanViewer/$_" })
    $dirty = @(& git -C $ViewerRepository status --porcelain=v1 -- $overlayPaths)
    if ($dirty.Count -gt 0) { throw "Commit or stash these files before applying a Unity profile (it overwrites them):`n$($dirty -join "`n")" }
    foreach ($relative in Get-OverlayFiles $Version) {
        $target = Join-Path $ViewerProject $relative.Replace('/', '\')
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $ViewerProfileRoot "$Version\overlay\$($relative.Replace('/', '\'))") -Destination $target -Force
    }
    Set-Content -LiteralPath $ViewerActiveProfileMarker -Value $Version -NoNewline
}
Write-Host "Unity project files now match $Version. Delete Library only if Unity reports stale imports."
