param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$TaskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'PublicFiles.ps1')
& (Join-Path $PSScriptRoot 'Scan-Public.ps1')
& (Join-Path $PSScriptRoot 'Test-PublicPackaging.ps1')
$TaskVersion = '0.1.21'
$TaskWork = Join-Path $TaskRoot ('.local\package-'+[Guid]::NewGuid().ToString('N'))
$TaskBuild = Join-Path $TaskWork 'build'
& (Join-Path $TaskRoot 'src\Build.ps1') -OutputDirectory $TaskBuild
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ((Get-Item -LiteralPath (Join-Path $TaskBuild 'TetoDesk.exe')).VersionInfo.FileVersion -ne ($TaskVersion+'.0')) { throw 'Package version differs from executable.' }
if (-not $SkipTests) {
    & (Join-Path $TaskRoot 'src\Test.ps1') -Mode deployment -QaDirectory (Join-Path $TaskRoot '.local\qa\deployment')
    foreach ($TaskMode in @('account-query','query-timing','panel-ui')) {
        & (Join-Path $TaskRoot 'src\test-build\TetoDesk.Tests.exe') $TaskMode (Join-Path $TaskRoot ('.local\qa\'+$TaskMode))
        if ($LASTEXITCODE -ne 0) { throw ('Headless verification failed: '+$TaskMode) }
    }
}
$TaskRelease = Join-Path $TaskRoot ('dist\TetoDesk-'+$TaskVersion)
New-Item -ItemType Directory -Path $TaskRelease -Force | Out-Null
$TaskRuntimeNames = @('TetoDesk.exe','TetoDesk.exe.config','NAudio.Core.dll','NAudio.Wasapi.dll','Bridge\deepseek-acp.patch.yml','Bridge\harness-aux.mjs','Bridge\licenses\NAudio-LICENSE.txt')
foreach ($TaskName in $TaskRuntimeNames) {
    $TaskTo = Join-Path $TaskRelease $TaskName
    New-Item -ItemType Directory -Path (Split-Path $TaskTo) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $TaskBuild $TaskName) -Destination $TaskTo -Force
}
foreach ($TaskName in @('LICENSE','ARTWORK.md','THIRD_PARTY_NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $TaskRoot $TaskName) -Destination $TaskRelease -Force }
Copy-Item -LiteralPath (Join-Path $TaskRoot 'docs\QUICKSTART.zh-CN.md') -Destination (Join-Path $TaskRelease '使用说明.md') -Force
$TaskHashes = [ordered]@{}
foreach ($TaskFile in Get-ChildItem -LiteralPath $TaskRelease -Recurse -File) {
    $TaskRelative = $TaskFile.FullName.Substring($TaskRelease.Length+1)
    if ($TaskRelative -eq 'release-manifest.json') { continue }
    if ($TaskRelative -notin ($TaskRuntimeNames+@('LICENSE','ARTWORK.md','THIRD_PARTY_NOTICES.md','使用说明.md'))) { throw ('Unexpected file in release directory: '+$TaskRelative) }
    Test-TetoPublicBytes ([IO.File]::ReadAllBytes($TaskFile.FullName)) $TaskRelative
    $TaskHashes[$TaskRelative.Replace('\','/')] = (Get-FileHash -LiteralPath $TaskFile.FullName).Hash
}
$TaskManifest = [ordered]@{version=$TaskVersion;platform='windows-x64';framework='.NET Framework 4.8';supportedClients=@('ChatGPT/Codex only','DeepSeek Harness only','both');stateLocation='%LOCALAPPDATA%/TetoDesk';portableFlag='portable.flag';bundledAccountData=$false;bundledExternalClients=$false;files=$TaskHashes}
$TaskManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $TaskRelease 'release-manifest.json') -Encoding UTF8
$TaskSource = Join-Path $TaskWork 'TetoDesk-source'
New-Item -ItemType Directory -Path $TaskSource -Force | Out-Null
foreach ($TaskFile in Get-TetoPublicSourceFiles $TaskRoot) {
    $TaskRelative = $TaskFile.FullName.Substring($TaskRoot.Length+1)
    $TaskTo = Join-Path $TaskSource $TaskRelative
    New-Item -ItemType Directory -Path (Split-Path $TaskTo) -Force | Out-Null
    Copy-Item -LiteralPath $TaskFile.FullName -Destination $TaskTo
}
$TaskBinaryZip = Join-Path $TaskRoot ('dist\TetoDesk-'+$TaskVersion+'-windows-x64.zip')
$TaskSourceZip = Join-Path $TaskRoot ('dist\TetoDesk-'+$TaskVersion+'-source.zip')
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $TaskRelease | ForEach-Object FullName) -DestinationPath $TaskBinaryZip -Force
Compress-Archive -LiteralPath $TaskSource -DestinationPath $TaskSourceZip -Force
& (Join-Path $PSScriptRoot 'Scan-Public.ps1') -Archive $TaskBinaryZip
& (Join-Path $PSScriptRoot 'Scan-Public.ps1') -Archive $TaskSourceZip
@($TaskBinaryZip,$TaskSourceZip) | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant()+'  '+(Split-Path $_ -Leaf) } | Set-Content -LiteralPath (Join-Path $TaskRoot 'dist\SHA256SUMS.txt') -Encoding ASCII
Write-Output ('Packaged '+$TaskVersion+': Windows ZIP, complete source ZIP, SHA256SUMS.txt. No user data or external clients included.')
