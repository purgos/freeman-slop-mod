# Freeman Slop Mod

Unofficial BepInEx 5 plugin finishing/fixing parts of *Freeman: Guerrilla Warfare* (Unity 2018.3.14, Mono).
Requires owning the game; this repo never contains game files (`decompiled/` is git-ignored, for reference only).

## Setup (Linux / Proton)
1. BepInEx 5.4.23.2 win_x64 is unpacked into the game folder (`winhttp.dll`, `doorstop_config.ini`, `BepInEx/`).
2. Steam → game → Properties → Launch Options: `WINEDLLOVERRIDES="winhttp=n,b" %command%`
3. `./install.sh` builds both DLLs; the preloader patcher goes to `BepInEx/patchers/`.
   - **Dev (default):** the plugin goes to `BepInEx/scripts/` and is hot-reloaded by ScriptEngine
     (BepInEx.Debug **r11.1**, unpack `ScriptEngine_r11.1.zip` into `vendor/scriptengine/`). `install.sh` copies
     `ScriptEngine.dll` into `plugins/` and enables `LoadOnStart` + `EnableFileSystemWatcher`, so re-running
     `./install.sh` reloads the mod in the running game (F6 also reloads). Only patcher changes need a restart.
   - **Release:** `./install.sh --release` puts the plugin in `BepInEx/plugins/` (no ScriptEngine needed).
   - The file watcher works under Proton: about 3 s after `./install.sh` the mod reloads by itself (F6 forces it).
4. Logs: **`<game>/BepInEx/LiveLog.txt`** is written live by the mod (all BepInEx sources + Unity errors) — read
   this one while the game runs. The BepInEx console window (`[Logging.Console] Enabled = true` in
   `BepInEx/config/BepInEx.cfg`) shows the same live. The files below are buffered under Proton and often only land
   when the game exits: `<game>/BepInEx/LogOutput.log` (look for "Freeman Slop Mod loaded").

Uninstall: delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, `BepInEx/` from the game folder.

## UI conventions (standard for all UI work)
AI assistants: see [`AGENTS.md`](AGENTS.md) for the full style and runtime rules.

- **Native look, always.** New/changed UI is built in the game's own uGUI, never IMGUI/`OnGUI` overlays: clone the
  game's existing elements (buttons, panel backgrounds, `Text` fonts) with `Instantiate` and relabel/rewire them,
  so font, outline, hover/press states and colors match exactly. Accent color = the game's orange
  (`0.93, 0.55, 0.13`). Reference implementation: `FreemanFixes/DepositPanelUi.cs`.
- Cloned buttons: replace `onClick` with a fresh `Button.ButtonClickedEvent()` to drop the original's scene wiring.
- **Leave the transfer panel's top bar alone** — don't put titles/labels/controls in it.
- Log a new panel's hierarchy on first build (`DumpHierarchy`) to tune layout from real rect sizes.

## Fixes
- **White screen under Proton** (preloader patcher): `StartFGW.exe` starts the game without a working directory;
  under Wine it lands in the exe's folder, so `Config.json`/StreamingAssets tables aren't found. The patcher
  `cd`s to the launcher folder before any game code runs (the plugin chainloader is too late).
- **Personal ↔ faction fund transfer** (`UIData` transfer panel, `DepositPanelUi.cs`): native uGUI — the panel's
  amount becomes a typeable InputField and a 0/10/25/50/75/MAX row (clones of the panel's CANCEL button on a copy
  of its background) hangs below it; slider works (starts at 0, whole numbers); fixes vanilla crediting the other
  side even when the debit failed. First open logs the panel hierarchy to `LogOutput.log` for layout tuning.
- **Info popup** (`InfoPopupPatches.cs`): compacted — lines sit in a `GridLayoutGroup` (300x40 cells), shrunk to
  font×1.4 with no spacing; the popup height follows the cell height. Armor-proficiency line no longer clips its number.
- **Popup placement** (`PopupPlacementPatches.cs`): the item info popup opens upward from the cursor, measured from
  what's actually drawn, and stays on screen below the top menu bar (vanilla mixed canvas units with screen pixels
  and ran off the bottom at high resolutions).
- **Party screen layout** (`PartyLayoutPatches.cs`): the soldier info/loadout panel (`UICombat.unitInfoPanel`) is
  lifted to just under the top menu bar while the party screen is open, so item popups fit; restored elsewhere.

## Ideas / TODO
- Diplomacy "Give money / Ask money / Persuade" (`UIPolitical`) confirm does nothing — unimplemented in vanilla.

## Runtime gotchas (old Unity Mono, CLR 2.0)
- Compile against the game's own `Managed/mscorlib|System|System.Core` (`NoStdLib`), not a .NET reference pack.
- Avoid lambdas/static method groups in listeners — use instance methods on small MonoBehaviours.
- Watch `params` calls (`Localization.Format(key)`, `TrimEnd()`, `GUILayout.*`) — pass explicit empty arrays.
- No `Array.Empty` (pass explicit `new GUILayoutOption[0]` to `GUILayout` params) and `LangVersion` ≤ 10
  (C# 11 `<>O` method-group delegate caches fail to load). `install.sh` refuses to install a DLL that uses them.
- Logs: `BepInEx/LogOutput.log`; Unity: `compatdata/773951/pfx/drive_c/users/steamuser/AppData/LocalLow/KK Game Studio/Freeman Guerrilla Warfare/output_log.txt`.

## Decompiling
`DOTNET_ROLL_FORWARD=Major ilspycmd -p -o decompiled -r <Managed> <Managed>/Assembly-CSharp.dll`
(ilspycmd 8.2 in `~/.dotnet/tools`).

## License
Freeman Slop Mod is free software, licensed under the **GNU General Public License v3.0** — see [`LICENSE`](LICENSE).
It covers this repo's own code only. *Freeman: Guerrilla Warfare* and its code/assets belong to their developers and
are not included or redistributed here; you need your own copy of the game. BepInEx is LGPL-2.1 and is installed
separately.
