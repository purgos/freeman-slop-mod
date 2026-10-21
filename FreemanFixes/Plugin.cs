using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace FreemanFixes
{
    [BepInPlugin(Id, "Freeman Slop Mod", Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string Id = "purgos.freeman.fixes";
        internal const string Version = "0.5.0";

        internal static ManualLogSource Log;

        private Harmony harmony;
        private LiveLog liveLog;

        private void Awake()
        {
            Log = Logger;
            liveLog = LiveLog.Start();
            LogTypeLoadErrors();
            // Unique per load: on a ScriptEngine reload the old copy's OnDestroy runs *after* the new copy's Awake,
            // and UnpatchSelf on a shared ID would strip the new copy's patches too.
            harmony = new Harmony("purgos.freeman.fixes." + System.DateTime.Now.Ticks);
            harmony.PatchAll();
            DevMode.Bind(Config);
            SaveOnExit.Bind(Config);
            DevMode.AttachToExisting();
            CompanionBases.RememberAll();
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
            SaveOnExit.Teardown();
            DevMode.Teardown();
            TalkMarkers.Teardown();
            ItemSort.Teardown();
            RecruitSort.Teardown();
            SquadAll.Teardown();
            RestScreens.Teardown();
            Logger.LogInfo("Freeman Slop Mod unloaded");
            if (liveLog != null)
            {
                liveLog.Dispose();
            }
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
