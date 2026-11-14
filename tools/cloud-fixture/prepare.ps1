param([Parameter(Mandatory=$true)][string]$UnityExecutable)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProgressPreference = 'SilentlyContinue'
function Stage([string]$Message) { Write-Output ('FIXTURE_STAGE ' + [DateTime]::UtcNow.ToString('o') + ' ' + $Message) }
Stage 'bootstrap-start'
if ($env:IS_BUILDER -ne 'true' -or $env:BUILDER_OS -ne 'WINDOWS') { throw 'Disposable Windows cloud builder required.' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repo 'ManhattanViewer'
$catalog = Join-Path $project 'Assets/Resources/CityCatalog.asset'
if ((Test-Path -LiteralPath $catalog) -or (Test-Path -LiteralPath ($catalog + '.meta'))) { throw 'Refusing to replace an existing city catalog or metadata. Use a clean disposable checkout.' }
if (-not (Test-Path -LiteralPath $UnityExecutable -PathType Leaf)) { throw 'UNITY_EXE does not identify an installed editor.' }
# Only this new private cache folder is written; no original source archive is replaced.
$scratch = Join-Path $repo ('.cache/cloud-footprint-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$archive = Join-Path $scratch 'Manhattan_Source_and_Materials.zip'
$url = 'https://github.com/sshs-ai-club/manhattan-headtracked-viewer/releases/download/city-source-v2/Manhattan_Source_and_Materials.zip'
Stage 'download-start bytes=81434566 timeoutSeconds=180'
Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing -TimeoutSec 180
Stage ('download-complete bytes=' + (Get-Item -LiteralPath $archive).Length)
if ((Get-Item -LiteralPath $archive).Length -ne 81434566 -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne '9d703c29ec6dc1192249a26e1d3577b1ca06beffddb61f12e43db1ded86f47b0') { throw 'Canonical source archive size or SHA256 mismatch.' }
Stage 'archive-hash-verified'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($archive)
try {
    # Extract only individually named members, never arbitrary archive paths.
    foreach ($name in @('data/buildings_metric.json', 'data/PROVENANCE.md')) {
        $entries = @($zip.Entries | Where-Object { $_.FullName -ceq $name })
        if ($entries.Count -ne 1) { throw "Missing or duplicate canonical member: $name" }
        $destination = Join-Path $scratch ([IO.Path]::GetFileName($name))
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entries[0], $destination, $false)
    }
} finally { $zip.Dispose() }
$source = Join-Path $scratch 'buildings_metric.json'
if ((Get-Item -LiteralPath $source).Length -ne 19186837 -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne '9bd3a981f30e6efcc783325389211219a47d47730bfb50d3d83469ebd548d091') { throw 'Canonical building source mismatch.' }
Stage 'source-extracted-and-hash-verified rowsExpected=44567'
$env:MANHATTAN_DISPOSABLE_FOOTPRINT_TEST = '1'
$env:MANHATTAN_FOOTPRINT_TEST_SOURCE = $source
$log = Join-Path $scratch 'fixture-setup.log'
# A separate editor invocation finishes before UBA starts its unchanged test run.
$arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'Manhattan.TestFixtureSetup.PrepareFootprintCatalog.Run', '-logFile', ('"' + $log + '"'))
Stage 'editor-start timeoutSeconds=600'
$process = Start-Process -FilePath $UnityExecutable -ArgumentList $arguments -PassThru -NoNewWindow
$clock = [Diagnostics.Stopwatch]::StartNew()
$reader = $null
$exitCode = -1
$heartbeat = 0
function Stream-Diagnostics {
    if ($null -eq $script:reader -and (Test-Path -LiteralPath $script:log)) {
        $stream = [IO.File]::Open($script:log, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
        $script:reader = New-Object IO.StreamReader($stream)
    }
    if ($null -ne $script:reader) {
        $drain = [Diagnostics.Stopwatch]::StartNew()
        $lines = 0
        while ($lines -lt 200 -and $drain.Elapsed.TotalMilliseconds -lt 250 -and $script:reader.Peek() -ge 0) {
            $lines++
            $line = $script:reader.ReadLine()
            # Do not print authentication/licensing credentials or arbitrary editor output.
            if ($line -match '(?i)token|password|secret|authorization|credential|serial|entitlement') { continue }
            if ($line -match 'MANHATTAN_FOOTPRINT_TEST_FIXTURE_READY rows=([0-9]+)') { Write-Output ('FIXTURE_EDITOR fixture-ready rows=' + $Matches[1]) }
            elseif ($line -match 'error (CS[0-9]+)') { Write-Output ('FIXTURE_EDITOR compiler-error code=' + $Matches[1]) }
            elseif ($line -match '(?i)Exception') { Write-Output 'FIXTURE_EDITOR exception-detected; inspect retained setup log' }
            elseif ($line -match '(?i)Aborting') { Write-Output 'FIXTURE_EDITOR abort-detected' }
            elseif ($line -match '(?i)Compilation|Asset Pipeline Refresh') { Write-Output 'FIXTURE_EDITOR compilation-or-import-progress' }
        }
    }
}
try {
    while (-not $process.WaitForExit(5000)) {
        if ($clock.Elapsed.TotalSeconds -ge 600) {
            Stage 'editor-timeout terminating-only-owned-editor'
            $process.Kill()
            if (-not $process.WaitForExit(10000)) { throw 'Owned fixture Editor did not terminate within 10 seconds; stop this disposable cloud job.' }
            throw 'Fixture setup exceeded 600 seconds. No test success claimed; inspect retained setup log.'
        }
        Stream-Diagnostics
        if ($clock.Elapsed.TotalSeconds -ge $heartbeat) {
            $bytes = if (Test-Path -LiteralPath $log) { (Get-Item -LiteralPath $log).Length } else { 0 }
            Stage ('editor-wait elapsedSeconds=' + [int]$clock.Elapsed.TotalSeconds + ' logBytes=' + $bytes)
            $heartbeat = $clock.Elapsed.TotalSeconds + 30
        }
    }
    $process.Refresh()
    $exitCode = $process.ExitCode
    Stream-Diagnostics
    Stage ('editor-exit code=' + $exitCode + ' elapsedSeconds=' + [int]$clock.Elapsed.TotalSeconds)
} finally {
    try { if ($null -ne $reader) { $reader.Dispose() } }
    finally {
        try {
            if (-not $process.HasExited) {
                $process.Kill()
                if (-not $process.WaitForExit(10000)) { throw 'Owned fixture Editor still running after termination attempt; stop this disposable cloud job.' }
            }
        } finally { $process.Dispose() }
    }
}
if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $log)) { throw "Fixture editor failed: $exitCode" }
if (-not (Select-String -LiteralPath $log -SimpleMatch 'MANHATTAN_FOOTPRINT_TEST_FIXTURE_READY rows=44567' -Quiet)) { throw 'Fixture readiness marker missing.' }
if (-not (Test-Path -LiteralPath $catalog -PathType Leaf)) { throw 'Expected generated fixture catalog not found.' }
Write-Output 'Canonical footprint fixture prepared. Continue unchanged EditMode tests; this is not a full city bake or rendering validation.'
