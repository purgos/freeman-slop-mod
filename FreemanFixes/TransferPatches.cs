using HarmonyLib;
using UnityEngine;

namespace FreemanFixes
{
    // Personal <-> own-faction fund transfer (UIData "转移资金面板").
    // Vanilla: the slider opens at the full balance and, depending on scene wiring, can't be adjusted, so
    // every transfer moves everything; ButtonDeposit also ignores whether the withdrawal side succeeded.
    [HarmonyPatch(typeof(UIData))]
    internal static class TransferPatches
    {
        internal static int Available(UIData ui)
        {
            return ui.isDeposit ? MapAgent.code.GetCoin() : MapAgent.code._Army.faction.Money;
        }

        internal static void SetAmount(UIData ui, int value)
        {
            value = Mathf.Clamp(value, 0, Available(ui));
            ui.amount = value;
            if (ui.depositSlider != null)
            {
                ui.depositSlider.value = value;
            }
            ShowAmount(ui, value);
        }

        private static void ShowAmount(UIData ui, int value)
        {
            if (DepositPanelUi.Current != null)
            {
                DepositPanelUi.Current.ShowAmount(value);
            }
            else if (ui.depositText != null)
            {
                ui.depositText.text = value.ToString();
            }
        }

        private static void PrepareSlider(UIData ui)
        {
            DepositPanelUi.Ensure(ui);
            var slider = ui.depositSlider;
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = Available(ui);
                slider.wholeNumbers = true;
                slider.interactable = true;
                // Make sure dragging the slider actually updates the amount (remove first so we never stack it).
                slider.onValueChanged.RemoveListener(ui.ChangeAmount);
                slider.onValueChanged.AddListener(ui.ChangeAmount);
            }
            SetAmount(ui, 0);
        }

        [HarmonyPostfix, HarmonyPatch("TransferFaction")]
        private static void AfterTransferFaction(UIData __instance)
        {
            PrepareSlider(__instance);
        }

        [HarmonyPostfix, HarmonyPatch("TransferSelf")]
        private static void AfterTransferSelf(UIData __instance)
        {
            PrepareSlider(__instance);
        }

        // Vanilla ChangeAmount (slider) writes depositText directly, which is now the input field's text component.
        [HarmonyPostfix, HarmonyPatch(nameof(UIData.ChangeAmount))]
        private static void AfterChangeAmount(UIData __instance)
        {
            ShowAmount(__instance, __instance.amount);
        }

        // Replaces vanilla: clamp to what's actually there and only credit the other side if the debit succeeded.
        [HarmonyPrefix, HarmonyPatch(nameof(UIData.ButtonDeposit))]
        private static bool SafeDeposit(UIData __instance)
        {
            int amount = Mathf.Clamp(__instance.amount, 0, Available(__instance));
            if (amount > 0)
            {
                var faction = MapAgent.code._Army.faction;
                if (__instance.isDeposit)
                {
                    if (MapAgent.code.AddCoin(-amount))
                    {
                        faction.AddMoney(amount);
                    }
                }
                else if (faction.AddMoney(-amount))
                {
                    MapAgent.code.AddCoin(amount);
                }
            }
            __instance.depositPanel.SetActive(false);
            return false;
        }
    }
}
