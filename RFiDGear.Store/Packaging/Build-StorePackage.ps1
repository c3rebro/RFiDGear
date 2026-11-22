<#
.SYNOPSIS
Builds the MSIX package of the Microsoft Store edition of RFiDGear (x64, self-contained).

.DESCRIPTION
The package carries the program with its own .NET runtime (a Store package cannot install the runtime), the assembly that talks
to the Store (RFiDGear.Store.dll and the two assemblies of the Windows SDK projection it calls through), and the Card Check
Assistant in the folder Extensions. It carries no driver of any reader: a package cannot install one, and the Store edition does
not offer to.

The steps: publish the program, build the Store assembly, build the Card Check Assistant and unpack its package, collect all of
it in one folder, check that nothing is in it that must not be, write the manifest and the logos, and pack the folder with
makeappx. The Store signs the package it receives, so an upload needs no signature. A package for a test on this machine does:
set RFIDGEAR_SIGN_PFX and RFIDGEAR_SIGN_PASSWORD (a certificate whose subject is the publisher of the manifest) and pass -Sign.

The identity (name, publisher) is in store-identity.json. With the placeholders in it, the package is named -test.msix and the
script says that it cannot be uploaded.

.PARAMETER OutputFolder
Where the package is written. Default: RFiDGear.Store\bin\package.

.PARAMETER Sign
Signs the package with the certificate of RFIDGEAR_SIGN_PFX (a test certificate; the Store does not need a signature).

.PARAMETER MakeAppx
makeappx.exe. Default: the newest of the Windows SDK.

.PARAMETER SignTool
signtool.exe, used with -Sign. Default: the newest of the Windows SDK.

.PARAMETER TimestampUrl
The time stamp server for -Sign.
#>
[CmdletBinding()]
param(
    [string]$OutputFolder,
    [switch]$Sign,
    [string]$MakeAppx,
    [string]$SignTool,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\..\RFiDGearBundleSetup\Release.Common.ps1')

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing

$Platform = 'x64'

# What must never be in the package: a driver (a package cannot install one, and the policy of the Store is against it), an
# installer, the files of the self-update, and the natives of other platforms.
$ForbiddenFiles = @('*.sys', '*.inf', '*.cat', '*.msi', 'pnputil.exe', 'update.xml', 'update.zip', 'RFiDGearBundleSetup.exe')
$ForbiddenFolders = @('drivers', 'runtimes\linux-x64', 'runtimes\osx-x64', 'runtimes\win-x86', 'runtimes\win-arm64')

function Get-StoreIdentity {
    $path = Join-Path $PSScriptRoot 'store-identity.json'
    $identity = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json

    foreach ($key in 'name', 'publisher', 'publisherDisplayName') {
        if ([string]::IsNullOrWhiteSpace($identity.$key)) {
            throw "store-identity.json has no value for '$key'."
        }
    }

    return $identity
}

# The version of the package is the version of the program. The Store keeps the fourth part for itself: it must be 0.
function Get-StoreVersion {
    $csproj = [xml](Get-Content -Raw -LiteralPath (Join-Path $script:RepositoryRoot 'RFiDGear\RFiDGear.csproj'))
    $version = $csproj.SelectSingleNode('//AssemblyVersion').InnerText

    if ($version -notmatch '^\d+\.\d+\.\d+\.0$') {
        throw "The version '$version' of RFiDGear.csproj is not usable for the Store: it must have four parts and end in .0."
    }

    return $version
}

# The newest tool of the Windows SDK (makeappx.exe, signtool.exe).
function Find-WindowsKitTool {
    param([string]$Name)

    $found = Get-ChildItem -Path (Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin\*\x64\$Name") -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName

    if (-not $found) {
        throw "$Name was not found in the Windows SDK; pass its path."
    }

    return $found
}

# A logo of the package, made from the icon of the program.
function Export-StoreAsset {
    param([string]$Icon, [string]$Path, [int]$Size)

    $source = New-Object System.Drawing.Icon($Icon, 256, 256)
    $original = $source.ToBitmap()
    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($original, 0, 0, $Size, $Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
        $original.Dispose()
        $source.Dispose()
    }
}

# The files of the Card Check Assistant, from the package (.rfext) that its build makes: the same files, the same hashes.
function Copy-ExtensionFromPackage {
    param([string]$Package, [string]$Destination)

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Package)

    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -eq 'extension.json') {
                continue
            }

            # The format allows flat, plain file names only (the reader of packages refuses anything else); check it again here.
            if ($entry.FullName -ne $entry.Name -or [string]::IsNullOrEmpty($entry.Name)) {
                throw "The extension package has an entry that is not a plain file name: '$($entry.FullName)'."
            }

            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $Destination $entry.Name), $true)
        }
    }
    finally {
        $zip.Dispose()
    }
}

# Stops the script when the folder that is going to be packed holds something that must not be in a Store package.
function Assert-StageLayout {
    param([string]$Stage)

    $problems = @()

    foreach ($pattern in $ForbiddenFiles) {
        # @() because a search that finds nothing must add nothing (adding $null would add an element).
        $problems += @(Get-ChildItem -LiteralPath $Stage -Recurse -File -Filter $pattern | ForEach-Object { $_.FullName.Substring($Stage.Length + 1) })
    }

    foreach ($folder in $ForbiddenFolders) {
        if (Test-Path -LiteralPath (Join-Path $Stage $folder)) {
            $problems += $folder
        }
    }

    $required = @('RFiDGear.exe', 'RFiDGear.dll', 'coreclr.dll', 'pcscreaders.dll', 'RFiDGear.Store.dll', 'Microsoft.Windows.SDK.NET.dll',
        'WinRT.Runtime.dll', 'RFiDGear_en.chm', 'RFiDGear_de.chm', 'Extensions\RFiDGear.Extensions.CardCheckAssistant.dll',
        'Extensions\sni.dll', 'Extensions\System.Data.SqlClient.dll', 'AppxManifest.xml')

    foreach ($file in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $Stage $file))) {
            $problems += "missing: $file"
        }
    }

    if ($problems.Count -gt 0) {
        throw ("The package folder is not as it must be:`n  " + ($problems -join "`n  "))
    }
}

$identity = Get-StoreIdentity
$version = Get-StoreVersion
$isPlaceholder = $identity.name -like 'Placeholder.*'

if (-not $OutputFolder) {
    $OutputFolder = Join-Path $script:RepositoryRoot 'RFiDGear.Store\bin\package'
}

$OutputFolder = Resolve-FullPath $OutputFolder
$suffix = if ($isPlaceholder) { '-test' } else { '' }
$package = Join-Path $OutputFolder "RFiDGear-$version-$Platform$suffix.msix"
$work = Join-Path $OutputFolder 'work'
$stage = Join-Path $work 'stage'

if (-not $MakeAppx) {
    $MakeAppx = Find-WindowsKitTool 'makeappx.exe'
}

if (Test-Path -LiteralPath $work) {
    Remove-Item -LiteralPath $work -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stage | Out-Null

$hostProject = Join-Path $script:RepositoryRoot 'RFiDGear\RFiDGear.csproj'
$storeProject = Join-Path $script:RepositoryRoot 'RFiDGear.Store\RFiDGear.Store.csproj'
$extensionProject = Join-Path $script:RepositoryRoot 'RFiDGear.Extensions\CardCheckAssistant\RFiDGear.Extensions.CardCheckAssistant.csproj'

# The runtime identifier is the old one (win10-x64) on purpose: the natives of LibLogicalAccess are filed under it in their NuGet
# package, and a publish for win-x64 leaves all 46 of them out, which would leave the PC/SC reader dead. The old runtime
# identifiers are known to the SDK only with the old runtime identifier graph, which is switched on for every project of the build.
Invoke-Step 'publish the program (self-contained, x64)' {
    dotnet publish $hostProject -c Release -r win10-x64 --self-contained true -p:Platform=$Platform -p:UseRidGraph=true -o (Join-Path $work 'host') --nologo -v:minimal
}

Invoke-Step 'build the Store assembly' {
    dotnet build $storeProject -c Release -p:Platform=$Platform --nologo -v:minimal
}

# The package (.rfext) of the Card Check Assistant is written into a folder of this run, and asked for by name: the build of an
# extension makes its package by default, but a failure of that step is only a warning unless the package is asked for, and the
# build would then succeed and leave an old package of an earlier build in the folder where this script looks.
$extensionPackages = Join-Path $work 'extension'

# The package takes every library of the output folder of the extension (PackExtension.targets): nothing of an earlier build may be
# left in it (a library that is not built any more, one that was copied in by hand), so the folder is cleared and the build is not
# incremental, as Build-Release.ps1 does. The deploy step of the extension build copies into a real RFiDGear when the machine says
# so (deploy.local.props, RFIDGEAR_DEPLOY_EXTENSIONS): it is off here, whatever the machine says.
$extensionOutput = Join-Path (Split-Path -Parent $extensionProject) "bin\$Platform\Release"
Remove-Item -LiteralPath $extensionOutput -Recurse -Force -ErrorAction SilentlyContinue

Invoke-Step 'build the Card Check Assistant' {
    dotnet build $extensionProject -c Release -p:Platform=$Platform -p:PackExtension=true "-p:ExtensionPackageFolder=$extensionPackages" -p:DeployExtensionsToRFiDGear=false --no-incremental --nologo -v:minimal
}

Write-Output '== collect the files'
Get-ChildItem -LiteralPath (Join-Path $work 'host') -Force | Where-Object { $_.Extension -ne '.pdb' } | Copy-Item -Destination $stage -Recurse -Force

$payloadJson = & dotnet msbuild $storeProject -p:Configuration=Release -p:Platform=$Platform -t:GetStorePayload -getItem:StorePayloadFile -nologo
if ($LASTEXITCODE -ne 0) {
    throw "The payload of the Store assembly could not be read (exit code $LASTEXITCODE)."
}

foreach ($item in ($payloadJson | ConvertFrom-Json).Items.StorePayloadFile) {
    Copy-Item -LiteralPath $item.FullPath -Destination $stage -Force
}

$extensionPackage = @(Get-ChildItem -LiteralPath $extensionPackages -Filter "*-$Platform.rfext")

if ($extensionPackage.Count -ne 1) {
    throw "The build of the Card Check Assistant made $($extensionPackage.Count) packages (.rfext) for Release in its own folder; exactly one was expected."
}

Copy-ExtensionFromPackage -Package $extensionPackage[0].FullName -Destination (Join-Path $stage 'Extensions')

Write-Output '== logos and manifest'
$assets = Join-Path $stage 'Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$icon = Join-Path $script:RepositoryRoot 'RFiDGear\Resources\logo.ico'
Export-StoreAsset -Icon $icon -Path (Join-Path $assets 'Square44x44Logo.png') -Size 44
Export-StoreAsset -Icon $icon -Path (Join-Path $assets 'Square150x150Logo.png') -Size 150
Export-StoreAsset -Icon $icon -Path (Join-Path $assets 'StoreLogo.png') -Size 50

$manifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.template.xml')
$values = @{
    IdentityName         = $identity.name
    Publisher            = $identity.publisher
    PublisherDisplayName = $identity.publisherDisplayName
    Version              = $version
}

foreach ($key in $values.Keys) {
    $manifest = $manifest.Replace("{{$key}}", [System.Security.SecurityElement]::Escape($values[$key]))
}

if ($manifest -match '\{\{') {
    throw 'The manifest still has a {{value}} that was not filled in.'
}

[xml]$manifest | Out-Null
[System.IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest, (New-Object System.Text.UTF8Encoding($false)))

Assert-StageLayout -Stage $stage

Invoke-Step 'pack the folder (makeappx)' {
    & $MakeAppx pack /d $stage /p $package /o
}

if ($Sign) {
    if (-not $env:RFIDGEAR_SIGN_PFX -or -not (Test-Path -LiteralPath $env:RFIDGEAR_SIGN_PFX)) {
        throw 'Set RFIDGEAR_SIGN_PFX to the path of a .pfx file (a test certificate whose subject is the publisher of the manifest), and RFIDGEAR_SIGN_PASSWORD to its password.'
    }

    if (-not $SignTool) {
        $SignTool = Find-WindowsKitTool 'signtool.exe'
    }

    $timestampServer = $TimestampUrl

    Invoke-Step 'sign the package' {
        & $SignTool sign /f $env:RFIDGEAR_SIGN_PFX /p $env:RFIDGEAR_SIGN_PASSWORD /fd SHA256 /tr $timestampServer /td SHA256 $package
    }
}

Remove-Item -LiteralPath $work -Recurse -Force

$info = Get-Item -LiteralPath $package
Write-Output "Package: $($info.FullName)"
Write-Output ("Size: {0:N1} MB, version {1}, SHA-256 {2}" -f ($info.Length / 1MB), $version, (Get-FileSha256 $package))

if ($isPlaceholder) {
    Write-Warning 'store-identity.json still has the placeholders: this package is for a test on this machine and cannot be uploaded to the Store.'
}
