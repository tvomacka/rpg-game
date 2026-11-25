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

# Unity.exe is a GUI-subsystem executable: the call operator (&) returns immediately without waiting
# and $LASTEXITCODE stays empty. Start the process explicitly, wait for it, and return its exit code.
function Invoke-Unity {
    param([string[]] $UnityArgs)
    $quoted = $UnityArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }
    $proc = Start-Process -FilePath $UnityPath -ArgumentList $quoted -PassThru -NoNewWindow
    $null = $proc.Handle # cache the handle so ExitCode is readable after exit
    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        Write-Log "Unity did not finish within $TimeoutSeconds s; killing it."
        try { $proc.Kill() } catch {}
        return 124
    }
    return $proc.ExitCode
}


# Ensure test results dir
if (-not (Test-Path $TestResultsDir)) { New-Item -ItemType Directory -Path $TestResultsDir -Force | Out-Null }
$projectFullPath = (Resolve-Path $ProjectPath).Path

function Get-BaseArgs {
    param([string] $LogFile)
    return @('-batchmode', '-nographics', '-silent-crashes', '-projectPath', $projectFullPath, '-logFile', $LogFile)
}

# If no test flags provided, we still want to start Unity in batchmode to force a compile and then quit
if (-not $RunEditModeTests -and -not $RunPlayModeTests) {
    Write-Log "No test flags provided; launching Unity in batchmode to force script compilation and exit. Use -RunEditModeTests/-RunPlayModeTests to run tests."
    $logFile = Join-Path $TestResultsDir 'unity.log'
    $exitCode = Invoke-Unity ((Get-BaseArgs $logFile) + @('-quit'))
    Write-Log "Unity exited with code $exitCode. Log: $logFile"
    exit $exitCode
}

# Runs one test platform in its own Unity invocation and returns 0 only if a results file was written
# and reports no failures. Do NOT pass -quit with -runTests: Unity then quits before the tests run.
function Invoke-UnityTests {
    param([string] $Platform)
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $resultsFile = Join-Path $TestResultsDir "TestResults-$Platform-$stamp.xml"
    $logFile = Join-Path $TestResultsDir "unity-$Platform.log"
    Write-Log "Running $Platform tests. Results -> $resultsFile, log -> $logFile"
    $rc = Invoke-Unity ((Get-BaseArgs $logFile) + @('-runTests', '-testPlatform', $Platform, '-testResults', $resultsFile))
    Write-Log "Unity $Platform exit code: $rc"

    if (-not (Test-Path $resultsFile)) {
        Write-Host "ERROR: $Platform produced no results file - tests did not run (compile error?). See $logFile" -ForegroundColor Red
        return 1
    }
    $run = ([xml](Get-Content $resultsFile -Raw)).'test-run'
    Write-Log "$Platform result: $($run.result) - total $($run.total), passed $($run.passed), failed $($run.failed), skipped $($run.skipped)"
    if ([int]$run.failed -gt 0 -or $rc -ne 0) { return 1 }
    return 0
}

$overallExit = 0
if ($RunEditModeTests) { if ((Invoke-UnityTests 'EditMode') -ne 0) { $overallExit = 1 } }
if ($RunPlayModeTests) { if ((Invoke-UnityTests 'PlayMode') -ne 0) { $overallExit = 1 } }
Write-Log "Combined exit status: $overallExit. Logs and results are in: $TestResultsDir"
exit $overallExit
