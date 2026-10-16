using Ex;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Item info popup (UICombat.ShowInfo → CreateLine into descriptionHolder).
    //  - Compact: the popup's height is recomputed every frame as InitHeight + lineCount * (first line's height)
    //    (UICombat.FixedUpdate), so tightening the line template + holder spacing shrinks the whole popup.
    //  - Armor requirement: vanilla appends the number to "Armor Proficiency Required:", but the line can clip it;
    //    rewrite it so the number always shows (red/green colouring is left to the game).
    [HarmonyPatch(typeof(UICombat), nameof(UICombat.ShowInfo))]
    internal static class InfoPopupPatches
    {
        private const float LineHeightPerFontPx = 1.2f;     // line box = font size * this
        private const float GridLineHeightPerFontPx = 1.4f; // grid cells: a little more, wrapped lines get no 2nd row
        private static bool compacted;

        private static void Postfix(UICombat __instance)
        {
            if (!compacted)
            {
                compacted = true;
                DepositPanelUi.DumpHierarchy(__instance.descriptionsPanel, "Info popup (before compacting)");
                Compact(__instance);
            }
            FixArmorLines(__instance);
        }

        private static void Compact(UICombat ui)
        {
            Transform prefab = ui.descriptionLinePrefab;
            Text prefabText = prefab != null ? prefab.GetComponent<Text>() : null;

            // Lines live in a GridLayoutGroup ("bg", 300x40 cells): the cell size is what actually sets line height.
            var grid = ui.descriptionHolder != null ? ui.descriptionHolder.GetComponent<GridLayoutGroup>() : null;
            if (grid != null && prefabText != null)
            {
                float cell = Mathf.Ceil(prefabText.fontSize * GridLineHeightPerFontPx);
                if (grid.cellSize.y > cell)
                {
                    grid.cellSize = new Vector2(grid.cellSize.x, cell);
                }
                grid.spacing = new Vector2(grid.spacing.x, 0f);
                Plugin.Log.LogInfo("Info popup grid cell -> " + grid.cellSize);
            }
            Text text = prefab != null ? prefab.GetComponent<Text>() : null;
            if (text == null)
            {
                return;
            }
            text.lineSpacing = Mathf.Min(text.lineSpacing, 0.9f);
            if (prefab.GetComponent<ContentSizeFitter>() != null)
            {
                return; // lines size themselves; spacing tweaks above are all we can safely do
            }
            float target = Mathf.Ceil(text.fontSize * LineHeightPerFontPx);
            var rt = (RectTransform)prefab;
            if (rt.sizeDelta.y > target)
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, target);
            }
            LayoutElement le = prefab.GetComponent<LayoutElement>();
            if (le != null)
            {
                if (le.preferredHeight > target)
                {
                    le.preferredHeight = target;
                }
                if (le.minHeight > target)
                {
                    le.minHeight = target;
                }
            }
            Plugin.Log.LogInfo("Info popup compacted: line height -> " + target + " (font " + text.fontSize + ")");
        }

        private static void FixArmorLines(UICombat ui)
        {
            if (ui.descriptionHolder == null)
            {
                return;
            }
            string prefix = Localization.Format("code_NeedArmorPoint", new object[0]).TrimEnd(new char[0]); // explicit arrays: no Array.Empty on old Mono
            foreach (Transform line in ui.descriptionHolder)
            {
                Text t = line.GetComponent<Text>();
                if (t == null || !t.text.StartsWith(prefix))
                {
                    continue;
                }
                string number = t.text.Substring(prefix.Length).Trim();
                t.text = prefix + " " + number;
                // Never wrap the number onto a hidden second line.
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }
    }
}
