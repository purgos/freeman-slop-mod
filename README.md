# Freeman Slop Mod

Unofficial BepInEx 5 plugin finishing/fixing parts of *Freeman: Guerrilla Warfare* (Unity 2018.3.14, Mono).
Requires owning the game; this repo never contains game files (`decompiled/` is git-ignored, for reference only).

## Installing
The mod is two DLLs, `FreemanFixes.dll` (the plugin) and `FreemanFixes.Patcher.dll` (a preloader patcher), loaded
by BepInEx. There are no prebuilt downloads yet, so build them first (see [Building the DLLs](#building-the-dlls)).

**Find the game folder:** Steam → right-click the game → Manage → Browse local files. That opens the launcher folder
(with `StartFGW.exe`); everything below goes one level down, in the inner `Freeman Guerrilla Warfare` folder next to
`Freeman Guerrilla Warfare.exe`. Typical paths:
- Windows: `C:\Program Files (x86)\Steam\steamapps\common\Freeman Guerrilla Warfare\Freeman Guerrilla Warfare`
- Linux: `~/.local/share/Steam/steamapps/common/Freeman Guerrilla Warfare/Freeman Guerrilla Warfare`
  (Flatpak Steam: `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/...`)

### Windows
1. Download **BepInEx 5.4.23.2, `BepInEx_win_x64_5.4.23.2.zip`** from
   [BepInEx releases](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2) and extract it into the game folder,
   so `winhttp.dll`, `doorstop_config.ini` and `BepInEx\` sit next to `Freeman Guerrilla Warfare.exe`.
2. Start the game once and quit; BepInEx creates `BepInEx\plugins`, `BepInEx\patchers` and `BepInEx\config`.
3. Copy `FreemanFixes.dll` into `BepInEx\plugins\` and `FreemanFixes.Patcher.dll` into `BepInEx\patchers\`.
4. Start the game. Settings > General now has a **Dev Mode** toggle, and `BepInEx\LiveLog.txt` says
   "Freeman Slop Mod loaded".

### Linux (Steam Proton)
1. Same BepInEx zip as Windows (the **win_x64** build, not the Linux one; the game runs under Proton). Extract it
   into the game folder next to `Freeman Guerrilla Warfare.exe`.
2. Steam → right-click the game → Properties → Launch Options:
   `WINEDLLOVERRIDES="winhttp=n,b" %command%`. Without it Proton ignores BepInEx's `winhttp.dll`.
3. Start the game once and quit, then copy `FreemanFixes.dll` into `BepInEx/plugins/` and `FreemanFixes.Patcher.dll`
   into `BepInEx/patchers/`.
4. Start the game and check as on Windows. The patcher also fixes the white screen the game shows under Proton.

**Campaign packs** someone shared go in `BepInEx/FreemanSlop_Mods/<name>/`. **Uninstall:** delete `winhttp.dll`,
`doorstop_config.ini`, `.doorstop_version` and `BepInEx/` from the game folder (and the launch option on Linux).

### Building the DLLs
Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and the game with BepInEx installed (the
build compiles against the game's DLLs and BepInEx's `core` folder). From the repo folder, with `<game>` being the
game folder above:
```
dotnet build FreemanFixes/FreemanFixes.csproj -c Release -p:GameDir="<game>" -p:BepCore="<game>/BepInEx/core"
dotnet build FreemanFixes.Patcher/FreemanFixes.Patcher.csproj -c Release -p:GameDir="<game>" -p:BepCore="<game>/BepInEx/core"
```
The DLLs land in `FreemanFixes/bin/Release/` and `FreemanFixes.Patcher/bin/Release/`. This works the same in
PowerShell on Windows. On Linux, `./install.sh` does the build, checks and copying in one step (next section).

## Development setup (Linux / Proton)
Needs the .NET 8 SDK (`~/.dotnet`) and `ilspycmd` (`dotnet tool install -g ilspycmd --version 8.2.0.7535`; `install.sh` uses
it to check the built DLLs). The build compiles against the game's own DLLs, so it only builds on a machine with the
game installed. The game path is set for Flatpak Steam in two places: `GameDir` in
`FreemanFixes/FreemanFixes.csproj` and `GAME` in `install.sh`; change both if your Steam library is elsewhere.

1. Unpack BepInEx 5.4.23.2 win_x64 (`vendor/BepInEx_win_x64_5.4.23.2.zip`) into the game folder (`winhttp.dll`,
   `doorstop_config.ini`, `BepInEx/`).
2. Steam → game → Properties → Launch Options: `WINEDLLOVERRIDES="winhttp=n,b" %command%` (Proton only; on Windows
   BepInEx loads without it).
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

## Settings
`<game>/BepInEx/config/purgos.freeman.fixes.cfg` (written on first run; BepInEx saves it with CRLF line endings):

| Section | Key | Default | What it does |
|---|---|---|---|
| `Dev Mode` | `Enabled` | `false` | Dev mode on/off (same as the Settings toggle). |
| `Dev Mode` | `ShowSettingsToggle` | `true` | Show the Dev Mode toggle in Settings > General. |
| `Dev Mode` | `StoreFree` | `true` | Dev item store prices are free. |
| `Save On Exit` | `Enabled` | `true` | Ask to save when leaving a manual-save game. |

## Contributing
GitHub ([purgos/freeman-slop-mod](https://github.com/purgos/freeman-slop-mod)) is the source repo. `main` is
protected: every change goes through a pull request (branch → push → PR → merge), and force pushes are blocked. A
private Forgejo copy pulls from GitHub hourly as a backup; don't push to it. Never commit game files or decompiled
code (see License).

## UI conventions (standard for all UI work)
AI assistants: see [`AGENTS.md`](AGENTS.md) for the full style and runtime rules.

- **Native look, always.** New/changed UI is built in the game's own uGUI, never IMGUI/`OnGUI` overlays: clone the
  game's existing elements (buttons, panel backgrounds, `Text` fonts) with `Instantiate` and relabel/rewire them,
  so font, outline, hover/press states and colors match exactly. Accent color = the game's orange
  (`0.93, 0.55, 0.13`). Reference implementation: `FreemanFixes/DepositPanelUi.cs`.
- Cloned buttons: replace `onClick` with a fresh `Button.ButtonClickedEvent()` to drop the original's scene wiring.
- **Leave the transfer panel's top bar alone** — don't put titles/labels/controls in it.
- Log a new panel's hierarchy on first build (`DumpHierarchy`) to tune layout from real rect sizes.

## Fixes and features
- **White screen under Proton** (preloader patcher): `StartFGW.exe` starts the game without a working directory;
  under Wine it lands in the exe's folder, so `Config.json`/StreamingAssets tables aren't found. The patcher
  `cd`s to the launcher folder before any game code runs (the plugin chainloader is too late).
- **Personal ↔ faction fund transfer** (`UIData` transfer panel, `DepositPanelUi.cs`): native uGUI — the panel's
  amount becomes a typeable InputField and a 0/10/25/50/75/MAX row (clones of the panel's CANCEL button on a copy
  of its background) hangs below it; slider works (starts at 0, whole numbers); fixes vanilla crediting the other
  side even when the debit failed. First open logs the panel hierarchy to `LiveLog.txt` for layout tuning.
- **Info popup** (`InfoPopupPatches.cs`): compacted — lines sit in a `GridLayoutGroup` (300x40 cells), shrunk to
  font×1.4 with no spacing; the popup height follows the cell height. Armor-proficiency line no longer clips its number.
- **Popup placement** (`PopupPlacementPatches.cs`): the item info popup opens upward from the cursor, measured from
  what's actually drawn, and stays on screen below the top menu bar (vanilla mixed canvas units with screen pixels
  and ran off the bottom at high resolutions).
- **Dev mode** (`DevMode.cs`, `DevModeToggle.cs`, `DevMenu*.cs`, `DevStats.cs`, `DevActions.cs`, `DevStore.cs`), off
  by default. Toggle it in the game's **Settings > General > Dev Mode** (or `[Dev Mode] Enabled` in
  `BepInEx/config/purgos.freeman.fixes.cfg`). Off = the whole End-key cheat panel is disabled, vanilla cheats too.
  On = in the cheat panel "Add All Items" becomes **Dev Menu** and the give-all / add-money / add-XP / Neutrality /
  Wood-Iron and city buttons are hidden. The full-screen menu (built from the game's own panel, header, row and button
  styles; a status line at the bottom shows results, since the game's popups draw behind it) has tabs:
  - **General:** set money, reputation, morale (+ 0/50/100), XP; Heal All; item store + free-prices toggle; export a
    campaign pack (a **Name** and a paragraph-long **Description**, shown in the New Game list); add any soldier type (count); "turn off dev mode and hide its setting" (two clicks; sets
    `[Dev Mode] ShowSettingsToggle = false`, set it back to `true` to restore the Settings toggle).
  - **Factions:** per faction your relation, status (Hostile..Ally), trade and mutual-defense agreements, money, and
    a Wars side column for war/peace between any two factions; Make All Hostile / Neutral / Allies.
  - **Companions:** you + every companion with status (in party / yours, away / prisoner / with a faction / free)
    and Edit / Max Lv / Recruit / "Recruit All Companions" (skips anyone already yours, per `Faction.GetAllHero`);
    stat editor for level, health, morale, unspent points, attributes, skills, weapon proficiency, affection and
    governor stats (cap 10), with Max All Attributes / Skills / Weapons / Companion Stats (zeroing those points).
  - **Cities:** Take All Cities, and every city with its owner: press **Owner** on a city and **Give** it to any
    faction. A change that would leave a faction with nothing (so the game destroys it) asks for a second click.
    Replaces the cheat panel's AddAllCity / AddRecentlyCity, which are hidden in dev mode.
  - **Item store:** a trader with one of every item that never runs out (restocked after every click, keeping
    anything you sold so Revert works), in category pages of up to 200 (the trade grid is a fixed 250 slots); page,
    Free and two-click Clear Bag controls over the grids (the page is a dropdown of every page); Deal works for free trades. Sorting the
    store's side (see Item sort and filter) re-pages each whole category in that order.
- **Item sort and filter** (`ItemSort.cs`): every item exchange screen (traders, the dev store, city storage, the
  armory, loot) has a row of buttons across the top of each side's grid, yours and theirs, with or without dev mode
  (the game's price ▲▼ buttons are hidden, since Sort: Price does the same; the dev store's page, Free and Clear Bag
  buttons sit in a second row):
  - **Category:** All / Weapons / Clothing / Ammo / Food & Medical / Other. It only hides the other items from view;
    nothing moves.
  - **Sort** (a dropdown): Default (the game's order), Price, Name, Damage, Velocity, Fire rate, Magazine, Marksmanship req.,
    Armor, Camouflage, Armor req. (the same number as Armor: the game requires that much Armor Proficiency) or
    Food amount. Items
    without that stat go last. The dropdown only lists the stats something on that side has (in its category; for
    the dev store, the current page's category), and a sort that stops applying falls back to Default.
  - **High first / Low first** (A to Z / Z to A for Name).

  The sort stays applied as items move, keeps your bag's special slots in place and doesn't disturb a pending trade.
  Settings last for the session. The dev store keeps its own category pages instead of the Category button.
- **Campaign packs** (`DevCampaigns.cs`, `CampaignMenu.cs`): the dev menu exports the current game as a campaign
  of its own, with a name and a paragraph-long description. It shows up in **New Game** after the game's story
  campaigns (Native mod only, since that's the only mod with that list), with the description and a summary, and
  starts the same way a story does: Normal/Realistic, then the usual character creation with the game's own
  backgrounds (packs are never backgrounds). The campaign starts at the starting level: you are the character you
  just created and the party joins at its usual level. What the pack sets: your and your companions' attributes,
  skills and weapon stats (yours are a base your character-creation answers add to, as they add to a background's
  in vanilla; a companion's Marksmanship and weapon stats become their base, see Companion bases)
  and companion stats (affection, governor stats); the whole party (soldier types + companions) in the same squads (names, icons, formations,
  slot positions) with everyone's exact loadout (every equipment slot, yours included); your bag, money and faction
  funds; and the political map: who owns every city, which factions are destroyed, your
  relation/status/agreements with every faction and all faction-vs-faction wars. Packs exported before 0.5.0 have
  no map and only hand over your own cities.
  Each export is a **campaign pack** (a small mod depending on this one) in `BepInEx/FreemanSlop_Mods/<name>/`
  (`manifest.json` with the required mod version, currently 0.5.0+, and `campaign.json`); copy the folder to share it. Packs load whether or not dev mode is on. Re-exporting a name asks for a second click.
- **Unread talk marker** (`TalkMarkers.cs`): a companion's Talk button in the companions panel gets a red **(!)**
  while they have an unread line (their life story, or a question their affection has unlocked), using the same
  rule the talk window uses to grey out lines already heard. It clears once you've talked it through and closed the
  window.
- **Companion bases** (`CompanionBases.cs`): a companion's Marksmanship and weapon stats come from their soldier
  table row, and the game recomputes them from it on every level-up and every load (`NPCGen.SetLevel`), so values
  set in the dev menu or by a campaign pack used to vanish. A companion whose values differ from the table now gets
  a base of their own, which the game's level scaling grows from instead. It's rebuilt from the stats restored on
  load (Save extras), so it needs no file of its own. Companions that match their table level up as in vanilla.
- **Save extras** (`DevSaveData.cs`): the game's save drops some values (your max health, morale and extra equipment
  slots; companions' Leadership, Intelligence, skills, unspent points, max health and most equipment slots), so every
  save also writes `FreemanSlop.json` into its folder and loading that save restores them once the world is up.
- **Save on exit** (`SaveOnExit.cs`): in manual-save games, Menu > back to desktop first asks "Save the game before
  exiting?" (Save / Don't Save, a clone of the menu's own return box), then shows the usual return box. Skipped where
  the game can't save, if the world clock hasn't moved since the last save, and in realistic games. Config
  `[Save On Exit] Enabled`.

## Runtime gotchas (old Unity Mono, CLR 2.0)
- Compile against the game's own `Managed/mscorlib|System|System.Core` (`NoStdLib`), not a .NET reference pack.
- Avoid lambdas/static method groups in listeners — use instance methods on small MonoBehaviours. No
  `System.Func`/`System.Action`.
- Don't compare `Type`/`FieldInfo` with `==`/`!=` (those operators don't exist in this runtime); cast to `object`.
- Watch `params` calls (`Localization.Format(key)`, `TrimEnd()`, `GUILayout.*`) — pass explicit empty arrays.
- No `Array.Empty` (pass explicit `new GUILayoutOption[0]` to `GUILayout` params) and `LangVersion` ≤ 10
  (C# 11 `<>O` method-group delegate caches fail to load). `install.sh` refuses to install a DLL that uses any of
  these.
- Logs: read `BepInEx/LiveLog.txt` while the game runs. `BepInEx/LogOutput.log` and the Unity log
  (`compatdata/773951/pfx/drive_c/users/steamuser/AppData/LocalLow/KK Game Studio/Freeman Guerrilla Warfare/output_log.txt`)
  are buffered under Proton.

## Decompiling
`DOTNET_ROLL_FORWARD=Major ilspycmd -p -o decompiled -r <Managed> <Managed>/Assembly-CSharp.dll`
(ilspycmd 8.2 in `~/.dotnet/tools`).

## License
Freeman Slop Mod is licensed under the **MIT License** — see [`LICENSE`](LICENSE). Versions before the switch
to MIT were released under GPL-3.0.
It covers this repo's own code only. *Freeman: Guerrilla Warfare* and its code/assets belong to their developers and
are not included or redistributed here; you need your own copy of the game. BepInEx is LGPL-2.1 and is installed
separately.
