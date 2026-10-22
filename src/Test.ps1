param([ValidateSet('offline','live','file-tools','regression','quota','preview','composer','catalog','composer-live','easter','animation','hover','interaction','pet-ui','reaction-chance','menu-preview','harness-prepare','harness-audio-check','harness-live','harness-logic','panel-ui','collapse-style','workspace-balance','query-timing','account-query','account-query-live','deployment')][string]$Mode = 'deployment', [string]$QaDirectory = (Join-Path $PSScriptRoot '..\.local\qa'))
$ErrorActionPreference = 'Stop'
$TaskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$TaskTestOutput = Join-Path $PSScriptRoot 'test-build'
New-Item -ItemType Directory -Path $TaskTestOutput -Force | Out-Null
$TaskTestArguments = @('/nologo','/utf8output','/target:exe','/platform:x64','/optimize+','/main:TetoDesk.TestProgram',('/out:' + (Join-Path $TaskTestOutput 'TetoDesk.Tests.exe')))
foreach ($TaskAssembly in @('System.dll','System.Core.dll','Microsoft.CSharp.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')) { $TaskTestArguments += '/reference:' + (Join-Path $TaskFramework $TaskAssembly) }
foreach ($TaskAssembly in @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll')) { $TaskTestArguments += '/reference:' + (Join-Path $TaskFramework ('WPF\' + $TaskAssembly)) }
foreach ($TaskName in @('NAudio.Core','NAudio.Wasapi')) { $TaskDll=Join-Path $PSScriptRoot ('Bridge\lib\' + $TaskName + '\lib\netstandard2.0\' + $TaskName + '.dll'); $TaskTestArguments += '/reference:' + $TaskDll; Copy-Item -LiteralPath $TaskDll -Destination $TaskTestOutput -Force }
$TaskTestArguments += '/reference:' + (Join-Path $TaskFramework 'netstandard.dll')
New-Item -ItemType Directory -Path (Join-Path $TaskTestOutput 'Bridge') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Bridge') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $TaskTestOutput ('Bridge\' + $_.Name)) -Force }
foreach ($TaskResource in @('teto.png','teto.ico','animations.json')) { $TaskTestArguments += '/resource:' + (Join-Path $PSScriptRoot ('Assets\' + $TaskResource)) + ',TetoDesk.' + $TaskResource }
$TaskQuotaSheet = Join-Path $PSScriptRoot 'Assets\quota-thinking.png'
$TaskQuotaManifest = Join-Path $PSScriptRoot 'Assets\quota-thinking.json'
if ((Test-Path -LiteralPath $TaskQuotaSheet) -ne (Test-Path -LiteralPath $TaskQuotaManifest)) { throw 'Quota animation requires both the approved sheet and its manifest.' }
if (Test-Path -LiteralPath $TaskQuotaSheet) { foreach ($TaskResource in @('quota-thinking.png','quota-thinking.json')) { $TaskTestArguments += '/resource:' + (Join-Path $PSScriptRoot ('Assets\' + $TaskResource)) + ',TetoDesk.' + $TaskResource } }
$TaskTestArguments += Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName
foreach ($TaskPairName in @('idle-baguette','double-click-angry','double-click-dizzy')) {
    $TaskSheet = Join-Path $PSScriptRoot ('Assets\' + $TaskPairName + '.png')
    $TaskManifest = Join-Path $PSScriptRoot ('Assets\' + $TaskPairName + '.json')
    if ((Test-Path -LiteralPath $TaskSheet) -ne (Test-Path -LiteralPath $TaskManifest)) { throw ($TaskPairName + ' requires both approved artwork and timing manifest.') }
    if (Test-Path -LiteralPath $TaskSheet) { foreach ($TaskExtension in @('.png','.json')) { $TaskTestArguments += '/resource:' + (Join-Path $PSScriptRoot ('Assets\' + $TaskPairName + $TaskExtension)) + ',TetoDesk.' + $TaskPairName + $TaskExtension } }
}
& (Join-Path $TaskFramework 'csc.exe') @TaskTestArguments
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& (Join-Path $TaskTestOutput 'TetoDesk.Tests.exe') $Mode $QaDirectory
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
