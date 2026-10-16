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

        private void Awake()
        {
            Log = Logger;
            LogTypeLoadErrors();
            new Harmony("purgos.freeman.fixes").PatchAll();
            Logger.LogInfo("Freeman Slop Mod loaded");
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
