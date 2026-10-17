using System.Text;
using BepInEx.Configuration;
using Ex;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Menu > "back to desktop" in a manual-save (ordinary) game: before the game's own "return to main menu?" box, a
    // clone of that box asks "Save before exiting?" with Save / Don't Save. Save runs the game's normal SaveGame (so
    // DevSaveData writes its extras too); either way the vanilla return box follows, so No there still cancels.
    // Skipped when the game can't save (battle, resting), when nothing happened since the last save (world clock
    // unchanged), and in realistic games, which already save-and-exit in one step.
    internal class SaveOnExit : MonoBehaviour
    {
        private const string Name = "FreemanSlop_SaveOnExitPrompt";

        internal static ConfigEntry<bool> Enabled;

        private static GameObject prompt;

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

        // Returns true if it showed its own prompt (skip the vanilla return box for now).
        internal static bool Prompt()
        {
            if (Enabled == null || !Enabled.Value || Mainframe.code == null || Global.code == null
                || UIMenu.code == null || UIMenu.code.uiReturn == null
                || Mainframe.code.CurGameType != GameType.ordinary || !CanSave() || SavedSinceLastChange())
            {
                return false;
            }
            if (prompt == null && !Build())
            {
                return false;
            }
            prompt.SetActive(true);
            return true;
        }

        internal static void Hide()
        {
            if (prompt != null)
            {
                prompt.SetActive(false);
            }
        }

        internal static void Teardown()
        {
            if (prompt != null)
            {
                Destroy(prompt);
            }
            prompt = null;
        }

        private static bool Build()
        {
            GameObject template = UIMenu.code.uiReturn;
            Button[] templateButtons = template.GetComponentsInChildren<Button>(true);
            Plugin.Log.LogInfo("Save on exit: return box layout:\n" + Describe(template.transform, ""));
            if (templateButtons.Length < 2)
            {
                Plugin.Log.LogWarning("Save on exit: return box has " + templateButtons.Length + " buttons; using vanilla");
                return false;
            }
            prompt = Instantiate(template, template.transform.parent, false);
            prompt.name = Name;
            prompt.transform.SetAsLastSibling();
            foreach (LocalizationText loc in prompt.GetComponentsInChildren<LocalizationText>(true))
            {
                DestroyImmediate(loc);
            }
            var me = prompt.AddComponent<SaveOnExit>();
            Button[] buttons = prompt.GetComponentsInChildren<Button>(true);
            // Same order as the template: the first button is the box's Yes (ButtonSureBack), the second its No.
            Wire(buttons[0], "Save", new UnityAction(me.Save));
            Wire(buttons[1], "Don't Save", new UnityAction(me.DontSave));
            for (int i = 2; i < buttons.Length; i++)
            {
                buttons[i].gameObject.SetActive(false);
            }
            foreach (Text t in prompt.GetComponentsInChildren<Text>(true))
            {
                if (t.GetComponentInParent<Button>() == null)
                {
                    t.text = "Save the game before exiting?";
                    break;
                }
            }
            prompt.SetActive(false);
            return true;
        }

        private static void Wire(Button b, string label, UnityAction action)
        {
            b.onClick = new Button.ButtonClickedEvent();
            b.interactable = true;
            b.onClick.AddListener(action);
            Text t = b.GetComponentInChildren<Text>(true);
            if (t != null)
            {
                t.text = label;
            }
        }

        private static string Describe(Transform t, string indent)
        {
            var sb = new StringBuilder();
            Text text = t.GetComponent<Text>();
            sb.Append(indent).Append(t.name);
            if (t.GetComponent<Button>() != null)
            {
                sb.Append(" [Button]");
            }
            if (text != null)
            {
                sb.Append(" [Text \"").Append(text.text).Append("\"]");
            }
            sb.Append('\n');
            for (int i = 0; i < t.childCount; i++)
            {
                sb.Append(Describe(t.GetChild(i), indent + "  "));
            }
            return sb.ToString();
        }

        public void Save()
        {
            Hide();
            Mainframe.code.SaveGame();
            ShowReturnBox();
        }

        public void DontSave()
        {
            Hide();
            ShowReturnBox();
        }

        // SaveGame closes the pause menu, and the menu's OnEnable hides the return box, so reopen in that order.
        private static void ShowReturnBox()
        {
            if (!UIMenu.code.gameObject.activeSelf)
            {
                UIMenu.code.ShowView(true);
            }
            UIMenu.code.uiReturn.SetActive(true);
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

        // Closing/reopening the pause menu shouldn't bring back a half-answered prompt.
        [HarmonyPostfix, HarmonyPatch(typeof(UIMenu), "OnEnable")]
        private static void AfterMenuEnabled()
        {
            SaveOnExit.Hide();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(OnFile), nameof(OnFile.SaveAll))]
        private static void AfterSave()
        {
            SaveOnExit.RecordSave();
        }
    }
}
