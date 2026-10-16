using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace FreemanFixes
{
    [BepInPlugin("purgos.freeman.fixes", "Freeman Slop Mod", "0.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            LogTypeLoadErrors();
            harmony = new Harmony("purgos.freeman.fixes");
            harmony.PatchAll();
            Logger.LogInfo("Freeman Slop Mod loaded");
        }

        // ScriptEngine hot reload destroys the old plugin before loading the new build: undo every patch and every
        // change made to the game's UI, so the reloaded copy starts from vanilla instead of stacking on top.
        private void OnDestroy()
        {
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
            DepositPanelUi.TeardownCurrent();
            InfoPopupPatches.Restore();
            PartyLayoutPatches.Restore();
            Logger.LogInfo("Freeman Slop Mod unloaded");
        }

        // The game's old Mono silently drops plugin types it can't load; say which and why.
        private void LogTypeLoadErrors()
        {
            try
            {
                typeof(Plugin).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (System.Exception e in ex.LoaderExceptions)
                {
                    Logger.LogError("Type load failure: " + e);
                }
            }
        }
    }
}
