using UnityEngine;

namespace FreemanFixes
{
    // What the dev menu's buttons do. Uses the game's own fields/methods so its UI and saves stay consistent.
    internal static class DevActions
    {
        internal static void SetMoney(int value)
        {
            value = Mathf.Max(0, value);
            MapAgent.code.GetComponent<Storage>().credits = value;
            Done("Money set to " + value);
        }

        internal static void SetReputation(int value)
        {
            MapAgent.code.reputation = value;
            Done("Reputation set to " + value);
        }

        // Same unit list the game's own Squad.AddMorale/GetMorale use (OriginalUnits); Squad.units can be null
        // outside battle.
        internal static void SetMorale(int value)
        {
            value = Mathf.Clamp(value, 0, 100);
            MapAgent.code.gen.morale = value;
            foreach (Transform squadT in MapAgent.code._Army.squads.items)
            {
                Squad squad = squadT != null ? squadT.GetComponent<Squad>() : null;
                if (squad == null || squad.OriginalUnits == null)
                {
                    continue;
                }
                foreach (Transform unit in squad.OriginalUnits)
                {
                    NPCGen gen = unit != null ? unit.GetComponent<NPCGen>() : null;
                    if (gen != null)
                    {
                        gen.morale = value;
                    }
                }
                squad.GetMorale(); // recompute the squad's cached morale + icon colour
            }
            Done("Morale set to " + value);
        }

        // Set current XP, then let the game's own AddExp level-up path catch up.
        internal static void SetXp(int value)
        {
            NPCGen gen = MapAgent.code.gen;
            gen.curExp = Mathf.Max(0, value);
            for (int i = 0; i < 200 && gen.curExp > gen.nextExp; i++)
            {
                int before = gen.Level;
                gen.AddExp(0);
                if (gen.Level == before)
                {
                    break;
                }
            }
            Done("XP set to " + gen.curExp + " (level " + gen.Level + ")");
        }

        internal static void SetRelation(Faction f, int value)
        {
            if (f == null || f.name == "Player")
            {
                return;
            }
            value = Mathf.Clamp(value, -100, 100);
            // SetRelationship won't lift a faction out of Hostile on its own (Faction.ChangeFactionRelation).
            if (f.relationValue == RelationValue.Hostile && value > -20)
            {
                f.SetRelationValue(value >= 20 ? RelationValue.Friendly : RelationValue.Neutral);
            }
            f.SetRelationship(value);
            Done(f.Name + " relation set to " + value);
        }

        // Every other faction to Neutral at relation 0 (SetRelationValue also drops them from your enemies/allies).
        internal static void AllNeutral(System.Collections.Generic.List<Faction> factions)
        {
            int changed = 0;
            foreach (Faction f in factions)
            {
                if (f == null || f.name == "Player")
                {
                    continue;
                }
                if (f.relationValue != RelationValue.Neutral)
                {
                    f.SetRelationValue(RelationValue.Neutral);
                }
                f.SetRelationship(0);
                changed++;
            }
            Done(changed + " factions set to Neutral");
        }

        // Every other faction Hostile at -50: SetRelationValue(Hostile) also declares war (adds you to their enemies).
        internal static void AllHostile(System.Collections.Generic.List<Faction> factions)
        {
            int changed = 0;
            foreach (Faction f in factions)
            {
                if (f == null || f.name == "Player")
                {
                    continue;
                }
                f.SetRelationValue(RelationValue.Hostile);
                f.SetRelationship(-50);
                changed++;
            }
            Done(changed + " factions set to Hostile");
        }

        // Every other faction Ally at 80 (SetRelationValue(Ally) drops them from your enemies).
        internal static void AllAllies(System.Collections.Generic.List<Faction> factions)
        {
            int changed = 0;
            foreach (Faction f in factions)
            {
                if (f == null || f.name == "Player")
                {
                    continue;
                }
                f.SetRelationValue(RelationValue.Ally);
                f.SetRelationship(80);
                changed++;
            }
            Done(changed + " factions set to Allies");
        }

        internal static void SetFactionMoney(Faction f, int value)
        {
            if (f == null)
            {
                return;
            }
            f.Money = Mathf.Max(0, value);
            Done(f.Name + " money set to " + f.Money);
        }

        internal static void AddTroops(Transform soldier, int count)
        {
            if (soldier == null)
            {
                return;
            }
            if (TerrainControl.code == null || !TerrainControl.code.isWorld)
            {
                DevStore.Prompt("Add troops from the world map");
                return;
            }
            count = Mathf.Clamp(count, 1, 100);
            for (int i = 0; i < count; i++)
            {
                MapAgent.code._Army.CreateAndAddSoldiers(soldier);
            }
            Done("Added " + count + " x " + soldier.GetComponent<NPCGen>().Name);
        }

        internal static void Report(string message)
        {
            Done(message);
        }

        private static void Done(string message)
        {
            if (UICombat.code != null)
            {
                UICombat.code.AddRollHint(message, Color.green, null);
            }
            Plugin.Log.LogInfo("Dev: " + message);
        }
    }
}
