using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FreemanFixes
{
    // A companion's Marksmanship and weapon stats aren't their own: NPCGen.SetLevel (on every level-up, and for every
    // unit when a save loads, OnFile.cs ~2555) recomputes them from the companion's Table_Soldier row with
    // NPCGen.ResetPercent: ceil(table value * (1 + PercentAdd * (Level - 1))), Marksmanship capped at 15 and weapons
    // at Global.WeaponPointFactor. So values from a campaign pack or the dev menu were gone at the next level-up.
    // A companion whose values differ from the table gets a base of its own instead: the level-1 value the same
    // formula would scale to what they have now, and a SetLevel postfix scales from that base instead of the table.
    // Bases are worked out again from the stats DevSaveData restores after a load, so they need no file of their own.
    // Companions that still match their table get no base and level up exactly as in vanilla.
    internal static class CompanionBases
    {
        private const float Epsilon = 0.001f; // so a base worked back from a value scales back to that value, not +1

        internal static readonly int[] Scaled =
        {
            DevStats.Marksmanship, DevStats.Armor, DevStats.Pistol, DevStats.Smg, DevStats.Shotgun, DevStats.Rifle,
            DevStats.AssaultRifle, DevStats.MachineGun, DevStats.Launcher, DevStats.Throwing
        };

        private static readonly Dictionary<int, Dictionary<int, float>> bases = new Dictionary<int, Dictionary<int, float>>();

        internal static void Clear()
        {
            bases.Clear();
        }

        // Record a companion's current values as their base (or drop it when they match the table again).
        internal static void Remember(NPCGen g)
        {
            HeroAgent h = g != null ? g.GetComponent<HeroAgent>() : null;
            Table_Soldier tab = Table(g);
            if (h == null || tab == null)
            {
                return;
            }
            var own = new Dictionary<int, float>();
            bool differs = false;
            float factor = Factor(tab, g.Level);
            foreach (int stat in Scaled)
            {
                float tableBase = TableValue(tab, stat);
                int current = DevStats.Get(g, h, stat);
                if (current == Scale(stat, tableBase, factor))
                {
                    own[stat] = tableBase;
                }
                else
                {
                    own[stat] = current / factor;
                    differs = true;
                }
            }
            if (differs)
            {
                bases[h.HeroId] = own;
            }
            else
            {
                bases.Remove(h.HeroId);
            }
        }

        // Hot reload starts with no bases, but a running game's companions still carry the values scaled from theirs.
        internal static void RememberAll()
        {
            if (MapAgent.code == null || TerrainControl.code == null || !TerrainControl.code.isWorld)
            {
                return; // no game running (HeroRecruitCtrl.Instance would make an empty one)
            }
            foreach (HeroAgent h in HeroRecruitCtrl.Instance.heroAgents)
            {
                if (h != null && h.gen != null)
                {
                    Remember(h.gen);
                }
            }
        }

        // A campaign pack's values, exported at exportLevel: their base, then scaled to the companion's level now.
        internal static void SetFromExport(NPCGen g, Dictionary<int, int> stats, int exportLevel)
        {
            HeroAgent h = g != null ? g.GetComponent<HeroAgent>() : null;
            Table_Soldier tab = Table(g);
            if (h == null || tab == null || stats == null)
            {
                return;
            }
            float exportFactor = Factor(tab, Mathf.Max(1, exportLevel));
            var own = new Dictionary<int, float>();
            foreach (int stat in Scaled)
            {
                int v;
                own[stat] = stats.TryGetValue(stat, out v) ? v / exportFactor : TableValue(tab, stat);
            }
            bases[h.HeroId] = own;
            Apply(g);
        }

        // SetLevel postfix: scale this companion's base, if it has one, to their level.
        internal static void Apply(NPCGen g)
        {
            HeroAgent h = g.GetComponent<HeroAgent>();
            Dictionary<int, float> own;
            Table_Soldier tab = Table(g);
            if (h == null || tab == null || !bases.TryGetValue(h.HeroId, out own))
            {
                return;
            }
            float factor = Factor(tab, g.Level);
            foreach (KeyValuePair<int, float> kv in own)
            {
                DevStats.Set(g, h, kv.Key, Scale(kv.Key, kv.Value, factor));
            }
        }

        private static Table_Soldier Table(NPCGen g)
        {
            Table_Soldier tab;
            return g != null && g.Id != 0 && g.GetComponent<MapAgent>() == null && RM.code != null
                && RM.code.TabSoldiersData.TryGetValue(g.Id, out tab) ? tab : null;
        }

        private static float Factor(Table_Soldier tab, int level)
        {
            return 1f + tab.PercentAdd * (level - 1);
        }

        // NPCGen.ResetPercent.
        private static int Scale(int stat, float baseValue, float factor)
        {
            int v = (int)System.Math.Ceiling(baseValue * factor - Epsilon);
            if (stat == DevStats.Marksmanship)
            {
                return Mathf.Min(v, 15);
            }
            return (float)v > Global.WeaponPointFactor ? (int)Global.WeaponPointFactor : v;
        }

        private static float TableValue(Table_Soldier tab, int stat)
        {
            switch (stat)
            {
                case DevStats.Marksmanship: return tab.Marksmanship;
                case DevStats.Armor: return tab.ArmorPoint;
                case DevStats.Pistol: return tab.PistolPoint;
                case DevStats.Smg: return tab.SmgPoint;
                case DevStats.Shotgun: return tab.ShotGunPoint;
                case DevStats.Rifle: return tab.RiflePoint;
                case DevStats.AssaultRifle: return tab.AssaultRiflePoint;
                case DevStats.MachineGun: return tab.MachineGunPoint;
                case DevStats.Launcher: return tab.LauncherPoint;
                default: return tab.ThrowingPoint;
            }
        }
    }

    [HarmonyPatch]
    internal static class CompanionBasesPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(NPCGen), nameof(NPCGen.SetLevel))]
        private static void LevelSet(NPCGen __instance)
        {
            CompanionBases.Apply(__instance);
        }

        // Every game (new or loaded) starts without bases; a load's are rebuilt from its restored stats.
        [HarmonyPrefix, HarmonyPatch(typeof(MapAgent), nameof(MapAgent.Initiate))]
        private static void GameStarting()
        {
            CompanionBases.Clear();
        }
    }
}
