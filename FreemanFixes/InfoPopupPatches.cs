using Ex;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Item info popup (UICombat.ShowInfo → CreateLine into descriptionHolder).
    //  - Compact: the popup's height is recomputed every frame as InitHeight + lineCount * (first line's height)
    //    (UICombat.FixedUpdate), so shrinking the line grid's cells shrinks the whole popup.
    //  - Armor requirement: vanilla appends the number to "Armor Proficiency Required:", but the line can clip it;
    //    rewrite it so the number always shows (red/green colouring is left to the game).
    [HarmonyPatch(typeof(UICombat), nameof(UICombat.ShowInfo))]
    internal static class InfoPopupPatches
    {
        private const float GridLineHeightPerFontPx = 1.4f; // grid cells: a little more, wrapped lines get no 2nd row
        private static bool compacted;

        // Originals, restored on unload (ScriptEngine hot reload).
        private static GridLayoutGroup grid;
        private static Vector2 gridCell, gridSpacing;
        private static Text prefabText;
        private static float prefabLineSpacing;

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

        // Lines are cells of a GridLayoutGroup ("bg", 300x40): the cell size, not the line prefab, sets line height.
        private static void Compact(UICombat ui)
        {
            prefabText = ui.descriptionLinePrefab != null ? ui.descriptionLinePrefab.GetComponent<Text>() : null;
            grid = ui.descriptionHolder != null ? ui.descriptionHolder.GetComponent<GridLayoutGroup>() : null;
            if (prefabText == null || grid == null)
            {
                return;
            }
            gridCell = grid.cellSize;
            gridSpacing = grid.spacing;
            prefabLineSpacing = prefabText.lineSpacing;

            float cell = Mathf.Ceil(prefabText.fontSize * GridLineHeightPerFontPx);
            grid.cellSize = new Vector2(gridCell.x, Mathf.Min(gridCell.y, cell));
            grid.spacing = new Vector2(gridSpacing.x, 0f);
            prefabText.lineSpacing = Mathf.Min(prefabLineSpacing, 0.9f);
            Plugin.Log.LogInfo("Info popup grid cell -> " + grid.cellSize);
        }

        internal static void Restore()
        {
            if (grid != null)
            {
                grid.cellSize = gridCell;
                grid.spacing = gridSpacing;
            }
            if (prefabText != null)
            {
                prefabText.lineSpacing = prefabLineSpacing;
            }
            grid = null;
            prefabText = null;
            compacted = false;
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
