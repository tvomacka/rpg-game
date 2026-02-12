<#
run-unity.ps1

Usage examples (PowerShell):
  # Use UNITY_PATH env var or pass -UnityPath
  .\run-unity.ps1 -RunEditModeTests
  .\run-unity.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.3.5f2\Editor\Unity.exe' -RunEditModeTests

What it does:
 - Detects Unity executable (ENV UNITY_PATH, then tries common Hub path using ProjectVersion)
 - Starts Unity in batchmode to cause a script compile and/or run tests
 - Runs EditMode and/or PlayMode tests if requested, writing results to TestResults/*.xml
 - Writes Unity log to TestResults/unity.log

Notes / assumptions:
 - Script is intended to live in the project root. It uses the ProjectSettings/ProjectVersion.txt to infer editor version.
 - You must have a licensed Unity Editor installed (or use Unity Hub with the version listed).
 - This script uses the Unity Test Runner CLI (-runTests). It assumes tests will be added under the project and that Unity will be able to compile them.

#>
param(
    [string] $UnityPath = $env:UNITY_PATH,
    [switch] $RunEditModeTests,
    [switch] $RunPlayModeTests,
    [switch] $ForceDetect,
    [string] $ProjectPath = $(Split-Path -Parent $MyInvocation.MyCommand.Definition),
    [string] $TestResultsDir = "$PSScriptRoot\TestResults",
    [int] $TimeoutSeconds = 600
)

function Write-Log { param([string]$m) Write-Host "[run-unity] $m" }

# Read ProjectVersion to get editor version
$projectVersionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
$editorVersion = $null
if (Test-Path $projectVersionFile) {
    $content = Get-Content -Path $projectVersionFile -Raw
    if ($content -match 'm_EditorVersion:\s*(.+)') { $editorVersion = $matches[1].Trim() }
}

Write-Log "Project path: $ProjectPath"
if ($editorVersion) { Write-Log "Project editor version: $editorVersion" } else { Write-Log "Could not find editor version in $projectVersionFile" }

function Find-UnityExecutable {
    param([string]$preferred)

    if ($preferred -and (Test-Path $preferred)) { return (Resolve-Path $preferred).Path }

    # Common Hub path
    if ($editorVersion) {
        $hubPath = "C:\\Program Files\\Unity\\Hub\\Editor\\$editorVersion\\Editor\\Unity.exe"
        if (Test-Path $hubPath) { return (Resolve-Path $hubPath).Path }
    }

    # Search Hub Editor directory for any version
    $hubEditorsRoot = 'C:\\Program Files\\Unity\\Hub\\Editor'
    if (Test-Path $hubEditorsRoot) {
        $candidates = Get-ChildItem -Path $hubEditorsRoot -Directory | Sort-Object Name -Descending
        foreach ($d in $candidates) {
            $cand = Join-Path $d.FullName 'Editor\Unity.exe'
            if (Test-Path $cand) { return (Resolve-Path $cand).Path }
        }
    }

    # Classic install locations
    $classic = 'C:\\Program Files\\Unity\\Editor\\Unity.exe'
    if (Test-Path $classic) { return (Resolve-Path $classic).Path }

    return $null
}

if (-not $UnityPath -or $ForceDetect) {
    $detected = Find-UnityExecutable -preferred $UnityPath
    if ($detected) { $UnityPath = $detected }
}

if (-not $UnityPath) {
    Write-Host "ERROR: Unity executable not found. Set environment variable UNITY_PATH or pass -UnityPath. Attempted to detect based on ProjectVersion and common install paths." -ForegroundColor Red
    exit 2
}

Write-Log "Using Unity executable: $UnityPath"

# Ensure test results dir
if (-not (Test-Path $TestResultsDir)) { New-Item -ItemType Directory -Path $TestResultsDir -Force | Out-Null }
$logFile = Join-Path $TestResultsDir 'unity.log'

# Build base arguments
$baseArgs = @(
    '-batchmode',
    '-nographics',
    '-silent-crashes',
    '-projectPath', (Resolve-Path $ProjectPath).Path,
    '-logFile', $logFile
)

# If no test flags provided, we still want to start Unity in batchmode to force a compile and then quit
if (-not $RunEditModeTests -and -not $RunPlayModeTests) {
    Write-Log "No test flags provided; launching Unity in batchmode to force script compilation and exit. Use -RunEditModeTests/-RunPlayModeTests to run tests."
    $args = $baseArgs + @('-quit')
    $startInfo = & "$UnityPath" $args
    # Unity returns exit code from process; capture it
    $exitCode = $LASTEXITCODE
    Write-Log "Unity exited with code $exitCode. Log: $logFile"
    exit $exitCode
}

# Build test args
$resultsFiles = @()
if ($RunEditModeTests) {
    $results = Join-Path $TestResultsDir "TestResults-EditMode-$(Get-Date -Format 'yyyyMMdd-HHmmss').xml"
    $resultsFiles += $results
    $testArgs = @('-runTests', '-testPlatform', 'EditMode', '-testResults', $results)
}
if ($RunPlayModeTests) {
    $results = Join-Path $TestResultsDir "TestResults-PlayMode-$(Get-Date -Format 'yyyyMMdd-HHmmss').xml"
    $resultsFiles += $results
    $testArgs = @()
    # If both flags set, run tests in separate runs to select platform cleanly
}

# If both Edit and Play requested, run two Unity invocations (clean)
$overallExit = 0
if ($RunEditModeTests -and $RunPlayModeTests) {
    Write-Log "Running EditMode tests first, then PlayMode tests in a second Unity invocation."

    # EditMode
    $editResults = Join-Path $TestResultsDir "TestResults-EditMode-$(Get-Date -Format 'yyyyMMdd-HHmmss').xml"
    $args1 = $baseArgs + @('-runTests', '-testPlatform', 'EditMode', '-testResults', $editResults, '-quit')
    Write-Log "Starting Unity (EditMode tests). Results: $editResults"
    & "$UnityPath" $args1
    $rc1 = $LASTEXITCODE
    Write-Log "Unity EditMode exit code: $rc1"

    # PlayMode
    $playResults = Join-Path $TestResultsDir "TestResults-PlayMode-$(Get-Date -Format 'yyyyMMdd-HHmmss').xml"
    $args2 = $baseArgs + @('-runTests', '-testPlatform', 'PlayMode', '-testResults', $playResults, '-quit')
    Write-Log "Starting Unity (PlayMode tests). Results: $playResults"
    & "$UnityPath" $args2
    $rc2 = $LASTEXITCODE
    Write-Log "Unity PlayMode exit code: $rc2"

    if ($rc1 -ne 0 -or $rc2 -ne 0) { $overallExit = 1 } else { $overallExit = 0 }
    Write-Log "Combined exit status: $overallExit"
    Write-Log "Logs and results are in: $TestResultsDir"
    exit $overallExit
}

# Single-mode run (either EditMode or PlayMode)
if ($RunEditModeTests -and -not $RunPlayModeTests) {
    $resultsFile = Join-Path $TestResultsDir "TestResults-EditMode-$(Get-Date -Format 'yyyyMMdd-HHmmss').xml"
    $args = $baseArgs + @('-runTests', '-testPlatform', 'EditMode', '-testResults', $resultsFile, '-quit')
    Write-Log "Running Unity tests (EditMode). Results -> $resultsFile"
    & "$UnityPath" $args
    $exitCode = $LASTEXITCODE
    Write-Log "Unity exit code: $exitCode"
    Write-Log "Results and logs: $TestResultsDir"
    exit $exitCode
}

if ($RunPlayModeTests -and -not $RunEditModeTests) {
    $resultsFile = Join-Path $TestResultsDir "TestResults-PlayMode-$(Get-Date -Format 'yyyyMMdd-HHmmss').xml"
    $args = $baseArgs + @('-runTests', '-testPlatform', 'PlayMode', '-testResults', $resultsFile, '-quit')
    Write-Log "Running Unity tests (PlayMode). Results -> $resultsFile"
    & "$UnityPath" $args
    $exitCode = $LASTEXITCODE
    Write-Log "Unity exit code: $exitCode"
    Write-Log "Results and logs: $TestResultsDir"
    exit $exitCode
}

# Fallback
Write-Host "No operation performed." -ForegroundColor Yellow
exit 3
