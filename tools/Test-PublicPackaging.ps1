$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PublicFiles.ps1')
$TaskTests = 0
function Assert-TetoRejected([scriptblock]$Action,[string]$Label) {
    $TaskRejected = $false
    try { & $Action } catch { $TaskRejected = $true }
    if (-not $TaskRejected) { throw ('Publication guard accepted '+$Label) }
    Write-Output ('PASS Public package rejects '+$Label)
    $script:TaskTests++
}
Assert-TetoRejected { Test-TetoPublicPath 'Data/session.json' } 'conversation state'
Assert-TetoRejected { Test-TetoPublicPath 'nested/Tasks/work.txt' } 'task files'
Assert-TetoRejected { Test-TetoPublicPath '.local/imported-runtime/auth.json' } 'ignored local copies'
Assert-TetoRejected { Test-TetoPublicPath '../outside.txt' } 'parent path traversal'
Assert-TetoRejected { Test-TetoPublicPath '/absolute.txt' } 'absolute archive paths'
Assert-TetoRejected { Test-TetoPublicPath 'private.key' } 'private keys'
$TaskFakeProfile = 'C:'+[char]92+'Users'+[char]92+'ExamplePrivateUser'+[char]92+'file.txt'
Assert-TetoRejected { Test-TetoPublicBytes ([Text.Encoding]::UTF8.GetBytes($TaskFakeProfile)) 'README.md' } 'personal profile paths'
$TaskFakeCredential = 'gh'+'p_'+('x'*32)
Assert-TetoRejected { Test-TetoPublicBytes ([Text.Encoding]::UTF8.GetBytes($TaskFakeCredential)) 'example.txt' } 'credential-shaped payloads'
$TaskVendor = Join-Path $PSScriptRoot '..\src\Bridge\lib\NAudio.Core\lib\netstandard2.0\NAudio.Core.dll'
$TaskAltered = [IO.File]::ReadAllBytes($TaskVendor); $TaskAltered[0] = $TaskAltered[0] -bxor 1
Assert-TetoRejected { Test-TetoPublicBytes $TaskAltered 'NAudio.Core.dll' } 'modified dependency binaries'
Test-TetoPublicBytes ([Text.Encoding]::UTF8.GetBytes('Public project documentation.')) 'README.md'
$TaskTests++
Write-Output ('Public packaging guards passed: '+$TaskTests+' checks; no user files, credentials or network requests used.')
