using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // The squads screen (UIParty) has Auto Equip (AutoEquipClick), Auto Supply (AutoSupply), Reward Troops
    // (ButtonRewardSquad) and All Level Up (AllLevelUpClick) buttons, but each only works on the selected squad
    // (UIParty.selectedSquad), so a big army means clicking through every squad. We add Equip All, Supply All,
    // Reward All and Level Up All, each a clone of the game's own button, which select every squad of your army in
    // turn, run the game's method, and put the selection back. Reward All mirrors RefreshSelectedSquad's rule (a squad
    // under 100 morale with someone able to fight) and its cost (Squad.GetCost * Global.RewardSquad); Level Up All
    // mirrors RefreshLevelUpBTN's (a unit past its next level's exp and under Global.NPCGenMaxLevel, at
    // NPCGen.GetLevelUpCost, one level per click as in the game). Both pay squad by squad and stop when the coin runs
    // out (AddCoin).
    internal static class SquadAll
    {
        private const float Gap = 6f; // between a game button and its "All" copy
        private const float TextPadding = 6f; // each side of a label inside its half-width button
        private const int MinFontSize = 12;
        private const string Prefix = "FreemanSlop_SquadAll_";

        private static Button equipAll;
        private static Button supplyAll;
        private static Button rewardAll;
        private static Text rewardLabel;
        private static Button levelAll;
        private static Text levelLabel;
        private static bool busy;

        // What we changed on the game's own objects, for Teardown.
        private static VerticalLayoutGroup column;
        private static TextAnchor columnAlignment;
        private static int columnPadLeft;
        private static readonly System.Collections.Generic.List<Halved> halved =
            new System.Collections.Generic.List<Halved>();

        private sealed class Halved
        {
            internal RectTransform button;
            internal Vector2 size;
            internal Text text;
            internal Vector2 textSize;
            internal bool bestFit;
            internal int minSize;
            internal int maxSize;
        }

        internal static void Ensure(UIParty ui)
        {
            if (equipAll != null || ui.btnAutoEquip == null || ui.btnAutoSupply == null || ui.btnRewardSquad == null
                || ui.btnAllLevelUp == null)
            {
                return;
            }
            DepositPanelUi.DumpHierarchy(ui.btnAutoEquip.transform.parent, "Squads screen buttons");
            if ((object)ui.btnRewardSquad.transform.parent != (object)ui.btnAutoEquip.transform.parent)
            {
                DepositPanelUi.DumpHierarchy(ui.btnRewardSquad.transform.parent, "Squads screen reward button");
            }
            LeftAlignColumn(ui.btnAutoEquip);
            SquadAllControl control;
            equipAll = Clone(ui.btnAutoEquip, "Equip All", out control);
            equipAll.onClick.AddListener(new UnityAction(control.EquipAll));
            supplyAll = Clone(ui.btnAutoSupply, "Supply All", out control);
            supplyAll.onClick.AddListener(new UnityAction(control.SupplyAll));
            rewardAll = Clone(ui.btnRewardSquad, "Reward All", out control);
            rewardAll.onClick.AddListener(new UnityAction(control.RewardAll));
            rewardLabel = rewardAll.transform.GetChild(0).GetComponent<Text>();
            levelAll = Clone(ui.btnAllLevelUp, "Level Up All", out control);
            levelAll.onClick.AddListener(new UnityAction(control.LevelAll));
            levelLabel = levelAll.transform.GetChild(0).GetComponent<Text>();
        }

        // The column (a VerticalLayoutGroup, "Squad Buttons") centres each button. A halved button would move to the
        // middle, so the column is aligned left instead, padded so the full-width buttons stay exactly where they were.
        private static void LeftAlignColumn(Button sample)
        {
            column = sample.transform.parent.GetComponent<VerticalLayoutGroup>();
            if (column == null)
            {
                return;
            }
            columnAlignment = column.childAlignment;
            columnPadLeft = column.padding.left;
            Plugin.Log.LogInfo("Squad all: column alignment " + column.childAlignment + ", control width "
                + column.childControlWidth + ", expand width " + column.childForceExpandWidth + ", padding left "
                + column.padding.left);
            float columnWidth = ((RectTransform)column.transform).rect.width;
            float buttonWidth = ((RectTransform)sample.transform).rect.width;
            int row = (int)columnAlignment / 3; // upper, middle or lower
            column.childAlignment = (TextAnchor)(row * 3);
            column.padding.left = columnPadLeft + Mathf.RoundToInt((columnWidth - column.padding.horizontal
                - buttonWidth) / 2f);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)column.transform);
        }

        // The game's button shrinks to the left half of its row; its label shrinks to fit.
        private static void Halve(Button button)
        {
            var rt = (RectTransform)button.transform;
            Text text = button.transform.GetChild(0).GetComponent<Text>();
            var h = new Halved();
            h.button = rt;
            h.size = rt.sizeDelta;
            h.text = text;
            h.textSize = ((RectTransform)text.transform).sizeDelta;
            h.bestFit = text.resizeTextForBestFit;
            h.minSize = text.resizeTextMinSize;
            h.maxSize = text.resizeTextMaxSize;
            halved.Add(h);
            float half = (rt.rect.width - Gap) / 2f;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, half);
            FitLabel(text, half);
        }

        private static void FitLabel(Text text, float buttonWidth)
        {
            ((RectTransform)text.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                buttonWidth - 2f * TextPadding);
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = MinFontSize;
            text.resizeTextForBestFit = true;
        }

        // A copy of the game's button in the right half of its row (appended last to the same parent), with its own
        // label and no wiring. Halves the original first.
        private static Button Clone(Button template, string label, out SquadAllControl control)
        {
            Halve(template);
            GameObject go = Object.Instantiate(template.gameObject, template.transform.parent, false);
            go.name = Prefix + label.Replace(" ", "");
            go.SetActive(true);
            Button b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            b.interactable = true;
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c != null && c.GetType().Name == "LocalizationText")
                {
                    Object.DestroyImmediate(c);
                }
            }
            Text t = go.transform.GetChild(0).GetComponent<Text>();
            t.text = label;
            t.color = Color.white;
            FitLabel(t, ((RectTransform)template.transform).rect.width);
            // The buttons sit in a VerticalLayoutGroup ("Squad Buttons"), which would stack the copy at the bottom;
            // it's left out of the layout and placed beside its original instead (Place).
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null)
            {
                le = go.AddComponent<LayoutElement>();
            }
            le.ignoreLayout = true;
            control = go.AddComponent<SquadAllControl>();
            control.Original = (RectTransform)template.transform;
            control.Place();
            Plugin.Log.LogInfo("Squad all: " + label + " at " + ((RectTransform)go.transform).anchoredPosition
                + " beside " + template.name + " at " + control.Original.anchoredPosition + " size "
                + control.Original.rect.size);
            return b;
        }

        // Right of the original, on its row.
        internal static void Place(RectTransform copy, RectTransform original)
        {
            copy.anchorMin = original.anchorMin;
            copy.anchorMax = original.anchorMax;
            copy.pivot = original.pivot;
            copy.sizeDelta = original.sizeDelta;
            copy.anchoredPosition = original.anchoredPosition + new Vector2(original.rect.width + Gap, 0f);
        }

        // Refresh postfix: the buttons follow their originals, and Reward All shows the total and is off when no squad can be rewarded.
        internal static void UpdateReward()
        {
            if (rewardAll == null)
            {
                return;
            }
            foreach (Button b in new Button[] { equipAll, supplyAll, rewardAll, levelAll })
            {
                b.GetComponent<SquadAllControl>().Place();
                b.gameObject.SetActive(b.GetComponent<SquadAllControl>().Original.gameObject.activeSelf);
            }
            int cost = 0;
            int count = 0;
            foreach (Squad s in Squads())
            {
                if (CanReward(s))
                {
                    cost += RewardCost(s);
                    count++;
                }
            }
            rewardAll.interactable = count > 0;
            rewardLabel.color = count > 0 ? Color.white : rewardAll.colors.disabledColor;
            rewardLabel.text = count > 0 ? "Reward All (-" + cost + ")" : "Reward All";
            cost = 0;
            count = 0;
            foreach (Squad s in Squads())
            {
                int c = LevelUpCost(s);
                if (c > 0)
                {
                    cost += c;
                    count++;
                }
            }
            levelAll.interactable = count > 0;
            levelLabel.color = count > 0 ? Color.white : levelAll.colors.disabledColor;
            levelLabel.text = count > 0 ? "Level Up All (-" + cost + ")" : "Level Up All";
        }

        internal static void EquipAll()
        {
            ForEachSquad(0);
        }

        internal static void SupplyAll()
        {
            ForEachSquad(1);
        }

        internal static void RewardAll()
        {
            ForEachSquad(2);
        }

        internal static void LevelAll()
        {
            ForEachSquad(3);
        }

        private static void ForEachSquad(int action)
        {
            UIParty ui = UIParty.code;
            if (ui == null || busy)
            {
                return;
            }
            busy = true;
            Squad selected = ui.selectedSquad;
            int done = 0;
            try
            {
                foreach (Squad s in Squads())
                {
                    if ((action == 2 && !CanReward(s)) || (action == 3 && LevelUpCost(s) <= 0))
                    {
                        continue;
                    }
                    ui.selectedSquad = s;
                    if (action == 0)
                    {
                        ui.AutoEquipClick();
                    }
                    else if (action == 1)
                    {
                        ui.AutoSupply();
                    }
                    else if (action == 3)
                    {
                        if (MapAgent.code._Storage.credits < LevelUpCost(s))
                        {
                            break; // out of coin (AllLevelUpClick's AddCoin would refuse)
                        }
                        ui.RefreshLevelUpBTN(); // fills the game's list of units to level for this squad
                        ui.AllLevelUpClick(); // pays and levels
                    }
                    else
                    {
                        if (!MapAgent.code.AddCoin(-RewardCost(s)))
                        {
                            break; // out of coin (AddCoin refuses, as for the game's button)
                        }
                        s.AddMorale(50); // as ButtonRewardSquad
                    }
                    done++;
                }
            }
            finally
            {
                ui.selectedSquad = selected;
                busy = false;
                ui.Refresh();
            }
            Plugin.Log.LogInfo("Squad all: " + (action == 0 ? "equipped" : action == 1 ? "supplied" : action == 2 ? "rewarded" : "levelled up") + " "
                + done + " squad(s)");
        }

        private static System.Collections.Generic.List<Squad> Squads()
        {
            var list = new System.Collections.Generic.List<Squad>();
            Army army = MapAgent.code != null ? MapAgent.code._Army : null;
            if (army == null)
            {
                return list;
            }
            foreach (Transform t in army.squads.items)
            {
                Squad s = t != null ? t.GetComponent<Squad>() : null;
                if (s != null)
                {
                    list.Add(s);
                }
            }
            return list;
        }

        private static bool CanReward(Squad s)
        {
            return s.GetMorale() < 100 && s.GetCanFightUnitsCount() > 0;
        }

        private static int RewardCost(Squad s)
        {
            return (int)((float)s.GetCost() * Global.RewardSquad);
        }

        // What the squad's All Level Up would cost (RefreshLevelUpBTN's rule); 0 when nobody can level.
        private static int LevelUpCost(Squad s)
        {
            int cost = 0;
            foreach (Transform t in s.OriginalUnits)
            {
                NPCGen g = t != null ? t.GetComponent<NPCGen>() : null;
                if (g != null && g.curExp > g.nextExp && g.Level < Global.NPCGenMaxLevel)
                {
                    cost += g.GetLevelUpCost();
                }
            }
            return cost;
        }

        // Hot reload: remove the buttons.
        internal static void Teardown()
        {
            foreach (Button b in new Button[] { equipAll, supplyAll, rewardAll, levelAll })
            {
                if (b != null)
                {
                    Object.Destroy(b.gameObject);
                }
            }
            equipAll = null;
            supplyAll = null;
            rewardAll = null;
            rewardLabel = null;
            levelAll = null;
            levelLabel = null;
            foreach (Halved h in halved)
            {
                if (h.button != null)
                {
                    h.button.sizeDelta = h.size;
                }
                if (h.text != null)
                {
                    ((RectTransform)h.text.transform).sizeDelta = h.textSize;
                    h.text.resizeTextForBestFit = h.bestFit;
                    h.text.resizeTextMinSize = h.minSize;
                    h.text.resizeTextMaxSize = h.maxSize;
                }
            }
            halved.Clear();
            if (column != null)
            {
                column.childAlignment = columnAlignment;
                column.padding.left = columnPadLeft;
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)column.transform);
            }
            column = null;
        }
    }

    internal class SquadAllControl : MonoBehaviour
    {
        public RectTransform Original;

        // Every frame, since the column can move the original after a refresh (the copy is outside its layout).
        public void LateUpdate()
        {
            Place();
        }

        public void Place()
        {
            if (Original != null)
            {
                SquadAll.Place((RectTransform)transform, Original);
            }
        }

        public void EquipAll()
        {
            SquadAll.EquipAll();
        }

        public void SupplyAll()
        {
            SquadAll.SupplyAll();
        }

        public void RewardAll()
        {
            SquadAll.RewardAll();
        }

        public void LevelAll()
        {
            SquadAll.LevelAll();
        }
    }

    [HarmonyPatch]
    internal static class SquadAllPatches
    {
        private static bool failed;

        [HarmonyPostfix, HarmonyPatch(typeof(UIParty), nameof(UIParty.Refresh))]
        private static void Refreshed(UIParty __instance)
        {
            try
            {
                SquadAll.Ensure(__instance);
                SquadAll.UpdateReward();
            }
            catch (System.Exception e)
            {
                if (!failed)
                {
                    failed = true;
                    Plugin.Log.LogError("Squad all: " + e);
                }
            }
        }
    }
}
