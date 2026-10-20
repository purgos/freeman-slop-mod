using HarmonyLib;
using UnityEngine;

namespace FreemanFixes
{
    // Character stats the dev menu can edit, for the player (MapAgent.code.gen) or a companion (HeroAgent + its NPCGen).
    // Ranges follow the game's own caps (attributes /15, skills /5, weapon proficiency /100 as shown on the character
    // screen). Level uses the game's NPCGen.SetLevel so derived stats follow the game's rules.
    internal static class DevStats
    {
        internal const int PlayerMaxLevel = 99;
        internal const int MaxAffection = 255;
        internal const int Level = 1;
        internal const int MaxHealth = 2;
        internal const int Morale = 3;
        internal const int AttributePoints = 4;
        internal const int SkillPoints = 5;
        internal const int WeaponPoints = 6;
        internal const int Constitution = 10;
        internal const int Marksmanship = 11;
        internal const int Agility = 12;
        internal const int Leadership = 13;
        internal const int Intelligence = 14;
        internal const int Commanding = 20;
        internal const int PrisonerManagement = 21;
        internal const int Training = 22;
        internal const int InventoryManagement = 23;
        internal const int Navigation = 24;
        internal const int Looting = 25;
        internal const int Medical = 26;
        internal const int FirstAid = 27;
        internal const int Trading = 28;
        internal const int Persuasion = 29;
        internal const int Stealing = 30;
        internal const int Armor = 40;
        internal const int Pistol = 41;
        internal const int Smg = 42;
        internal const int Shotgun = 43;
        internal const int Rifle = 44;
        internal const int AssaultRifle = 45;
        internal const int MachineGun = 46;
        internal const int Launcher = 47;
        internal const int Throwing = 48;
        internal const int Affection = 60;       // companions only
        internal const int GovLeadership = 61;   // companions only (governor stats)
        internal const int GovNavigation = 62;
        internal const int GovTactics = 63;
        internal const int GovMedical = 64;
        internal const int GovFinancial = 65;
        internal const int GovGoverning = 66;

        internal static readonly int[] VitalsAndAttributes =
        {
            Level, MaxHealth, Morale, AttributePoints, SkillPoints, WeaponPoints,
            Constitution, Marksmanship, Agility, Leadership, Intelligence
        };

        internal static readonly int[] Attributes = { Constitution, Marksmanship, Agility, Leadership, Intelligence };

        internal static readonly int[] Skills =
        {
            Commanding, PrisonerManagement, Training, InventoryManagement, Navigation, Looting, Medical, FirstAid,
            Trading, Persuasion, Stealing
        };

        internal static readonly int[] Weapons =
        {
            Armor, Pistol, Smg, Shotgun, Rifle, AssaultRifle, MachineGun, Launcher, Throwing
        };

        internal static readonly int[] CompanionStats =
        {
            Affection, GovLeadership, GovNavigation, GovTactics, GovMedical, GovFinancial, GovGoverning
        };

        internal static readonly int[] WeaponsAndCompanion =
        {
            Armor, Pistol, Smg, Shotgun, Rifle, AssaultRifle, MachineGun, Launcher, Throwing,
            Affection, GovLeadership, GovNavigation, GovTactics, GovMedical, GovFinancial, GovGoverning
        };

        internal static bool CompanionOnly(int stat)
        {
            return stat >= Affection;
        }

        // Not shown when editing a companion: free points, the attributes other than Marksmanship, and skills.
        // Companions fight on Marksmanship and weapon stats (NPCGen.SetLevel scales those from their soldier row).
        internal static bool PlayerOnly(int stat)
        {
            switch (stat)
            {
                case Level:
                case MaxHealth:
                case Morale:
                case Marksmanship:
                    return false;
            }
            return !CompanionOnly(stat) && (stat < Armor || stat > Throwing);
        }

        // Labels match the character screen's wording.
        internal static string Label(int stat)
        {
            switch (stat)
            {
                case Level: return "Level";
                case MaxHealth: return "Health";
                case Morale: return "Morale";
                case AttributePoints: return "Attribute pts";
                case SkillPoints: return "Skill pts";
                case WeaponPoints: return "Weapon pts";
                case Constitution: return "Constitution";
                case Marksmanship: return "Marksmanship";
                case Agility: return "Agility";
                case Leadership: return "Leadership";
                case Intelligence: return "Intelligence";
                case Commanding: return "Commands";
                case PrisonerManagement: return "Prisoners Mgmt";
                case Training: return "Instructor";
                case InventoryManagement: return "Inventory";
                case Navigation: return "Marching speed";
                case Looting: return "Looting";
                case Medical: return "Medical";
                case FirstAid: return "First Aid";
                case Trading: return "Trading";
                case Persuasion: return "Diplomacy";
                case Stealing: return "Steal";
                case Armor: return "Armor";
                case Pistol: return "Pistol";
                case Smg: return "SMG";
                case Shotgun: return "Shotgun";
                case Rifle: return "Rifle";
                case AssaultRifle: return "Assault Rifle";
                case MachineGun: return "Machine Gun";
                case Launcher: return "Launcher";
                case Throwing: return "Throwing";
                case Affection: return "Affection";
                case GovLeadership: return "Gov: Leadership";
                case GovNavigation: return "Gov: Navigation";
                case GovTactics: return "Gov: Tactics";
                case GovMedical: return "Gov: Medical";
                case GovFinancial: return "Gov: Financial";
                case GovGoverning: return "Gov: Governing";
                default: return "?";
            }
        }

        internal static int Max(int stat)
        {
            if (stat >= Constitution && stat <= Intelligence)
            {
                return 15;
            }
            if (stat >= Commanding && stat <= Stealing)
            {
                return 5;
            }
            if ((stat >= Armor && stat <= Throwing) || stat == Morale)
            {
                return 100;
            }
            if (stat == Level)
            {
                return PlayerMaxLevel; // companions are further clamped to Global.NPCGenMaxLevel by NPCGen.SetLevel
            }
            if (stat >= GovLeadership && stat <= GovGoverning)
            {
                return 10; // companion screen shows these as "x / 10" (HeroPoint)
            }
            return 99999; // includes affection: the game has no ceiling (HeroRecruitCtrl.AddRelationship only floors it at 0)
        }

        // What the Max All buttons set: the cap, except affection, which has none, so it goes to MaxAffection
        // (well past the 150 the last romance line needs, ShowDilogByTalk).
        internal static int MaxAllValue(int stat)
        {
            return stat == Affection ? MaxAffection : Max(stat);
        }

        internal static int Get(NPCGen g, HeroAgent h, int stat)
        {
            switch (stat)
            {
                case Level: return g.Level;
                case MaxHealth: return Mathf.RoundToInt(g.maxHealth);
                case Morale: return g.morale;
                case AttributePoints: return g.attributePoints;
                case SkillPoints: return g.playerPoints;
                case WeaponPoints: return g.weaponPoint;
                case Constitution: return g.constitution;
                case Marksmanship: return Mathf.RoundToInt(g.marksmanship);
                case Agility: return g.agility;
                case Leadership: return g.leadership;
                case Intelligence: return g.intelligence;
                case Commanding: return g.commanding;
                case PrisonerManagement: return g.prisonermanagement;
                case Training: return g.training;
                case InventoryManagement: return g.inventorymanagement;
                case Navigation: return g.navigation;
                case Looting: return g.looting;
                case Medical: return g.medical;
                case FirstAid: return g.firstaid;
                case Trading: return g.trading;
                case Persuasion: return g.persuation;
                case Stealing: return g.stealing;
                case Armor: return g.armorPoint;
                case Pistol: return g.pistolPoint;
                case Smg: return g.SmgPoint;
                case Shotgun: return g.shotGunPoint;
                case Rifle: return g.riflePoint;
                case AssaultRifle: return g.assaultRiflePoint;
                case MachineGun: return g.machineGunPoint;
                case Launcher: return g.launcherPoint;
                case Throwing: return g.throwingPoint;
            }
            if (h == null)
            {
                return 0;
            }
            switch (stat)
            {
                case Affection: return h.Relationship;
                case GovLeadership: return h.Leadership;
                case GovNavigation: return h.Navigation;
                case GovTactics: return h.Tactics;
                case GovMedical: return h.Medical;
                case GovFinancial: return h.Financial;
                case GovGoverning: return h.Governing;
            }
            return 0;
        }

        internal static void Set(NPCGen g, HeroAgent h, int stat, int v)
        {
            v = Mathf.Clamp(v, stat == Level ? 1 : 0, Max(stat));
            switch (stat)
            {
                case Level: SetLevel(g, v); return;
                case MaxHealth: g.maxHealth = v; g.curHealth = v; return; // max health, fully healed
                case Morale: g.morale = v; if (g.squad != null) { g.squad.GetMorale(); } return;
                case AttributePoints: g.attributePoints = v; return;
                case SkillPoints: g.playerPoints = v; return;
                case WeaponPoints: g.weaponPoint = v; return;
                case Constitution: g.constitution = v; return;
                case Marksmanship: g.marksmanship = v; return;
                case Agility: g.agility = v; return;
                case Leadership: g.leadership = v; return;
                case Intelligence: g.intelligence = v; return;
                case Commanding: g.commanding = v; return;
                case PrisonerManagement: g.prisonermanagement = v; return;
                case Training: g.training = v; return;
                case InventoryManagement: g.inventorymanagement = v; return;
                case Navigation: g.navigation = v; return;
                case Looting: g.looting = v; return;
                case Medical: g.medical = v; return;
                case FirstAid: g.firstaid = v; return;
                case Trading: g.trading = v; return;
                case Persuasion: g.persuation = v; return;
                case Stealing: g.stealing = v; return;
                case Armor: g.armorPoint = v; return;
                case Pistol: g.pistolPoint = v; return;
                case Smg: g.SmgPoint = v; return;
                case Shotgun: g.shotGunPoint = v; return;
                case Rifle: g.riflePoint = v; return;
                case AssaultRifle: g.assaultRiflePoint = v; return;
                case MachineGun: g.machineGunPoint = v; return;
                case Launcher: g.launcherPoint = v; return;
                case Throwing: g.throwingPoint = v; return;
            }
            if (h == null)
            {
                return;
            }
            switch (stat)
            {
                case Affection: h.Relationship = v; return;
                case GovLeadership: h.Leadership = v; return;
                case GovNavigation: h.Navigation = v; return;
                case GovTactics: h.Tactics = v; return;
                case GovMedical: h.Medical = v; return;
                case GovFinancial: h.Financial = v; return;
                case GovGoverning: h.Governing = v; return;
            }
        }

        // Player: walk up one level at a time through SetLevel so each level grants its points like a real level-up;
        // going down just sets it. Companions: SetLevel recomputes their table-based stats for the new level.
        private static void SetLevel(NPCGen g, int v)
        {
            if (g.GetComponent<MapAgent>() == null)
            {
                g.SetLevel(v);
                return;
            }
            if (v > g.Level)
            {
                // Grant the in-between levels' rewards like NPCGen.SetLevel's player branch does (attribute point
                // each level, skill point every even level, 8 weapon points), then let SetLevel do the last level so
                // health/hint come from the game - one "level up" message instead of dozens.
                for (int lv = g.Level + 1; lv < v; lv++)
                {
                    g.Level = lv;
                    g.attributePoints++;
                    if (lv % 2 == 0)
                    {
                        g.playerPoints++;
                    }
                    g.weaponPoint += 8;
                }
                g.SetLevel(v);
            }
            else
            {
                g.Level = v;
                g.nextExp = v * 200 + v * v * 600; // same formula as NPCGen.SetLevel
            }
            g.curExp = Mathf.Min(g.curExp, g.nextExp);
        }

        // After maxing a group there's nothing left to spend those points on, so zero that group's unspent pool:
        // 0 attributes -> attributePoints, 1 skills -> playerPoints, 2 weapons -> weaponPoint, 3 companion (governor)
        // stats -> HeroAgent PointValue - ConsumedPoint (HeroAgent.GetPointNum).
        internal static void ClearUnspent(NPCGen g, HeroAgent h, int group)
        {
            switch (group)
            {
                case 0:
                    g.attributePoints = 0;
                    break;
                case 1:
                    g.playerPoints = 0;
                    break;
                case 2:
                    g.weaponPoint = 0;
                    break;
                case 3:
                    if (h != null)
                    {
                        h.ConsumedPoint = h.PointValue;
                    }
                    break;
            }
        }

        // NPCGen.Name is the object name ("MapAgent") for the player; the character screen shows gen.rename.
        internal static string DisplayName(NPCGen g)
        {
            if (g.GetComponent<MapAgent>() != null && !string.IsNullOrEmpty(g.rename))
            {
                return g.rename;
            }
            return g.Name;
        }

        internal static int MaxLevelFor(NPCGen g)
        {
            return g.GetComponent<MapAgent>() != null ? PlayerMaxLevel : Global.NPCGenMaxLevel;
        }

        internal static bool InParty(HeroAgent h)
        {
            NPCGen g = h != null ? h.gen : null;
            return g != null && MapAgent.code != null && g._Army == MapAgent.code._Army;
        }

        // The faction a companion serves (leading an army, governing a city, ...), per Faction.GetAllHero; null if free.
        internal static Faction Owner(HeroAgent h)
        {
            if (h == null || Global.code == null)
            {
                return null;
            }
            foreach (Transform t in Global.code.factions.items)
            {
                Faction f = t != null ? t.GetComponent<Faction>() : null;
                if (f != null && f.GetAllHero().Contains(h.transform))
                {
                    return f;
                }
            }
            return null;
        }

        internal static bool IsPlayerFaction(Faction f)
        {
            return f != null && MapAgent.code != null && f == MapAgent.code._Army.faction;
        }

        internal static bool IsYourPrisoner(HeroAgent h)
        {
            return h != null && MapAgent.code != null && MapAgent.code._Army.prisoners.items.Contains(h.transform);
        }

        // Status shown in the dev menu's character list.
        internal static string Status(HeroAgent h)
        {
            if (InParty(h))
            {
                return "in party";
            }
            if (IsYourPrisoner(h))
            {
                return "your prisoner";
            }
            Faction owner = Owner(h);
            if (IsPlayerFaction(owner))
            {
                return "yours, away";
            }
            if (owner != null)
            {
                return "with " + owner.Name;
            }
            return h.tag == "D" ? "dead" : "not recruited";
        }

        // Recruitable unless already yours (party, away on your behalf, or your prisoner) or dead. Companions serving
        // another faction can be poached, like vanilla "Add All Companions" does.
        internal static bool CanRecruit(HeroAgent h)
        {
            return h != null && h.gen != null && h.tag != "D" && !InParty(h) && !IsYourPrisoner(h)
                && !IsPlayerFaction(Owner(h));
        }

        // Same steps as vanilla UICheat.AddAllHero, for one companion.
        internal static bool Recruit(HeroAgent h)
        {
            if (!CanRecruit(h))
            {
                return false;
            }
            if (TerrainControl.code == null || !TerrainControl.code.isWorld)
            {
                DevStore.Prompt("Recruit companions from the world map");
                return false;
            }
            if (h.mapArmy != null)
            {
                Object.Destroy(h.mapArmy.gameObject);
            }
            if (h._appointArea != null && h._appointArea.GetComponent<City>() != null)
            {
                h._appointArea.GetComponent<City>().ClearCityManager();
            }
            NPCGen g = h.GetComponent<NPCGen>();
            MapAgent.code._Army.AddUnit(g);
            h.IsCanBuy = false;
            // BattleCtrl.BuyHero is an Action<NPCGen>; go through Traverse so this DLL never references System.Action.
            if (BattleCtrl.Instance != null)
            {
                var buyHero = Traverse.Create(BattleCtrl.Instance).Field("BuyHero").GetValue() as System.Delegate;
                if (buyHero != null)
                {
                    buyHero.DynamicInvoke(new object[] { g });
                }
            }
            return true;
        }
    }
}
