using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Dev menu tabs + the "Companions" tab: you + every companion (HeroRecruitCtrl.heroAgents) on the left with
    // Edit/Recruit, and a stat editor for the selected character on the right (DevStats does the reading/writing).
    internal partial class DevMenu
    {
        private const int ActTabGeneral = 11;
        private const int ActTabCompanions = 12;
        private const int ActTabFactions = 16;
        private const int ActRecruitAll = 18;
        private const int ActSelect = 13;
        private const int ActRecruit = 14;
        private const int ActStat = 15;
        private const float CompanionListWidth = 530f; // 530 + 3 x 400 + 3 x 30 gaps = 1820 page width
        private const float StatColumnWidth = 400f;

        private GameObject generalPage;
        private GameObject factionsPage;
        private GameObject companionsPage;
        private Transform companionList;
        private Text statsHeader;
        private readonly List<HeroAgent> heroes = new List<HeroAgent>(); // index 0 = null = the player
        private readonly Dictionary<int, GameObject> companionOnlyRows = new Dictionary<int, GameObject>();
        private int selected;

        private GameObject Page(RectTransform root, string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)page.transform;
            rt.SetParent(root, false);
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1820f, 860f));
            var h = page.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 30f;
            h.childAlignment = TextAnchor.UpperCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return page;
        }

        private void BuildTabs(RectTransform root)
        {
            var bar = new GameObject("Tabs", typeof(RectTransform));
            var rt = (RectTransform)bar.transform;
            rt.SetParent(root, false);
            Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(630f, 36f));
            var h = bar.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            MakeButton(bar.transform, "General", ActTabGeneral, 0);
            MakeButton(bar.transform, "Factions", ActTabFactions, 0);
            MakeButton(bar.transform, "Companions", ActTabCompanions, 0);
        }

        private void BuildCompanionsPage(RectTransform root)
        {
            companionsPage = Page(root, "CompanionsPage");
            RectTransform page = (RectTransform)companionsPage.transform;

            Transform list = Column(page, CompanionListWidth);
            Header(list, "Characters  (Edit to change stats)");
            Transform allRow = Row(list);
            MakeButton(allRow, "Recruit All Companions", ActRecruitAll, 0).gameObject
                .AddComponent<LayoutElement>().flexibleWidth = 1f;
            companionList = ScrollArea(list);

            Transform c1 = Column(page, StatColumnWidth);
            Header(c1, "Vitals & Attributes");
            foreach (int stat in DevStats.VitalsAndAttributes)
            {
                StatEditRow(c1, stat);
            }
            Transform c2 = Column(page, StatColumnWidth);
            Header(c2, "Skills");
            foreach (int stat in DevStats.Skills)
            {
                StatEditRow(c2, stat);
            }
            Transform c3 = Column(page, StatColumnWidth);
            statsHeader = HeaderText(c3, "Weapons");
            foreach (int stat in DevStats.WeaponsAndCompanion)
            {
                if (stat == DevStats.Affection)
                {
                    GameObject companionHeader = HeaderText(c3, "Companion").transform.parent.gameObject;
                    companionOnlyRows[-1] = companionHeader;
                }
                StatEditRow(c3, stat);
            }
            companionsPage.SetActive(false);
        }

        private Text HeaderText(Transform parent, string text)
        {
            Header(parent, text);
            return parent.GetChild(parent.childCount - 1).GetComponentInChildren<Text>();
        }

        private void StatEditRow(Transform col, int stat)
        {
            Transform row = Row(col);
            Label(row, DevStats.Label(stat), 0f, 1f);
            values[Key(ActStat, stat)] = Label(row, "", 60f, 0f);
            inputs[Key(ActStat, stat)] = MakeInput(row, 80f);
            MakeButton(row, "Set", ActStat, stat).gameObject.AddComponent<LayoutElement>().preferredWidth = 60f;
            if (DevStats.CompanionOnly(stat))
            {
                companionOnlyRows[stat] = row.gameObject;
            }
        }

        private void ShowTab(GameObject page)
        {
            generalPage.SetActive(page == generalPage);
            factionsPage.SetActive(page == factionsPage);
            companionsPage.SetActive(page == companionsPage);
            Refresh();
        }

        private void RebuildCompanionList()
        {
            for (int i = companionList.childCount - 1; i >= 0; i--)
            {
                Destroy(companionList.GetChild(i).gameObject);
            }
            heroes.Clear();
            heroes.Add(null); // you
            if (HeroRecruitCtrl.Instance != null)
            {
                foreach (HeroAgent h in HeroRecruitCtrl.Instance.heroAgents)
                {
                    if (h != null && h.gen != null)
                    {
                        heroes.Add(h);
                    }
                }
            }
            selected = Mathf.Clamp(selected, 0, heroes.Count - 1);
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroAgent h = heroes[i];
                NPCGen g = h != null ? h.gen : MapAgent.code.gen;
                Transform row = Row(companionList);
                string status = h == null ? "you" : (DevStats.InParty(h) ? "in party" : "not recruited");
                Text name = Label(row, g.Name + "  (Lv " + g.Level + ", " + status + ")", 0f, 1f);
                if (i == selected)
                {
                    name.color = UnfriendlyColor; // the game's orange marks the one being edited
                }
                MakeButton(row, "Edit", ActSelect, i).gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;
                if (h != null && !DevStats.InParty(h))
                {
                    MakeButton(row, "Recruit", ActRecruit, i).gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;
                }
                else
                {
                    Label(row, "", 100f, 0f);
                }
            }
        }

        private void RefreshCompanions()
        {
            if (companionsPage == null || !companionsPage.activeSelf)
            {
                return;
            }
            RebuildCompanionList();
            HeroAgent h = heroes[selected];
            NPCGen g = h != null ? h.gen : MapAgent.code.gen;
            statsHeader.text = "Weapons  -  editing " + g.Name;
            foreach (KeyValuePair<int, GameObject> kv in companionOnlyRows)
            {
                kv.Value.SetActive(h != null);
            }
            foreach (int stat in DevStats.VitalsAndAttributes)
            {
                SetValue(ActStat, stat, DevStats.Get(g, h, stat).ToString());
            }
            foreach (int stat in DevStats.Skills)
            {
                SetValue(ActStat, stat, DevStats.Get(g, h, stat).ToString());
            }
            foreach (int stat in DevStats.WeaponsAndCompanion)
            {
                SetValue(ActStat, stat, DevStats.Get(g, h, stat).ToString());
            }
        }

        // Returns true if the action belonged to the Companions tab.
        private bool DoCompanions(int action, int arg)
        {
            switch (action)
            {
                case ActTabGeneral:
                    ShowTab(generalPage);
                    return true;
                case ActTabFactions:
                    ShowTab(factionsPage);
                    return true;
                case ActTabCompanions:
                    ShowTab(companionsPage);
                    return true;
                case ActRecruitAll:
                    int recruited = 0;
                    for (int i = 1; i < heroes.Count; i++)
                    {
                        if (!DevStats.InParty(heroes[i]) && DevStats.Recruit(heroes[i]))
                        {
                            recruited++;
                        }
                        else if (!DevStats.InParty(heroes[i]) && (TerrainControl.code == null || !TerrainControl.code.isWorld))
                        {
                            break; // Recruit already said "world map only"; don't repeat it per companion
                        }
                    }
                    if (recruited > 0)
                    {
                        DevActions.Report("Recruited " + recruited + " companions");
                    }
                    Refresh();
                    return true;
                case ActSelect:
                    selected = arg;
                    Refresh();
                    return true;
                case ActRecruit:
                    if (arg < heroes.Count && DevStats.Recruit(heroes[arg]))
                    {
                        selected = arg;
                        DevActions.Report("Recruited " + heroes[arg].Name);
                    }
                    Refresh();
                    return true;
                case ActStat:
                    InputField field;
                    int value;
                    if (!inputs.TryGetValue(Key(ActStat, arg), out field) || !int.TryParse(field.text, out value))
                    {
                        DevStore.Prompt("Type a number first");
                        return true;
                    }
                    HeroAgent h = heroes[selected];
                    NPCGen g = h != null ? h.gen : MapAgent.code.gen;
                    DevStats.Set(g, h, arg, value);
                    DevActions.Report(g.Name + ": " + DevStats.Label(arg) + " set to " + DevStats.Get(g, h, arg));
                    field.text = string.Empty;
                    Refresh();
                    return true;
            }
            return false;
        }
    }
}
