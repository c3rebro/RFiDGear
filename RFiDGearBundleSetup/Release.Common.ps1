# Shared helpers of Build-Release.ps1, Sign-Release.ps1 and Pack-Release.ps1 (dot-sourced; nothing runs here).
Set-StrictMode -Version Latest

$script:RepositoryRoot = Split-Path -Parent $PSScriptRoot

# Runs a native command and stops the script when it ends with an error code.
function Invoke-Step {
    param([string]$Title, [scriptblock]$Command)

    Write-Output "== $Title"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Title failed (exit code $LASTEXITCODE)."
    }
}

# The three places that carry the version of a release, by hand: the host (and with it the MSIs and the update check), the
# bundle, and the update manifest. The release is built only when they agree.
function Get-ReleaseVersion {
    $csproj = [xml](Get-Content -Raw -LiteralPath (Join-Path $script:RepositoryRoot 'RFiDGear\RFiDGear.csproj'))
    $host_ = $csproj.SelectSingleNode('//AssemblyVersion').InnerText

    $bundleText = Get-Content -Raw -LiteralPath (Join-Path $script:RepositoryRoot 'RFiDGearBundleSetup\Bundle.wxs')
    $match = [regex]::Match($bundleText, '<\?define VersionNumber="([0-9.]+)" \?>')
    if (-not $match.Success) {
        throw 'Bundle.wxs does not define VersionNumber.'
    }

    $bundle = $match.Groups[1].Value
    $manifest = ([xml](Get-Content -Raw -LiteralPath (Join-Path $script:RepositoryRoot 'RFiDGear\update.xml'))).Manifest.version

    if ($host_ -ne $bundle -or $host_ -ne $manifest) {
        throw "The versions differ: RFiDGear.csproj $host_, Bundle.wxs $bundle, update.xml $manifest."
    }

    return $host_
}

# A property of an MSI (ProductVersion, ...), read through the Windows Installer.
function Get-MsiProperty {
    param([string]$Msi, [string]$Name)

    $installer = New-Object -ComObject WindowsInstaller.Installer
    $db = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @((Resolve-Path -LiteralPath $Msi).Path, 0))
    $view = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, @("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = '$Name'"))
    $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null) | Out-Null
    $record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
    $value = $null
    if ($null -ne $record) {
        $value = $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, @(1))
    }

    $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null

    return $value
}

# The names of the features of an MSI.
function Get-MsiFeature {
    param([string]$Msi)

    $installer = New-Object -ComObject WindowsInstaller.Installer
    $db = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @((Resolve-Path -LiteralPath $Msi).Path, 0))
    $view = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db, @('SELECT `Feature` FROM `Feature`'))
    $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null) | Out-Null
    $names = @()
    while ($true) {
        $record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
        if ($null -eq $record) {
            break
        }

        $names += $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, @(1))
    }

    $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null

    return $names
}

# The folder of the WiX 3 tools (heat, insignia, ...).
function Get-WixToolPath {
    $path = Join-Path ${env:ProgramFiles(x86)} 'WiX Toolset v3.14\bin'
    if (-not (Test-Path -LiteralPath (Join-Path $path 'heat.exe'))) {
        throw "The WiX Toolset 3.14 is not installed ('$path' has no heat.exe)."
    }

    return $path
}

# The MSBuild of Visual Studio that builds the WiX projects (they need the one of Visual Studio 2026 or later, because the
# bundle references the host project, which is an SDK 10 project).
function Get-MSBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) {
            return $found
        }
    }

    $fallback = 'C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe'
    if (Test-Path -LiteralPath $fallback) {
        return $fallback
    }

    throw 'No MSBuild of Visual Studio was found.'
}

function Get-FileSha256 {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
