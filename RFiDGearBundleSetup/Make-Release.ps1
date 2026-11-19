<#
.SYNOPSIS
Makes a release in one go: builds the installers, signs the bundle with your certificate and packs the files for GitHub.

.DESCRIPTION
Runs Build-Release.ps1, Sign-Release.ps1 and Pack-Release.ps1 one after the other and stops at the first one that fails.

It asks for the certificate (the .pfx file) and for its password, which is typed hidden. Both go into the environment variables
that Sign-Release.ps1 reads (RFIDGEAR_SIGN_PFX, RFIDGEAR_SIGN_PASSWORD) for this run only: nothing is written to a file, and the
variables are put back as they were when the script ends.

Before the build, which takes a while, it checks what a typing error would otherwise show only at the end: the working tree is
clean, the three versions agree, the certificate file opens with the password, it has a private key and it is valid now. Then it
shows what it is going to make and asks once.

Nothing is uploaded. The GitHub release is made by hand from OutDir\upload (DEVELOPER_NOTES.md, Making a release). The build never
copies into a real RFiDGear installation.

.PARAMETER Certificate
The .pfx file. Asked for when it is not given; Enter takes the value of RFIDGEAR_SIGN_PFX if that is set.

.PARAMETER Password
The password of the certificate, as a SecureString. Asked for (hidden) when it is not given.

.PARAMETER OutDir
The folder for the result. Default: release\<version> in the repository (git-ignored). An existing folder is replaced.

.PARAMETER CheckOnly
Ask for everything and check it, then stop before the build. A way to try the certificate and the password.
#>
[CmdletBinding()]
param(
    [string]$Certificate,
    [securestring]$Password,
    [string]$OutDir,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')

$repo = $script:RepositoryRoot
$version = Get-ReleaseVersion
$commit = (git -C $repo rev-parse --short HEAD).Trim()
$branch = (git -C $repo rev-parse --abbrev-ref HEAD).Trim()
if (@(git -C $repo status --porcelain).Count -gt 0) {
    throw 'The working tree has uncommitted changes. A release is built from a clean checkout of the merged commit.'
}

if (-not $OutDir) {
    $OutDir = Join-Path $repo "release\$version"
}

$OutDir = Resolve-FullPath $OutDir

# ---- the certificate and its password
if (-not $Certificate) {
    $default = $env:RFIDGEAR_SIGN_PFX
    $question = 'Certificate (.pfx file)'
    if ($default) {
        $question += " [$default]"
    }

    $Certificate = Read-Host $question
    if (-not $Certificate) {
        $Certificate = $default
    }
}

if (-not $Certificate) {
    throw 'No certificate given.'
}

# A path that was dragged into the window comes with quotes.
$pfx = Resolve-FullPath $Certificate.Trim().Trim('"')
if (-not (Test-Path -LiteralPath $pfx -PathType Leaf)) {
    throw "The certificate file does not exist: '$pfx'."
}

if (-not $Password) {
    $Password = Read-Host 'Password of the certificate (not shown)' -AsSecureString
}

if ($Password.Length -eq 0) {
    throw 'No password given.'
}

try {
    $opened = [Security.Cryptography.X509Certificates.X509Certificate2]::new($pfx, $Password)
}
catch {
    throw 'The certificate could not be opened: the password is wrong, or the file is not a .pfx.'
}

try {
    if (-not $opened.HasPrivateKey) {
        throw 'The certificate has no private key, so it cannot sign.'
    }

    $now = Get-Date
    if ($opened.NotBefore -gt $now -or $opened.NotAfter -lt $now) {
        throw "The certificate is not valid now (valid from $($opened.NotBefore.ToString('yyyy-MM-dd')) to $($opened.NotAfter.ToString('yyyy-MM-dd')))."
    }

    $subject = $opened.Subject
    $validTo = $opened.NotAfter.ToString('yyyy-MM-dd')
}
finally {
    $opened.Dispose()
}

Write-Output ''
Write-Output "Release      $version, commit $commit on $branch"
Write-Output "Certificate  $subject (valid until $validTo)"
Write-Output "Result       $OutDir$(if (Test-Path -LiteralPath $OutDir) { ' (exists and will be replaced)' })"
Write-Output ''

if ($CheckOnly) {
    Write-Output 'Everything is in order. Nothing was built (-CheckOnly).'
    return
}

$answer = Read-Host 'Build, sign and pack this release now? It takes a while. [y/N]'
if ($answer -notmatch '^(y|yes|j|ja)$') {
    Write-Output 'Cancelled, nothing was done.'
    return
}

# ---- build, sign, pack
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

$savedPfx = $env:RFIDGEAR_SIGN_PFX
$savedPassword = $env:RFIDGEAR_SIGN_PASSWORD
$env:RFIDGEAR_SIGN_PFX = $pfx
$env:RFIDGEAR_SIGN_PASSWORD = $plain
try {
    $unsigned = Join-Path $OutDir 'unsigned\RFiDGearBundleSetup.exe'
    $signed = Join-Path $OutDir 'signed\RFiDGearBundleSetup.exe'

    & (Join-Path $PSScriptRoot 'Build-Release.ps1') -OutDir $OutDir
    & (Join-Path $PSScriptRoot 'Sign-Release.ps1') -Bundle $unsigned -OutFile $signed
    & (Join-Path $PSScriptRoot 'Pack-Release.ps1') -Bundle $signed -OutDir $OutDir
}
finally {
    $env:RFIDGEAR_SIGN_PFX = $savedPfx
    $env:RFIDGEAR_SIGN_PASSWORD = $savedPassword
    $plain = $null
}
