param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
if(-not (Test-Path -LiteralPath $compiler) -or -not (Test-Path -LiteralPath (Join-Path $refs 'mscorlib.dll'))){throw 'The .NET Framework C# compiler and .NET Framework 4.7.2 reference assemblies are required.'}
$stage=Join-Path $PSScriptRoot ('obj\'+[guid]::NewGuid().ToString('N'))
$bin=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $stage,$bin -Force | Out-Null
$common=@('/nologo','/noconfig','/nostdlib+','/warnaserror+','/optimize+','/codepage:65001','/langversion:5')
foreach($assembly in @('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','System.Runtime.Serialization','System.Management','System.Windows.Forms','System.Drawing','System.IO.Compression','System.IO.Compression.FileSystem')){$common+=('/reference:'+(Join-Path $refs ($assembly+'.dll')))}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse | Where-Object Name -ne 'Program.cs' | ForEach-Object FullName)
& $compiler @common '/target:library' ('/resource:'+(Join-Path $PSScriptRoot 'src\Assets\holder.ico')+',ZX6DisplayControl.holder.ico') ('/out:'+(Join-Path $stage 'ZX6DisplayControl.Core.dll')) @sources
if($LASTEXITCODE -ne 0){throw 'Core build failed.'}
$tests=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -Recurse | ForEach-Object FullName)
& $compiler @common '/target:exe' ('/reference:'+(Join-Path $refs 'Accessibility.dll')) ('/reference:'+(Join-Path $stage 'ZX6DisplayControl.Core.dll')) ('/out:'+(Join-Path $stage 'ZX6DisplayControl.Tests.exe')) @tests
if($LASTEXITCODE -ne 0){throw 'Test build failed.'}
$program=Join-Path $PSScriptRoot 'src\Program.cs'
if(Test-Path -LiteralPath $program){
 & $compiler @common '/target:winexe' ('/reference:'+(Join-Path $stage 'ZX6DisplayControl.Core.dll')) ('/win32icon:'+(Join-Path $PSScriptRoot 'src\Assets\holder.ico')) ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) ('/out:'+(Join-Path $stage 'ZX6DisplayControl.exe')) $program (Join-Path $PSScriptRoot 'src\AssemblyInfo.cs')
 if($LASTEXITCODE -ne 0){throw 'Application build failed.'}
}
Get-ChildItem -LiteralPath $stage -File | Copy-Item -Destination $bin
$inputs=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Recurse -File)
$inputs+=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'Build.ps1'),(Join-Path $PSScriptRoot 'app.manifest')
$fingerprints=@($inputs | ForEach-Object {[pscustomobject]@{Path=$_.FullName.Substring($PSScriptRoot.Length+1);Hash=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$binaries=@(Get-ChildItem -LiteralPath $stage -File | Where-Object {$_.Extension -in '.exe','.dll'} | ForEach-Object {[pscustomobject]@{Path=$_.Name;Hash=(Get-FileHash -LiteralPath $_.FullName).Hash}})
[pscustomobject]@{Sources=$fingerprints;Binaries=$binaries} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $bin 'build-info.json') -Encoding UTF8
Write-Output ('Build OK: '+$bin)
