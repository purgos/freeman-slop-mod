using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace FreemanFixes
{
    // The game's save leaves out some values the dev menu (and campaign loadouts) can set: for you max health,
    // morale and the extra equipment slots; for companions Leadership, Intelligence, every skill, unspent points,
    // max health and most equipment slots (SaveSoldierData only keeps a few). So whenever the game saves
    // (OnFile.SaveAll - manual saves and autosaves), we write FreemanSlop.json into that save's folder with your
    // and every companion's full stats and loadout, and put them back after that save is loaded (OnFile.LoadAll),
    // once the world is up. Level isn't restored (the game saves it; re-setting it would re-run level-up logic).
    internal static class DevSaveData
    {
        private const string FileName = "FreemanSlop.json";

        internal static SaveExtras pending;
        private static GameObject runner;

        private static string SaveFolder
        {
            get { return Application.persistentDataPath + OnFile.MainPath; }
        }

        // ---------------------------------------------------------------- save

        internal static void Write()
        {
            try
            {
                if (MapAgent.code == null || string.IsNullOrEmpty(OnFile.MainPath))
                {
                    return;
                }
                var x = new SaveExtras();
                x.Player = Capture(MapAgent.code.gen, null);
                if (HeroRecruitCtrl.Instance != null)
                {
                    foreach (HeroAgent h in HeroRecruitCtrl.Instance.heroAgents)
                    {
                        if (h != null && h.gen != null)
                        {
                            CharacterExtras c = Capture(h.gen, h);
                            c.HeroId = h.HeroId;
                            x.Companions.Add(c);
                        }
                    }
                }
                Directory.CreateDirectory(SaveFolder);
                File.WriteAllText(Path.Combine(SaveFolder, FileName), JsonConvert.SerializeObject(x));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Saving extra values failed: " + e);
            }
        }

        private static CharacterExtras Capture(NPCGen g, HeroAgent h)
        {
            var c = new CharacterExtras();
            AddStats(c.Stats, g, h, DevStats.VitalsAndAttributes);
            AddStats(c.Stats, g, h, DevStats.Skills);
            AddStats(c.Stats, g, h, DevStats.Weapons);
            if (h != null)
            {
                AddStats(c.Stats, g, h, DevStats.CompanionStats);
            }
            c.Stats.Remove(DevStats.Level);     // saved by the game
            c.Stats.Remove(DevStats.MaxHealth); // kept as floats below
            c.MaxHealth = g.maxHealth;
            c.CurHealth = g.curHealth;
            c.Loadout = Uniform.Of(g);
            return c;
        }

        private static void AddStats(Dictionary<int, int> stats, NPCGen g, HeroAgent h, int[] ids)
        {
            foreach (int id in ids)
            {
                stats[id] = DevStats.Get(g, h, id);
            }
        }

        // ---------------------------------------------------------------- load

        internal static void Read()
        {
            try
            {
                string file = Path.Combine(SaveFolder, FileName);
                if (!File.Exists(file))
                {
                    return; // saved before this feature, or by the game without the mod
                }
                pending = JsonConvert.DeserializeObject<SaveExtras>(File.ReadAllText(file));
                if (pending != null && runner == null)
                {
                    runner = new GameObject("FreemanSlop_SaveExtrasRunner");
                    UnityEngine.Object.DontDestroyOnLoad(runner);
                    runner.AddComponent<SaveExtrasRunner>();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Reading extra values failed: " + e);
            }
        }

        internal static void Apply(SaveExtras x)
        {
            int restored = 0;
            if (x.Player != null)
            {
                Restore(MapAgent.code.gen, null, x.Player);
                restored++;
            }
            if (HeroRecruitCtrl.Instance != null)
            {
                foreach (CharacterExtras c in x.Companions)
                {
                    foreach (HeroAgent h in HeroRecruitCtrl.Instance.heroAgents)
                    {
                        if (h != null && h.gen != null && h.HeroId == c.HeroId)
                        {
                            Restore(h.gen, h, c);
                            restored++;
                            break;
                        }
                    }
                }
            }
            Plugin.Log.LogInfo("Restored extra saved values for " + restored + " characters");
        }

        private static void Restore(NPCGen g, HeroAgent h, CharacterExtras c)
        {
            if (c.Loadout != null)
            {
                c.Loadout.ApplyTo(g);
            }
            foreach (KeyValuePair<int, int> kv in c.Stats)
            {
                DevStats.Set(g, h, kv.Key, kv.Value);
            }
            g.maxHealth = c.MaxHealth;
            g.curHealth = Mathf.Min(c.CurHealth, c.MaxHealth);
            if (h != null)
            {
                CompanionBases.Remember(g); // the load's SetLevel scaled them from the table; these are theirs
            }
        }

        internal static void Teardown()
        {
            pending = null;
            if (runner != null)
            {
                UnityEngine.Object.Destroy(runner);
            }
            runner = null;
        }
    }

    public class SaveExtras
    {
        public CharacterExtras Player;
        public List<CharacterExtras> Companions = new List<CharacterExtras>();
    }

    public class CharacterExtras
    {
        public int HeroId;
        public Dictionary<int, int> Stats = new Dictionary<int, int>(); // DevStats ids
        public float MaxHealth;
        public float CurHealth;
        public Uniform Loadout;
    }

    // Waits for the loaded world to finish setting up, then restores once.
    internal class SaveExtrasRunner : MonoBehaviour
    {
        private const float SettleSeconds = 3f;
        private float readySince = -1f;

        private void Update()
        {
            if (DevSaveData.pending == null)
            {
                Destroy(gameObject);
                return;
            }
            // TerrainControl.initiated is battle-only; the world map is ready once isWorld holds for a moment.
            bool ready = TerrainControl.code != null && TerrainControl.code.isWorld && MapAgent.code != null
                && MapAgent.code.gen != null && HeroRecruitCtrl.Instance != null;
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
            SaveExtras x = DevSaveData.pending;
            DevSaveData.pending = null;
            try
            {
                DevSaveData.Apply(x);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Restoring extra values failed: " + e);
            }
            Destroy(gameObject);
        }
    }

    [HarmonyPatch]
    internal static class DevSaveDataPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(OnFile), nameof(OnFile.SaveAll))]
        private static void AfterSave()
        {
            DevSaveData.Write();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(OnFile), nameof(OnFile.LoadAll))]
        private static void AfterLoad()
        {
            DevSaveData.Read();
        }
    }
}
