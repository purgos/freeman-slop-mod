# Instructions for AI assistants

Freeman Slop Mod is a BepInEx 5 mod for *Freeman: Guerrilla Warfare* (Unity 2018.3.14, Mono, run under Proton).
Read `README.md` for setup, current fixes, and log locations. These rules are how work here is expected to look.

## UI style (mandatory)
- **Native look only.** Build all UI from the game's own uGUI: `Instantiate` existing buttons, panel backgrounds and
  `Text` objects, then relabel and rewire them, so fonts, outlines, hover/press states and colors match the game.
  Never use IMGUI/`OnGUI` overlays or invented styling.
- Accent color is the game's orange `new Color(0.93f, 0.55f, 0.13f)`; everything else comes from cloned elements.
- When cloning a button, replace `onClick` with `new Button.ButtonClickedEvent()` to drop the original's scene wiring,
  and set `interactable = true`: a clone copies the template's *current* state (e.g. Deal is disabled while no trade
  is pending), so it can come out dead.
- Game code addresses children by index (`GetChild(0)`, `GetChild(1)`): **append** new children last, never insert.
- **Leave the transfer panel's top bar alone**: don't put titles, labels or controls in it.
- Layout is unknown until you see it: on a panel's first build, log its hierarchy with `DepositPanelUi.DumpHierarchy`
  and tune from real rect sizes. Check how the game sizes a panel before resizing it (for example, the info popup's lines
  are `GridLayoutGroup` cells, so the cell size, not the line prefab, sets the line height).
- Prefer changing an existing game panel over adding a new one. Restore vanilla layout on screens a change wasn't meant for.
- Reference implementation: `FreemanFixes/DepositPanelUi.cs`.

## Runtime constraints (the game runs old Mono, CLR 2.0)
Breaking these compiles fine but fails in game, usually silently or as a white or blank UI.
- Compile against the game's own `Managed/mscorlib|System|System.Core` (already set up in the `.csproj` files).
- No `Array.Empty`: pass explicit empty arrays to `params` methods (`Localization.Format(key, new object[0])`,
  `TrimEnd(new char[0])`, `GUILayout.*`).
- `LangVersion` stays ≤ 10. C# 11 method-group delegate caches (`<>O`) don't load.
- No lambdas, LINQ delegates or `System.Func`/`System.Action`. For Unity events, use instance methods on small
  `MonoBehaviour`s with `new UnityAction(...)` / `new UnityAction<T>(...)`.
- Don't compare `FieldInfo`/`MethodInfo`/`Type` with `==`/`!=` (those operators are .NET 4 only); cast to
  `object` first.
- `install.sh` refuses to install a DLL containing `Array::Empty`, `<>O`, or `System.Func`/`System.Action`.
  Never bypass that check.
- Anything that must run before game code (working directory, config) goes in the preloader patcher
  (`FreemanFixes.Patcher`), not the plugin. The plugin chainloader starts after the first scene.

- `TerrainControl.initiated` is only true in battle (`CheckCombat`); "world map ready" = `TerrainControl.code.isWorld`
  plus a few seconds of settling.

## Code style
- Match the surrounding code: 4-space indent, braces on their own lines, explicit types where the existing code uses them.
- One feature per file, named for the feature (`TransferPatches.cs`, `InfoPopupPatches.cs`); Harmony patch classes are
  `internal static`, with `[HarmonyPatch]` on the class and `Postfix`/`Prefix` methods.
- Each patch file starts with a comment saying **what the game does and why we change it**, naming the game class and
  method (and decompiled file/line where useful). Keep inline comments for non-obvious reasons only.
- Prefer postfixes; use a skipping prefix (`return false`) only when replacing vanilla behavior wholesale, and say so.
- Mirror the game's own checks (for example `NPCGen` equip rules) rather than inventing new rules.
- Log via `Plugin.Log`; log once per panel or session, never every frame.
- Tunables (sizes, fractions, colors) go in named `const`s at the top of the class.

## Workflow
- Understand the game code first: decompile with `ilspycmd` into `decompiled/` (git-ignored) and cite what you find.
- Build and install with `./install.sh` (dev mode hot-reloads via ScriptEngine; no restart except for the patcher).
  Hot reload only works if the plugin undoes itself: anything that changes game state or UI must record the original
  and restore it from `Plugin.OnDestroy` (see `DepositPanelUi.TeardownCurrent`, `InfoPopupPatches.Restore`).
  The old copy's `OnDestroy` runs *after* the new copy's `Awake`, so teardown must only touch its own state
  (hence the per-load Harmony ID).
- Read `BepInEx/LiveLog.txt` (written live by `LiveLog.cs`) while the game runs. `LogOutput.log` and Unity's
  `output_log.txt` are buffered under Proton and lag until the game exits, so an absent line there proves nothing.
- Verify through `BepInEx/LogOutput.log` and the Unity `output_log.txt`
  (paths in README). Ask the user for an in-game screenshot to confirm UI changes; don't claim they work unseen.
- Keep `README.md` current: add each fix under **Fixes** and each new pitfall under **Runtime gotchas**.

## Legal and repo rules
- GPL-3.0. Commit only original code and docs. **Never commit or paste game code, decompiled output, game DLLs,
  assets or tables** (`decompiled/`, `decompiled-launcher/`, `vendor/` stay ignored).
- GitHub (`origin`) is the source of truth; push and merge there. The Forgejo copy
  (git.purgos.lilium.wtf/purgos/freeman-slop-mod) is a read-only pull mirror (hourly), so never push to it.
- Work on feature branches and merge to `main` through PRs; keep `main` releasable.
