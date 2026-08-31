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

## Packaging for distribution

Drop a `README.txt` beside the `.exe` first — the build does not generate one, so it must be copied
in after every build. Keep it covering: extract the whole folder, the SmartScreen click-through, the
save location, and how to send back `Player.log`.

Then zip the folder. **Do not use `Compress-Archive` or .NET's `ZipFile.CreateFromDirectory` on
Windows PowerShell 5.1** — .NET Framework writes `\` as the path separator, which violates the ZIP
spec (entry names must use `/`). Windows Explorer tolerates it; macOS and Linux `unzip` do not, and
produce a pile of files with literal backslashes in their names instead of a folder tree.

Build entry names explicitly instead:

```powershell
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$src = "C:\Builds\ArchonsRise-<version>"; $dst = "$src.zip"; $root = "ArchonsRise-<version>"
$bs = [char]92; $fs = [char]47
$zip = [System.IO.Compression.ZipFile]::Open($dst, 'Create')
foreach ($f in Get-ChildItem -LiteralPath $src -Recurse -File) {
  $rel = $f.FullName.Substring($src.Length + 1).Replace($bs, $fs)
  [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, "$root$fs$rel", 'Optimal')
}
$zip.Dispose()
```

Zipping the folder (not its contents) matters: it stops extraction from dumping eight loose items
into the tester's Downloads, and keeps the `.exe` beside `Archon's Rise_Data`, without which the
game will not launch.

Verify before sending: exactly one top-level entry, zero entries containing a backslash, and a
round-trip extract whose file count and total bytes match the source. 0.1.0-alpha packed 161MB of
build into a **49MB** zip (222 files).

## Re-releasing

For a routine version bump: change **Version** only, rebuild, re-run the smoke test, then repackage
per the section above (README copy included — a rebuild does not carry it over). Every other field
in the settings table is already correct and should stay put.
