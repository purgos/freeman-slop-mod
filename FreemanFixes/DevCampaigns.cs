using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace FreemanFixes
{
    // Exported campaigns: from dev mode, turn the current game into a new start "background" in the normal
    // character-creation dialog (UICampaign.ShowPLayerPreSetDialog_1 lists every Table_PlayerPresets entry).
    // A new game with it is otherwise completely normal (random world, character creator, faction name/flag).
    //  - Copied: the whole party as soldier types + companions (no levels or stats), everyone's exact loadout
    //    (every equipment slot, yours included), your bag (items + size), every city you own (the game itself only hands out the first preset city in
    //    MapAgent.Initiate, so the rest are given the same way once the world is up), your money (InitMoney), your
    //    faction's funds, your status/relation/trade/defense with every faction, and all faction-vs-faction wars.
    //  - Everything else comes from the background you started this game with (your gen.Id preset).
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
                    c.Troops.Add(u);
                }
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
                c.Factions.Add(st);
            }
            WritePack(folder, c);
            Inject();
            DevActions.Report("Campaign \"" + name + "\" exported: " + c.Troops.Count + " troops, " +
                c.Companions.Count + " companions, " + c.Cities.Count + " cities - pick it as your background in a new game");
            return true;
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
                // Deep copy of the background the campaign was exported from.
                var p = JsonConvert.DeserializeObject<Table_PlayerPresets>(JsonConvert.SerializeObject(basePreset));
                p.ID = nextId++;
                string nameKey = "FREEMANSLOP_CAMPAIGN_" + p.ID;
                AddText(nameKey, c.Name);
                p.QuestKey = nameKey;
                p.Desc = c.Description;
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
                presets[p.ID] = p;
                active[p.ID] = c;
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
            pending = null;
            if (runner != null)
            {
                UnityEngine.Object.Destroy(runner);
            }
            runner = null;
        }

        // ---------------------------------------------------------------- new game with an exported background

        // Postfix for MapAgent.Initiate (new game setup): uniforms + faction funds, which presets can't express;
        // cities beyond the first and the political map wait until the world is up (DevCampaignRunner).
        internal static void AfterNewGameSetup()
        {
            if (UICampaign.code == null)
            {
                return;
            }
            DevCampaign c;
            if (!active.TryGetValue(UICampaign.code.PlayerPresetID, out c) || (Mainframe.code != null && Mainframe.code.IsFromFile()))
            {
                return;
            }
            MapAgent.code._Army.faction.Money = c.FactionMoney;
            NPCGen me = MapAgent.code.gen;
            if (c.Player != null)
            {
                c.Player.ApplyTo(me);
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
            var troopsByType = new Dictionary<int, Queue<Uniform>>();
            foreach (Uniform u in c.Troops)
            {
                if (!troopsByType.ContainsKey(u.Id))
                {
                    troopsByType[u.Id] = new Queue<Uniform>();
                }
                troopsByType[u.Id].Enqueue(u);
            }
            foreach (NPCGen gen in DevActions.PartyUnits())
            {
                HeroAgent hero = gen.GetComponent<HeroAgent>();
                if (hero != null)
                {
                    foreach (Uniform u in c.Companions)
                    {
                        if (u.Id == hero.HeroId)
                        {
                            u.ApplyTo(gen);
                        }
                    }
                }
                else
                {
                    Queue<Uniform> q;
                    if (troopsByType.TryGetValue(gen.Id, out q) && q.Count > 0)
                    {
                        q.Dequeue().ApplyTo(gen);
                    }
                }
            }
            Plugin.Log.LogInfo("Campaign \"" + c.Name + "\": loadouts, bag and faction funds applied");
            pending = c;
            if (runner == null)
            {
                runner = new GameObject("FreemanSlop_CampaignRunner");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<DevCampaignRunner>();
            }
        }

        // Once the new world is up: the extra cities and the political map.
        internal static void ApplyWorld(DevCampaign c)
        {
            Faction me = MapAgent.code._Army.faction;
            int extraCities = 0;
            for (int i = 1; i < c.Cities.Count; i++) // [0] was handed out by the game from the preset
            {
                City city = Global.code.GetCityById(c.Cities[i]);
                if (city == null || city._Army.faction == me)
                {
                    continue;
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
                extraCities++;
            }
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
            DevActions.Report("Campaign \"" + c.Name + "\": " + (c.Cities.Count > 0 ? 1 + extraCities : 0) + " cities, " +
                factionCount + " factions' relations restored");
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
    }

    public class FactionState
    {
        public string Name;
        public int Relationship;
        public int Status; // RelationValue
        public bool Trading;
        public bool Defense;
        public List<string> Enemies = new List<string>();
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

        internal static Uniform Of(NPCGen g)
        {
            var u = new Uniform();
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

        [HarmonyPostfix, HarmonyPatch(typeof(MapAgent), nameof(MapAgent.Initiate))]
        private static void NewGameSetUp()
        {
            DevCampaigns.AfterNewGameSetup();
        }
    }
}
