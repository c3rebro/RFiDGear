<#
.SYNOPSIS
Signs the setup bundle: the same steps as signtool\sign.bat (sign the engine of the bundle, put it back, sign the bundle), with
the paths given, so a bundle built anywhere can be signed.

.DESCRIPTION
The certificate is not part of the repository. Set both before running:
  $env:RFIDGEAR_SIGN_PFX       the path of your .pfx file
  $env:RFIDGEAR_SIGN_PASSWORD  its password

.PARAMETER Bundle
The unsigned RFiDGearBundleSetup.exe (it is not changed).

.PARAMETER OutFile
Where the signed bundle is written. The file name must stay RFiDGearBundleSetup.exe if the file is meant for update.zip.

.PARAMETER SignTool
signtool.exe. Default: the one in RFiDGearBundleSetup\signtool (a local folder), else the newest of the Windows SDK.

.PARAMETER TimestampUrl
The time stamp server. Default: the one of sign.bat.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Bundle,
    [Parameter(Mandatory)][string]$OutFile,
    [string]$SignTool,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')

$OutFile = Resolve-FullPath $OutFile

if (-not $env:RFIDGEAR_SIGN_PFX -or -not (Test-Path -LiteralPath $env:RFIDGEAR_SIGN_PFX)) {
    throw 'Set RFIDGEAR_SIGN_PFX to the path of your .pfx file, and RFIDGEAR_SIGN_PASSWORD to its password.'
}

if (-not $SignTool) {
    $local = Join-Path $PSScriptRoot 'signtool\signtool.exe'
    if (Test-Path -LiteralPath $local) {
        $SignTool = $local
    }
    else {
        $SignTool = Get-ChildItem -Path (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin\*\x64\signtool.exe') -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
}

if (-not $SignTool -or -not (Test-Path -LiteralPath $SignTool)) {
    throw 'signtool.exe was not found; pass -SignTool.'
}

$insignia = Join-Path (Get-WixToolPath) 'insignia.exe'
$bundlePath = (Resolve-Path -LiteralPath $Bundle).Path
$signArguments = @('sign', '/f', $env:RFIDGEAR_SIGN_PFX, '/p', $env:RFIDGEAR_SIGN_PASSWORD, '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')
$work = Join-Path ([IO.Path]::GetTempPath()) ('rfidgear-sign-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

try {
    $engine = Join-Path $work 'engine.exe'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutFile) | Out-Null

    Invoke-Step 'detach the engine of the bundle' { & $insignia -ib $bundlePath -o $engine }
    Invoke-Step 'sign the engine' { & $SignTool @signArguments $engine }
    Invoke-Step 'attach the engine to the bundle' { & $insignia -ab $engine $bundlePath -o $OutFile }
    Invoke-Step 'sign the bundle' { & $SignTool @signArguments $OutFile }
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

$signature = Get-AuthenticodeSignature -LiteralPath $OutFile
Write-Output "Signed bundle: $OutFile"
Write-Output "Signature: $($signature.Status), $($signature.SignerCertificate.Subject)"
