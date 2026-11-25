# Testing — Local runs and CI

This document explains how to compile the project and run Unity Test Runner tests locally and in CI.

Local test runner helper

At the repository root there is a PowerShell script `run-unity.ps1` which can be used to force a Unity script compile or run EditMode/PlayMode tests.

Examples (PowerShell):

- Force a compile (no tests):

```powershell
.\run-unity.ps1
```

- Run EditMode tests:

```powershell
.\run-unity.ps1 -RunEditModeTests
```

- Run both EditMode and PlayMode tests (runs EditMode then PlayMode):

```powershell
.\run-unity.ps1 -RunEditModeTests -RunPlayModeTests
```

By default the script attempts to detect the Unity executable via the `UNITY_PATH` environment variable, or by reading `ProjectSettings/ProjectVersion.txt` and probing common install locations. You can also pass `-UnityPath` to the script to point to the editor directly.

Output

Test results and logs are written to the `TestResults/` directory (XML test result files and `unity.log`). The script returns a non-zero exit code when Unity or the test runner reports failures.