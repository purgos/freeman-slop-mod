using System.Collections.Generic;
using Ex;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Campaign packs as New Game entries. With the Native mod (Mainframe.isBattleMode), New Game opens
    // UIDesktop.battlePenal: UIDesktop.SetBattleParameters fills fatherPenal with "Classical" (child) plus one clone of
    // child per Table_Story type, and Sure (OnSelectBattleSureBtn) starts a story through Mainframe.SetBattle(type):
    // Normal/Realistic, then the usual character creation. Each pack gets a clone of the same button, appended after
    // the stories, and goes down that same path with its preset ID as the battle ID (DevCampaigns.Selected).
    // Character creation's background question (UICampaign.ShowPLayerPreSetDialog_1) lists every preset, so the
    // packs' presets (kept in the table for saves started from them before 0.5.0) are hidden from it.
    internal static class CampaignMenu
    {
        private const string EntryPrefix = "FreemanSlop_Campaign_";

        private static bool dumped;

        // Postfix: vanilla has just rebuilt the list (destroying children 1+, our old entries included).
        internal static void AddEntries(UIDesktop desktop)
        {
            if (desktop == null || desktop.child == null || desktop.fatherPenal == null)
            {
                return;
            }
            foreach (KeyValuePair<int, DevCampaign> kv in DevCampaigns.Active)
            {
                Transform entry = Object.Instantiate(desktop.child, desktop.fatherPenal.transform);
                entry.name = EntryPrefix + kv.Key;
                entry.GetComponent<Image>().enabled = false;
                Button button = entry.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.interactable = true;
                entry.GetChild(0).GetComponent<Text>().text = kv.Value.Name;
                CampaignEntry ce = entry.gameObject.AddComponent<CampaignEntry>();
                ce.presetId = kv.Key;
                ce.description = DevCampaigns.Summary(kv.Value);
                button.onClick.AddListener(new UnityAction(ce.Select));
            }
            if (!dumped)
            {
                dumped = true;
                DepositPanelUi.DumpHierarchy(desktop.fatherPenal.transform, "New Game list");
            }
        }

        // Hot reload: drop our entries, and fall back to Classical if one of them was selected.
        internal static void Teardown()
        {
            UIDesktop desktop = UIDesktop.code;
            if (desktop == null || desktop.fatherPenal == null)
            {
                return;
            }
            Transform list = desktop.fatherPenal.transform;
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                Transform t = list.GetChild(i);
                if (t.name.StartsWith(EntryPrefix))
                {
                    if (desktop.selectButton == t)
                    {
                        desktop.selectButton = desktop.child;
                        desktop.selectBattleIndex = 0;
                        desktop.child.GetComponent<Image>().enabled = true;
                        desktop.battleDes.text = Localization.Format("BATTLE_CLASSICALBATTLE_DES", new object[0]);
                    }
                    Object.Destroy(t.gameObject);
                }
            }
        }

        // Presets the background question offers: the game's own, never a pack's.
        internal static Dictionary<int, Table_PlayerPresets> BackgroundChoices(Dictionary<int, Table_PlayerPresets> all)
        {
            var result = new Dictionary<int, Table_PlayerPresets>();
            foreach (KeyValuePair<int, Table_PlayerPresets> kv in all)
            {
                if (!DevCampaigns.IsPack(kv.Key))
                {
                    result.Add(kv.Key, kv.Value);
                }
            }
            return result;
        }
    }

    // One pack's button in the New Game list; mirrors the story buttons' click handler in SetBattleParameters.
    internal class CampaignEntry : MonoBehaviour
    {
        internal int presetId;
        internal string description;

        internal void Select()
        {
            UIDesktop desktop = UIDesktop.code;
            if (desktop == null)
            {
                return;
            }
            desktop.battleDes.text = description;
            desktop.selectBattleIndex = presetId;
            desktop.selectBattleDes = DevCampaigns.NameKey(presetId);
            GetComponent<Image>().enabled = true;
            if (desktop.selectButton != null && desktop.selectButton != transform)
            {
                desktop.selectButton.GetComponent<Image>().enabled = false;
            }
            desktop.selectButton = transform;
        }
    }

    [HarmonyPatch]
    internal static class CampaignMenuPatches
    {
        private static readonly System.Reflection.FieldInfo PresetsField =
            AccessTools.Field(typeof(TableManager), "PlayerPresetsData");

        private static Dictionary<int, Table_PlayerPresets> hiddenPresets;

        [HarmonyPostfix, HarmonyPatch(typeof(UIDesktop), nameof(UIDesktop.SetBattleParameters))]
        private static void BattleListBuilt(UIDesktop __instance, bool isClassical)
        {
            if (isClassical) // vanilla's name for "build the list" (Mainframe.isBattleMode)
            {
                CampaignMenu.AddEntries(__instance);
            }
        }

        // The dialog reads TableManager.GetPlayerPresets() while it builds its buttons: swap in the narrowed table for
        // that call only. The finalizer puts the real one back even if the dialog throws.
        [HarmonyPrefix, HarmonyPatch(typeof(UICampaign), "ShowPLayerPreSetDialog_1")]
        private static void BackgroundsShowing()
        {
            hiddenPresets = null;
            Dictionary<int, Table_PlayerPresets> all = TableManager.GetPlayerPresets();
            if (all == null || (object)PresetsField == null) // object compare: FieldInfo.op_Equality is .NET 4 only
            {
                return;
            }
            Dictionary<int, Table_PlayerPresets> shown = CampaignMenu.BackgroundChoices(all);
            if (shown.Count == 0 || shown.Count == all.Count)
            {
                return;
            }
            hiddenPresets = all;
            PresetsField.SetValue(null, shown);
        }

        [HarmonyFinalizer, HarmonyPatch(typeof(UICampaign), "ShowPLayerPreSetDialog_1")]
        private static void BackgroundsShown()
        {
            if (hiddenPresets != null)
            {
                PresetsField.SetValue(null, hiddenPresets);
                hiddenPresets = null;
            }
        }
    }
}
