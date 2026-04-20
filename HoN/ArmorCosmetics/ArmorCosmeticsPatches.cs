using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ArmorCosmetics
{
    /// <summary>
    /// Patches that apply cosmetic state to spawned agents.
    ///
    /// IMPORTANT: The actual visual mesh swap requires AgentVisuals manipulation
    /// which is not currently implemented (HoN had stub methods marked
    /// "requires deeper engine access - placeholder"). The patches below trigger
    /// the lookup logic, which currently runs as a no-op for the visual side.
    /// State is correctly stored and read; only the visual output is missing.
    /// </summary>
    [HarmonyPatch]
    public static class ArmorCosmeticsPatches
    {
        [HarmonyPatch(typeof(Mission), "SpawnAgent")]
        [HarmonyPostfix]
        public static void SpawnAgent_Postfix(Agent __result)
        {
            try
            {
                if (__result == null) return;
                if (!ArmorCosmeticsSettings.Get().EnableArmorCosmetics) return;

                var hero = GetHeroForAgent(__result);
                if (hero == null) return;

                ApplyCosmeticVisuals(__result, hero);
            }
            catch { }
        }

        private static Hero GetHeroForAgent(Agent agent)
        {
            if (agent?.Character == null) return null;

            if (agent.IsMainAgent && Hero.MainHero != null)
                return Hero.MainHero;

            if (agent.Character is CharacterObject charObj && charObj.IsHero)
                return charObj.HeroObject;

            return null;
        }

        private static void ApplyCosmeticVisuals(Agent agent, Hero hero)
        {
            foreach (var slot in ArmorCosmeticsManager.CosmeticSlots)
            {
                if (!ArmorCosmeticsManager.IsSlotVisible(hero, slot))
                {
                    HideArmorSlotVisual(agent, slot);
                    continue;
                }

                var visualItem = ArmorCosmeticsManager.GetVisualItem(hero, slot);
                if (visualItem != null && ArmorCosmeticsManager.HasCosmeticOverride(hero, slot))
                {
                    ApplyArmorVisual(agent, slot, visualItem);
                }
            }
        }

        // Stubs — see class comment.
        private static void HideArmorSlotVisual(Agent agent, EquipmentIndex slot) { }
        private static void ApplyArmorVisual(Agent agent, EquipmentIndex slot, ItemObject visualItem) { }
    }
}
