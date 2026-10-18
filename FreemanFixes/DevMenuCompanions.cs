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
        private const int ActMaxLevel = 23;
        private const int ActMaxGroup = 21; // arg: 0 attributes, 1 skills, 2 weapons, 3 companion stats
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
            Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(840f, 36f));
            var h = bar.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            MakeButton(bar.transform, "General", ActTabGeneral, 0);
            MakeButton(bar.transform, "Factions", ActTabFactions, 0);
            MakeButton(bar.transform, "Companions", ActTabCompanions, 0);
            MakeButton(bar.transform, "Cities", ActTabCities, 0);
        }

        private void BuildCompanionsPage(RectTransform root)
        {
            companionsPage = Page(root, "CompanionsPage");
            RectTransform page = (RectTransform)companionsPage.transform;

            Transform list = Column(page, CompanionListWidth);
            Header(list, "Characters  (Edit to change stats)");
            Transform allRow = Row(list);
            MakeButton(allRow, "Recruit All Companions", ActRecruitAll, 0).gameObject
                .AddComponent<LayoutElement>().preferredWidth = 300f;
            companionList = ScrollArea(list);

            Transform c1 = Column(page, StatColumnWidth);
            Header(c1, "Vitals & Attributes");
            foreach (int stat in DevStats.VitalsAndAttributes)
            {
                StatEditRow(c1, stat);
            }
            Transform maxRow = Row(c1);
            MakeButton(maxRow, "Max All Attributes", ActMaxGroup, 0).gameObject
                .AddComponent<LayoutElement>().flexibleWidth = 1f;
            Transform c2 = Column(page, StatColumnWidth);
            Header(c2, "Skills");
            foreach (int stat in DevStats.Skills)
            {
                StatEditRow(c2, stat);
            }
            Transform maxSkills = Row(c2);
            MakeButton(maxSkills, "Max All Skills", ActMaxGroup, 1).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Transform c3 = Column(page, StatColumnWidth);
            statsHeader = HeaderText(c3, "Weapons");
            foreach (int stat in DevStats.WeaponsAndCompanion)
            {
                if (stat == DevStats.Affection)
                {
                    Transform maxWeapons = Row(c3);
                    MakeButton(maxWeapons, "Max All Weapons", ActMaxGroup, 2).gameObject
                        .AddComponent<LayoutElement>().flexibleWidth = 1f;
                    GameObject companionHeader = HeaderText(c3, "Companion").transform.parent.gameObject;
                    companionOnlyRows[-1] = companionHeader;
                }
                StatEditRow(c3, stat);
            }
            Transform maxCompanion = Row(c3);
            MakeButton(maxCompanion, "Max All Companion Stats", ActMaxGroup, 3).gameObject
                .AddComponent<LayoutElement>().flexibleWidth = 1f;
            companionOnlyRows[-2] = maxCompanion.gameObject;
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
            citiesPage.SetActive(page == citiesPage);
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
                string status = h == null ? "you" : DevStats.Status(h);
                Text name = Label(row, DevStats.DisplayName(g) + "  (Lv " + g.Level + ", " + status + ")", 0f, 1f);
                if (i == selected)
                {
                    name.color = UnfriendlyColor; // the game's orange marks the one being edited
                }
                MakeButton(row, "Edit", ActSelect, i).gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;
                MakeButton(row, "Max Lv", ActMaxLevel, i).gameObject.AddComponent<LayoutElement>().preferredWidth = 80f;
                if (DevStats.CanRecruit(h))
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
            statsHeader.text = "Weapons  -  editing " + DevStats.DisplayName(g);
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
                        if (!DevStats.CanRecruit(heroes[i]))
                        {
                            continue; // already yours (party, away, prisoner) or dead
                        }
                        if (DevStats.Recruit(heroes[i]))
                        {
                            recruited++;
                        }
                        else if (TerrainControl.code == null || !TerrainControl.code.isWorld)
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
                case ActMaxLevel:
                    if (arg < heroes.Count)
                    {
                        HeroAgent lh = heroes[arg];
                        NPCGen lg = lh != null ? lh.gen : MapAgent.code.gen;
                        if (lh != null)
                        {
                            CompanionBases.Remember(lg); // level up from what they have, not from the table
                        }
                        DevStats.Set(lg, lh, DevStats.Level, DevStats.MaxLevelFor(lg));
                        DevActions.Report(DevStats.DisplayName(lg) + ": level " + lg.Level);
                    }
                    Refresh();
                    return true;
                case ActMaxGroup:
                    HeroAgent mh = heroes[selected];
                    NPCGen mg = mh != null ? mh.gen : MapAgent.code.gen;
                    int[] group = arg == 1 ? DevStats.Skills : arg == 2 ? DevStats.Weapons
                        : arg == 3 ? DevStats.CompanionStats : DevStats.Attributes;
                    if (arg == 3 && mh == null)
                    {
                        return true; // companion-only stats
                    }
                    foreach (int stat in group)
                    {
                        DevStats.Set(mg, mh, stat, DevStats.Max(stat));
                    }
                    DevStats.ClearUnspent(mg, mh, arg);
                    if (mh != null)
                    {
                        CompanionBases.Remember(mg);
                    }
                    string what = arg == 1 ? "skills" : arg == 2 ? "weapon proficiencies"
                        : arg == 3 ? "companion stats" : "attributes";
                    DevActions.Report(DevStats.DisplayName(mg) + ": all " + what + " set to max");
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
                    if (h != null)
                    {
                        CompanionBases.Remember(g); // else the next level-up recomputes it from the table
                    }
                    DevActions.Report(DevStats.DisplayName(g) + ": " + DevStats.Label(arg) + " set to " + DevStats.Get(g, h, arg));
                    field.text = string.Empty;
                    Refresh();
                    return true;
            }
            return false;
        }
    }
}
