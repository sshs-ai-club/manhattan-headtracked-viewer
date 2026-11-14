param([Parameter(Mandatory=$true)][string]$UnityExecutable)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
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
Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
if ((Get-Item -LiteralPath $archive).Length -ne 81434566 -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne '9d703c29ec6dc1192249a26e1d3577b1ca06beffddb61f12e43db1ded86f47b0') { throw 'Canonical source archive size or SHA256 mismatch.' }
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
$env:MANHATTAN_DISPOSABLE_FOOTPRINT_TEST = '1'
$env:MANHATTAN_FOOTPRINT_TEST_SOURCE = $source
$log = Join-Path $scratch 'fixture-setup.log'
# A separate editor invocation finishes before UBA starts its unchanged test run.
$arguments = @('-batchmode', '-quit', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'Manhattan.TestFixtureSetup.PrepareFootprintCatalog.Run', '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityExecutable -ArgumentList $arguments -PassThru -Wait -NoNewWindow
$exitCode = $process.ExitCode
$process.Dispose()
if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log | Write-Output }
if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $log)) { throw "Fixture editor failed: $exitCode" }
if (-not (Select-String -LiteralPath $log -SimpleMatch 'MANHATTAN_FOOTPRINT_TEST_FIXTURE_READY rows=44567' -Quiet)) { throw 'Fixture readiness marker missing.' }
if (-not (Test-Path -LiteralPath $catalog -PathType Leaf)) { throw 'Expected generated fixture catalog not found.' }
Write-Output 'Canonical footprint fixture prepared. Continue unchanged EditMode tests; this is not a full city bake or rendering validation.'
