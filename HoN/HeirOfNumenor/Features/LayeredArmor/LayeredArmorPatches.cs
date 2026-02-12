using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.LayeredArmor
{
    [HarmonyPatch]
    public static class LayeredArmorPatches
    {
        [HarmonyPatch(typeof(Agent), "GetBaseArmorEffectivenessForBodyPart")]
        [HarmonyPostfix]
        public static void GetBaseArmorEffectivenessForBodyPart_Postfix(
            Agent __instance,
            BoneBodyPartType bodyPart,
            ref float __result)
        {
            try
            {
                if (!(Settings.Instance?.EnableLayeredArmor ?? false))
                    return;
                
                var hero = __instance?.Character as CharacterObject;
                if (hero?.HeroObject == null)
                    return;
                
                var slot = bodyPart switch
                {
                    BoneBodyPartType.Head => EquipmentIndex.Head,
                    BoneBodyPartType.Neck => EquipmentIndex.Head,
                    BoneBodyPartType.Chest => EquipmentIndex.Body,
                    BoneBodyPartType.Abdomen => EquipmentIndex.Body,
                    BoneBodyPartType.ShoulderLeft => EquipmentIndex.Body,
                    BoneBodyPartType.ShoulderRight => EquipmentIndex.Body,
                    BoneBodyPartType.ArmLeft => EquipmentIndex.Gloves,
                    BoneBodyPartType.ArmRight => EquipmentIndex.Gloves,
                    BoneBodyPartType.Legs => EquipmentIndex.Leg,
                    _ => EquipmentIndex.Body
                };
                
                float layerBonus = LayeredArmorManager.CalculateCombinedArmor(hero.HeroObject, slot) - __result;
                if (layerBonus > 0)
                {
                    __result += layerBonus;
                }
            }
            catch { }
        }
    }
}
