using HarmonyLib;
using UnityEngine;

namespace FreemanFixes
{
    // A city stays "under siege" after you win the siege. When you join an ally's siege, the ally's map army is
    // already in an auto-combat with the city (MapArmy.autoCombatStarter, both sides' Army.AutoCombatTargetArmy).
    // Winning the battle (UIResult.LeaveCombat, "WinInvasion") hands the city to that ally and deletes its garrison,
    // but leaves the auto-combat running. So the city keeps announcing "under siege by ..." every hour (City.EndHour),
    // until the ally's next hourly MapArmy.CheckAndAccountAutoCombat finds no defenders, or until autoCombatLimitTime
    // (12-36 hours) runs out if new soldiers arrived. We settle it as soon as you leave the battle, as that check
    // would: if the city went to the attacker's faction and has no defenders, it's the attacker's "Win"
    // (ResolveAutoCombat: its surviving troops move in as the garrison and a mayor is appointed); otherwise the
    // game's CancelAutoCombat. Any helper army still aimed at the city is released too.
    [HarmonyPatch]
    internal static class SiegeEndPatches
    {
        [HarmonyPrefix, HarmonyPatch(typeof(UIResult), nameof(UIResult.LeaveCombat))]
        private static void LeavingCombat(string ___result, out Transform __state)
        {
            __state = null;
            MapAgent agent = MapAgent.code;
            if (___result == "WinInvasion" && agent != null && agent.curLocation != null
                && agent.curLocation.GetComponent<City>() != null)
            {
                __state = agent.curLocation;
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIResult), nameof(UIResult.LeaveCombat))]
        private static void LeftCombat(Transform __state)
        {
            if (__state == null)
            {
                return;
            }
            try
            {
                EndSiege(__state);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Siege end: " + e);
            }
        }

        private static void EndSiege(Transform city)
        {
            Army cityArmy = city.GetComponent<Army>();
            if (cityArmy == null || Global.code == null)
            {
                return;
            }
            int settled = 0;
            for (int i = Global.code.mapArmies.items.Count - 1; i >= 0; i--)
            {
                Transform t = Global.code.mapArmies.items[i];
                MapArmy mapArmy = t != null ? t.GetComponent<MapArmy>() : null;
                if (mapArmy == null || !mapArmy.autoCombatStarter || mapArmy._Army == null
                    || mapArmy._Army.AutoCombatTargetArmy != city)
                {
                    continue;
                }
                // Win moves this army's own troops into the city (Army.MergeArmy, same faction only); no new ones.
                if (cityArmy.faction == mapArmy._Army.faction && cityArmy.GetCanFightUnitsLeft() <= 0)
                {
                    cityArmy.LeaveAutoCombat();
                    mapArmy.ResolveAutoCombat("Win");
                }
                else
                {
                    mapArmy.CancelAutoCombat();
                }
                settled++;
            }
            for (int i = Global.code.mapArmies.items.Count - 1; i >= 0; i--)
            {
                Transform t = Global.code.mapArmies.items[i];
                Army army = t != null ? t.GetComponent<Army>() : null;
                if (army != null && army.AutoCombatTargetArmy == city)
                {
                    army.AutoCombatTargetArmy = null;
                    MapArmy mapArmy = t.GetComponent<MapArmy>();
                    if (mapArmy != null)
                    {
                        mapArmy.AI_ReFreshState();
                    }
                    settled++;
                }
            }
            if (cityArmy.AutoCombatTargetArmy != null)
            {
                cityArmy.AutoCombatTargetArmy = null;
                settled++;
            }
            if (settled > 0)
            {
                Plugin.Log.LogInfo("Siege end: settled " + settled + " auto-combat link(s) on " + city.name);
            }
        }
    }
}
