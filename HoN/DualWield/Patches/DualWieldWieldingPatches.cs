using System.Collections.Generic;
using DualWield.Core;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DualWield.Patches
{
    /// <summary>
    /// Auto-pairs the off-hand weapon when a dual wielder selects a one-handed weapon.
    ///
    /// v7.47: Native weapon-cycling UX — works like shields.
    /// Swap-detection: when mainhand becomes what was the offhand → enter single mode
    /// (offhand unwielded). Single mode stays active while cycling 1H weapons.
    /// Switching to non-1H (2H, bow, fists) exits single mode → next 1H re-engages DW.
    /// No special toggle key needed — pure mouse-wheel flow.
    /// </summary>
    [HarmonyPatch(typeof(Agent), "OnWieldedItemIndexChange")]
    public static class DualWieldWieldingPatches
    {
        private static bool _isAutoEquipping;

        // Swap-detection: tracks which slot is currently the offhand for each agent.
        // When mainhand switches TO this slot → user scrolled through DW → enter single mode.
        private static readonly Dictionary<int, EquipmentIndex> _trackedOffHand = new Dictionary<int, EquipmentIndex>();

        // Single mode: agent has unwielded offhand via scroll. Stays active until
        // the agent switches to a non-1H weapon (or fists), which resets the state.
        private static readonly HashSet<int> _agentsInSingleMode = new HashSet<int>();

        [HarmonyPostfix]
        public static void Postfix(Agent __instance, bool isOffHand, bool isWieldedInstantly, bool isWieldedOnSpawn)
        {
            try
            {
                if (!DualWieldMissionBehavior.IsActive) return;
                if (_isAutoEquipping) return;

                DualWieldLog.Log($"[Wield] OnWieldedItemIndexChange: agent={__instance?.Name}, isOffHand={isOffHand}, instant={isWieldedInstantly}, onSpawn={isWieldedOnSpawn}");

                if (isOffHand) return;
                if (Mission.Current == null) return;
                if (!DualWieldSettings.Get().EnableDualWield) return;
                if (__instance == null || !__instance.IsHuman) return;

                if (!DualWieldStateManager.IsDualWielding(__instance))
                {
                    DualWieldLog.Log($"[Wield] Agent {__instance.Name}: NOT in StateManager, skipping");
                    return;
                }

                ForceOffHandPairing(__instance, isWieldedInstantly, isWieldedOnSpawn);
            }
            catch (System.Exception ex)
            {
                DualWieldLog.Log($"[Wield] ERROR: {ex}");
                DualWieldSettings.DebugLog($"WieldingPatch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Pairs or unpairs the off-hand weapon based on the current mainhand.
        /// Implements swap-detection for native mouse-wheel unwielding.
        /// </summary>
        public static void ForceOffHandPairing(Agent agent, bool instant = true, bool onSpawn = false)
        {
            if (agent == null) return;
            if (Mission.Current == null) return;

            var behavior = Mission.Current.GetMissionBehavior<DualWieldMissionBehavior>();
            if (behavior == null) return;

            var mainIndex = agent.GetPrimaryWieldedItemIndex();
            DualWieldLog.Log($"[Wield] ForceOffHandPairing: agent={agent.Name}, mainSlot={mainIndex}, onSpawn={onSpawn}");

            // No weapon wielded → exit single mode + remove off-hand
            if (mainIndex == EquipmentIndex.None)
            {
                _agentsInSingleMode.Remove(agent.Index);
                _trackedOffHand.Remove(agent.Index);
                DualWieldLog.Log($"[Wield] No weapon wielded → removing off-hand, exiting single mode");
                behavior.RemoveOffHandAttachment(agent);
                return;
            }

            var mainWeapon = agent.Equipment[mainIndex];
            if (mainWeapon.IsEmpty || !IsOneHandedMeleeWeapon(mainWeapon))
            {
                // Switched to 2-handed, bow, etc. → exit single mode + remove off-hand
                _agentsInSingleMode.Remove(agent.Index);
                _trackedOffHand.Remove(agent.Index);
                DualWieldLog.Log($"[Wield] Main weapon not 1h melee → removing off-hand, exiting single mode");
                behavior.RemoveOffHandAttachment(agent);
                return;
            }

            // --- Player-only: single mode via swap-detection ---
            if (agent.IsPlayerControlled)
            {
                // Already in single mode → stay single (don't re-pair while cycling 1H weapons)
                if (_agentsInSingleMode.Contains(agent.Index))
                {
                    DualWieldLog.Log($"[Wield] Agent in single mode → skipping pairing");
                    behavior.RemoveOffHandAttachment(agent);
                    return;
                }

                // Swap detected: mainhand is now what was the offhand → enter single mode
                if (!onSpawn && _trackedOffHand.TryGetValue(agent.Index, out var prevOff) && prevOff == mainIndex)
                {
                    _agentsInSingleMode.Add(agent.Index);
                    _trackedOffHand.Remove(agent.Index);
                    behavior.RemoveOffHandAttachment(agent);
                    DualWieldLog.Log($"[Wield] Swap detected (main=former offhand) → entering single mode");
                    InformationManager.DisplayMessage(
                        new InformationMessage("[DW] Offhand abgelegt — Waffentyp wechseln zum Reaktivieren", Colors.Yellow));
                    return;
                }
            }

            // --- Normal pairing: find partner 1H weapon ---
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i <= EquipmentIndex.Weapon3; i++)
            {
                if (i == mainIndex) continue;
                var weapon = agent.Equipment[i];
                if (weapon.IsEmpty || !IsOneHandedMeleeWeapon(weapon)) continue;

                DualWieldLog.Log($"[Wield] Found partner: slot {(int)i}, main: slot {(int)mainIndex}");

                _isAutoEquipping = true;
                try
                {
                    behavior.AttachOffHandWeapon(agent, i);
                    _trackedOffHand[agent.Index] = i;
                    DualWieldSettings.DebugLog($"Paired off-hand slot {(int)i} (main: slot {(int)mainIndex})");
                }
                finally
                {
                    _isAutoEquipping = false;
                }
                return;
            }

            // No partner weapon found → clean up
            _trackedOffHand.Remove(agent.Index);
            DualWieldLog.Log($"[Wield] No partner 1h weapon found → removing off-hand");
            behavior.RemoveOffHandAttachment(agent);
        }

        /// <summary>
        /// Clears all tracking state (call on mission end).
        /// </summary>
        public static void ClearTrackingState()
        {
            _trackedOffHand.Clear();
            _agentsInSingleMode.Clear();
        }

        internal static bool IsOneHandedMeleeWeapon(MissionWeapon weapon)
        {
            if (weapon.IsEmpty || weapon.CurrentUsageItem == null) return false;
            if (weapon.CurrentUsageItem.IsShield) return false;
            var wc = weapon.CurrentUsageItem.WeaponClass;
            return wc == WeaponClass.OneHandedSword
                || wc == WeaponClass.Dagger
                || wc == WeaponClass.OneHandedAxe
                || wc == WeaponClass.Mace;
        }
    }
}
