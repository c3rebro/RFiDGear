<#
.SYNOPSIS
Compiles the help of RFiDGear (RFiDGear_de.chm, RFiDGear_en.chm) with the command-line compiler of HTML Help Workshop.

.DESCRIPTION
The help is written as HTML in docs\chm_de (German) and docs\chm_en (English). The compiled files are committed next to their sources,
RFiDGear.csproj copies them to the output folder, and the WiX harvest carries them into the installers. Run this script after a change of
a page, and commit the new .chm together with it.

hhc.exe is the compiler that hhw.exe (the HTML Help Workshop window) uses. It reports success with the exit code 1 and failure with 0.
This script treats a compile as good only when the exit code is 1, no line of the output is an HHC error or warning, and the file exists.

.PARAMETER Language
de, en or both (default).

.PARAMETER HhcPath
The full path of hhc.exe. By default the folder of HTML Help Workshop under Program Files (x86) is used.

.PARAMETER Check
Compiles a copy in a temporary folder and leaves the repository untouched. Use it to see that the pages compile.

.EXAMPLE
.\docs\Build-Help.ps1

.EXAMPLE
.\docs\Build-Help.ps1 -Language en -Check
#>
[CmdletBinding()]
param(
    [ValidateSet('de', 'en', 'both')]
    [string]$Language = 'both',

    [string]$HhcPath,

    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-HhcPath {
    param([string]$Explicit)

    if ($Explicit) {
        if (-not (Test-Path -LiteralPath $Explicit)) { throw "hhc.exe was not found at $Explicit" }
        return (Resolve-Path -LiteralPath $Explicit).Path
    }

    $folders = @(${env:ProgramFiles(x86)}, $env:ProgramFiles) | Where-Object { $_ }
    foreach ($folder in $folders) {
        $candidate = Join-Path -Path $folder -ChildPath 'HTML Help Workshop\hhc.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    throw 'hhc.exe was not found. Install HTML Help Workshop, or pass its path with -HhcPath.'
}

function Invoke-HelpCompile {
    param(
        [string]$Hhc,
        [string]$Folder,
        [string]$Project,
        [string]$ChmName
    )

    Push-Location -LiteralPath $Folder
    try {
        $output = & $Hhc $Project 2>&1 | ForEach-Object { "$_" }
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $problems = @($output | Where-Object { $_ -match 'HHC\d+:\s*(Error|Warning)' })
    $chm = Join-Path -Path $Folder -ChildPath $ChmName

    if ($exitCode -ne 1 -or $problems.Count -gt 0 -or -not (Test-Path -LiteralPath $chm) -or (Get-Item -LiteralPath $chm).Length -eq 0) {
        $report = ($output | Where-Object { $_.Trim() }) -join [Environment]::NewLine
        throw "The help project $Project did not compile (hhc exit code $exitCode, $($problems.Count) problems).$([Environment]::NewLine)$report"
    }

    return $chm
}

$docs = $PSScriptRoot
$hhcFile = Get-HhcPath -Explicit $HhcPath
$helps = @(
    @{ Code = 'de'; Folder = 'chm_de'; Project = 'rfidgear_de.hhp'; Chm = 'RFiDGear_de.chm' },
    @{ Code = 'en'; Folder = 'chm_en'; Project = 'rfidgear_en.hhp'; Chm = 'RFiDGear_en.chm' }
) | Where-Object { $Language -eq 'both' -or $_.Code -eq $Language }

foreach ($help in $helps) {
    $source = Join-Path -Path $docs -ChildPath $help.Folder
    $work = $source
    $temp = $null

    if ($Check) {
        $temp = Join-Path -Path ([IO.Path]::GetTempPath()) -ChildPath ('rfidgear-help-' + [Guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $source -Destination $temp -Recurse
        $work = $temp
    }

    try {
        $chm = Invoke-HelpCompile -Hhc $hhcFile -Folder $work -Project $help.Project -ChmName $help.Chm
        $size = (Get-Item -LiteralPath $chm).Length
        $where = if ($Check) { 'checked, not written' } else { $chm }
        Write-Output ('{0}: {1} bytes ({2})' -f $help.Chm, $size, $where)
    }
    finally {
        if ($temp -and (Test-Path -LiteralPath $temp)) { Remove-Item -LiteralPath $temp -Recurse -Force }
    }
}

# hhc leaves its own 1 (success) in the exit code: a caller must see 0.
exit 0
