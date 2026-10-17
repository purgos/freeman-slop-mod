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
- **Dev mode** (`DevMode.cs`, `DevModeToggle.cs`, `DevMenu*.cs`, `DevStats.cs`, `DevActions.cs`, `DevStore.cs`), off
  by default. Toggle it in the game's **Settings > General > Dev Mode** (or `[Dev Mode] Enabled` in
  `BepInEx/config/purgos.freeman.fixes.cfg`). Off = the whole End-key cheat panel is disabled, vanilla cheats too.
  On = in the cheat panel "Add All Items" becomes **Dev Menu** and the give-all / add-money / add-XP / Neutrality /
  Wood-Iron buttons are hidden. The full-screen menu (built from the game's own panel, header, row and button
  styles; a status line at the bottom shows results, since the game's popups draw behind it) has tabs:
  - **General:** set money, reputation, morale (+ 0/50/100), XP; Heal All; item store + free-prices toggle; export a
    campaign background; add any soldier type (count); "turn off dev mode and hide its setting" (two clicks; sets
    `[Dev Mode] ShowSettingsToggle = false`, set it back to `true` to restore the Settings toggle).
  - **Factions:** per faction your relation, status (Hostile..Ally), trade and mutual-defense agreements, money, and
    a Wars side column for war/peace between any two factions; Make All Hostile / Neutral / Allies.
  - **Companions:** you + every companion with status (in party / yours, away / prisoner / with a faction / free)
    and Edit / Max Lv / Recruit / "Recruit All Companions" (skips anyone already yours, per `Faction.GetAllHero`);
    stat editor for level, health, morale, unspent points, attributes, skills, weapon proficiency, affection and
    governor stats (cap 10), with Max All Attributes / Skills / Weapons / Companion Stats (zeroing those points).
  - **Item store:** a trader with one of every item that never runs out (restocked after every click, keeping
    anything you sold so Revert works), in category pages of up to 200 (the trade grid is a fixed 250 slots); page,
    Free and two-click Clear Bag controls in the trade screen's top bar; Deal works for free trades.
- **Campaign backgrounds** (`DevCampaigns.cs`): the dev menu exports the current game as a new start background in
  the normal character creation (random world, character creator and faction name/flag as usual). It carries the
  whole party (soldier types + companions) with levels, XP and all stats, in the same squads (names, icons,
  formations, slot positions), everyone's exact loadout (every equipment slot), your bag, every city you own, your
  money and faction funds, your relation/status/agreements with every faction and all faction-vs-faction wars. Each export is a **campaign pack** - a small mod depending on this one - in
  `BepInEx/FreemanSlop_Mods/<name>/` (`manifest.json` with the required mod version + `campaign.json`); copy the
  folder to share it. Packs load whether or not dev mode is on. Re-exporting a name asks for a second click.
- **Save extras** (`DevSaveData.cs`): the game's save drops some values (your max health, morale and extra equipment
  slots; companions' Leadership, Intelligence, skills, unspent points, max health and most equipment slots), so every
  save also writes `FreemanSlop.json` into its folder and loading that save restores them once the world is up.
- **Save on exit** (`SaveOnExit.cs`): in manual-save games, Menu > back to desktop first asks "Save the game before
  exiting?" (Save / Don't Save, a clone of the menu's own return box), then shows the usual return box. Skipped where
  the game can't save, if the world clock hasn't moved since the last save, and in realistic games. Config
  `[Save On Exit] Enabled`.

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
