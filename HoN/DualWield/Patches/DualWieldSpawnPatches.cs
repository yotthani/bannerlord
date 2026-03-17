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

                // v9.0: Diagnostics — check if our custom usage sets are registered in native engine
                if (__result.IsPlayerControlled)
                {
                    var eq = __result.Equipment;
                    string s0 = eq[EquipmentIndex.Weapon0].IsEmpty ? "empty" : eq[EquipmentIndex.Weapon0].Item?.StringId ?? "?";
                    string s1 = eq[EquipmentIndex.Weapon1].IsEmpty ? "empty" : eq[EquipmentIndex.Weapon1].Item?.StringId ?? "?";

                    // Check native usage set indices
                    int offhandIdx = MBItem.GetItemUsageIndex("dw_offhand");
                    int mainhandIdx = MBItem.GetItemUsageIndex("dw_mainhand_swing_thrust");
                    int shieldIdx = MBItem.GetItemUsageIndex("hand_shield");

                    // Check actual offhand weapon usage index
                    string offUsage = "none";
                    int offUsageIdx = -1;
                    for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i <= EquipmentIndex.Weapon3; i++)
                    {
                        if (i == __result.GetPrimaryWieldedItemIndex()) continue;
                        var wpn = eq[i];
                        if (!wpn.IsEmpty && wpn.CurrentUsageItem != null)
                        {
                            offUsage = wpn.CurrentUsageItem.ItemUsage ?? "null";
                            offUsageIdx = string.IsNullOrEmpty(wpn.CurrentUsageItem.ItemUsage)
                                ? -1 : MBItem.GetItemUsageIndex(wpn.CurrentUsageItem.ItemUsage);
                            break;
                        }
                    }

                    DualWieldLog.Log($"[Spawn] PLAYER: slot0={s0}, slot1={s1}, isDW={isDW}");
                    DualWieldLog.Log($"[Spawn] UsageIdx: dw_offhand={offhandIdx}, dw_mainhand={mainhandIdx}, hand_shield={shieldIdx}");
                    DualWieldLog.Log($"[Spawn] Offhand weapon usage: '{offUsage}' idx={offUsageIdx}");

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[DW] UsageIdx: offhand={offhandIdx} main={mainhandIdx} shield={shieldIdx}",
                        offhandIdx >= 0 ? Colors.Green : Colors.Red));
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[DW] OffWpn usage='{offUsage}' idx={offUsageIdx}",
                        offUsageIdx >= 0 ? Colors.Green : Colors.Red));
                }

                if (isDW)
                {
                    DualWieldStateManager.Register(__result);

                    // v9.0: Check if engine already auto-wielded offhand natively.
                    // Items with HeldInOffHand + ForceAttachOffHandPrimaryItemBone flags
                    // should be auto-wielded by the engine (like shields). If so, the
                    // require_left_hand_usage_root_set mechanism handles everything.
                    var offIdx = __result.GetOffhandWieldedItemIndex();
                    if (offIdx != EquipmentIndex.None)
                    {
                        var offWeapon = __result.Equipment[offIdx];
                        string offId = offWeapon.IsEmpty ? "empty" : offWeapon.Item?.StringId ?? "?";
                        DualWieldLog.Log($"[Spawn] {__result.Name}: Engine AUTO-WIELDED offhand slot {(int)offIdx} ({offId}) — skipping ForceOffHandPairing");
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[DW] Engine auto-wielded offhand: {offId} (slot {(int)offIdx})",
                            Colors.Green));
                    }
                    else
                    {
                        // Engine didn't auto-wield → use our manual pairing (vanilla weapons)
                        DualWieldLog.Log($"[Spawn] {__result.Name}: No auto-wield → ForceOffHandPairing");
                        DualWieldWieldingPatches.ForceOffHandPairing(__result, instant: true, onSpawn: true);
                    }

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
