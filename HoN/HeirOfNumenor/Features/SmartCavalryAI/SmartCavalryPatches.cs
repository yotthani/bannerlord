using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.SmartCavalryAI
{
    [HarmonyPatch]
    public static class SmartCavalryPatches
    {
        [HarmonyPatch(typeof(Formation), "SetMovementOrder")]
        [HarmonyPostfix]
        public static void SetMovementOrder_Postfix(Formation __instance, MovementOrder value)
        {
            if (!(Settings.Instance?.EnableSmartCavalryAI ?? false)) return;
            
            try
            {
                if (value.OrderType == OrderType.ChargeWithTarget && IsCavalryFormation(__instance))
                {
                    var behavior = Mission.Current?.GetMissionBehavior<SmartCavalryAIBehavior>();
                    if (behavior != null && value.TargetFormation != null)
                    {
                        behavior.InitiateLineCharge(__instance, value.TargetFormation.CurrentPosition.ToVec3());
                    }
                }
            }
            catch { }
        }
        
        private static bool IsCavalryFormation(Formation formation)
        {
            if (formation?.CountOfUnits == 0) return false;
            int mounted = 0;
            foreach (var unit in formation.UnitsWithoutLooseDetachedOnes)
            {
                if ((unit as Agent)?.HasMount == true) mounted++;
            }
            return mounted > formation.CountOfUnits / 2;
        }
    }
}
