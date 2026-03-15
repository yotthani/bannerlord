using DualWield.Core;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.MountAndBlade;

namespace DualWield.Patches
{
    /// <summary>
    /// Adjusts parry/defense stats for dual wielders.
    ///
    /// Since we bypass the native off-hand system (AttachWeaponToBone instead of
    /// SetWieldedItemIndexAsClient), the engine doesn't recognize an off-hand weapon.
    /// OffhandWeaponDefendSpeedMultiplier therefore has no effect.
    ///
    /// Instead, we boost HandlingMultiplier which affects weapon recovery speed
    /// (faster transitions between attack and defense = better parry timing).
    /// This is applied in DualWieldStatPatches.UpdateAgentStats alongside other bonuses.
    /// </summary>
    [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), "InitializeAgentStats")]
    public static class DualWieldBlockPatches
    {
        [HarmonyPostfix]
        public static void Postfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            try
            {
                if (!DualWieldMissionBehavior.IsActive) return;
                if (!DualWieldSettings.Get().EnableDualWield) return;
                if (!DualWieldStateManager.IsDualWielding(agent)) return;

                var settings = DualWieldSettings.Get();

                // Boost handling for faster parry transitions
                // (HandlingMultiplier is also boosted in UpdateAgentStats, this is the initial set)
                float parryBoost = 1f + settings.ParryWindowReduction;
                agentDrivenProperties.HandlingMultiplier *= parryBoost;

                DualWieldSettings.DebugLog($"Initial parry boost for {agent.Name}: Handling x{parryBoost:F2}");
            }
            catch (System.Exception ex)
            {
                DualWieldSettings.DebugLog($"BlockPatch error: {ex.Message}");
            }
        }
    }
}
