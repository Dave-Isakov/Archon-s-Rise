# Windows Build Runbook

How to cut a playable Windows `.exe`. Written while producing **0.1.0-alpha** (2026-08-11).

## Prerequisites

- Unity **6000.5.1f1** (`ProjectSettings/ProjectVersion.txt` pins it).
- The **`windowsstandalonesupport`** module, at
  `C:\Program Files\Unity\Hub\Editor\6000.5.1f1\Editor\Data\PlaybackEngines\`.
- Nothing else. No IL2CPP module, no Visual Studio C++ toolchain — the project builds on **Mono**.

## Why builds are driven from the GUI

`Temp/UnityLockfile` exists whenever the editor is open, and it blocks `-batchmode` builds. Either
close the editor to build headless, or build from the open editor. The GUI is the path of least
resistance for a solo project and is what 0.1.0-alpha used.

## Player Settings

`Edit > Project Settings > Player`. Values below are the shipped 0.1.0-alpha configuration.

| Setting | Value | Notes |
|---|---|---|
| Company Name | `Archon's Rise` | **Load-bearing** — see the save-path warning below |
| Product Name | `Archon's Rise` | |
| Version | `0.1.0-alpha` | The only field to bump for a routine re-release |
| Default Icon | `Assets/Images/AppIcon/AppIcon.png` | Must import as Texture Type **Default**, not Sprite |
| Fullscreen Mode | Windowed | Alpha testers are on unknown monitors |
| Default Screen Width / Height | 1600 / 900 | |
| Resizable Window | ☑ | |
| Allow Fullscreen Switch | ☑ | Alt+Enter is the only way to go fullscreen — there is no options screen |
| Scripting Backend | Mono | Default; do not change without installing the IL2CPP module |

### ⚠️ Company Name decides the save folder

The save lives at `%USERPROFILE%\AppData\LocalLow\<Company Name>\Archon's Rise\Save.json`
(`DataManager.SaveFilePath`, via `Application.persistentDataPath`). **Changing Company Name orphans
every existing save**, in the editor and in every installed build. It was set to `Archon's Rise` for
0.1.0-alpha, before any tester had a save. Do not touch it again.

## Build

`File > Build Profiles` → **Windows**, architecture **x86_64**, Development Build **off** → Build.

Output to a folder **outside the repo** — e.g. `C:\Builds\ArchonsRise-<version>\`. `/[Bb]uild/` is
gitignored, but a build tree inside `Assets/` confuses the asset importer.

Scenes ship in `EditorBuildSettings`: `MainMenu` at index 0, `GameBoard` at 1. `RunEndController`
loads scene 0 by index, so **MainMenu must stay first**.

## Smoke test

Run the `.exe` (not the editor) and confirm:

1. Main menu paints; **New Game** enters `GameBoard`.
2. A card plays, a hex reveals, a fight resolves.
3. **Save**, quit the process, relaunch, **Load Game** restores the run.
4. `Quit` closes the process from both the main menu and mid-run.
5. `%USERPROFILE%\AppData\LocalLow\Archon's Rise\Archon's Rise\Player.log` has no exception spam.

`Player.log` sits beside the save and is the single best artifact to request from a tester — the
project's ~58 `Debug.Log` calls land there even in a non-development build.

## Known gotchas

- **Test assemblies** are already safe: all four (`ArchonsRise.Tests.EditMode`, `.SaveData.Tests`,
  `.MapMode.Tests`, `.Exploration.Tests`) are `includePlatforms: [Editor]` with a
  `UNITY_INCLUDE_TESTS` define constraint, so they cannot leak into a player build.
- **The Unity splash screen cannot be removed** without a Pro licence. Expect it.
- `Archon's Rise_BurstDebugInformation_DoNotShip/` at the repo root is empty 2022 leftover cruft,
  unrelated to current builds.
- The main menu's **Options** button has an empty `m_Calls` and opens nothing. Hide it or wire it
  before shipping to anyone who isn't you.

## Re-releasing

For a routine version bump: change **Version** only, rebuild, re-run the smoke test. Every other
field above is already correct and should stay put.
