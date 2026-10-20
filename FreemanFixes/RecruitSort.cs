using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Sort for the troop recruitment screen (UIRecruitSoldier: cities, villages, mercenary camps). Vanilla always
    // lists soldiers by name, descending (UIRecruitSoldier.SortSoliders, called from Show on every rebuild, including
    // after each purchase). A postfix on SortSoliders re-sorts the same list by the chosen stat, so the order holds
    // through purchases and the "buy with equipment" toggle. The controls (a sort dropdown cloned like the trade
    // screen's, TradeButtons.CloneDropdown, and a direction button cloned from the trade screen's Deal button) sit
    // just left of that toggle (UIRecruitSoldier.filtrate), in the header bar's empty middle. Stats are what the row
    // and unit info show: name, level, marksmanship, price (NPCGen.GetCost with the toggle's setting), max health,
    // plus the damage of the soldier's gun and the total armor of their gear. The choice lasts for the session. Each
    // row also gets a line with damage, armor and health (AddStats, a Show postfix), since the row only shows level
    // and marksmanship.
    internal static class RecruitSort
    {
        internal const int Default = 0;
        internal const int Name = 1;
        internal const int Level = 2;
        internal const int Marksmanship = 3;
        internal const int Price = 4;
        internal const int Health = 5;
        internal const int WeaponDamage = 6;
        internal const int Armor = 7;

        private static readonly string[] Names =
        {
            "Default", "Name", "Level", "Marksmanship", "Price", "Health", "Weapon damage", "Armor"
        };

        private const float RowWidth = 440f;
        private const float DirectionWidth = 140f;
        private const float Gap = 16f; // between the row and the toggle
        private const string RowName = "FreemanSlop_RecruitSort";
        private const string StatsName = "FreemanSlop_RecruitStats";
        private const float StatsLift = 12f;    // level and marksmanship move up this much
        private const float StatsDrop = 22f;    // the stats line sits this far below their old centre
        private const float StatsHeight = 30f;
        private const int StatsFontSize = 18;
        private static bool statsLogged;

        internal static int Key = Default;
        internal static bool HighFirst = true;

        private static GameObject row;
        private static Dropdown dropdown;
        private static Text directionLabel;
        private static bool syncing; // Unity 2018 has no Dropdown.SetValueWithoutNotify

        // UIRecruitSoldier.Show prefix: build the controls once the screen exists.
        internal static void Ensure(UIRecruitSoldier ui)
        {
            if (row != null || ui.filtrate == null)
            {
                return;
            }
            Button template = UITrading.code != null ? UITrading.code.btnDeal : null;
            if (template == null)
            {
                Plugin.Log.LogWarning("Recruit sort: no button to clone; controls skipped");
                return;
            }
            DepositPanelUi.DumpHierarchy(ui.transform, "Recruit screen");
            var toggle = (RectTransform)ui.filtrate.transform;
            row = new GameObject(RowName, typeof(RectTransform));
            var rt = (RectTransform)row.transform;
            rt.SetParent(toggle.parent, false); // appended last
            rt.anchorMin = toggle.anchorMin;
            rt.anchorMax = toggle.anchorMax;
            rt.pivot = new Vector2(1f, 0.5f);
            float height = ((RectTransform)template.transform).rect.height;
            rt.sizeDelta = new Vector2(RowWidth, height);
            float toggleLeft = toggle.anchoredPosition.x - toggle.pivot.x * toggle.rect.width;
            float toggleMiddle = toggle.anchoredPosition.y + (0.5f - toggle.pivot.y) * toggle.rect.height;
            rt.anchoredPosition = new Vector2(toggleLeft - Gap, toggleMiddle);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            dropdown = TradeButtons.CloneDropdown(rt, "FreemanSlop_RecruitSortKey", 1f);
            if (dropdown != null)
            {
                var names = new List<string>();
                foreach (string n in Names)
                {
                    names.Add("Sort: " + n);
                }
                dropdown.AddOptions(names);
                TradeButtons.FitList(dropdown);
                RecruitSortControl d = dropdown.gameObject.AddComponent<RecruitSortControl>();
                dropdown.onValueChanged.AddListener(new UnityAction<int>(d.KeyChanged));
            }
            directionLabel = TradeButtons.Clone(template, rt, "", DirectionWidth, 0f);
            Button direction = TradeButtons.ButtonOf(directionLabel);
            RecruitSortControl b = direction.gameObject.AddComponent<RecruitSortControl>();
            direction.onClick.AddListener(new UnityAction(b.DirectionClicked));
            UpdateLabels();
            Plugin.Log.LogInfo("Recruit sort: controls at " + rt.anchoredPosition + " next to the toggle at "
                + toggle.anchoredPosition + " size " + toggle.rect.size);
        }

        internal static void SetKey(int key)
        {
            if (syncing)
            {
                return;
            }
            Key = Mathf.Clamp(key, 0, Names.Length - 1);
            HighFirst = Key != Name; // names A to Z, numbers highest first
            Rebuild();
        }

        internal static void ToggleDirection()
        {
            HighFirst = !HighFirst;
            Rebuild();
        }

        private static void Rebuild()
        {
            UpdateLabels();
            UIRecruitSoldier ui = UIRecruitSoldier.code;
            if (ui != null && ui.gameObject.activeInHierarchy && ui.templist != null)
            {
                ui.Show(ui.templist); // re-sorts (SortSoliders) and rebuilds the rows
            }
        }

        private static void UpdateLabels()
        {
            if (dropdown != null && dropdown.value != Key)
            {
                syncing = true;
                dropdown.value = Key;
                syncing = false;
            }
            if (directionLabel != null)
            {
                directionLabel.text = Key == Name ? (HighFirst ? "Z to A" : "A to Z") : (HighFirst ? "High first" : "Low first");
            }
        }

        // SortSoliders postfix: vanilla has just sorted by name; re-sort by our key.
        internal static void Sorted(List<Transform> soldiers)
        {
            if (Key == Default || soldiers == null || soldiers.Count < 2)
            {
                return;
            }
            bool withGear = UIRecruitSoldier.code == null || UIRecruitSoldier.code.IsBuyEquips;
            var order = new RecruitOrder();
            order.values = new float[soldiers.Count];
            order.names = Key == Name ? new string[soldiers.Count] : null;
            order.highFirst = HighFirst;
            var positions = new List<int>(soldiers.Count);
            for (int i = 0; i < soldiers.Count; i++)
            {
                positions.Add(i);
                NPCGen gen = soldiers[i] != null ? soldiers[i].GetComponent<NPCGen>() : null;
                if (Key == Name)
                {
                    order.names[i] = gen != null ? gen.Name : string.Empty;
                }
                else
                {
                    order.values[i] = gen != null ? Value(gen, withGear) : 0f;
                }
            }
            positions.Sort(order);
            var copy = new List<Transform>(soldiers);
            for (int i = 0; i < positions.Count; i++)
            {
                soldiers[i] = copy[positions[i]];
            }
        }

        private static float Value(NPCGen gen, bool withGear)
        {
            switch (Key)
            {
                case Level: return gen.Level;
                case Marksmanship: return gen.marksmanship;
                case Price: return gen.GetCost(withGear);
                case Health: return gen.maxHealth;
                case WeaponDamage: return Mathf.Max(GunDamage(gen.rifleId), GunDamage(gen.pistolId), GunDamage(gen.launcherId));
                case Armor:
                    return ClothesArmor(gen.helmetId) + ClothesArmor(gen.maskId) + ClothesArmor(gen.shirtId)
                        + ClothesArmor(gen.armorId) + ClothesArmor(gen.pantsId);
            }
            return 0f;
        }

        // As the item info popup shows it (UICombat.ShowInfo).
        private static float GunDamage(int id)
        {
            Transform t;
            WeaponRaycast gun = id != 0 && RM.code.AllWeaponsItem.TryGetValue(id, out t) && t != null
                ? t.GetComponent<WeaponRaycast>() : null;
            if (gun == null)
            {
                return 0f;
            }
            Item item = gun.GetComponent<Item>();
            return item != null && item.SlotType == SlotType.MELEE ? gun.Damage : gun.damage_Ori;
        }

        private static float ClothesArmor(int id)
        {
            Transform t;
            Appeal clothes = id != 0 && RM.code.AllItem.TryGetValue(id, out t) && t != null ? t.GetComponent<Appeal>() : null;
            return clothes != null ? clothes.armor : 0f;
        }

        // Show postfix: under each row's level and marksmanship (moved up a little), a line with the soldier's gun
        // damage, gear armor and health. Rows are rebuilt by every Show: it hides and Destroys the old ones (gone only
        // at the end of the frame, so they're still children here) and appends one active row per templist item, in
        // order, so the active rows after the template match the list.
        internal static void AddStats(UIRecruitSoldier ui)
        {
            if (ui.content == null || ui.templist == null)
            {
                return;
            }
            GridLayoutGroup grid = ui.content.GetComponent<GridLayoutGroup>();
            float rowHeight = grid != null ? grid.cellSize.y : 0f;
            if (!statsLogged)
            {
                statsLogged = true;
                Plugin.Log.LogInfo("Recruit sort: row height " + rowHeight);
            }
            List<Transform> soldiers = ui.templist.items;
            int i = 0;
            for (int c = 1; c < ui.content.childCount && i < soldiers.Count; c++)
            {
                Transform row = ui.content.GetChild(c);
                if (!row.gameObject.activeSelf)
                {
                    continue; // an old row waiting to be destroyed
                }
                NPCGen gen = soldiers[i] != null ? soldiers[i].GetComponent<NPCGen>() : null;
                i++;
                var level = row.Find("level") as RectTransform;
                var marks = row.Find("marksmanship") as RectTransform;
                if (gen == null || level == null || marks == null || row.Find(StatsName) != null)
                {
                    continue;
                }
                level.anchoredPosition += new Vector2(0f, StatsLift);
                marks.anchoredPosition += new Vector2(0f, StatsLift);
                GameObject go = Object.Instantiate(level.gameObject, row, false); // appended last
                go.name = StatsName;
                var rt = (RectTransform)go.transform;
                float left = level.anchoredPosition.x - level.pivot.x * level.rect.width;
                float right = marks.anchoredPosition.x + (1f - marks.pivot.x) * marks.rect.width;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(right - left, StatsHeight);
                rt.anchoredPosition = new Vector2(left, level.anchoredPosition.y - StatsLift - StatsDrop);
                Text t = go.GetComponent<Text>();
                t.fontSize = StatsFontSize;
                t.resizeTextForBestFit = false;
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow; // Truncate draws nothing if the line doesn't fit
                float damage = Mathf.Max(GunDamage(gen.rifleId), GunDamage(gen.pistolId), GunDamage(gen.launcherId));
                float armor = ClothesArmor(gen.helmetId) + ClothesArmor(gen.maskId) + ClothesArmor(gen.shirtId)
                    + ClothesArmor(gen.armorId) + ClothesArmor(gen.pantsId);
                t.text = "Damage:" + Mathf.RoundToInt(damage) + "   Armor:" + Mathf.RoundToInt(armor)
                    + "   Health:" + Mathf.RoundToInt(gen.maxHealth);
            }
        }

        // Hot reload: remove the controls.
        internal static void Teardown()
        {
            if (row != null)
            {
                Object.Destroy(row);
            }
            row = null;
            dropdown = null;
            directionLabel = null;
        }
    }

    // Compares positions in the list; the position breaks ties, which makes List.Sort stable.
    internal sealed class RecruitOrder : IComparer<int>
    {
        internal float[] values;
        internal string[] names;
        internal bool highFirst;

        public int Compare(int a, int b)
        {
            int c = names != null
                ? string.Compare(names[a], names[b], System.StringComparison.CurrentCultureIgnoreCase)
                : values[a].CompareTo(values[b]);
            if (c != 0)
            {
                return highFirst ? -c : c;
            }
            return a.CompareTo(b);
        }
    }

    internal class RecruitSortControl : MonoBehaviour
    {
        public void KeyChanged(int value)
        {
            RecruitSort.SetKey(value);
        }

        public void DirectionClicked()
        {
            RecruitSort.ToggleDirection();
        }
    }

    [HarmonyPatch]
    internal static class RecruitSortPatches
    {
        private static bool failed;

        // Never lets a failure here stop the screen from opening (an exception in a prefix skips Show).
        [HarmonyPrefix, HarmonyPatch(typeof(UIRecruitSoldier), nameof(UIRecruitSoldier.Show))]
        private static void Showing(UIRecruitSoldier __instance)
        {
            try
            {
                RecruitSort.Ensure(__instance);
            }
            catch (System.Exception e)
            {
                if (!failed)
                {
                    failed = true;
                    Plugin.Log.LogError("Recruit sort: controls failed: " + e);
                }
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIRecruitSoldier), nameof(UIRecruitSoldier.Show))]
        private static void Shown(UIRecruitSoldier __instance)
        {
            try
            {
                RecruitSort.AddStats(__instance);
            }
            catch (System.Exception e)
            {
                if (!failed)
                {
                    failed = true;
                    Plugin.Log.LogError("Recruit sort: stat lines failed: " + e);
                }
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIRecruitSoldier), "SortSoliders")]
        private static void SoldiersSorted(List<Transform> temp)
        {
            RecruitSort.Sorted(temp);
        }
    }
}
