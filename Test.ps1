param([string]$Filter='',[string]$OutputDirectory=(Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $OutputDirectory
& (Join-Path $OutputDirectory 'ZX6DisplayControl.Tests.exe') $Filter
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
