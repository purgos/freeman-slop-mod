using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Let vertical scroll lists (inventories, trade, loot, ...) scroll a bit past their last row, so an item at the
    // bottom can be brought up far enough for its info popup to fit on screen. Done by adding bottom padding to the
    // list's layout group once; content sized by a ContentSizeFitter then grows to match.
    [HarmonyPatch(typeof(ScrollRect), "OnEnable")]
    internal static class OverscrollPatches
    {
        private const float ExtraFraction = 0.35f; // of the visible viewport height

        private static void Postfix(ScrollRect __instance)
        {
            if (!__instance.vertical || __instance.content == null)
            {
                return;
            }
            GameObject content = __instance.content.gameObject;
            if (content.GetComponent<OverscrollMarker>() != null)
            {
                return;
            }
            LayoutGroup layout = content.GetComponent<LayoutGroup>();
            if (layout == null || content.GetComponent<ContentSizeFitter>() == null)
            {
                return; // hand-sized content: leave it alone
            }
            RectTransform viewport = __instance.viewport != null ? __instance.viewport : (RectTransform)__instance.transform;
            int extra = Mathf.RoundToInt(viewport.rect.height * ExtraFraction);
            if (extra <= 0)
            {
                return; // not laid out yet; try again next time it's enabled
            }
            content.AddComponent<OverscrollMarker>();
            layout.padding = new RectOffset(layout.padding.left, layout.padding.right, layout.padding.top,
                layout.padding.bottom + extra);
            LayoutRebuilder.MarkLayoutForRebuild(__instance.content);
        }
    }

    internal class OverscrollMarker : MonoBehaviour
    {
    }
}
