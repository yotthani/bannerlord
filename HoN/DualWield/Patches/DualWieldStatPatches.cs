using DualWield.Core;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.MountAndBlade;

namespace DualWield.Patches
{
    [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), "UpdateAgentStats")]
    public static class DualWieldStatPatches
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

                // Attack speed bonus: increase SwingSpeedMultiplier
                agentDrivenProperties.SwingSpeedMultiplier *= (1f + settings.AttackSpeedBonus);

                // Handling bonus (faster weapon recovery)
                agentDrivenProperties.HandlingMultiplier *= (1f + settings.AttackSpeedBonus * 0.5f);

                // Movement speed bonus
                agentDrivenProperties.MaxSpeedMultiplier *= (1f + settings.MovementSpeedBonus);
                agentDrivenProperties.CombatMaxSpeedMultiplier *= (1f + settings.MovementSpeedBonus);

                DualWieldSettings.DebugLog($"Stats applied to {agent.Name}: SwingSpeed={agentDrivenProperties.SwingSpeedMultiplier:F2}, MaxSpeed={agentDrivenProperties.MaxSpeedMultiplier:F2}");
            }
            catch (System.Exception ex)
            {
                DualWieldSettings.DebugLog($"UpdateAgentStats patch error: {ex.Message}");
            }
        }
    }
}
