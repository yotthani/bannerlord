using DualWield.Core;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DualWield.Patches
{
    /// <summary>
    /// Registers dual wielders at spawn and triggers off-hand pairing.
    ///
    /// v7.50: Also responsible for LAZY INJECTION of DualWieldMissionBehavior.
    /// BL 1.3's OnMissionBehaviorInitialize only fires for StartUp, not combat missions.
    /// So on first combat agent spawn, we add the behavior to the mission here.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "SpawnAgent")]
    public static class DualWieldSpawnPatches
    {
        [HarmonyPostfix]
        public static void Postfix(Mission __instance, Agent __result, AgentBuildData agentBuildData)
        {
            try
            {
                if (__result == null || !__result.IsHuman) return;
                if (__instance == null) return;
                if (!DualWieldSettings.Get().EnableDualWield) return;

                // v7.50: Lazy behavior injection — add DualWieldMissionBehavior
                // to combat missions on first agent spawn. This is the PRIMARY way
                // the behavior gets added, since OnMissionBehaviorInitialize doesn't
                // fire for combat missions in BL 1.3.
                if (!DualWieldMissionBehavior.IsActive)
                {
                    DualWieldSubModule.TryAddBehaviorToMission(__instance);

                    // If behavior was just added, IsActive is now true.
                    // If mission wasn't combat, IsActive is still false → bail out.
                    if (!DualWieldMissionBehavior.IsActive) return;
                }

                var behavior = __instance.GetMissionBehavior<DualWieldMissionBehavior>();
                if (behavior == null) return;

                // Check mission equipment on the spawned agent (postfix = agent exists)
                bool isDW = DualWieldDetector.IsDualWieldEquipped(__result.Equipment);

                // v7.48: Always log player spawn for diagnostics
                if (__result.IsPlayerControlled)
                {
                    var eq = __result.Equipment;
                    string s0 = eq[EquipmentIndex.Weapon0].IsEmpty ? "empty" : eq[EquipmentIndex.Weapon0].Item?.StringId ?? "?";
                    string s1 = eq[EquipmentIndex.Weapon1].IsEmpty ? "empty" : eq[EquipmentIndex.Weapon1].Item?.StringId ?? "?";
                    DualWieldLog.Log($"[Spawn] PLAYER: slot0={s0}, slot1={s1}, isDW={isDW}");
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[DW Spawn] slot0={s0} slot1={s1} → DW={isDW}",
                        isDW ? Colors.Green : Colors.Yellow));
                }

                if (isDW)
                {
                    DualWieldStateManager.Register(__result);
                    DualWieldWieldingPatches.ForceOffHandPairing(__result, instant: true, onSpawn: true);
                    DualWieldLog.Log($"[Spawn] {__result.Name} registered as dual wielder");
                }
            }
            catch (System.Exception ex)
            {
                DualWieldSettings.DebugLog($"SpawnAgent patch error: {ex.Message}");
            }
        }
    }
}
