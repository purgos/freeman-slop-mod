using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FreemanFixes
{
    // Management screens while resting. Resting at an inn (MapAgent.SetSleep -> Sleep("Sleeping"), state "sleep") or
    // working a job (Sleep("Working")) sets MapAgent.sleeping while the hours pass, and every top-bar screen's opener
    // on Global (ToggleInventory, ToggleParty, ShowHeroWindow, ToggleData, TogglePrisoners, ToggleQuest,
    // ShowAchievement, ShowSilkRoad) refuses while it's set. So for those two states the openers run as if awake:
    // sleeping is cleared just for the call. The rest continues underneath (the hourly inn charge, healing and the
    // fast clock all read sleeping, which is back on). Opening a screen hides the rest panel (UICombat.HidePanels), so
    // it's shown again once no screen is open (Global.CheckOnGUI postfix). Esc closes an open screen through
    // Global.HandleKeys, which also calls MapAgent.CancelSleep (Global.cs:1431); while a screen is open
    // (Global.onGUI) that wake-up is skipped, so Esc only closes the screen. With nothing open, Esc and the rest
    // panel's button wake you as before. Detention, stealing, extorting and plundering stay locked.
    internal static class RestScreens
    {
        private static bool panelHidden;

        internal static bool Resting()
        {
            MapAgent agent = MapAgent.code;
            return agent != null && agent.sleeping && (agent.state == "sleep" || agent.state == "Working");
        }

        internal static void ScreenOpened()
        {
            panelHidden = true;
        }

        // Global.CheckOnGUI postfix (every frame): bring the rest panel back once the screens are closed.
        internal static void CheckPanel()
        {
            if (!panelHidden || Global.code == null || Global.code.onGUI || UICombat.code == null)
            {
                return;
            }
            panelHidden = false;
            if (Resting() && UICombat.code.sleeppanel != null && !UICombat.code.sleeppanel.gameObject.activeSelf)
            {
                UICombat.code.sleeppanel.gameObject.SetActive(true);
                UICombat.code.btnsleep.gameObject.SetActive(true); // both states are cancellable (Sleep's cancancel)
            }
        }

        internal static void Teardown()
        {
            panelHidden = false;
        }
    }

    [HarmonyPatch]
    internal static class RestScreensOpenPatches
    {
        private static readonly string[] Openers =
        {
            "ToggleInventory", "ToggleParty", "ShowHeroWindow", "ToggleData", "TogglePrisoners", "ToggleQuest",
            "ShowAchievement", "ShowSilkRoad"
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var list = new List<MethodBase>();
            foreach (string name in Openers)
            {
                MethodInfo m = AccessTools.Method(typeof(Global), name);
                if ((object)m != null)
                {
                    list.Add(m);
                }
            }
            return list;
        }

        private static void Prefix(out bool __state)
        {
            __state = RestScreens.Resting();
            if (__state)
            {
                MapAgent.code.sleeping = false;
            }
        }

        // A finalizer, so sleeping comes back even if the opener throws.
        private static System.Exception Finalizer(System.Exception __exception, bool __state)
        {
            if (__state && MapAgent.code != null)
            {
                MapAgent.code.sleeping = true;
                RestScreens.ScreenOpened();
            }
            return __exception;
        }
    }

    [HarmonyPatch]
    internal static class RestScreensPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(MapAgent), nameof(MapAgent.CancelSleep))]
        private static bool CancellingSleep()
        {
            // Skipping prefix: Esc that closes a screen shouldn't also end the rest.
            return !(Global.code != null && Global.code.onGUI && RestScreens.Resting());
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Global), "CheckOnGUI")]
        private static void OnGuiChecked()
        {
            RestScreens.CheckPanel();
        }
    }
}
