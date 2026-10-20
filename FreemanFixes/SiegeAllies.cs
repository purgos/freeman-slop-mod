using HarmonyLib;

namespace FreemanFixes
{
    // Allies sit still in sieges. Every army in a battle gets a Platoon whose plan comes from
    // Platoon.AI_RefreshBattleType (run at Start and every 90 s). It picks the plan from the player's action, not the
    // platoon's side: a siege means "Defend", which suits the city's garrison but is given to the allies attacking
    // with you too, so their squads walk to the nearest flag and hold it. The only way out is the enemy being twice as
    // strong (num2 / num > 2). For an allied platoon in a siege we set the plan to Attack. The game's own attack
    // targeting (AI_RefreshTargetAndDis) already handles sieges: it aims at the city's flags 30% of the time and at
    // enemy units otherwise.
    // At Start the target is picked before the platoon has counted its units (tempCountUnit is first set by CS15),
    // so no target is found for the first 10 s; we count the units here so the attack starts at once.
    [HarmonyPatch]
    internal static class SiegeAlliesPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(Platoon), "AI_RefreshBattleType")]
        private static void BattleTypeRefreshed(Platoon __instance)
        {
            if (!IsSiegeAlly(__instance))
            {
                return;
            }
            __instance.AI_BattleType = BattleType.Attack;
            if (__instance.tempCountUnit <= 0)
            {
                __instance.tempCountUnit = __instance.army.GetCanFightUnitsLeft();
            }
        }

        private static bool IsSiegeAlly(Platoon platoon)
        {
            MapAgent agent = MapAgent.code;
            return platoon != null && platoon.army != null && agent != null && agent.agentAction == MapAgentAction.siege
                && agent.curAllyArmy != null && agent.curAllyArmy.Contains(platoon.army.transform);
        }
    }
}
