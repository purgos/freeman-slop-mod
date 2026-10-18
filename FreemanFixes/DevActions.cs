using UnityEngine;

namespace FreemanFixes
{
    // What the dev menu's buttons do. Uses the game's own fields/methods so its UI and saves stay consistent.
    internal static class DevActions
    {
        // Every unit in your army except you: squad members (Squad.OriginalUnits, like the game's own morale code)
        // plus the unassigned pool, where new recruits and companions can sit. Dead units (tag "D") skipped.
        internal static System.Collections.Generic.List<NPCGen> PartyUnits()
        {
            var result = new System.Collections.Generic.List<NPCGen>();
            NPCGen me = MapAgent.code.gen;
            Army army = MapAgent.code._Army;
            foreach (Transform squadT in army.squads.items)
            {
                Squad squad = squadT != null ? squadT.GetComponent<Squad>() : null;
                if (squad == null || squad.OriginalUnits == null)
                {
                    continue;
                }
                foreach (Transform unit in squad.OriginalUnits)
                {
                    AddUnit(result, unit, me);
                }
            }
            foreach (Transform unit in army.unassignedUnits.items)
            {
                AddUnit(result, unit, me);
            }
            return result;
        }

        private static void AddUnit(System.Collections.Generic.List<NPCGen> list, Transform unit, NPCGen me)
        {
            NPCGen gen = unit != null && unit.tag != "D" ? unit.GetComponent<NPCGen>() : null;
            if (gen != null && gen != me && !list.Contains(gen))
            {
                list.Add(gen);
            }
        }

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

        internal static void SetMorale(int value)
        {
            value = Mathf.Clamp(value, 0, 100);
            MapAgent.code.gen.morale = value;
            foreach (NPCGen gen in PartyUnits())
            {
                gen.morale = value;
            }
            foreach (Transform squadT in MapAgent.code._Army.squads.items)
            {
                Squad squad = squadT != null ? squadT.GetComponent<Squad>() : null;
                if (squad != null && squad.OriginalUnits != null)
                {
                    squad.GetMorale(); // recompute the squad's cached morale + icon colour
                }
            }
            Done("Morale set to " + value);
        }

        internal static void HealAll()
        {
            NPCGen me = MapAgent.code.gen;
            me.curHealth = me.maxHealth;
            int healed = 1;
            foreach (NPCGen gen in PartyUnits())
            {
                gen.curHealth = gen.maxHealth;
                healed++;
            }
            Done("Healed " + healed + " (you + troops)");
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

        // ---- your status and agreements with a faction (set directly: exact, no cascade to third parties) ----

        internal static readonly RelationValue[] StatusCycle =
        {
            RelationValue.Hostile, RelationValue.Unfriendly, RelationValue.Neutral, RelationValue.Friendly, RelationValue.Ally
        };

        // A relation number that matches each status (Faction.ChangeFactionRelation bands: -20..20 neutral, >=20
        // friendly), so the game doesn't immediately re-derive a different status from the number.
        internal static int RelationFor(RelationValue v)
        {
            switch (v)
            {
                case RelationValue.Hostile: return -50;
                case RelationValue.Unfriendly: return -30;
                case RelationValue.Friendly: return 30;
                case RelationValue.Ally: return 80;
                default: return 0;
            }
        }

        internal static Faction PlayerFaction
        {
            get { return MapAgent.code._Army.faction; }
        }

        internal static void SetStatus(Faction f, RelationValue v)
        {
            if (f == null || f == PlayerFaction)
            {
                return;
            }
            f.relationValue = v;
            f.relationship = RelationFor(v);
            if (v == RelationValue.Hostile)
            {
                f.AddEnemyFaction(PlayerFaction); // at war with you
            }
            else
            {
                f.RemoveEnemyFaction(PlayerFaction);
            }
            f.TreatyRefresh(); // drops trade/defense if the new relation is too low for them, like the game does
            Done(f.Name + ": " + v);
        }

        internal static void CycleStatus(Faction f)
        {
            int i = System.Array.IndexOf(StatusCycle, f.relationValue);
            SetStatus(f, StatusCycle[(i + 1) % StatusCycle.Length]);
        }

        // Trade needs relation >= 20 and mutual defense >= 40, or Faction.TreatyRefresh cancels them.
        internal static void ToggleTrade(Faction f)
        {
            f.isTrading = !f.isTrading;
            if (f.isTrading && f.relationship < 20)
            {
                f.relationship = 20;
            }
            Done(f.Name + ": trade agreement " + (f.isTrading ? "on" : "off"));
        }

        internal static void ToggleDefense(Faction f)
        {
            f.isAttackAneDefend = !f.isAttackAneDefend;
            if (f.isAttackAneDefend && f.relationship < 40)
            {
                f.relationship = 40;
            }
            Done(f.Name + ": mutual defense " + (f.isAttackAneDefend ? "on" : "off"));
        }

        // War between two factions is each one's enemy list, kept symmetric by the game's Add/RemoveEnemyFaction.
        internal static void SetWar(Faction a, Faction b, bool war)
        {
            if (a == null || b == null || a == b)
            {
                return;
            }
            if (war)
            {
                a.AddEnemyFaction(b);
            }
            else
            {
                a.RemoveEnemyFaction(b);
            }
            Done(a.Name + (war ? " now at war with " : " at peace with ") + b.Name);
        }

        // A city changes hands the way UICheat.AddAllCity does it (refresh its garrison flag, drop its governor), but
        // through Army.SetFaction like the story setup (Mainframe.LoadFaction), so its squads and villages follow.
        internal static void SetCityOwner(City city, Faction to)
        {
            if (city == null || city._Army == null || to == null || city._Army.faction == to)
            {
                return;
            }
            GiveCity(city, to);
            Faction me = MapAgent.code._Army.faction;
            if (to == me && me.GetAllCity().Count == 1) // UICheat.AddRecentlyCity: your first city
            {
                OfferIndependence();
            }
            Done(city.Name + " now belongs to " + to.Name);
        }

        internal static void TakeAllCities()
        {
            Faction me = MapAgent.code._Army.faction;
            int taken = 0;
            foreach (Transform t in Global.code.locations.items)
            {
                City city = t != null ? t.GetComponent<City>() : null;
                if (city != null && city._Army != null && city._Army.faction != me)
                {
                    GiveCity(city, me);
                    taken++;
                }
            }
            if (me.GetAllCity().Count > 0) // UICheat.AddAllCity
            {
                OfferIndependence();
            }
            Done(taken + " cities taken");
        }

        // Factions the game would wipe out after this: Faction.EndHour destroys one with no locations, camps or
        // heroes left (never Player or REBEL; id 999 goes by map armies instead).
        internal static System.Collections.Generic.List<Faction> DestroyedBy(City city, Faction to)
        {
            var result = new System.Collections.Generic.List<Faction>();
            Faction from = city != null && city._Army != null ? city._Army.faction : null;
            if (from != null && from != to && WouldBeDestroyed(from, 1))
            {
                result.Add(from);
            }
            return result;
        }

        internal static System.Collections.Generic.List<Faction> DestroyedByTakeAll()
        {
            Faction me = MapAgent.code._Army.faction;
            var lost = new System.Collections.Generic.Dictionary<Faction, int>();
            foreach (Transform t in Global.code.locations.items)
            {
                City city = t != null ? t.GetComponent<City>() : null;
                Faction f = city != null && city._Army != null ? city._Army.faction : null;
                if (f != null && f != me)
                {
                    int n;
                    lost.TryGetValue(f, out n);
                    lost[f] = n + 1;
                }
            }
            var result = new System.Collections.Generic.List<Faction>();
            foreach (System.Collections.Generic.KeyValuePair<Faction, int> kv in lost)
            {
                if (WouldBeDestroyed(kv.Key, kv.Value))
                {
                    result.Add(kv.Key);
                }
            }
            return result;
        }

        private static bool WouldBeDestroyed(Faction f, int losing)
        {
            if (f.isDestroyed || f.name == "Player" || f.name == "REBEL" || f.id == 999)
            {
                return false;
            }
            return f.GetAllCityAndCampCount().Count - losing <= 0 && f.GetAllHero().Count <= 0;
        }

        private static void GiveCity(City city, Faction to)
        {
            city.IsRefreshSolide = true;
            city.ClearCityManager();
            city._Army.SetFaction(to);
        }

        // UICheat's follow-up: while your faction is still unnamed ("ME"), owning a city offers to found it.
        private static void OfferIndependence()
        {
            Faction me = MapAgent.code._Army.faction;
            if (me.Name != Ex.Localization.Format("ME", new object[0]) || UINotice.code == null || UICampaign.code == null)
            {
                return;
            }
            UINotice.code.ShowNotice("DIPLOMACY_DULI");
            UINotice.code.ok.onClick.AddListener(UICampaign.code.ShowFactionPanel);
            DevMenu.ShowStatus("Close the dev menu to name your new faction", true);
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
            DevMenu.ShowStatus(message, false);
            Plugin.Log.LogInfo("Dev: " + message);
        }
    }
}
