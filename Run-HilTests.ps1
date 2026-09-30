#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the RFiDGear Hardware-in-the-Loop (HIL) DESFire test suite.

.DESCRIPTION
    Builds RFiDGear.HIL.Tests and executes it against a live reader and card.
    Both Elatec TWN4 and PC/SC readers are auto-detected by the test fixture.

    Prerequisites
    -------------
    - dotnet 8 SDK installed
    - An Elatec TWN4 **or** a PC/SC-compatible reader connected via USB
    - A factory-default DESFire EV1/EV2/EV3 card on the reader
      (all keys must be zero — no custom keys)

    Exit codes
    ----------
    0  All tests passed (or all were skipped because no hardware was found)
    1  One or more tests failed with a real error
    2  Build failed — fix compilation errors first
#>
[CmdletBinding()]
param (
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir   = $PSScriptRoot
$ProjectFile = Join-Path $ScriptDir 'RFiDGear.HIL.Tests\RFiDGear.HIL.Tests.csproj'
$ResultsDir  = Join-Path $ScriptDir 'TestResults\HIL'
$TrxFileName = "hil-$(Get-Date -Format 'yyyyMMdd-HHmmss').trx"
$TrxFile     = Join-Path $ResultsDir $TrxFileName

# ── Header ────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║          RFiDGear  ·  HIL DESFire Test Runner               ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Prerequisites" -ForegroundColor Yellow
Write-Host "  • Elatec TWN4 or PC/SC reader connected via USB"
Write-Host "  • Factory-default DESFire EV1 / EV2 / EV3 card on the reader"
Write-Host "    (all keys must be zero — never enrolled in any application)"
Write-Host ""
Write-Host "  If no reader or card is detected the tests skip automatically."
Write-Host ""

# ── Verify dotnet SDK ─────────────────────────────────────────────────────────
try {
    $dotnetVersion = & dotnet --version 2>&1
    Write-Host "dotnet SDK : $dotnetVersion" -ForegroundColor DarkGray
} catch {
    Write-Host "ERROR: 'dotnet' command not found. Install the .NET 8 SDK." -ForegroundColor Red
    exit 2
}

Write-Host "Project    : $ProjectFile" -ForegroundColor DarkGray
Write-Host "Config     : $Configuration" -ForegroundColor DarkGray
Write-Host "Results    : $TrxFile" -ForegroundColor DarkGray
Write-Host ""

# ── Build ─────────────────────────────────────────────────────────────────────
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

# ── Run tests ─────────────────────────────────────────────────────────────────
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

# ── Parse TRX for accurate counts ────────────────────────────────────────────
Write-Host ""
$passed  = 0
$failed  = 0
$skipped = 0

if (Test-Path $TrxFile) {
    [xml]$trx = Get-Content $TrxFile

    # TRX namespace
    $ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }

    $results = Select-Xml -Xml $trx -Namespace $ns -XPath '//t:UnitTestResult'
    foreach ($r in $results) {
        switch ($r.Node.outcome) {
            'Passed'  { $passed++  }
            'Failed'  {
                # xunit 2.x dynamic skip shows as Failed with $XunitDynamicSkip$ prefix
                $msg = $r.Node.Output.ErrorInfo.Message
                if ($msg -and $msg.StartsWith('$XunitDynamicSkip$')) {
                    $skipped++
                } else {
                    $failed++
                }
            }
            default   { $skipped++ }
        }
    }
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Results" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ("  Passed  : {0,3}" -f $passed)  -ForegroundColor Green
Write-Host ("  Skipped : {0,3}  (no hardware detected)" -f $skipped) -ForegroundColor Yellow
Write-Host ("  Failed  : {0,3}" -f $failed)  -ForegroundColor $(if ($failed -gt 0) { 'Red' } else { 'Green' })
Write-Host ""

if ($skipped -gt 0 -and $failed -eq 0 -and $passed -eq 0) {
    Write-Host "  No reader or DESFire card detected — connect hardware and re-run." -ForegroundColor Yellow
} elseif ($failed -gt 0) {
    Write-Host "  One or more card operations failed. Check output above." -ForegroundColor Red
    Write-Host "  TRX report: $TrxFile" -ForegroundColor DarkGray
} else {
    Write-Host "  All tests passed." -ForegroundColor Green
}

Write-Host ""

# Exit 0 if no real failures (skips are acceptable)
exit $(if ($failed -gt 0) { 1 } else { 0 })
