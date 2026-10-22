param([string]$Archive,[switch]$Staged)
$ErrorActionPreference = 'Stop'
$TaskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'PublicFiles.ps1')
$TaskCount = 0
if ($Archive) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $TaskZip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Archive))
    try {
        foreach ($TaskEntry in $TaskZip.Entries) {
            if ($TaskEntry.FullName.EndsWith('/')) { continue }
            if ($TaskEntry.Length -gt 30MB) { throw 'Unexpected oversized archive entry.' }
            $TaskStream = $TaskEntry.Open(); $TaskMemory = New-Object IO.MemoryStream
            try { $TaskStream.CopyTo($TaskMemory); Test-TetoPublicBytes $TaskMemory.ToArray() $TaskEntry.FullName; $TaskCount++ }
            finally { $TaskStream.Dispose(); $TaskMemory.Dispose() }
        }
    } finally { $TaskZip.Dispose() }
} else {
    $TaskFiles = @(Get-TetoPublicSourceFiles $TaskRoot)
    foreach ($TaskFile in $TaskFiles) { Test-TetoPublicBytes ([IO.File]::ReadAllBytes($TaskFile.FullName)) $TaskFile.FullName.Substring($TaskRoot.Length+1); $TaskCount++ }
    if ($Staged) {
        $TaskAllowed = @{}; foreach ($TaskFile in $TaskFiles) { $TaskAllowed[$TaskFile.FullName.Substring($TaskRoot.Length+1).Replace('\','/')] = $true }
        $TaskTracked = @(& git -C $TaskRoot -c core.quotepath=false ls-files --cached)
        if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect Git index.' }
        foreach ($TaskPath in $TaskTracked) {
            Test-TetoPublicPath $TaskPath
            if (-not $TaskAllowed.ContainsKey($TaskPath)) { throw ('Tracked file outside source allowlist: '+$TaskPath) }
            $TaskIndexed = & git -C $TaskRoot rev-parse (':' + $TaskPath)
            $TaskWorking = & git -C $TaskRoot hash-object ('--path='+$TaskPath) (Join-Path $TaskRoot $TaskPath)
            if ($TaskIndexed -ne $TaskWorking) { throw ('Scan and staged content differ: '+$TaskPath) }
        }
    }
}
Write-Output ('Public-content scan passed: '+$TaskCount+' files; no session/settings directories, personal profile paths or credential patterns.')
