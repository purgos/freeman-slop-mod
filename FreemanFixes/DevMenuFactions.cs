using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Dev menu "Factions" tab: one row per faction with your relation, status (Hostile..Ally), trade and mutual
    // defense agreements, and money; plus a side column listing war/peace between the selected faction and every
    // other faction (DevActions does the changes, using the game's own fields and enemy lists).
    internal partial class DevMenu
    {
        private const int ActStatus = 30;
        private const int ActTrade = 31;
        private const int ActDefense = 32;
        private const int ActWarsFor = 33;
        private const int ActWarToggle = 34;
        private const float FactionsWidth = 1220f;
        private const float WarsWidth = 570f;

        private readonly List<Text> statusLabels = new List<Text>();
        private readonly List<Text> tradeLabels = new List<Text>();
        private readonly List<Text> defenseLabels = new List<Text>();
        private Transform warList;
        private Text warsHeader;
        private int warsFor = -1;

        private void BuildFactionsPage(RectTransform root)
        {
            factionsPage = Page(root, "FactionsPage");
            var page = (RectTransform)factionsPage.transform;
            Transform col = Column(page, FactionsWidth);
            Header(col, "Factions  -  your relation (-100..100), status, agreements, money");
            Transform allRow = Row(col);
            MakeButton(allRow, "Make All Hostile", ActAllHostile, 0).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(allRow, "Make All Neutral", ActAllNeutral, 0).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(allRow, "Make All Allies", ActAllAllies, 0).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            if (Global.code != null && Global.code.factions != null)
            {
                foreach (Transform t in Global.code.factions.items)
                {
                    Faction f = t != null ? t.GetComponent<Faction>() : null;
                    if (f == null)
                    {
                        continue;
                    }
                    if (f.name == "Player")
                    {
                        factions.Insert(0, f);
                    }
                    else
                    {
                        factions.Add(f);
                    }
                }
            }
            for (int i = 0; i < factions.Count; i++)
            {
                Transform row = Row(col);
                factionNames.Add(Label(row, "", 0f, 1f));
                bool you = factions[i].name == "Player";
                if (you)
                {
                    Label(row, "", 60f + 70f + 55f + 130f + 105f + 115f + 5 * 8f, 0f); // nothing to set with yourself
                    statusLabels.Add(null);
                    tradeLabels.Add(null);
                    defenseLabels.Add(null);
                }
                else
                {
                    values[Key(ActRelation, i)] = Label(row, "", 60f, 0f);
                    inputs[Key(ActRelation, i)] = MakeInput(row, 70f);
                    Width(MakeButton(row, "Set", ActRelation, i), 55f);
                    statusLabels.Add(LabelOf(Width(MakeButton(row, "", ActStatus, i), 130f)));
                    tradeLabels.Add(LabelOf(Width(MakeButton(row, "", ActTrade, i), 105f)));
                    defenseLabels.Add(LabelOf(Width(MakeButton(row, "", ActDefense, i), 115f)));
                }
                values[Key(ActFactionMoney, i)] = Label(row, "", 140f, 0f);
                inputs[Key(ActFactionMoney, i)] = MakeInput(row, 100f);
                Width(MakeButton(row, "Set", ActFactionMoney, i), 55f);
                if (you)
                {
                    Label(row, "", 70f, 0f);
                }
                else
                {
                    Width(MakeButton(row, "Wars", ActWarsFor, i), 70f);
                }
            }

            Transform wars = Column(page, WarsWidth);
            warsHeader = HeaderText(wars, "Wars  -  press Wars on a faction");
            warList = ScrollArea(wars);
            factionsPage.SetActive(false);
        }

        private static Button Width(Button b, float width)
        {
            b.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            return b;
        }

        private static Text LabelOf(Button b)
        {
            return b.GetComponentInChildren<Text>(true);
        }

        private void RefreshFactions()
        {
            for (int i = 0; i < factions.Count; i++)
            {
                Faction f = factions[i];
                if (f == null)
                {
                    continue;
                }
                factionNames[i].text = f.Name;
                factionNames[i].color = FactionColor(f);
                SetValue(ActRelation, i, f.relationship.ToString());
                SetValue(ActFactionMoney, i, "$ " + f.Money.ToString("N0"));
                if (statusLabels[i] != null)
                {
                    statusLabels[i].text = f.relationValue.ToString();
                    tradeLabels[i].text = f.isTrading ? "Trade: ON" : "Trade: off";
                    defenseLabels[i].text = f.isAttackAneDefend ? "Defense: ON" : "Defense: off";
                }
            }
            RebuildWarList();
        }

        private void RebuildWarList()
        {
            if (warList == null)
            {
                return;
            }
            for (int i = warList.childCount - 1; i >= 0; i--)
            {
                Destroy(warList.GetChild(i).gameObject);
            }
            if (warsFor < 0 || warsFor >= factions.Count)
            {
                return;
            }
            Faction a = factions[warsFor];
            warsHeader.text = "Wars  -  " + a.Name;
            for (int j = 0; j < factions.Count; j++)
            {
                Faction b = factions[j];
                if (j == warsFor || b == null || b.name == "Player")
                {
                    continue; // your own wars are the Status button
                }
                bool atWar = a.IsEnemyFaction(b);
                Transform row = Row(warList);
                Text name = Label(row, b.Name, 0f, 1f);
                name.color = atWar ? HostileColor : tRowText.color;
                Width(MakeButton(row, atWar ? "At war" : "Peace", ActWarToggle, j), 120f);
            }
        }

        // Returns true if the action belonged to the Factions tab.
        private bool DoFactions(int action, int arg)
        {
            switch (action)
            {
                case ActStatus:
                    DevActions.CycleStatus(factions[arg]);
                    break;
                case ActTrade:
                    DevActions.ToggleTrade(factions[arg]);
                    break;
                case ActDefense:
                    DevActions.ToggleDefense(factions[arg]);
                    break;
                case ActWarsFor:
                    warsFor = arg;
                    break;
                case ActWarToggle:
                    if (warsFor >= 0)
                    {
                        Faction a = factions[warsFor];
                        DevActions.SetWar(a, factions[arg], !a.IsEnemyFaction(factions[arg]));
                    }
                    break;
                case ActAllNeutral:
                    DevActions.AllNeutral(factions);
                    break;
                case ActAllHostile:
                    DevActions.AllHostile(factions);
                    break;
                case ActAllAllies:
                    DevActions.AllAllies(factions);
                    break;
                default:
                    return false;
            }
            Refresh();
            return true;
        }
    }
}
