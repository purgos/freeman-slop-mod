using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine.Events;

namespace FreemanFixes
{
    // Menu > "back to desktop" in a manual-save (ordinary) game: instead of the plain "return?" box, the game's own
    // confirmation dialog offers Save & Exit / Exit / Cancel. Save & Exit runs the game's normal SaveGame, so it's a
    // regular save (and DevSaveData writes its extras). Where the game can't save (battle, resting) it says so and
    // offers Exit / Cancel. No prompt if nothing happened since the last save (world clock unchanged). Realistic
    // games already save-and-exit in one step, so they keep the vanilla box.
    internal class SaveOnExit
    {
        internal static ConfigEntry<bool> Enabled;

        private static string savedGame;
        private static int savedDay = -1;
        private static int savedHour;
        private static int savedMinute;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Save On Exit", "Enabled", true,
                "In manual-save games, ask whether to save when going back to the main menu.");
        }

        internal static void RecordSave()
        {
            if (Mainframe.code == null || Global.code == null)
            {
                return;
            }
            savedGame = Mainframe.UsingCachaName;
            savedDay = Global.code.Days;
            savedHour = Global.code.Hours;
            savedMinute = Global.code.Minute;
        }

        private static bool SavedSinceLastChange()
        {
            return savedDay >= 0 && savedGame == Mainframe.UsingCachaName && savedDay == Global.code.Days
                && savedHour == Global.code.Hours && savedMinute == Global.code.Minute;
        }

        private static bool CanSave()
        {
            return TerrainControl.code != null && TerrainControl.code.isWorld && MapAgent.code != null
                && !MapAgent.code.sleeping;
        }

        // Returns true if it showed its own prompt (skip the vanilla one).
        internal static bool Prompt()
        {
            if (Enabled == null || !Enabled.Value || Mainframe.code == null || Global.code == null
                || UIConfirmation.code == null || UIMenu.code == null
                || Mainframe.code.CurGameType != GameType.ordinary)
            {
                return false;
            }
            var me = new SaveOnExit();
            if (!CanSave())
            {
                UIConfirmation.code.ShowConfirmation("You can't save right now. Exit to the main menu without saving?",
                    true, new UnityAction(me.Exit), new UnityAction(me.Cancel), null,
                    new string[] { "Exit", "Cancel" });
                return true;
            }
            if (SavedSinceLastChange())
            {
                return false;
            }
            UIConfirmation.code.ShowConfirmation("Save the game before exiting to the main menu?",
                true, new UnityAction(me.SaveAndExit), new UnityAction(me.Cancel), new UnityAction(me.Exit),
                new string[] { "Save & Exit", "Cancel", "Exit" });
            return true;
        }

        public void SaveAndExit()
        {
            UIConfirmation.code.Close();
            if (CanSave())
            {
                Mainframe.code.SaveGame();
            }
            UIMenu.code.ButtonSureBack();
        }

        public void Exit()
        {
            UIConfirmation.code.Close();
            UIMenu.code.ButtonSureBack();
        }

        public void Cancel()
        {
            UIConfirmation.code.Close();
        }
    }

    [HarmonyPatch]
    internal static class SaveOnExitPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Mainframe), nameof(Mainframe.ButtonBackToMainMenu))]
        private static bool BeforeBackToMainMenu()
        {
            return !SaveOnExit.Prompt();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(OnFile), nameof(OnFile.SaveAll))]
        private static void AfterSave()
        {
            SaveOnExit.RecordSave();
        }
    }
}
