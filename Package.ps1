param([switch]$UseExistingBuild,[string]$BuildDirectory=(Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference='Stop'
if(-not $UseExistingBuild){& (Join-Path $PSScriptRoot 'Test.ps1') -OutputDirectory $BuildDirectory;if($LASTEXITCODE -ne 0){throw 'Tests failed; package not created.'}}
$info=Get-Content -Raw -LiteralPath (Join-Path $BuildDirectory 'build-info.json') | ConvertFrom-Json
$actualInputs=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Recurse -File)
$actualInputs+=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'Build.ps1'),(Join-Path $PSScriptRoot 'app.manifest')
if($actualInputs.Count -ne $info.Sources.Count){throw 'Build is out of date; rebuild first.'}
foreach($entry in $info.Sources){if((Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $entry.Path)).Hash -ne $entry.Hash){throw 'Build is out of date; rebuild first.'}}
foreach($entry in $info.Binaries){if((Get-FileHash -LiteralPath (Join-Path $BuildDirectory $entry.Path)).Hash -ne $entry.Hash){throw 'A build artifact has changed; rebuild first.'}}
$dist=Join-Path $PSScriptRoot 'dist'
$stage=Join-Path $PSScriptRoot ('obj\package-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dist,$stage -Force | Out-Null
foreach($name in @('ZX6DisplayControl.exe','ZX6DisplayControl.Core.dll')){Copy-Item -LiteralPath (Join-Path $BuildDirectory $name) -Destination $stage}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\Guide.txt') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $stage
$revision=(& git -C $PSScriptRoot rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0){throw 'Git revision is unavailable.'}
if((& git -C $PSScriptRoot status --porcelain)){throw 'Commit changes before packaging so that the source archive matches the build.'}
@('Z-X6 Display Control 0.1.0-beta.1',('Source commit: '+$revision),('Packaged: '+[DateTimeOffset]::Now.ToString('o'))) | Set-Content -LiteralPath (Join-Path $stage 'Version.txt') -Encoding UTF8
$archive=Join-Path $dist 'ZX6DisplayControl.zip'
$source=Join-Path $dist 'ZX6DisplayControl-source.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
& git -C $PSScriptRoot archive --format=zip ('--output='+$source) HEAD
if($LASTEXITCODE -ne 0){throw 'Source archive creation failed.'}
$hashes=@(Get-FileHash -LiteralPath $archive,$source)
$hashes+=Get-ChildItem -LiteralPath $stage -File | Get-FileHash
$hashes | ForEach-Object {$_.Hash+'  '+[IO.Path]::GetFileName($_.Path)} | Set-Content -LiteralPath (Join-Path $dist 'SHA256.txt') -Encoding ASCII
Write-Output ('Package: '+$archive)
Write-Output ('Source: '+$source)
Write-Output ('SHA256: '+(Join-Path $dist 'SHA256.txt'))
