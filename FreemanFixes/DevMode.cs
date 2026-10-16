using System.Collections.Generic;
using BepInEx.Configuration;
using Ex;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Dev mode (config: [Dev Mode] Enabled, off by default). Builds on the game's own cheat panel (UICheat, toggled with
    // End): its "Add All Items" button becomes "Dev Menu", a full-screen menu (DevMenu) that also opens the item store
    // (DevStore).
    // With dev mode off, the vanilla cheat panel is left exactly as it is.
    internal static class DevMode
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> StoreFree;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Dev Mode", "Enabled", false,
                "Adds dev controls to the game's cheat panel (End key): set money/reputation/morale/XP, faction money " +
                "and relations, add troops, and an item store with 7 of every item.");
            StoreFree = config.Bind("Dev Mode", "StoreFree", true,
                "Items in the dev store cost nothing (also toggleable in the store).");
        }

        internal static bool On
        {
            get { return Enabled != null && Enabled.Value; }
        }

        // Hot reload: the cheat panel already exists, so relabel its button directly.
        internal static void AttachToExisting()
        {
            CheatButton.Relabel();
        }

        internal static void Teardown()
        {
            DevMenu.Teardown();
            DevStore.Teardown();
            CheatButton.Restore();
        }
    }

    // In dev mode the cheat panel's "Add All Items" (dump every item into your bag) becomes "Dev Menu", and the
    // other add-everything / add-money / add-XP buttons are hidden (the dev menu replaces them), plus
    // Neutrality and Add Wood/Metal/Iron at the user's request.
    // Names are UICheat.InitGM's.
    internal static class CheatButton
    {
        private const string VanillaName = "Add All Items";
        private static readonly string[] Hidden =
        {
            "Get All Clothing", "Get all Weapons", "Add All Soldiers", "AddAllHero", "Add 1000000 cr", "Add 10000 exp",
            "Neutrality", "Add Wood,Metal,Iron"
        };
        private static readonly List<GameObject> hiddenButtons = new List<GameObject>();
        private static Text label;
        private static string originalText;
        private static Behaviour localization;

        internal static void Relabel()
        {
            if (!DevMode.On || UICheat.code == null || label != null)
            {
                return;
            }
            Transform button = UICheat.code.transform.Find(VanillaName);
            label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label == null)
            {
                return;
            }
            originalText = label.text;
            localization = label.GetComponent<LocalizationText>();
            if (localization != null)
            {
                localization.enabled = false; // would put the vanilla text back
            }
            label.text = "Dev Menu";
            foreach (string name in Hidden)
            {
                Transform t = UICheat.code.transform.Find(name);
                if (t != null && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    hiddenButtons.Add(t.gameObject);
                }
            }
        }

        internal static void Restore()
        {
            if (label != null)
            {
                label.text = originalText;
            }
            if (localization != null)
            {
                localization.enabled = true;
            }
            foreach (GameObject go in hiddenButtons)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }
            hiddenButtons.Clear();
            label = null;
            localization = null;
        }
    }

    [HarmonyPatch]
    internal static class DevModePatches
    {
        // The cheat buttons are created in UICheat.Start (InitGM); relabel once they exist.
        [HarmonyPostfix, HarmonyPatch(typeof(UICheat), "Start")]
        private static void CheatStarted()
        {
            CheatButton.Relabel();
        }

        // Vanilla dumps every item into your bag (and caps the bag at 250); in dev mode open the dev menu instead.
        [HarmonyPrefix, HarmonyPatch(typeof(UICheat), nameof(UICheat.AddAllItems))]
        private static bool AddAllItems()
        {
            if (!DevMode.On)
            {
                return true;
            }
            DevMenu.Open();
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Item), nameof(Item.GetBuyCost))]
        private static void BuyCost(ref int __result)
        {
            if (DevStore.IsFreeNow())
            {
                __result = 0;
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UITrading), nameof(UITrading.Deal))]
        private static void AfterDeal()
        {
            DevStore.RestockIfOpen();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UITrading), nameof(UITrading.OnEnable))]
        private static void TradeOpened(UITrading __instance)
        {
            DevStore.SyncControls(__instance);
        }
    }
}
