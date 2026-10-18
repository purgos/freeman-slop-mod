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
        internal static ConfigEntry<bool> ShowSettingsToggle;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Dev Mode", "Enabled", false,
                "Adds dev controls to the game's cheat panel (End key): set money/reputation/morale/XP, faction money " +
                "and relations, add troops, and an endless item store.");
            ShowSettingsToggle = config.Bind("Dev Mode", "ShowSettingsToggle", true,
                "Show the Dev Mode toggle in the game's Settings > General. The dev menu's \"turn off dev mode and " +
                "hide its setting\" button sets this to false; set it back to true here to get the toggle back.");
            Enabled.SettingChanged += OnEnabledChanged;
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
            DevCampaigns.Inject(); // campaign packs work with or without dev mode
            CheatGate.Attach(UICheat.code);
            DevModeToggle.Sync(UISettings.code);
        }

        // Switching dev mode at runtime (Settings toggle, dev menu button, config edit + reload).
        private static void OnEnabledChanged(object sender, System.EventArgs e)
        {
            if (On)
            {
                CheatButton.Relabel();
            }
            else
            {
                CheatButton.Restore();
                DevMenu.Teardown();
                if (UICheat.code != null)
                {
                    UICheat.code.gameObject.SetActive(false);
                }
            }
            DevModeToggle.Refresh();
            Plugin.Log.LogInfo("Dev mode " + (On ? "on" : "off"));
        }

        // Dev menu button: dev mode off and the Settings toggle hidden; only the config file brings it back.
        internal static void DisableAndHide()
        {
            ShowSettingsToggle.Value = false;
            Enabled.Value = false; // OnEnabledChanged does the rest
            DevModeToggle.Sync(UISettings.code);
        }

        internal static void Teardown()
        {
            if (Enabled != null)
            {
                Enabled.SettingChanged -= OnEnabledChanged;
            }
            CheatGate.Detach();
            DevModeToggle.Teardown();
            DevMenu.Teardown();
            DevCampaigns.Remove();
            DevSaveData.Teardown();
            DevStore.Teardown();
            CheatButton.Restore();
        }
    }

    // In dev mode the cheat panel's "Add All Items" (dump every item into your bag) becomes "Dev Menu", and the
    // other add-everything / add-money / add-XP buttons are hidden (the dev menu replaces them), plus
    // Neutrality and Add Wood/Metal/Iron at the user's request, and the city cheats (the dev menu's Cities tab).
    // Names are UICheat.InitGM's.
    internal static class CheatButton
    {
        private const string VanillaName = "Add All Items";
        private static readonly string[] Hidden =
        {
            "Get All Clothing", "Get all Weapons", "Add All Soldiers", "AddAllHero", "Add 1000000 cr", "Add 10000 exp",
            "Neutrality", "Add Wood,Metal,Iron", "AddAllCity", "AddRecentlyCity"
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
        [HarmonyPostfix, HarmonyPatch(typeof(UICheat), nameof(UICheat.Awake))]
        private static void CheatAwake(UICheat __instance)
        {
            CheatGate.Attach(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UISettings), "OnEnable")]
        private static void SettingsShown(UISettings __instance)
        {
            DevModeToggle.Sync(__instance);
        }

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

        // Every click in the trade screen goes through InventorySlot.Select; keep the dev store's stock endless.
        [HarmonyPostfix, HarmonyPatch(typeof(InventorySlot), nameof(InventorySlot.Select))]
        private static void AfterSlotClick()
        {
            DevStore.RestockAfterClick();
        }

        // UITrading.Update only enables Deal when the balance is non-zero, so free dev-store purchases (balance 0)
        // could never be confirmed - and closing the screen Reverts them. Enable it whenever a trade is pending.
        [HarmonyPostfix, HarmonyPatch(typeof(UITrading), nameof(UITrading.Update))]
        private static void AfterTradeUpdate(UITrading __instance)
        {
            if (__instance.btnDeal != null && DevStore.HasPendingTrade())
            {
                __instance.btnDeal.interactable = true;
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
