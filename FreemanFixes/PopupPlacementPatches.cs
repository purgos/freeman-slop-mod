using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Item info popup placement (UICombat.FixedUpdate moves UICombat.descriptionsPanel to the cursor every tick).
    // Vanilla hangs the popup below the cursor and tries to keep it on screen, but compares canvas units to screen
    // pixels and ignores parts drawn outside the panel rect, so at high resolutions it runs off the bottom. We
    // re-place it after vanilla: opening upward from the cursor, measured from what is actually drawn, kept clear of
    // the top menu bar and the screen edges.
    [HarmonyPatch(typeof(UICombat), "FixedUpdate")]
    internal static class PopupPlacementPatches
    {
        private const float CursorGap = 14f;         // px between cursor and the popup's bottom edge
        private const float TopBarFraction = 0.055f; // keep below the top menu bar
        private const float EdgeMargin = 8f;         // px from the screen edges

        private static int lastLineCount = -1;
        private static readonly Vector3[] Corners = new Vector3[4];

        private static void Postfix(UICombat __instance)
        {
            RectTransform panel = __instance.descriptionsPanel;
            if (panel == null || !panel.gameObject.activeSelf)
            {
                lastLineCount = -1;
                return;
            }
            // New lines were just added: lay them out now so we measure the real size, not last frame's.
            int lines = __instance.descriptionHolder != null ? __instance.descriptionHolder.childCount : 0;
            if (lines != lastLineCount)
            {
                lastLineCount = lines;
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            }

            // The popup lives on a screen-space-overlay canvas (vanilla sets position = mouse position),
            // so world coordinates are screen pixels.
            Rect drawn = VisibleBounds(panel);
            if (drawn.width <= 0f || drawn.height <= 0f)
            {
                return;
            }
            Vector3 mouse = Input.mousePosition;
            float top = Screen.height * (1f - TopBarFraction);

            float bottom = mouse.y + CursorGap;
            if (bottom + drawn.height > top)
            {
                bottom = top - drawn.height;
            }
            if (bottom < EdgeMargin)
            {
                bottom = EdgeMargin;
            }

            float left = drawn.xMin;
            if (drawn.xMax > Screen.width - EdgeMargin)
            {
                left -= drawn.xMax - (Screen.width - EdgeMargin);
            }
            if (left < EdgeMargin)
            {
                left = EdgeMargin;
            }

            panel.position += new Vector3(left - drawn.xMin, bottom - drawn.yMin, 0f);
        }

        private static Rect VisibleBounds(RectTransform root)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(false))
            {
                if (!g.enabled || g.color.a <= 0f)
                {
                    continue;
                }
                g.rectTransform.GetWorldCorners(Corners);
                for (int i = 0; i < 4; i++)
                {
                    xMin = Mathf.Min(xMin, Corners[i].x);
                    yMin = Mathf.Min(yMin, Corners[i].y);
                    xMax = Mathf.Max(xMax, Corners[i].x);
                    yMax = Mathf.Max(yMax, Corners[i].y);
                }
            }
            return xMax < xMin ? new Rect(0f, 0f, 0f, 0f) : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
