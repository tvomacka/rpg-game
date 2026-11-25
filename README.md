# rpg-game

An isometric open-world RPG in a medieval world where magic is everywhere — and so is magical crime.
You play an investigator who learns to read the traces magic leaves behind: on victims, on witnesses,
on plants, animals and stone. The world is free to explore; the investigations are what pull you through it.

The full design — pillars, scope, systems and the MVP — lives in **[DESIGN.md](DESIGN.md)**.

## Opening the project

Open the repository root in **Unity 6.3 LTS (6000.3.5f2)**, the version in
`ProjectSettings/ProjectVersion.txt`, via Unity Hub. Project code and content live under `Assets/_Project/`;
the MVP scene is `Assets/_Project/Scenes/FirstCase.unity`.

## Running tests

Use the PowerShell helper at the repository root:

```powershell
.\run-unity.ps1                                        # compile only
.\run-unity.ps1 -RunEditModeTests                      # EditMode tests
.\run-unity.ps1 -RunEditModeTests -RunPlayModeTests    # both
```

Results and the Unity log go to `TestResults/`. See [Docs/Testing.md](Docs/Testing.md) for details.
