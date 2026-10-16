using HarmonyLib;
using UnityEngine;

namespace FreemanFixes
{
    // Party screen: the soldier info/loadout panel (UICombat.unitInfoPanel) sits low on the right, so item popups opened
    // from it run off the bottom of the screen. While the party screen is open, lift the panel so its top sits just
    // under the top menu bar; restore its original spot on every other screen (recruit, prisoners, ...).
    [HarmonyPatch(typeof(UICombat), nameof(UICombat.ShowUnitInfo))]
    internal static class PartyLayoutPatches
    {
        private const float TopBarFraction = 0.065f; // top menu bar height as a fraction of the screen

        private static RectTransform panel;
        private static Vector2 originalPos;
        private static bool haveOriginal;

        private static void Postfix(UICombat __instance)
        {
            if (__instance.unitInfoPanel == null)
            {
                return;
            }
            var rt = __instance.unitInfoPanel.transform as RectTransform;
            if (rt == null)
            {
                return;
            }
            if (rt != panel)
            {
                panel = rt;
                originalPos = rt.anchoredPosition;
                haveOriginal = true;
            }
            bool partyOpen = UIParty.code != null && UIParty.code.gameObject.activeInHierarchy;
            if (!partyOpen)
            {
                rt.anchoredPosition = originalPos;
                return;
            }
            rt.anchoredPosition = originalPos;
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }
            canvas = canvas.rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float topOnScreen = RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y;
            float desiredTop = Screen.height * (1f - TopBarFraction);
            float lift = (desiredTop - topOnScreen) / canvas.scaleFactor;
            if (lift > 0f)
            {
                rt.anchoredPosition = originalPos + new Vector2(0f, lift);
            }
        }

        // Put it back when leaving the party screen so other screens keep the vanilla layout.
        [HarmonyPostfix, HarmonyPatch(typeof(UIParty), "OnDisable")]
        private static void PartyClosed()
        {
            if (panel != null && haveOriginal)
            {
                panel.anchoredPosition = originalPos;
            }
        }
    }
}
