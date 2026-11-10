#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the RFiDGear Hardware-in-the-Loop (HIL) test suite.

.DESCRIPTION
    Builds RFiDGear.HIL.Tests and executes it against a live reader and card.
    Both Elatec TWN4 and PC/SC readers are auto-detected by the test fixture.

    The suite has two parts. The DESFire workflow tests need a factory-default
    DESFire card. The reader-scan tests (ReaderScanTests) need a reader and a
    card on it, and they run only when hil.settings.json (next to this script)
    asks for them: "readerScan" for the four cases that need no help,
    "interactive" for the three that ask you to move cards (message boxes). If
    the file is missing, this script creates it from hil.settings.sample.json.
    See RFiDGear.HIL.Tests\README.md.

    Prerequisites
    -------------
    - dotnet 8 SDK installed
    - An Elatec TWN4 or a PC/SC-compatible reader connected via USB
    - A factory-default DESFire EV1/EV2/EV3 card on the reader
      (all keys must be zero -- no custom keys)

    Exit codes
    ----------
    0  All tests passed (or all were skipped because no hardware was found)
    1  One or more tests failed with a real error
    2  Build failed -- fix compilation errors first

.PARAMETER Interactive
    Runs the three reader-scan cases that ask you to move cards in this run,
    whatever hil.settings.json says. The reader-scan tests must still be on in
    the file ("readerScan"). The tests learn of the switch from a variable of
    this process, which dies with it; nothing has to be set by hand.

.PARAMETER DryRun
    Shows what would run and stops. Nothing is built, no reader is touched, and
    hil.settings.json is not created.
#>
[CmdletBinding()]
param (
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $Interactive,

    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Prints one line, in a colour if asked. The runner is a console tool and the colours are its point. The lines added
# with the settings file go through this function, so that the rule against Write-Host is suppressed in one place,
# with its reason.
function Write-Line {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '', Justification = 'A console runner: the colours are the point.')]
    param (
        [Parameter(Position = 0)]
        [string] $Text = '',

        [ConsoleColor] $ForegroundColor
    )

    if ($PSBoundParameters.ContainsKey('ForegroundColor')) {
        Write-Host $Text -ForegroundColor $ForegroundColor
    } else {
        Write-Host $Text
    }
}

$ScriptDir   = $PSScriptRoot
$ProjectFile = Join-Path $ScriptDir 'RFiDGear.HIL.Tests\RFiDGear.HIL.Tests.csproj'
$ResultsDir  = Join-Path $ScriptDir 'TestResults\HIL'
$TrxFileName = "hil-$(Get-Date -Format 'yyyyMMdd-HHmmss').trx"
$TrxFile     = Join-Path $ResultsDir $TrxFileName

# ---- Settings ----------------------------------------------------------------
# The reader-scan tests read hil.settings.json themselves. This script makes sure that the file exists (a copy of the
# sample; an existing file is never touched) and says what is in it.
$SettingsFile = Join-Path $ScriptDir 'hil.settings.json'
$SampleFile   = Join-Path $ScriptDir 'hil.settings.sample.json'
$settingsNote = ''
if (-not (Test-Path $SettingsFile)) {
    if ($DryRun) {
        $settingsNote = '  (does not exist yet; it would be created from hil.settings.sample.json)'
    } else {
        try {
            Copy-Item -Path $SampleFile -Destination $SettingsFile -ErrorAction Stop
            $settingsNote = '  (did not exist; created from hil.settings.sample.json)'
        } catch {
            $settingsNote = "  (does not exist and could not be created: $($_.Exception.Message))"
        }
    }
}

$readerScan      = $false
$fileInteractive     = $false
$settingsProblem = $null
try {
    $settingsPath = if (Test-Path $SettingsFile) { $SettingsFile } else { $SampleFile }
    # The tests accept // comments (whole lines, or after a value) and trailing commas, which Windows PowerShell 5.1
    # cannot read by itself. A // counts as a comment after a value only with a space before it and no quote after it
    # on the line, so a string is never cut. /* */ comments are not understood here; the file is then reported below.
    $settingsText = (Get-Content -Raw -Path $settingsPath) -replace '(?m)^\s*//.*$', '' -replace '(?m)\s//[^"\r\n]*$', '' -replace ',(\s*[}\]])', '$1'
    # The same rules, and the same words, as the tests (HilSettings.cs): what the tests would not accept is no setting.
    if (-not ("$settingsText").TrimStart().StartsWith('{')) { throw 'the file must hold one JSON object' }
    $settings     = $settingsText | ConvertFrom-Json
    foreach ($property in $settings.PSObject.Properties) {
        $name = $property.Name.ToLowerInvariant()
        if ($name -ne 'readerscan' -and $name -ne 'interactive') {
            throw "unknown setting `"$($property.Name)`"; the settings are `"readerScan`" and `"interactive`""
        }
        if ($property.Value -isnot [bool]) { throw "`"$($property.Name)`" must be true or false" }
        if ($name -eq 'readerscan') { $readerScan = $property.Value } else { $fileInteractive = $property.Value }
    }
    # ConvertFrom-Json keeps the last of two equal keys; the tests treat a setting that is set twice as a mistake.
    foreach ($key in 'readerScan', 'interactive') {
        if ([regex]::Matches($settingsText, '(?i)"' + $key + '"\s*:').Count -gt 1) { throw "`"$key`" is set twice" }
    }
} catch {
    $settingsProblem = ($_.Exception.Message -split '\r?\n')[0]
    $readerScan  = $false
    $fileInteractive = $false
}

# -Interactive turns the questions on for this run, whatever the file says; the reader-scan tests must still be on there.
# The tests learn of it from a variable of this process (and clear it for a run without the switch).
$interactiveRun = $fileInteractive -or ($Interactive -and $readerScan)
$env:RFIDGEAR_HIL_INTERACTIVE = $(if ($Interactive) { '1' } else { $null })

# ---- Header ------------------------------------------------------------------
Write-Host ""
Write-Host "+--------------------------------------------------------------+" -ForegroundColor Cyan
Write-Line "|         RFiDGear  .  HIL Test Runner                        |" -ForegroundColor Cyan
Write-Host "+--------------------------------------------------------------+" -ForegroundColor Cyan
Write-Host ""
Write-Host "Prerequisites" -ForegroundColor Yellow
Write-Host "  * Elatec TWN4 or PC/SC reader connected via USB"
Write-Host "  * Factory-default DESFire EV1 / EV2 / EV3 card on the reader"
Write-Host "    (all keys must be zero -- never enrolled in any application)"
Write-Line "    The reader-scan tests also need a card on the reader; any card the"
Write-Line "    reader can read will do."
if ($settingsProblem) {
    Write-Line "  * hil.settings.json could not be read by this script: $settingsProblem" -ForegroundColor Yellow
    Write-Line "    The reader-scan tests are skipped if the tests cannot read it either."
} elseif (-not $readerScan) {
    Write-Line '  * The reader-scan tests are switched off. Set "readerScan": true in hil.settings.json.' -ForegroundColor Yellow
    if ($Interactive) { Write-Line '    -Interactive has no effect while they are off.' -ForegroundColor Yellow }
} elseif ($interactiveRun) {
    Write-Line "  * Interactive: message boxes will ask you to put cards on the reader"
    Write-Line "    and to take them off. Have a second card and a non-MIFARE HF card ready."
} else {
    Write-Line '  * Set "interactive": true in hil.settings.json, or start with -Interactive, to'
    Write-Line "    also run the 3 cases that ask you to move cards."
}
Write-Host ""
Write-Host "  If no reader or card is detected the tests skip automatically."
Write-Host ""

# ---- Verify dotnet SDK -------------------------------------------------------
try {
    $dotnetVersion = & dotnet --version 2>&1
    Write-Host "dotnet SDK : $dotnetVersion" -ForegroundColor DarkGray
} catch {
    Write-Host "ERROR: 'dotnet' command not found. Install the .NET 8 SDK." -ForegroundColor Red
    exit 2
}

Write-Host "Project    : $ProjectFile" -ForegroundColor DarkGray
Write-Host "Config     : $Configuration" -ForegroundColor DarkGray
Write-Line ("Settings   : {0}{1}" -f $SettingsFile, $settingsNote) -ForegroundColor DarkGray
if ($settingsProblem) {
    Write-Line "             readerScan = ?, interactive = ?  (this script could not read the file)" -ForegroundColor DarkGray
} else {
    $forThisRun = if ($interactiveRun -and -not $fileInteractive) { '  (on for this run: -Interactive)' } else { '' }
    Write-Line ("             readerScan = {0}, interactive = {1}{2}" -f $readerScan, $fileInteractive, $forThisRun) -ForegroundColor DarkGray
}
Write-Host "Results    : $TrxFile" -ForegroundColor DarkGray
Write-Host ""

# ---- Dry run -----------------------------------------------------------------
if ($DryRun) {
    Write-Line "Dry run: nothing is built, no reader is touched, and no file is created." -ForegroundColor Yellow
    Write-Line "  would run: dotnet test $ProjectFile -c $Configuration"
    exit 0
}

# ---- Build -------------------------------------------------------------------
Write-Host "Building..." -ForegroundColor Cyan
$buildOutput = & dotnet build $ProjectFile -c $Configuration --nologo 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "BUILD FAILED" -ForegroundColor Red
    $buildOutput | ForEach-Object { Write-Host $_ }
    exit 2
}
Write-Host "Build OK" -ForegroundColor Green
Write-Host ""

# ---- Run tests ---------------------------------------------------------------
$null = New-Item -ItemType Directory -Path $ResultsDir -Force

Write-Host "Running HIL tests..." -ForegroundColor Cyan
Write-Host "(Place the card on the reader now if you haven't already.)"
Write-Host ""

& dotnet test $ProjectFile `
    --no-build `
    -c $Configuration `
    --logger "console;verbosity=normal" `
    --logger "trx;LogFileName=$TrxFileName" `
    --results-directory $ResultsDir

$testExitCode = $LASTEXITCODE

# ---- Parse TRX for accurate counts ------------------------------------------
Write-Host ""
$passed  = 0
$failed  = 0
$skipped = 0

if (Test-Path $TrxFile) {
    [xml]$trx = Get-Content $TrxFile

    $ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }

    $results = Select-Xml -Xml $trx -Namespace $ns -XPath '//t:UnitTestResult'
    foreach ($r in $results) {
        switch ($r.Node.outcome) {
            'Passed' { $passed++ }
            'Failed' {
                # xunit 2.x dynamic skip shows as Failed with $XunitDynamicSkip$ prefix
                $msg = $r.Node.Output.ErrorInfo.Message
                if ($msg -and $msg.StartsWith('$XunitDynamicSkip$')) {
                    $skipped++
                } else {
                    $failed++
                }
            }
            default { $skipped++ }
        }
    }
}

# ---- Summary -----------------------------------------------------------------
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host "  Results" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ("  Passed  : {0,3}" -f $passed)  -ForegroundColor Green
Write-Line ("  Skipped : {0,3}  (no hardware detected, or cases not asked for)" -f $skipped) -ForegroundColor Yellow

$failColor = if ($failed -gt 0) { 'Red' } else { 'Green' }
Write-Host ("  Failed  : {0,3}" -f $failed) -ForegroundColor $failColor
Write-Host ""

if ($skipped -gt 0 -and $failed -eq 0 -and $passed -eq 0) {
    Write-Host "  No reader or DESFire card detected -- connect hardware and re-run." -ForegroundColor Yellow
} elseif ($failed -gt 0) {
    Write-Host "  One or more card operations failed. Check output above." -ForegroundColor Red
    Write-Host "  TRX report: $TrxFile" -ForegroundColor DarkGray
} else {
    Write-Host "  All tests passed." -ForegroundColor Green
}

if ($settingsProblem) {
    Write-Line '  This script could not read hil.settings.json (see the Settings line above); the test output says what the tests made of it.' -ForegroundColor DarkGray
} elseif (-not $readerScan) {
    Write-Line '  The reader-scan tests did not run. Set "readerScan": true in hil.settings.json.' -ForegroundColor DarkGray
} elseif (-not $interactiveRun) {
    Write-Line '  The 3 reader-scan cases that move cards did not run. Set "interactive": true in hil.settings.json, or start with -Interactive.' -ForegroundColor DarkGray
}

Write-Host ""

# Exit 0 if no real failures (skips are acceptable)
exit $(if ($failed -gt 0) { 1 } else { 0 })
