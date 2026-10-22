param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build'), [switch]$UiQa)
$ErrorActionPreference = 'Stop'
$TaskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$TaskCompiler = Join-Path $TaskFramework 'csc.exe'
if (-not (Test-Path -LiteralPath $TaskCompiler)) { throw 'Requires the Windows .NET Framework C# compiler.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$TaskEntryPoint = if ($UiQa) { 'TetoDesk.UiQaProgram' } else { 'TetoDesk.Program' }
$TaskCompilerArguments = @('/nologo','/utf8output','/target:winexe','/platform:x64','/optimize+',('/main:' + $TaskEntryPoint),('/out:' + (Join-Path $OutputDirectory 'TetoDesk.exe')),('/win32icon:' + (Join-Path $PSScriptRoot 'Assets\teto.ico')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
foreach ($TaskAssembly in @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')) { $TaskCompilerArguments += '/reference:' + (Join-Path $TaskFramework $TaskAssembly) }
foreach ($TaskAssembly in @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll')) { $TaskCompilerArguments += '/reference:' + (Join-Path $TaskFramework ('WPF\' + $TaskAssembly)) }
foreach ($TaskName in @('NAudio.Core','NAudio.Wasapi')) { $TaskDll=Join-Path $PSScriptRoot ('Bridge\lib\' + $TaskName + '\lib\netstandard2.0\' + $TaskName + '.dll'); $TaskCompilerArguments += '/reference:' + $TaskDll; Copy-Item -LiteralPath $TaskDll -Destination $OutputDirectory -Force }
$TaskCompilerArguments += '/reference:' + (Join-Path $TaskFramework 'netstandard.dll')
foreach ($TaskResource in @('teto.png','teto.ico','animations.json')) { $TaskCompilerArguments += '/resource:' + (Join-Path $PSScriptRoot ('Assets\' + $TaskResource)) + ',TetoDesk.' + $TaskResource }
$TaskQuotaSheet = Join-Path $PSScriptRoot 'Assets\quota-thinking.png'
$TaskQuotaManifest = Join-Path $PSScriptRoot 'Assets\quota-thinking.json'
if ((Test-Path -LiteralPath $TaskQuotaSheet) -ne (Test-Path -LiteralPath $TaskQuotaManifest)) { throw 'Quota animation requires both the approved sheet and its manifest.' }
if (Test-Path -LiteralPath $TaskQuotaSheet) { foreach ($TaskResource in @('quota-thinking.png','quota-thinking.json')) { $TaskCompilerArguments += '/resource:' + (Join-Path $PSScriptRoot ('Assets\' + $TaskResource)) + ',TetoDesk.' + $TaskResource } }
$TaskCompilerArguments += Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object { $_.Name -notlike '*Tests.cs' } | ForEach-Object FullName
foreach ($TaskPairName in @('idle-baguette','double-click-angry','double-click-dizzy')) {
    $TaskSheet = Join-Path $PSScriptRoot ('Assets\' + $TaskPairName + '.png')
    $TaskManifest = Join-Path $PSScriptRoot ('Assets\' + $TaskPairName + '.json')
    if ((Test-Path -LiteralPath $TaskSheet) -ne (Test-Path -LiteralPath $TaskManifest)) { throw ($TaskPairName + ' requires both approved artwork and timing manifest.') }
    if (Test-Path -LiteralPath $TaskSheet) { foreach ($TaskExtension in @('.png','.json')) { $TaskCompilerArguments += '/resource:' + (Join-Path $PSScriptRoot ('Assets\' + $TaskPairName + $TaskExtension)) + ',TetoDesk.' + $TaskPairName + $TaskExtension } }
}
& $TaskCompiler @TaskCompilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TetoDesk.exe.config') -Destination $OutputDirectory -Force
New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'Bridge') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Bridge') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $OutputDirectory ('Bridge\' + $_.Name)) -Force }
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Bridge\licenses')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Bridge\licenses') -Destination (Join-Path $OutputDirectory 'Bridge') -Recurse -Force }
Write-Output (Join-Path $OutputDirectory 'TetoDesk.exe')
