using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.MixedFormations
{
    [HarmonyPatch]
    public static class FormationLayoutPatches
    {
        private static MixedFormationLayoutBehavior _behavior;
        
        public static void SetBehavior(MixedFormationLayoutBehavior behavior)
        {
            _behavior = behavior;
        }
        
        [HarmonyPatch(typeof(Formation), "GetOrderPositionOfUnit")]
        [HarmonyPrefix]
        public static bool GetOrderPositionOfUnit_Prefix(
            Formation __instance,
            Agent unit,
            ref WorldPosition __result)
        {
            try
            {
                if (!(Settings.Instance?.EnableMixedFormationLayouts ?? false))
                    return true;
                
                if (_behavior == null)
                    return true;
                
                var layout = _behavior.GetFormationLayout(__instance);
                if (layout == FormationLayoutType.Vanilla)
                    return true;
                
                // Custom positioning will be calculated
                // For now, let vanilla handle it - full implementation would
                // intercept and modify position based on unit type
            }
            catch { }
            
            return true;
        }
        
        [HarmonyPatch(typeof(Formation), "OnTick")]
        [HarmonyPostfix]
        public static void Formation_OnTick_Postfix(Formation __instance, float dt)
        {
            try
            {
                if (!(Settings.Instance?.EnableMixedFormationLayouts ?? false))
                    return;
                
                if (_behavior == null)
                    return;
                
                var layout = _behavior.GetFormationLayout(__instance);
                if (layout == FormationLayoutType.Vanilla)
                    return;
                
                // Periodic enforcement of layout
                // Full implementation would reorder units based on layout type
            }
            catch { }
        }
    }
}
