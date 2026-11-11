<#
.SYNOPSIS
    Downloads the .NET Desktop Runtime installers that the setup bundle carries.

.DESCRIPTION
    Reads dotnet-runtime.json (version, URL and SHA-512 of each installer) and puts the files into
    the runtime folder next to it. A file that is already there with the pinned hash is left alone.
    A download is kept only if its SHA-512 matches the pin and it carries a valid Authenticode
    signature of Microsoft. Nothing is executed.

    To move to a new runtime version: take the file names, URLs and hashes of the Windows Desktop
    Runtime (win-x64 and win-x86 installer) from the release metadata named in "source" and change
    dotnet-runtime.json.

    The MSBuild project of the bundle runs this script before the build, so a plain msbuild is enough.

.PARAMETER Force
    Download again even if the file is present with the right hash.
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PinFile = Join-Path $ScriptDir 'dotnet-runtime.json'
$TargetDir = Join-Path $ScriptDir 'runtime'

# Windows PowerShell 5.1 does not negotiate TLS 1.2 by default on older systems.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

function Get-Sha512([string]$Path) {
    return (Get-FileHash -Algorithm SHA512 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$pin = Get-Content -LiteralPath $PinFile -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir | Out-Null
}

Write-Host ".NET Desktop Runtime $($pin.version)"

foreach ($package in $pin.packages) {
    $target = Join-Path $TargetDir $package.file
    $expected = $package.sha512.ToLowerInvariant()

    if (-not $Force -and (Test-Path -LiteralPath $target) -and (Get-Sha512 $target) -eq $expected) {
        Write-Host "  $($package.file): present, hash ok"
        continue
    }

    # The download lands in its own empty folder first; only a verified file reaches the runtime folder.
    $work = Join-Path ([IO.Path]::GetTempPath()) ('rfidgear-runtime-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $download = Join-Path $work $package.file
        Write-Host "  $($package.file): downloading"
        Invoke-WebRequest -Uri $package.url -OutFile $download -UseBasicParsing

        $actual = Get-Sha512 $download
        if ($actual -ne $expected) {
            throw "SHA-512 of $($package.file) does not match the pin. Expected $expected, got $actual."
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $download
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
            throw "$($package.file) has no valid Microsoft signature (status $($signature.Status))."
        }

        Move-Item -LiteralPath $download -Destination $target -Force
        Write-Host "  $($package.file): ok"
    }
    finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}
