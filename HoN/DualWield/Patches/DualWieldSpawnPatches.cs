using DualWield.Core;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace DualWield.Patches
{
    /// <summary>
    /// Registers dual wielders at spawn and triggers off-hand pairing.
    ///
    /// Guard: Only activates when DualWieldMissionBehavior is present in the mission.
    /// This prevents firing on CharacterTableau preview agents (inventory screen)
    /// which also go through Mission.SpawnAgent but don't have our behavior registered.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "SpawnAgent")]
    public static class DualWieldSpawnPatches
    {
        [HarmonyPostfix]
        public static void Postfix(Agent __result, AgentBuildData agentBuildData)
        {
            try
            {
                if (!DualWieldMissionBehavior.IsActive) return;
                if (__result == null || !__result.IsHuman) return;
                if (Mission.Current == null) return;
                if (!DualWieldSettings.Get().EnableDualWield) return;

                var behavior = Mission.Current.GetMissionBehavior<DualWieldMissionBehavior>();
                if (behavior == null) return;

                // Check mission equipment on the spawned agent (postfix = agent exists)
                if (DualWieldDetector.IsDualWieldEquipped(__result.Equipment))
                {
                    DualWieldStateManager.Register(__result);
                    // ForceOffHandPairing handles both HUD state AND bone attachment
                    DualWieldWieldingPatches.ForceOffHandPairing(__result, instant: true, onSpawn: true);
                    DualWieldSettings.DebugLog($"Agent {__result.Name} registered as dual wielder");
                }
            }
            catch (System.Exception ex)
            {
                DualWieldSettings.DebugLog($"SpawnAgent patch error: {ex.Message}");
            }
        }
    }
}
