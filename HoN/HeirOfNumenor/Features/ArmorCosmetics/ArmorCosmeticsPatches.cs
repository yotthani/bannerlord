using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.ArmorCosmetics
{
    /// <summary>
    /// Patches to apply cosmetic overrides to agent visuals.
    /// Stats come from real equipment, visuals from cosmetics.
    /// </summary>
    [HarmonyPatch]
    public static class ArmorCosmeticsPatches
    {
        /// <summary>
        /// Patch agent equipment visuals when spawning.
        /// </summary>
        [HarmonyPatch(typeof(Agent), "OnWieldedItemIndexChange")]
        [HarmonyPostfix]
        public static void OnWieldedItemIndexChange_Postfix(Agent __instance)
        {
            // Future: refresh cosmetics when equipment changes
        }
        
        /// <summary>
        /// Main patch: Override visual equipment on agent spawn.
        /// </summary>
        [HarmonyPatch(typeof(Mission), "SpawnAgent")]
        [HarmonyPostfix]
        public static void SpawnAgent_Postfix(Agent __result)
        {
            try
            {
                if (__result == null) return;
                if (!(Settings.Instance?.EnableArmorCosmetics ?? false)) return;
                
                // Get hero for this agent
                var hero = GetHeroForAgent(__result);
                if (hero == null) return;
                
                // Apply cosmetic overrides to visual mesh
                ApplyCosmeticVisuals(__result, hero);
            }
            catch { }
        }
        
        private static Hero GetHeroForAgent(Agent agent)
        {
            if (agent?.Character == null) return null;
            
            // Check if main hero
            if (agent.IsMainAgent && Hero.MainHero != null)
                return Hero.MainHero;
            
            // Check companions
            if (agent.Character is CharacterObject charObj && charObj.IsHero)
                return charObj.HeroObject;
            
            return null;
        }
        
        private static void ApplyCosmeticVisuals(Agent agent, Hero hero)
        {
            foreach (var slot in ArmorCosmeticsManager.CosmeticSlots)
            {
                // Check visibility
                if (!ArmorCosmeticsManager.IsSlotVisible(hero, slot))
                {
                    // Hide this slot visually
                    HideArmorSlotVisual(agent, slot);
                    continue;
                }
                
                // Check cosmetic override
                var visualItem = ArmorCosmeticsManager.GetVisualItem(hero, slot);
                if (visualItem != null && ArmorCosmeticsManager.HasCosmeticOverride(hero, slot))
                {
                    // Apply cosmetic mesh
                    ApplyArmorVisual(agent, slot, visualItem);
                }
            }
        }
        
        private static void HideArmorSlotVisual(Agent agent, EquipmentIndex slot)
        {
            // This requires deeper engine access - placeholder
            // Real implementation would use agent.AgentVisuals or similar
        }
        
        private static void ApplyArmorVisual(Agent agent, EquipmentIndex slot, ItemObject visualItem)
        {
            // This requires deeper engine access - placeholder
            // Real implementation would swap mesh on agent.AgentVisuals
        }
    }
}
