<#
.SYNOPSIS
Builds the Store package and signs it with a test certificate of yours, so that it can be installed on this PC.

.DESCRIPTION
Asks for the certificate (the .pfx file) and for its password, which is typed hidden. Both go into the environment variables that
Build-StorePackage.ps1 reads (RFIDGEAR_SIGN_PFX, RFIDGEAR_SIGN_PASSWORD) for this run only: nothing is written to a file, and the
variables are put back as they were when the script ends.

Before the build, which takes a while, it checks what a typing error would otherwise show only at the end: the certificate file
opens with the password, it has a private key, it is valid now, and it is meant for code signing. It tells you if this PC does not
trust it (Windows then installs nothing that it signed). Then it shows what it is going to make and asks once. When the package is
made it offers to install it for your user.

The package is for a test on this machine. While store-identity.json has the placeholders, its publisher is taken from the
certificate (Windows refuses a package whose publisher is not the subject of the certificate that signed it); with a real identity
the certificate has to match it. Nothing is uploaded: the Store signs a package that is uploaded, and needs none.

.PARAMETER Certificate
The .pfx file. Asked for when it is not given; Enter takes the value of RFIDGEAR_SIGN_PFX if that is set.

.PARAMETER Password
The password of the certificate, as a SecureString. Asked for (hidden) when it is not given.

.PARAMETER OutputFolder
Where the package is written. Default: RFiDGear.Store\bin\package.

.PARAMETER CheckOnly
Ask for everything and check it, then stop before the build. A way to try the certificate and the password.
#>
[CmdletBinding()]
param(
    [string]$Certificate,
    [securestring]$Password,
    [string]$OutputFolder,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\..\RFiDGearBundleSetup\Release.Common.ps1')
. (Join-Path $PSScriptRoot 'StoreCertificate.ps1')

$identity = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'store-identity.json') | ConvertFrom-Json
$isPlaceholder = $identity.name -like 'Placeholder.*'

if (-not $OutputFolder) {
    $OutputFolder = Join-Path $script:RepositoryRoot 'RFiDGear.Store\bin\package'
}

$OutputFolder = Resolve-FullPath $OutputFolder

# ---- the certificate and its password
if (-not $Certificate) {
    $default = $env:RFIDGEAR_SIGN_PFX
    $question = 'Test certificate (.pfx file)'
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
    $opened = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($pfx, $Password)
}
catch {
    throw 'The certificate could not be opened: the password is wrong, or the file is not a .pfx.'
}

try {
    $check = Test-StoreSigningCertificate -Certificate $opened

    if ($check.Problems.Count -gt 0) {
        throw ($check.Problems -join ' ')
    }

    $subject = $opened.Subject
    $validTo = $opened.NotAfter.ToString('yyyy-MM-dd')
}
finally {
    $opened.Dispose()
}

if (-not $isPlaceholder -and $identity.publisher -ne $subject) {
    throw "The publisher of the package ('$($identity.publisher)', from store-identity.json) is not the subject of the certificate ('$subject'). Windows refuses a package whose signature has another subject: use a certificate whose subject is that publisher, or put the placeholders back for a package for a test."
}

Write-Output ''
Write-Output "Certificate  $subject (valid until $validTo)"
Write-Output "Package      $($identity.name), publisher $subject$(if ($isPlaceholder) { ' (taken from the certificate: store-identity.json has the placeholders)' })"
Write-Output "Result       $OutputFolder"
foreach ($warning in $check.Warnings) {
    Write-Warning $warning
}

Write-Output ''

if ($CheckOnly) {
    Write-Output 'Everything is in order. Nothing was built (-CheckOnly).'
    return
}

$answer = Read-Host 'Build and sign the package now? It takes a few minutes. [y/N]'
if ($answer -notmatch '^(y|yes|j|ja)$') {
    Write-Output 'Cancelled, nothing was done.'
    return
}

# ---- build and sign
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
try {
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

$started = Get-Date
$savedPfx = $env:RFIDGEAR_SIGN_PFX
$savedPassword = $env:RFIDGEAR_SIGN_PASSWORD
$env:RFIDGEAR_SIGN_PFX = $pfx
$env:RFIDGEAR_SIGN_PASSWORD = $plain
try {
    & (Join-Path $PSScriptRoot 'Build-StorePackage.ps1') -OutputFolder $OutputFolder -Sign
}
finally {
    $env:RFIDGEAR_SIGN_PFX = $savedPfx
    $env:RFIDGEAR_SIGN_PASSWORD = $savedPassword
    $plain = $null
}

# ---- install
$built = Get-ChildItem -LiteralPath $OutputFolder -Filter '*.msix' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -ge $started } | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $built) {
    throw 'No package was made.'
}

Write-Output ''
$answer = Read-Host 'Install the package for your user now? A test package of the same name that is installed is replaced. [y/N]'
if ($answer -notmatch '^(y|yes|j|ja)$') {
    Write-Output "Not installed. To install it later: Add-AppxPackage -Path '$($built.FullName)'"
    return
}

# The Appx cmdlets are the ones of Windows PowerShell; the path goes through an environment variable, so that nothing in it needs quoting.
$env:RFIDGEAR_MSIX_PATH = $built.FullName
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -Command 'try { Add-AppxPackage -Path $env:RFIDGEAR_MSIX_PATH -ForceUpdateFromAnyVersion -ErrorAction Stop; ''Installed.'' } catch { ''Not installed: '' + $_.Exception.Message; exit 1 }'
    $installed = ($LASTEXITCODE -eq 0)
}
finally {
    Remove-Item Env:\RFIDGEAR_MSIX_PATH -ErrorAction SilentlyContinue
}

if ($installed) {
    Write-Output "To remove it again: Get-AppxPackage -Name $($identity.name) | Remove-AppxPackage"
}
