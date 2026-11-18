<#
.SYNOPSIS
Builds the installers of a release: the x64 and x86 MSIs (each in German and English) and the setup bundle. Nothing is signed.

.DESCRIPTION
Builds the host, the sample plugin and the Card Check Assistant in Release for x64 and x86, the two MSI projects and the
bundle, from a clean build, and puts the result into OutDir:
  OutDir\unsigned\RFiDGearBundleSetup.exe   the bundle, to be signed (Sign-Release.ps1) and then packed (Pack-Release.ps1)
  OutDir\msi\                               the four MSIs, for the record (they are inside the bundle)
  OutDir\update.xml                         the manifest of this commit
  OutDir\SHA256SUMS-unsigned.txt

It stops when the version differs between RFiDGear.csproj, Bundle.wxs and update.xml, when the working tree has changes (use
-AllowDirty for a trial), and when an MSI does not carry the version or the Card Check Assistant feature. It never copies
anything into a real RFiDGear installation (the deploy step of the extension builds is switched off).

Needs: the .NET SDK, the WiX Toolset 3.14, Visual Studio 2026 (or later) with MSBuild, and a network connection the first time
(the .NET Desktop Runtime installers that go into the bundle are downloaded and checked by Get-DotNetRuntime.ps1).

.PARAMETER OutDir
The folder for the result. Default: release\<version> in the repository (git-ignored).

.PARAMETER AllowDirty
Build although the working tree has uncommitted changes. The result is not a release.
#>
[CmdletBinding()]
param(
    [string]$OutDir,
    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Release.Common.ps1')

$repo = $script:RepositoryRoot
$version = Get-ReleaseVersion
Write-Output "Release $version"

$commit = (git -C $repo rev-parse --short HEAD).Trim()
$dirty = @(git -C $repo status --porcelain).Count -gt 0
if ($dirty -and -not $AllowDirty) {
    throw 'The working tree has uncommitted changes. Commit them, or pass -AllowDirty for a trial that is not a release.'
}

Write-Output "Commit $commit$(if ($dirty) { ' (with uncommitted changes)' })"

if (-not $OutDir) {
    $OutDir = Join-Path $repo "release\$version"
}

$msbuild = Get-MSBuildPath
[void](Get-WixToolPath)

# The deploy step of the extension builds copies into a real RFiDGear when asked to: it is off here, whatever the machine says.
$noDeploy = '-p:DeployExtensionsToRFiDGear=false'

foreach ($platform in 'x64', 'x86') {
    foreach ($project in 'RFiDGear\RFiDGear.csproj', 'RFiDGear.Extensions\DesfirePluginSample', 'RFiDGear.Extensions\CardCheckAssistant') {
        Invoke-Step "dotnet build $project ($platform)" { dotnet build (Join-Path $repo $project) -c Release "-p:Platform=$platform" $noDeploy --no-incremental -nologo -v:m }
    }

    Invoke-Step "MSI $platform (German and English)" { & $msbuild (Join-Path $repo "RFiDGearSetup\RFiDGear_Setup_$platform\RFiDGearSetup_$platform.wixproj") -t:Rebuild -p:Configuration=Release "-p:Platform=$platform" -nologo -v:m }
}

# The bundle project starts Windows PowerShell (Get-DotNetRuntime.ps1). Started from PowerShell 7 it would inherit the module
# path of PowerShell 7 and could not find its own modules (Get-FileHash), so it gets the path of the system and the user.
$savedModulePath = $env:PSModulePath
$env:PSModulePath = (@([Environment]::GetEnvironmentVariable('PSModulePath', 'User'), [Environment]::GetEnvironmentVariable('PSModulePath', 'Machine')) | Where-Object { $_ }) -join ';'
try {
    Invoke-Step 'bundle' { & $msbuild (Join-Path $repo 'RFiDGearBundleSetup\RFiDGearBundleSetup.wixproj') -t:Rebuild -p:Configuration=Release -p:Platform=x86 -nologo -v:m }
}
finally {
    $env:PSModulePath = $savedModulePath
}

# ---- what came out, and whether it is what it should be
$bundle = Join-Path $repo 'RFiDGearBundleSetup\bin\Release\RFiDGearBundleSetup.exe'
if (-not (Test-Path -LiteralPath $bundle)) {
    throw "The bundle was not built: '$bundle'."
}

if (Test-Path -LiteralPath $OutDir) {
    Remove-Item -LiteralPath $OutDir -Recurse -Force
}

$msiFolder = Join-Path $OutDir 'msi'
New-Item -ItemType Directory -Force -Path (Join-Path $OutDir 'unsigned'), $msiFolder | Out-Null

foreach ($platform in 'x64', 'x86') {
    foreach ($culture in 'de-de', 'en-us') {
        $source = Join-Path $repo "RFiDGearSetup\RFiDGear_Setup_$platform\bin\$platform\Release\$culture\RFiDGear_Setup_$platform.msi"
        if (-not (Test-Path -LiteralPath $source)) {
            throw "The MSI is missing: '$source'."
        }

        $msiVersion = Get-MsiProperty $source 'ProductVersion'
        if ($msiVersion -ne $version) {
            throw "'$source' has the version $msiVersion, not $version."
        }

        if ((Get-MsiFeature $source) -notcontains 'CardCheckAssistant') {
            throw "'$source' does not have the feature CardCheckAssistant."
        }

        Copy-Item -LiteralPath $source -Destination (Join-Path $msiFolder "RFiDGear_Setup_${platform}_$culture.msi")
    }
}

$bundleVersion = (Get-Item -LiteralPath $bundle).VersionInfo.FileVersion
if ($bundleVersion -ne $version) {
    throw "The bundle has the version $bundleVersion, not $version."
}

Copy-Item -LiteralPath $bundle -Destination (Join-Path $OutDir 'unsigned\RFiDGearBundleSetup.exe')
Copy-Item -LiteralPath (Join-Path $repo 'RFiDGear\update.xml') -Destination (Join-Path $OutDir 'update.xml')

$sums = Get-ChildItem -LiteralPath $OutDir -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS-unsigned.txt' } | ForEach-Object {
    '{0}  {1}' -f (Get-FileSha256 $_.FullName), $_.FullName.Substring($OutDir.Length).TrimStart('\')
}
Set-Content -LiteralPath (Join-Path $OutDir 'SHA256SUMS-unsigned.txt') -Value $sums

Write-Output ''
Write-Output "Built release $version from commit $commit into $OutDir"
Get-ChildItem -LiteralPath $OutDir -Recurse -File | ForEach-Object { '  {0,12:N0}  {1}' -f $_.Length, $_.FullName.Substring($OutDir.Length).TrimStart('\') }
Write-Output ''
Write-Output 'Next: sign the bundle (the certificate is not in the repository):'
Write-Output '  $env:RFIDGEAR_SIGN_PFX = "<path of your .pfx>"; $env:RFIDGEAR_SIGN_PASSWORD = "<its password>"'
Write-Output "  .\RFiDGearBundleSetup\Sign-Release.ps1 -Bundle `"$OutDir\unsigned\RFiDGearBundleSetup.exe`" -OutFile `"$OutDir\signed\RFiDGearBundleSetup.exe`""
Write-Output 'then pack what is uploaded:'
Write-Output "  .\RFiDGearBundleSetup\Pack-Release.ps1 -Bundle `"$OutDir\signed\RFiDGearBundleSetup.exe`" -OutDir `"$OutDir`""
