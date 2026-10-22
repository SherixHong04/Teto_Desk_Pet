function Get-TetoPublicSourceFiles([string]$Repository) {
    $TaskRoot = [IO.Path]::GetFullPath($Repository).TrimEnd('\')
    $TaskFiles = @()
    foreach ($TaskName in @('README.md','README.en.md','LICENSE','ARTWORK.md','THIRD_PARTY_NOTICES.md','AGENTS.md','.gitignore','.gitattributes')) {
        $TaskPath = Join-Path $TaskRoot $TaskName
        if (Test-Path -LiteralPath $TaskPath) { $TaskFiles += Get-Item -LiteralPath $TaskPath }
    }
    $TaskFiles += Get-ChildItem -LiteralPath (Join-Path $TaskRoot 'src') -File | Where-Object { $_.Extension -in @('.cs','.ps1','.config','.manifest') }
    foreach ($TaskFolder in @('src\Assets','src\Bridge','tools','docs','ArtDesignAssets','.github')) {
        $TaskPath = Join-Path $TaskRoot $TaskFolder
        if (Test-Path -LiteralPath $TaskPath) { $TaskFiles += Get-ChildItem -LiteralPath $TaskPath -Recurse -File -Force }
    }
    foreach ($TaskFile in $TaskFiles) {
        if (-not $TaskFile.FullName.StartsWith($TaskRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or ($TaskFile.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Public source must be a regular file within the repository.' }
        for ($TaskParent = $TaskFile.Directory; $TaskParent -and $TaskParent.FullName -ne $TaskRoot; $TaskParent = $TaskParent.Parent) {
            if ($TaskParent.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Public source must not traverse directory links.' }
        }
        if ($TaskFile.Extension -in @('.exe','.pdb','.log','.tmp','.zip')) { throw ('Unexpected generated file in public source: '+$TaskFile.Name) }
    }
    return @($TaskFiles | Sort-Object FullName -Unique)
}
function Test-TetoPublicPath([string]$Relative) {
    $TaskName = $Relative.Replace('\','/')
    if ($TaskName.StartsWith('/') -or $TaskName -match '(^|/)\.\.(/|$)' -or $TaskName.Contains(':')) { throw 'Archive path escapes its root.' }
    if ($TaskName -match '(?i)(^|/)(Data|Tasks|Attachments|\.local|\.runtime|\.git|test-build|build|qa[^/]*)(/|$)' -or $TaskName -match '(?i)(^|/)(auth\.json|providers\.json|settings\.json|session[^/]*\.json|\.env[^/]*|[^/]+\.(log|pdb|pem|key))$') { throw ('Private/generated path in public files: '+$TaskName) }
}
function Test-TetoPublicBytes([byte[]]$Bytes,[string]$Relative) {
    Test-TetoPublicPath $Relative
    # Upstream NAudio embeds its own build-machine paths. Permit only the two
    # exact reviewed 2.2.1 binaries, never arbitrary binaries with local paths.
    $TaskVendor = @{
        'NAudio.Core.dll'='FCF493FC47A2F478A65303886B975FBDBF714CBB1F2D79F7FCE97E4BB16B01A8'
        'NAudio.Wasapi.dll'='618EF0E49D64E7A66DFE64BBF6AE81705B9D9683D8A9F321E5C3024D666BDF82'
    }
    $TaskVendorFile = [IO.Path]::GetFileName($Relative.Replace('/','\'))
    $TaskKnownVendor = $false
    if ($TaskVendor.ContainsKey($TaskVendorFile)) {
        $TaskDigest = [Security.Cryptography.SHA256]::Create()
        try { $TaskKnownVendor = [BitConverter]::ToString($TaskDigest.ComputeHash($Bytes)).Replace('-','') -eq $TaskVendor[$TaskVendorFile] }
        finally { $TaskDigest.Dispose() }
        if (-not $TaskKnownVendor) { throw ('Unreviewed dependency binary: '+$Relative) }
    }
    # Scan both ASCII-compatible and UTF-16 metadata, including binary assets.
    foreach ($TaskText in @([Text.Encoding]::UTF8.GetString($Bytes),[Text.Encoding]::Unicode.GetString($Bytes))) {
        if ((-not $TaskKnownVendor -and $TaskText -match '(?i)\b[A-Z]:[\\/]+Users[\\/]+(?!Public\b|Default\b)[A-Za-z0-9_.-]+[\\/]') -or $TaskText -match '\b(?:gh[pousr]_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9_-]{24,})\b' -or $TaskText -match '(?i)"(?:access_token|refresh_token|api_key)"\s*:\s*"[A-Za-z0-9_./+-]{16,}"') { throw ('Potential private path or credential in '+$Relative+'; inspect locally before publishing.') }
    }
}
