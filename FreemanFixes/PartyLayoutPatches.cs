using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Party screen: the soldier info/loadout panel (UICombat.unitInfoPanel) sits low on the right, so item popups opened
    // from it run off the bottom of the screen. While the party screen is open, lift the panel so its top sits just
    // under the top menu bar; restore its original spot on every other screen (recruit, prisoners, ...).
    [HarmonyPatch(typeof(UICombat), nameof(UICombat.ShowUnitInfo))]
    internal static class PartyLayoutPatches
    {
        private const float TopBarFraction = 0.06f; // top menu bar (~5% of screen) plus a small gap

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
            float topOnScreen = VisibleTop(rt, cam);
            float desiredTop = Screen.height * (1f - TopBarFraction);
            float lift = (desiredTop - topOnScreen) / canvas.scaleFactor;
            if (lift > 0f)
            {
                rt.anchoredPosition = originalPos + new Vector2(0f, lift);
            }
        }

        // The panel's header (portrait, orange rule) is drawn above its own rect, so measure the highest visible
        // graphic in it rather than the rect itself.
        private static float VisibleTop(RectTransform rt, Camera cam)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float top = RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y;
            foreach (Graphic g in rt.GetComponentsInChildren<Graphic>(false))
            {
                if (!g.enabled || g.color.a <= 0f)
                {
                    continue;
                }
                g.rectTransform.GetWorldCorners(corners);
                top = Mathf.Max(top, RectTransformUtility.WorldToScreenPoint(cam, corners[1]).y);
            }
            return top;
        }

        // Put it back when leaving the party screen so other screens keep the vanilla layout.
        [HarmonyPostfix, HarmonyPatch(typeof(UIParty), "OnDisable")]
        private static void PartyClosed()
        {
            Restore();
        }

        internal static void Restore()
        {
            if (panel != null && haveOriginal)
            {
                panel.anchoredPosition = originalPos;
            }
        }
    }
}
