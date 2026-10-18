using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace FreemanFixes
{
    // Exported campaigns: from dev mode, turn the current game into a campaign of its own in the New Game list, next
    // to the game's story campaigns (CampaignMenu). Starting one runs the story-campaign flow: Normal/Realistic, then
    // the usual character creation with the game's own backgrounds. While MapAgent.Initiate sets up the new game, the
    // chosen background's party, cities and money are swapped for the pack's (BeforeNewGameSetup).
    //  - The campaign starts at the starting level: you are the character you just created, the party joins at
    //    its usual level, and nobody keeps exported levels or XP.
    //  - Copied: the whole party (soldier types + companions) in the same squads (names, icons, formations, slot
    //    positions), your and your companions' attributes, skills and weapon stats, everyone's exact loadout
    //    (every equipment slot, yours included), your
    //    bag (items + size), your money (InitMoney) and faction funds, and the political map: who owns every city,
    //    which factions are destroyed, your status/relation/trade/defense with every faction and all wars.
    //  - Everything else comes from the background picked in character creation.
    // The map goes in where a story campaign sets up its factions (Mainframe.LoadBattleData, before the factions buy
    // their map armies); relations and wars wait until the world has settled (DevCampaignRunner).
    // Each export is a campaign pack - a small mod that depends on this one - in BepInEx/FreemanSlop_Mods/<name>/
    // (manifest.json + campaign.json; copy the folder to share it). Packs are added to the preset table in memory
    // after the game loads its tables (TableManager.Initialize), so no game files change. Names show via localization keys the mod
    // adds (unknown keys come out upper-cased).
    internal static class DevCampaigns
    {
        private const string PacksFolder = "FreemanSlop_Mods";
        private const string LegacyFolder = "FreemanSlop_Campaigns"; // BepInEx/config, before packs
        private const string ManifestFile = "manifest.json";
        private const string CampaignFile = "campaign.json";
        private const int FirstId = 9000;

        private static readonly Dictionary<int, DevCampaign> active = new Dictionary<int, DevCampaign>();
        internal static DevCampaign pending; // waiting for the new world to be ready
        private static GameObject runner;

        private static string Dir
        {
            get { return Path.Combine(Paths.BepInExRootPath, PacksFolder); }
        }

        internal static int Count
        {
            get { return PackFolders().Count; }
        }

        private static List<string> PackFolders()
        {
            var result = new List<string>();
            if (Directory.Exists(Dir))
            {
                foreach (string d in Directory.GetDirectories(Dir))
                {
                    if (File.Exists(Path.Combine(d, CampaignFile)))
                    {
                        result.Add(d);
                    }
                }
            }
            result.Sort();
            return result;
        }

        // Exports from before packs were plain JSON files in BepInEx/config/FreemanSlop_Campaigns: wrap each in a pack.
        private static void MigrateLegacy()
        {
            string legacy = Path.Combine(Paths.ConfigPath, LegacyFolder);
            if (!Directory.Exists(legacy))
            {
                return;
            }
            foreach (string file in Directory.GetFiles(legacy, "*.json"))
            {
                try
                {
                    var c = JsonConvert.DeserializeObject<DevCampaign>(File.ReadAllText(file));
                    if (c != null)
                    {
                        WritePack(Path.Combine(Dir, Path.GetFileNameWithoutExtension(file)), c);
                    }
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Campaign migration of " + file + " failed: " + e.Message);
                }
            }
            if (Directory.GetFileSystemEntries(legacy).Length == 0)
            {
                Directory.Delete(legacy);
            }
        }

        private static void WritePack(string folder, DevCampaign c)
        {
            Directory.CreateDirectory(folder);
            var m = new PackManifest();
            m.Name = c.Name;
            m.Description = c.Description;
            m.Requires = Plugin.Id;
            m.RequiresVersion = Plugin.Version;
            File.WriteAllText(Path.Combine(folder, ManifestFile), JsonConvert.SerializeObject(m, Formatting.Indented));
            File.WriteAllText(Path.Combine(folder, CampaignFile), JsonConvert.SerializeObject(c, Formatting.Indented));
        }

        // "0.4.0" >= "0.3.10"? Compare dotted numbers.
        private static bool VersionAtLeast(string have, string need)
        {
            if (string.IsNullOrEmpty(need))
            {
                return true;
            }
            string[] h = have.Split('.');
            string[] n = need.Split('.');
            for (int i = 0; i < Math.Max(h.Length, n.Length); i++)
            {
                int hv = 0, nv = 0;
                if (i < h.Length) { int.TryParse(h[i], out hv); }
                if (i < n.Length) { int.TryParse(n[i], out nv); }
                if (hv != nv)
                {
                    return hv > nv;
                }
            }
            return true;
        }

        // ---------------------------------------------------------------- export (dev menu)

        private const float ReplaceConfirmSeconds = 5f;
        private static string armedName;
        private static float armedUntil;

        // Name waiting for a second Export click to be replaced, or null.
        internal static string ArmedReplace
        {
            get { return armedName != null && Time.unscaledTime <= armedUntil ? armedName : null; }
        }

        // Returns true when it exported. Replacing an existing campaign of the same name takes a second click
        // within a few seconds, so an export from the wrong game can't silently wipe a good one.
        internal static bool Export(string name, string description)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                DevStore.Prompt("Give the campaign a name first");
                return false;
            }
            string folder = Path.Combine(Dir, SafeName(name));
            if (File.Exists(Path.Combine(folder, CampaignFile)) && !(armedName == name && Time.unscaledTime <= armedUntil))
            {
                armedName = name;
                armedUntil = Time.unscaledTime + ReplaceConfirmSeconds;
                DevMenu.ShowStatus("\"" + name + "\" already exists - click the button again within 5 seconds to replace it", true);
                return false;
            }
            armedName = null;
            var c = new DevCampaign();
            c.Name = name;
            c.Description = (description ?? string.Empty).Trim();
            NPCGen me = MapAgent.code.gen;
            // If this game was itself started from an exported background, base on that one's game background:
            // our own presets (FirstId+) aren't in the table yet when packs load.
            DevCampaign source;
            c.BasePreset = active.TryGetValue(me.Id, out source) ? source.BasePreset : me.Id;
            c.Money = MapAgent.code.GetCoin();
            c.FactionMoney = MapAgent.code._Army.faction.Money;
            c.Player = Uniform.Of(me);
            Storage bag = MapAgent.code.GetComponent<Storage>();
            c.BagSlots = bag.maxSlots;
            foreach (int id in bag.items)
            {
                if (id != 0)
                {
                    c.Bag.Add(id);
                }
            }
            foreach (Transform t in MapAgent.code._Army.faction.GetAllCity())
            {
                City city = t != null ? t.GetComponent<City>() : null;
                if (city != null)
                {
                    c.Cities.Add(city.id);
                }
            }
            var troopIndex = new Dictionary<NPCGen, int>();
            foreach (NPCGen gen in DevActions.PartyUnits())
            {
                Uniform u = Uniform.Of(gen);
                HeroAgent hero = gen.GetComponent<HeroAgent>();
                if (hero != null)
                {
                    u.Id = hero.HeroId;
                    c.Companions.Add(u);
                }
                else
                {
                    u.Id = gen.Id;
                    troopIndex[gen] = c.Troops.Count;
                    c.Troops.Add(u);
                }
            }
            foreach (Transform squadT in MapAgent.code._Army.squads.items)
            {
                Squad squad = squadT != null ? squadT.GetComponent<Squad>() : null;
                if (squad == null || squad.OriginalUnits == null)
                {
                    continue;
                }
                var layout = new SquadLayout();
                layout.Name = squad.rename;
                layout.Icon = squad.Icon != null ? squad.Icon.name : string.Empty;
                layout.Formation = squad.FormationIndex;
                foreach (Transform unit in squad.OriginalUnits)
                {
                    NPCGen gen = unit != null && unit.tag != "D" ? unit.GetComponent<NPCGen>() : null;
                    HeroAgent hero = gen != null ? gen.GetComponent<HeroAgent>() : null;
                    int ti;
                    if (hero != null)
                    {
                        layout.Slots.Add("H" + hero.HeroId);
                    }
                    else if (gen != null && troopIndex.TryGetValue(gen, out ti))
                    {
                        layout.Slots.Add("T" + ti);
                    }
                    else
                    {
                        layout.Slots.Add(string.Empty);
                    }
                }
                c.Squads.Add(layout);
            }
            foreach (Transform t in Global.code.factions.items)
            {
                Faction f = t != null ? t.GetComponent<Faction>() : null;
                if (f == null)
                {
                    continue;
                }
                var st = new FactionState();
                st.Name = f.name;
                st.Relationship = f.relationship;
                st.Status = (int)f.relationValue;
                st.Trading = f.isTrading;
                st.Defense = f.isAttackAneDefend;
                st.Enemies = new List<string>(f.enemyFactions);
                st.Destroyed = f.isDestroyed;
                foreach (Transform t2 in f.GetAllCity())
                {
                    City city = t2 != null ? t2.GetComponent<City>() : null; // villages follow their city
                    if (city != null)
                    {
                        st.Cities.Add(city.id);
                    }
                }
                c.Factions.Add(st);
            }
            WritePack(folder, c);
            Inject();
            DevActions.Report("Campaign \"" + name + "\" exported: " + c.Troops.Count + " troops, " +
                c.Companions.Count + " companions, " + c.Cities.Count + " cities - start it from New Game");
            return true;
        }

        // ---------------------------------------------------------------- New Game list (CampaignMenu)

        // Loaded packs by preset ID, in folder order. The preset ID doubles as the pack's Mainframe.battleID.
        internal static Dictionary<int, DevCampaign> Active
        {
            get { return active; }
        }

        internal static bool IsPack(int presetId)
        {
            return active.ContainsKey(presetId);
        }

        internal static string NameKey(int presetId)
        {
            return "FREEMANSLOP_CAMPAIGN_" + presetId;
        }

        // The pack picked in the New Game list, if the game being set up is one (story flow: isClassical false and
        // battleID set by Mainframe.SetBattle; battleID stays stale after picking Classical, hence the isClassical check).
        internal static DevCampaign Selected
        {
            get
            {
                DevCampaign c;
                return Mainframe.code != null && Mainframe.code.isBattleMode && !Mainframe.code.isClassical
                    && active.TryGetValue(Mainframe.code.battleID, out c) ? c : null;
            }
        }

        internal static string Summary(DevCampaign c)
        {
            int cities = 0;
            int factions = 0;
            foreach (FactionState st in c.Factions)
            {
                if (st.Cities != null && st.Cities.Count > 0)
                {
                    cities += st.Cities.Count;
                    factions++;
                }
            }
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(c.Description))
            {
                sb.Append(c.Description).Append("\n\n");
            }
            sb.Append("Party: ").Append(c.Troops.Count).Append(" soldiers, ").Append(c.Companions.Count)
                .Append(" companions in ").Append(c.Squads.Count).Append(" squads\n");
            sb.Append("Money: ").Append(c.Money).Append(", faction funds: ").Append(c.FactionMoney)
                .Append(", bag: ").Append(c.Bag.Count).Append(" items\n");
            sb.Append("Your cities: ").Append(c.Cities.Count);
            if (factions > 0)
            {
                sb.Append(" (map: ").Append(cities).Append(" cities held by ").Append(factions).Append(" factions)");
            }
            return sb.ToString();
        }

        private static string SafeName(string name)
        {
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                {
                    chars[i] = '-';
                }
            }
            return new string(chars);
        }

        // ---------------------------------------------------------------- add to the game's preset table

        internal static void Inject()
        {
            Dictionary<int, Table_PlayerPresets> presets = TableManager.GetPlayerPresets();
            if (presets == null || presets.Count == 0)
            {
                return; // tables not loaded yet; the TableManager.Initialize postfix will call us
            }
            foreach (int id in active.Keys)
            {
                presets.Remove(id);
            }
            active.Clear();
            MigrateLegacy();
            int nextId = FirstId;
            foreach (string folder in PackFolders())
            {
                DevCampaign c;
                try
                {
                    string manifestPath = Path.Combine(folder, ManifestFile);
                    if (File.Exists(manifestPath))
                    {
                        var m = JsonConvert.DeserializeObject<PackManifest>(File.ReadAllText(manifestPath));
                        if (m != null && !string.IsNullOrEmpty(m.Requires) && m.Requires != Plugin.Id)
                        {
                            Plugin.Log.LogWarning("Campaign pack " + folder + " is for another mod (" + m.Requires + ")");
                            continue;
                        }
                        if (m != null && !VersionAtLeast(Plugin.Version, m.RequiresVersion))
                        {
                            Plugin.Log.LogWarning("Campaign pack \"" + m.Name + "\" needs Freeman Slop Mod " +
                                m.RequiresVersion + " or newer (this is " + Plugin.Version + "); skipped");
                            continue;
                        }
                    }
                    c = JsonConvert.DeserializeObject<DevCampaign>(File.ReadAllText(Path.Combine(folder, CampaignFile)));
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Campaign pack " + folder + " unreadable: " + e.Message);
                    continue;
                }
                if (c == null)
                {
                    continue;
                }
                Table_PlayerPresets basePreset;
                if (!presets.TryGetValue(c.BasePreset, out basePreset))
                {
                    basePreset = FirstGamePreset(presets);
                    if (basePreset == null)
                    {
                        continue;
                    }
                    Plugin.Log.LogWarning("Campaign \"" + c.Name + "\": background " + c.BasePreset +
                        " not found, using " + basePreset.ID);
                }
                // Packs aren't backgrounds, but saves started from one before 0.5.0 have its ID as the player's gen.Id
                // and NPCGen looks that up on every level-up (BaseHealth): keep the ID, pointing at the game's own
                // background it was based on. Never offered in character creation (CampaignMenu).
                int id = nextId++;
                AddText(NameKey(id), c.Name);
                presets[id] = basePreset;
                active[id] = c;
            }
            if (active.Count > 0)
            {
                Plugin.Log.LogInfo("Campaigns: added " + active.Count + " exported background(s)");
            }
        }

        private static Table_PlayerPresets FirstGamePreset(Dictionary<int, Table_PlayerPresets> presets)
        {
            Table_PlayerPresets best = null;
            foreach (KeyValuePair<int, Table_PlayerPresets> kv in presets)
            {
                if (kv.Key < FirstId && (best == null || kv.Key < best.ID))
                {
                    best = kv.Value;
                }
            }
            return best;
        }

        // Register a localization key so menus show our text as typed.
        // Localization.Format: KEY -> LocalizationConfig.dictLocalization (key map) -> TableManager localization row.
        private static void AddText(string key, string text)
        {
            text = text.Replace("{", "{{").Replace("}", "}}"); // the game runs it through string.Format
            var row = new Table_Localization();
            row.Key = key;
            row.English = text;
            row.Chinese = text;
            row.Russian = text;
            TableManager.GetLocalization()[key] = row;
            var map = AccessTools.Field(typeof(LocalizationConfig), "dictLocalization").GetValue(null) as Dictionary<string, string>;
            if (map != null)
            {
                map[key] = key;
            }
        }

        internal static void Remove()
        {
            Dictionary<int, Table_PlayerPresets> presets = TableManager.GetPlayerPresets();
            if (presets != null)
            {
                foreach (int id in active.Keys)
                {
                    presets.Remove(id);
                }
            }
            active.Clear();
            CampaignMenu.Teardown();
            pending = null;
            if (runner != null)
            {
                UnityEngine.Object.Destroy(runner);
            }
            runner = null;
        }

        // ---------------------------------------------------------------- new game with an exported background

        // Prefix for MapAgent.Initiate, which spawns the party, money and first city from
        // RM.code.TabPlayerPresetsData[UICampaign.code.PlayerPresetID]: for that one call the chosen background's
        // entry is a copy carrying the pack's soldiers, companions, cities and money. RestorePreset puts it back.
        private static Table_PlayerPresets swappedPreset;

        internal static void BeforeNewGameSetup()
        {
            swappedPreset = null;
            DevCampaign c = Selected;
            if (c == null || UICampaign.code == null || Mainframe.code.IsFromFile())
            {
                return;
            }
            Dictionary<int, Table_PlayerPresets> presets = RM.code.TabPlayerPresetsData;
            int id = UICampaign.code.PlayerPresetID;
            Table_PlayerPresets chosen;
            if (!presets.TryGetValue(id, out chosen))
            {
                return;
            }
            var p = JsonConvert.DeserializeObject<Table_PlayerPresets>(JsonConvert.SerializeObject(chosen));
            p.Soldiers = new List<int>();
            foreach (Uniform u in c.Troops)
            {
                p.Soldiers.Add(u.Id);
            }
            p.Heros = new List<int>();
            foreach (Uniform u in c.Companions)
            {
                p.Heros.Add(u.Id);
            }
            p.Citys = new List<int>(c.Cities);
            p.InitMoney = c.Money;
            swappedPreset = chosen;
            presets[id] = p;
        }

        internal static void RestorePreset()
        {
            if (swappedPreset != null)
            {
                RM.code.TabPlayerPresetsData[swappedPreset.ID] = swappedPreset;
                swappedPreset = null;
            }
        }

        // Postfix for MapAgent.Initiate (new game setup): uniforms + faction funds, which presets can't express;
        // the cities follow in ApplyMap, relations and wars once the world is up (DevCampaignRunner).
        internal static void AfterNewGameSetup()
        {
            if (UICampaign.code == null)
            {
                return;
            }
            DevCampaign c = Selected;
            if (c == null || Mainframe.code.IsFromFile())
            {
                return;
            }
            MapAgent.code._Army.faction.Money = c.FactionMoney;
            NPCGen me = MapAgent.code.gen;
            if (c.Player != null)
            {
                c.Player.ApplyAttributesAndSkills(me);
                c.Player.ApplyTo(me);
                // NPCGen.LevelUp's player formula, so the carried-over Constitution counts.
                me.maxHealth = RM.code.TabPlayerPresetsData[me.Id].BaseHealth + me.constitution * Global.ConstitutionFactor;
                me.curHealth = me.maxHealth;
            }
            if (c.Bag != null && c.Bag.Count > 0)
            {
                Storage bag = MapAgent.code.GetComponent<Storage>();
                bag.maxSlots = Mathf.Max(bag.maxSlots, c.BagSlots, c.Bag.Count);
                var items = new List<int>(bag.maxSlots);
                foreach (int id in c.Bag)
                {
                    if (RM.code.AllItem.ContainsKey(id))
                    {
                        items.Add(id);
                    }
                }
                while (items.Count < bag.maxSlots)
                {
                    items.Add(0);
                }
                bag.items = items; // replaces the background's starting items
            }
            // Match each exported person to a live unit: companions by HeroId, troops by type in export order.
            var heroes = new Dictionary<int, NPCGen>();
            var liveByType = new Dictionary<int, Queue<NPCGen>>();
            foreach (NPCGen gen in DevActions.PartyUnits())
            {
                HeroAgent hero = gen.GetComponent<HeroAgent>();
                if (hero != null)
                {
                    heroes[hero.HeroId] = gen;
                }
                else
                {
                    if (!liveByType.ContainsKey(gen.Id))
                    {
                        liveByType[gen.Id] = new Queue<NPCGen>();
                    }
                    liveByType[gen.Id].Enqueue(gen);
                }
            }
            var troops = new NPCGen[c.Troops.Count];
            for (int i = 0; i < c.Troops.Count; i++)
            {
                Queue<NPCGen> q;
                if (liveByType.TryGetValue(c.Troops[i].Id, out q) && q.Count > 0)
                {
                    troops[i] = q.Dequeue();
                    c.Troops[i].ApplyTo(troops[i]);
                }
            }
            foreach (Uniform u in c.Companions)
            {
                NPCGen gen;
                if (heroes.TryGetValue(u.Id, out gen))
                {
                    u.ApplyAttributesAndSkills(gen);
                    u.ApplyTo(gen);
                }
            }
            if (c.Squads.Count > 0)
            {
                RebuildSquads(c, heroes, troops);
            }
            Plugin.Log.LogInfo("Campaign \"" + c.Name + "\": attributes, skills, weapon stats, loadouts, bag and faction funds applied");
            pending = c;
            if (runner == null)
            {
                runner = new GameObject("FreemanSlop_CampaignRunner");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<DevCampaignRunner>();
            }
        }

        // Replace the new game's default squads with the exported layout (names, icons, formations, slot positions),
        // using the game's own steps: units go to the unassigned pool (like UIParty.Disband), then Army.NewSquad +
        // Squad.AddUnit(gen, slot). Anyone not placed stays unassigned.
        private static void RebuildSquads(DevCampaign c, Dictionary<int, NPCGen> heroes, NPCGen[] troops)
        {
            Army army = MapAgent.code._Army;
            var old = new List<Squad>();
            foreach (Transform t in army.squads.items)
            {
                Squad sq = t != null ? t.GetComponent<Squad>() : null;
                if (sq != null)
                {
                    old.Add(sq);
                }
            }
            foreach (Squad sq in old)
            {
                for (int i = sq.OriginalUnits.Length - 1; i >= 0; i--)
                {
                    Transform unit = sq.OriginalUnits[i];
                    if (unit != null)
                    {
                        if (army.unassignedUnits.AddItemDifferentObject(unit))
                        {
                            unit.SetParent(army.transform);
                        }
                        sq.RemoveUnit(unit);
                    }
                }
                army.squads.RemoveItem(sq.transform);
                UnityEngine.Object.Destroy(sq.gameObject);
            }
            int placed = 0;
            foreach (SquadLayout layout in c.Squads)
            {
                Squad sq = army.NewSquad();
                if (!string.IsNullOrEmpty(layout.Name))
                {
                    sq.rename = layout.Name;
                }
                Texture2D icon;
                if (!string.IsNullOrEmpty(layout.Icon) && RM.code.SquadIconData.TryGetValue(layout.Icon, out icon))
                {
                    sq.Icon = icon;
                }
                sq.FormationIndex = layout.Formation;
                if (layout.Slots.Count > sq.OriginalUnits.Length)
                {
                    sq.OriginalUnits = new Transform[layout.Slots.Count];
                }
                for (int slot = 0; slot < layout.Slots.Count; slot++)
                {
                    NPCGen gen = Resolve(layout.Slots[slot], heroes, troops);
                    if (gen == null)
                    {
                        continue;
                    }
                    army.unassignedUnits.RemoveItem(gen.transform);
                    sq.AddUnit(gen, slot);
                    placed++;
                }
            }
            Plugin.Log.LogInfo("Campaign \"" + c.Name + "\": " + c.Squads.Count + " squads rebuilt, " + placed + " units placed");
        }

        private static NPCGen Resolve(string slot, Dictionary<int, NPCGen> heroes, NPCGen[] troops)
        {
            if (string.IsNullOrEmpty(slot) || slot.Length < 2)
            {
                return null;
            }
            int n;
            if (!int.TryParse(slot.Substring(1), out n))
            {
                return null;
            }
            if (slot[0] == 'H')
            {
                NPCGen gen;
                return heroes.TryGetValue(n, out gen) ? gen : null;
            }
            return slot[0] == 'T' && n >= 0 && n < troops.Length ? troops[n] : null;
        }

        // Replaces Mainframe.LoadBattleData for a pack (story data would destroy every faction not in the story):
        // hands every city to its exported owner, the way the story code does (Army.SetFaction), and marks the
        // factions that were destroyed (story code: unless NotCanDestory). Your own cities get a fresh garrison like
        // MapAgent.Initiate's preset city. Packs from before 0.5.0 only list your cities (Cities).
        internal static void ApplyMap(DevCampaign c)
        {
            Faction me = MapAgent.code._Army.faction;
            bool hasMap = false;
            foreach (FactionState st in c.Factions)
            {
                if (st.Cities != null && st.Cities.Count > 0)
                {
                    hasMap = true;
                }
            }
            int moved = 0;
            if (!hasMap)
            {
                foreach (int id in c.Cities)
                {
                    if (GiveCity(Global.code.GetCityById(id), me, me))
                    {
                        moved++;
                    }
                }
                Plugin.Log.LogInfo("Campaign \"" + c.Name + "\": no map in this pack, " + moved + " of your cities handed over");
                return;
            }
            int destroyed = 0;
            foreach (FactionState st in c.Factions)
            {
                Faction f = Global.code.GetFactionByName(st.Name);
                if (f == null)
                {
                    continue;
                }
                if (st.Destroyed && !f.NotCanDestory)
                {
                    f.isDestroyed = true;
                    destroyed++;
                }
                if (st.Cities == null)
                {
                    continue;
                }
                foreach (int id in st.Cities)
                {
                    if (GiveCity(Global.code.GetCityById(id), f, me))
                    {
                        moved++;
                    }
                }
            }
            Plugin.Log.LogInfo("Campaign \"" + c.Name + "\": map applied, " + moved + " cities changed hands, " +
                destroyed + " factions destroyed");
        }

        private static bool GiveCity(City city, Faction to, Faction me)
        {
            if (city == null || city._Army == null || city._Army.faction == to)
            {
                return false;
            }
            if (to != me)
            {
                city._Army.SetFaction(to);
                return true;
            }
            // Same steps as MapAgent.Initiate's preset-city code.
            city.IsRefreshSolide = true;
            city.ClearCityManager();
            city._Army.SetFaction(me);
            city._Army.squads.DeleteItems();
            if (me.PossibleSoldiers.items.Count > 0)
            {
                for (int n = 0; n < 15; n++)
                {
                    Transform type = me.PossibleSoldiers.items[UnityEngine.Random.Range(0, me.PossibleSoldiers.items.Count)];
                    Transform soldier = type != null ? city._Army.CreateNewSoldier(type) : null;
                    if (soldier != null)
                    {
                        city._Army.AddSoldiers(soldier);
                    }
                }
            }
            return true;
        }

        // Once the new world is up: relations, agreements and wars.
        internal static void ApplyWorld(DevCampaign c)
        {
            int factionCount = 0;
            foreach (FactionState st in c.Factions)
            {
                Faction f = Global.code.GetFactionByName(st.Name);
                if (f == null)
                {
                    continue;
                }
                f.enemyFactions = new List<string>();
                foreach (string enemy in st.Enemies)
                {
                    if (Global.code.GetFactionByName(enemy) != null)
                    {
                        f.enemyFactions.Add(enemy);
                    }
                }
                if (f.name != "Player")
                {
                    f.relationship = st.Relationship;
                    f.relationValue = (RelationValue)st.Status;
                    f.isTrading = st.Trading;
                    f.isAttackAneDefend = st.Defense;
                }
                factionCount++;
            }
            DevActions.Report("Campaign \"" + c.Name + "\": " + factionCount + " factions' relations restored");
        }
    }

    // manifest.json of a campaign pack: what it is and which version of this mod it needs.
    public class PackManifest
    {
        public string Name;
        public string Description;
        public string Requires;        // Plugin.Id
        public string RequiresVersion; // Plugin.Version it was exported with
    }

    public class DevCampaign
    {
        public string Name;
        public string Description;
        public int BasePreset;
        public int Money;
        public int FactionMoney;
        public List<int> Cities = new List<int>();
        public Uniform Player;
        public int BagSlots;
        public List<int> Bag = new List<int>();
        public List<Uniform> Troops = new List<Uniform>();     // Id = soldier type (Soldier table)
        public List<Uniform> Companions = new List<Uniform>(); // Id = HeroId (Hero table)
        public List<FactionState> Factions = new List<FactionState>();
        public List<SquadLayout> Squads = new List<SquadLayout>();
    }

    // One squad: name, icon, formation, and per slot "H<heroId>", "T<index into Troops>" or "" (empty).
    public class SquadLayout
    {
        public string Name;
        public string Icon;
        public int Formation;
        public List<string> Slots = new List<string>();
    }

    public class FactionState
    {
        public string Name;
        public int Relationship;
        public int Status; // RelationValue
        public bool Trading;
        public bool Defense;
        public List<string> Enemies = new List<string>();
        public bool Destroyed;
        public List<int> Cities = new List<int>(); // City.id of every city it holds (0.5.0+)
    }

    // Waits for the new world to finish setting up, then applies the campaign's cities and politics once.
    internal class DevCampaignRunner : MonoBehaviour
    {
        private const float SettleSeconds = 3f;
        private float readySince = -1f;

        private void Update()
        {
            if (DevCampaigns.pending == null)
            {
                Destroy(gameObject);
                return;
            }
            // Note: TerrainControl.initiated is only set in battle (CheckCombat), never on the world map.
            bool ready = TerrainControl.code != null && TerrainControl.code.isWorld && MapAgent.code != null
                && MapAgent.code.gen != null && Global.code != null && Global.code.factions != null;
            if (!ready)
            {
                readySince = -1f;
                return;
            }
            if (readySince < 0f)
            {
                readySince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - readySince < SettleSeconds)
            {
                return;
            }
            DevCampaign c = DevCampaigns.pending;
            DevCampaigns.pending = null;
            try
            {
                DevCampaigns.ApplyWorld(c);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Campaign world setup failed: " + e);
            }
            Destroy(gameObject);
        }
    }

    // A person's exact loadout: every NPCGen equipment slot (NPCGen.cs ~202-264) - uniform, armor, weapons with
    // attachments, belt (misc) and pouch (pa) slots. Applied as-is, even where the game would refuse the item for
    // low skill. (Class kept as "Uniform" for the JSON shape; Id = soldier type or HeroId.)
    public class Uniform
    {
        private static readonly string[] Slots =
        {
            "shirtId", "pantsId", "helmetId", "armorId", "maskId", "pistolId", "rifleId", "meleeId", "launcherId",
            "rifleScopeId_L", "rifleScopeId_R", "rifleSilencerId_L", "rifleSilencerId_R", "pistolScopeId",
            "pistolSilencerId", "pistolFlashLightId", "misc1Id", "misc2Id", "misc3Id", "misc4Id", "misc5Id",
            "misc6Id", "misc7Id", "misc8Id", "misc9Id", "misc10Id", "misc11Id", "misc12Id",
            "pa1Id", "pa2Id", "pa3Id", "pa4Id"
        };

        public int Id;
        public Dictionary<string, int> Equipment = new Dictionary<string, int>();
        public int Level;
        public int Exp;
        public float MaxHealth;
        public Dictionary<int, int> Stats = new Dictionary<int, int>(); // DevStats ids, Level excluded

        internal static Uniform Of(NPCGen g)
        {
            var u = new Uniform();
            u.Level = g.Level;
            u.Exp = g.curExp;
            u.MaxHealth = g.maxHealth;
            HeroAgent hero = g.GetComponent<HeroAgent>();
            AddStats(u.Stats, g, hero, DevStats.VitalsAndAttributes);
            AddStats(u.Stats, g, hero, DevStats.Skills);
            AddStats(u.Stats, g, hero, DevStats.Weapons);
            if (hero != null)
            {
                AddStats(u.Stats, g, hero, DevStats.CompanionStats);
            }
            u.Stats.Remove(DevStats.Level);
            u.Stats.Remove(DevStats.MaxHealth);
            foreach (string slot in Slots)
            {
                var f = typeof(NPCGen).GetField(slot);
                if ((object)f != null) // object compare: FieldInfo.op_Inequality is missing on the game's old Mono
                {
                    u.Equipment[slot] = (int)f.GetValue(g);
                }
            }
            return u;
        }

        private static void AddStats(Dictionary<int, int> stats, NPCGen g, HeroAgent h, int[] ids)
        {
            foreach (int id in ids)
            {
                stats[id] = DevStats.Get(g, h, id);
            }
        }

        // Attributes, skills and weapon stats only (campaign packs start at the starting level).
        internal void ApplyAttributesAndSkills(NPCGen g)
        {
            if (Stats == null)
            {
                return;
            }
            HeroAgent hero = g.GetComponent<HeroAgent>();
            ApplyStats(g, hero, DevStats.Attributes);
            ApplyStats(g, hero, DevStats.Skills);
            ApplyStats(g, hero, DevStats.Weapons);
        }

        private void ApplyStats(NPCGen g, HeroAgent hero, int[] ids)
        {
            foreach (int id in ids)
            {
                int v;
                if (Stats.TryGetValue(id, out v))
                {
                    DevStats.Set(g, hero, id, v);
                }
            }
        }

        internal int Get(string slot)
        {
            int v;
            return Equipment != null && Equipment.TryGetValue(slot, out v) ? v : 0;
        }

        internal void ApplyTo(NPCGen g)
        {
            if (Equipment == null)
            {
                return;
            }
            foreach (KeyValuePair<string, int> kv in Equipment)
            {
                var f = typeof(NPCGen).GetField(kv.Key);
                if ((object)f != null) // object compare: FieldInfo.op_Inequality is missing on the game's old Mono
                {
                    f.SetValue(g, Valid(kv.Value));
                }
            }
        }

        private static int Valid(int id)
        {
            return id == 0 || (RM.code != null && RM.code.AllItem.ContainsKey(id)) ? id : 0;
        }
    }

    [HarmonyPatch]
    internal static class DevCampaignPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(TableManager), nameof(TableManager.Initialize))]
        private static void TablesLoaded()
        {
            DevCampaigns.Inject();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(MapAgent), nameof(MapAgent.Initiate))]
        private static void NewGameSettingUp()
        {
            DevCampaigns.BeforeNewGameSetup();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(MapAgent), nameof(MapAgent.Initiate))]
        private static void NewGameSetUp()
        {
            DevCampaigns.AfterNewGameSetup();
        }

        // Runs even if Initiate throws, so the background never keeps the pack's party.
        [HarmonyFinalizer, HarmonyPatch(typeof(MapAgent), nameof(MapAgent.Initiate))]
        private static void NewGameSetupDone()
        {
            DevCampaigns.RestorePreset();
        }

        // Skipping prefix for a pack: InitGameObject.Awake calls LoadBattleData for every non-Classical new game,
        // and the story code would destroy every faction its (absent) story rows don't list.
        [HarmonyPrefix, HarmonyPatch(typeof(Mainframe), nameof(Mainframe.LoadBattleData))]
        private static bool StoryDataLoading()
        {
            DevCampaign c = DevCampaigns.Selected;
            if (c == null)
            {
                return true;
            }
            try
            {
                DevCampaigns.ApplyMap(c);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Campaign map setup failed: " + e);
            }
            return false;
        }
    }
}
