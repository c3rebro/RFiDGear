<#
.SYNOPSIS
Makes the files of a release that are uploaded to GitHub: RFiDGearBundleSetup.exe, update.zip (the bundle in a zip, which is what
an installed RFiDGear downloads) and update.xml, and the notes of the release.

.DESCRIPTION
Checks that the bundle is signed and of the version of this commit, that update.zip holds exactly the bundle under the name the
update check starts, and that update.xml is the manifest of this version. Writes OutDir\upload\:
  RFiDGearBundleSetup.exe, update.zip, update.xml, release-notes.md (the text for the GitHub release), SHA256SUMS.txt

.PARAMETER Bundle
The signed RFiDGearBundleSetup.exe (from Sign-Release.ps1).

.PARAMETER OutDir
The folder of the release (the one Build-Release.ps1 wrote). Default: release\<version> in the repository.

.PARAMETER AllowUnsigned
Pack a bundle without a valid signature. Only for a trial: Windows warns about such a bundle.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Bundle,
    [string]$OutDir,
    [switch]$AllowUnsigned
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repo = $script:RepositoryRoot
$version = Get-ReleaseVersion
if (-not $OutDir) {
    $OutDir = Join-Path $repo "release\$version"
}

$OutDir = Resolve-FullPath $OutDir

$Bundle = (Resolve-Path -LiteralPath $Bundle).Path

$signature = Get-AuthenticodeSignature -LiteralPath $Bundle
if ($signature.Status -ne 'Valid') {
    if (-not $AllowUnsigned) {
        throw "The bundle has no valid signature ($($signature.Status)). Sign it with Sign-Release.ps1, or pass -AllowUnsigned for a trial."
    }

    Write-Warning "The bundle is not signed ($($signature.Status))."
}

$bundleVersion = (Get-Item -LiteralPath $Bundle).VersionInfo.FileVersion
if ($bundleVersion -ne $version) {
    throw "The bundle has the version $bundleVersion, this commit is $version."
}

$upload = Join-Path $OutDir 'upload'
if (Test-Path -LiteralPath $upload) {
    Remove-Item -LiteralPath $upload -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $upload | Out-Null

# The update check extracts update.zip and starts RFiDGearBundleSetup.exe from it: that is the only entry, under that name.
Copy-Item -LiteralPath $Bundle -Destination (Join-Path $upload 'RFiDGearBundleSetup.exe')
$zip = Join-Path $upload 'update.zip'
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $Bundle, 'RFiDGearBundleSetup.exe', [IO.Compression.CompressionLevel]::Optimal)
}
finally {
    $archive.Dispose()
}

$check = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    if ($check.Entries.Count -ne 1 -or $check.Entries[0].FullName -ne 'RFiDGearBundleSetup.exe') {
        throw 'update.zip does not hold exactly RFiDGearBundleSetup.exe.'
    }

    $stream = $check.Entries[0].Open()
    try {
        $inside = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
    }
}
finally {
    $check.Dispose()
}

if ($inside -ne (Get-FileSha256 $Bundle)) {
    throw 'The bundle inside update.zip is not the bundle that was given.'
}

Copy-Item -LiteralPath (Join-Path $repo 'RFiDGear\update.xml') -Destination (Join-Path $upload 'update.xml')

# The notes of the GitHub release are the newest entry of the manifest.
$info = ([xml](Get-Content -Raw -LiteralPath (Join-Path $upload 'update.xml'))).Manifest.VersionInfoText
$entries = [regex]::Matches($info, '(?m)^Version ([0-9.]+)\s*$')
if ($entries.Count -lt 2 -or $entries[0].Groups[1].Value -ne $version) {
    throw "The notes of update.xml do not start with 'Version $version'."
}

$newestLines = ($info.Substring($entries[0].Index, $entries[1].Index - $entries[0].Index) -replace '\r', '').Trim() -split "`n" | Where-Object { $_ -notmatch '^-{10,}\s*$' }
$newest = $newestLines -join "`n"
$previous = $entries[1].Groups[1].Value
$notes = $newest + "`n`n**Full Changelog**: https://github.com/c3rebro/RFiDGear/compare/v$previous...v$version`n"
[IO.File]::WriteAllText((Join-Path $upload 'release-notes.md'), $notes, (New-Object Text.UTF8Encoding($false)))

$sums = Get-ChildItem -LiteralPath $upload -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object { '{0}  {1}' -f (Get-FileSha256 $_.FullName), $_.Name }
Set-Content -LiteralPath (Join-Path $upload 'SHA256SUMS.txt') -Value $sums

Write-Output "Release $version, files to upload in $upload"
Get-ChildItem -LiteralPath $upload -File | ForEach-Object { '  {0,12:N0}  {1}' -f $_.Length, $_.Name }
Write-Output ''
Write-Output "GitHub release: tag v$version, title 'RFiDGear v$($version -replace '\.0$', '')', text from release-notes.md; upload RFiDGearBundleSetup.exe, update.zip and update.xml."
