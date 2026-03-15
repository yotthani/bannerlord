using System.Reflection;
using DualWield.Core;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace DualWield.Patches
{
    /// <summary>
    /// Modifies melee blow magnitude for dual wielders.
    /// Off-hand strikes deal reduced damage but benefit from speed bonuses.
    /// </summary>
    [HarmonyPatch]
    public static class DualWieldCombatPatches
    {
        static MethodBase TargetMethod()
        {
            // ComputeBlowMagnitudeMelee is private static
            return AccessTools.Method(
                typeof(MissionCombatMechanicsHelper),
                "ComputeBlowMagnitudeMelee");
        }

        [HarmonyPostfix]
        public static void Postfix(
            AttackInformation attackInformation,
            ref float baseMagnitude,
            ref float specialMagnitude)
        {
            try
            {
                if (!DualWieldMissionBehavior.IsActive) return;
                if (!DualWieldSettings.Get().EnableDualWield) return;

                var settings = DualWieldSettings.Get();
                var attacker = attackInformation.AttackerAgent;

                // Attacker is dual wielding: check which hand is striking
                // Hand was decided by DualWieldAnimationPatches.BeginAttack on act_ready_*
                if (attacker != null && DualWieldStateManager.IsDualWielding(attacker))
                {
                    bool isOffHand = DualWieldStateManager.IsCurrentStrikeOffHand(attacker);

                    if (isOffHand)
                    {
                        baseMagnitude *= settings.OffHandDamageMultiplier;
                        specialMagnitude *= settings.OffHandDamageMultiplier;
                        DualWieldSettings.DebugLog($"[Combat] Off-hand strike: magnitude {baseMagnitude:F1}");
                    }
                    else
                    {
                        DualWieldSettings.DebugLog($"[Combat] Main-hand strike: magnitude {baseMagnitude:F1}");
                    }
                }

                // Victim is dual wielding: armor penalty (no shield = more vulnerable)
                var victim = attackInformation.VictimAgent;
                if (victim != null && DualWieldStateManager.IsDualWielding(victim) && settings.ArmorPenalty > 0f)
                {
                    float armorPenaltyMultiplier = 1f + settings.ArmorPenalty;
                    baseMagnitude *= armorPenaltyMultiplier;
                    specialMagnitude *= armorPenaltyMultiplier;
                    DualWieldSettings.DebugLog($"Victim armor penalty: magnitude increased to {baseMagnitude:F1}");
                }
            }
            catch (System.Exception ex)
            {
                DualWieldSettings.DebugLog($"CombatPatch error: {ex.Message}");
            }
        }
    }
}
