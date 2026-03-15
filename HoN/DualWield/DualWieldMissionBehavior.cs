using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DualWield.Core;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DualWield
{
    /// <summary>
    /// v7.39: Auto left-hand follow-up after right-hand attacks.
    ///
    /// MECHANISM: Detects right-hand release animations on ch0 via GetName(),
    /// then after a short delay fires a complementary left_stance action on ch0.
    /// Only thrust and slashleft _left_stance actually animate the left hand.
    ///
    /// DIRECTION MAPPING:
    ///   RH Thrust     → LH SlashLeft  (stab → sweep combo)
    ///   RH Overswing  → LH Thrust     (overhead → upstab combo)
    ///   RH SlashRight → LH SlashLeft  (mirror sweep combo)
    ///   RH SlashLeft  → LH Thrust     (sweep → stab combo)
    ///
    /// ANTI-REPETITION: If direction mapping gives same type as last LH action,
    /// flips to the other type (engine rejects same action type twice on ch0).
    /// Quick/full release also alternates for visual variety.
    /// </summary>
    public class DualWieldMissionBehavior : MissionBehavior
    {
        /// <summary>
        /// True only when a real combat mission has this behavior registered.
        /// All Harmony patches check this FIRST to avoid firing during preview/tableau missions.
        /// </summary>
        public static bool IsActive { get; private set; }

        private readonly HashSet<int> _agentsWithAttachment = new HashSet<int>();
        private readonly Dictionary<int, EquipmentIndex> _offHandSlots = new Dictionary<int, EquipmentIndex>();

        private bool _indicatorShown;

        #region Follow-Up State

        private enum AttackDir { None, Thrust, Overswing, SlashRight, SlashLeft }

        private bool _followUpQueued;
        private int _followUpDelay;
        private int _followUpCooldown;
        private AttackDir _detectedDir;
        private bool _lastLeftWasThrust;   // alternation tracker
        private int _followUpCount;
        private ActionIndexCache _lastDetectedAction; // track ch0 action changes
        // v7.43c: Track ALL channels to find where LMB animations actually play
        private readonly ActionIndexCache[] _lastChannelActions = new ActionIndexCache[4];

        // Timing constants (ticks, ~60/sec)
        private const int FOLLOW_UP_DELAY = 18;     // ~0.3s after RH release → fire LH
        private const int FOLLOW_UP_COOLDOWN = 45;  // ~0.75s after LH fires → let RH recover cleanly

        #endregion

        #region Left-Hand Follow-Up Caches (fist_left_stance on CH1 — v7.16 proven)

        // v7.42c: Using fist_left_stance actions on ch1 (overlay), NOT 1h_left_stance on ch0.
        // fist_left_stance on ch1 was confirmed working in v7.16 (X-key test).
        // Direction mapping: thrust→uppercut, overswing→direct, slashR→swingR, slashL→swingL
        private static readonly ActionIndexCache LH_Uppercut = ActionIndexCache.Create("act_release_uppercut_fist_left_stance");
        private static readonly ActionIndexCache LH_Direct   = ActionIndexCache.Create("act_release_direct_fist_left_stance");
        private static readonly ActionIndexCache LH_SwingR   = ActionIndexCache.Create("act_release_swingright_fist_left_stance");
        private static readonly ActionIndexCache LH_SwingL   = ActionIndexCache.Create("act_release_swingleft_fist_left_stance");

        #endregion

        // IK state (legacy, kept for cleanup)
        private bool _ikActive;

        // Diagnostic
        private bool _tickLoggedOnce;
        private int _tickCount;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        #region Offhand Flag Injection

        // Tracks original ItemFlags per ItemObject so we can restore them.
        // ForceAttachOffHandPrimaryItemBone tells the engine to orient the weapon
        // for left-hand grip. Without it, swords render with right-hand rotation.
        private static readonly Dictionary<ItemObject, ItemFlags> _originalItemFlags = new Dictionary<ItemObject, ItemFlags>();
        private static PropertyInfo _itemFlagsProp;

        private static void InjectOffHandFlags(ItemObject item)
        {
            if (item == null) return;

            // Already has offhand flag → nothing to do (e.g., our custom dw_items)
            if ((item.ItemFlags & ItemFlags.ForceAttachOffHandPrimaryItemBone) != 0) return;

            // Cache reflection accessor
            if (_itemFlagsProp == null)
                _itemFlagsProp = typeof(ItemObject).GetProperty("ItemFlags", BindingFlags.Public | BindingFlags.Instance);

            if (_itemFlagsProp == null)
            {
                DualWieldLog.Log("[FlagInject] ItemFlags property not found via reflection");
                return;
            }

            // Save original and inject offhand flags
            if (!_originalItemFlags.ContainsKey(item))
                _originalItemFlags[item] = item.ItemFlags;

            var newFlags = item.ItemFlags
                | ItemFlags.ForceAttachOffHandPrimaryItemBone
                | ItemFlags.HeldInOffHand;
            _itemFlagsProp.SetValue(item, newFlags);

            DualWieldLog.Log($"[FlagInject] Set offhand flags on '{item.StringId}': {item.ItemFlags}");
        }

        private static void RestoreItemFlags(ItemObject item)
        {
            if (item == null) return;
            if (!_originalItemFlags.TryGetValue(item, out var original)) return;

            _itemFlagsProp?.SetValue(item, original);
            _originalItemFlags.Remove(item);
            DualWieldLog.Log($"[FlagInject] Restored flags on '{item.StringId}': {original}");
        }

        private static void RestoreAllItemFlags()
        {
            if (_itemFlagsProp == null || _originalItemFlags.Count == 0) return;
            foreach (var kvp in _originalItemFlags)
            {
                try { _itemFlagsProp.SetValue(kvp.Key, kvp.Value); }
                catch { /* ignore cleanup errors */ }
            }
            _originalItemFlags.Clear();
        }

        #endregion

        public void AttachOffHandWeapon(Agent agent, EquipmentIndex offHandSlot)
        {
            if (agent == null || !agent.IsActive()) return;
            RemoveOffHandAttachment(agent);

            if (!DualWieldStateManager.IsDualWielding(agent))
            {
                DualWieldStateManager.Register(agent);
                DualWieldLog.Log($"AttachOffHandWeapon: Registered agent {agent.Name} in StateManager");
            }

            var weapon = agent.Equipment[offHandSlot];
            if (weapon.IsEmpty) return;

            // v7.47: Inject ForceAttachOffHandPrimaryItemBone flag on vanilla weapons
            // so the engine orients the weapon for left-hand grip.
            InjectOffHandFlags(weapon.Item);

            try
            {
                int mainUsageIndex = 0;
                var mainIdx = agent.GetPrimaryWieldedItemIndex();
                if (mainIdx != EquipmentIndex.None)
                {
                    var mainWpn = agent.Equipment[mainIdx];
                    if (!mainWpn.IsEmpty) mainUsageIndex = mainWpn.CurrentUsageIndex;
                }
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, offHandSlot, true, false, mainUsageIndex);
                DualWieldLog.Log($"SetWieldedItemIndexAsClient: offhand slot {(int)offHandSlot} for {agent.Name}");
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"SetWieldedItemIndexAsClient failed: {ex.Message}");
            }

            _agentsWithAttachment.Add(agent.Index);
            _offHandSlots[agent.Index] = offHandSlot;
        }

        public void RemoveOffHandAttachment(Agent agent)
        {
            if (agent == null) return;
            if (!_agentsWithAttachment.Contains(agent.Index)) return;

            // Restore original item flags before unwielding
            if (_offHandSlots.TryGetValue(agent.Index, out var slot) && agent.IsActive())
            {
                var weapon = agent.Equipment[slot];
                if (!weapon.IsEmpty) RestoreItemFlags(weapon.Item);
            }

            if (agent.IsActive())
            {
                try
                {
                    agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, EquipmentIndex.None, true, false, 0);
                }
                catch (Exception ex)
                {
                    DualWieldLog.Log($"Unwield offhand failed: {ex.Message}");
                }
            }

            _agentsWithAttachment.Remove(agent.Index);
            _offHandSlots.Remove(agent.Index);
            Patches.DualWieldAnimationPatches.ClearAgentState(agent.Index);
            DualWieldLog.Log($"OffHand removed for agent {agent.Name}");
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!DualWieldSettings.Get().EnableDualWield) return;

            if (!_tickLoggedOnce)
            {
                _tickLoggedOnce = true;
                IsActive = true;
                DualWieldLog.Log("OnMissionTick: v7.47 — native scroll cycling, no toggle key");
                DualWieldLog.Log($"  LH_Uppercut idx={LH_Uppercut.Index}, LH_Direct idx={LH_Direct.Index}");
                DualWieldLog.Log($"  LH_SwingR idx={LH_SwingR.Index}, LH_SwingL idx={LH_SwingL.Index}");
                Patches.ForceLeftStancePatch.ForceLeftStance = false;
            }

            _tickCount++;

            if (_agentsWithAttachment.Count == 0) return;

            var agent = Agent.Main;

            // Process attack system for player
            if (agent != null && agent.IsActive() && _agentsWithAttachment.Contains(agent.Index))
            {
                if (_ikActive) { agent.ClearHandInverseKinematics(); _ikActive = false; }

                int mode = DualWieldSettings.Get().AttackMode;
                if (mode == 1)
                    ProcessAlternatingMode(agent);
                else
                    ProcessSeparatedMode(agent);

                ProcessManualTest(agent); // V key = manual LH attack (diagnostic)
            }

            // DW indicator — delayed to avoid firing during loading
            if (_agentsWithAttachment.Count > 0 && !_indicatorShown && _tickCount > 120)
            {
                _indicatorShown = true;
                InformationManager.DisplayMessage(
                    new InformationMessage(">>> DUAL WIELD aktiv <<<", Colors.Green));
            }

            // Heartbeat
            if (_tickCount % 300 == 0)
            {
                DualWieldLog.Log($"[Tick] heartbeat #{_tickCount}, tracking {_agentsWithAttachment.Count}, followUps={_followUpCount}");
            }

            // Agent cleanup
            foreach (var agentIndex in _agentsWithAttachment.ToArray())
            {
                var a = Mission.Current?.FindAgentWithIndex(agentIndex);
                if (a == null || !a.IsActive())
                {
                    _agentsWithAttachment.Remove(agentIndex);
                    _offHandSlots.Remove(agentIndex);
                    Patches.DualWieldAnimationPatches.ClearAgentState(agentIndex);
                }
            }
        }

        #region Separated Mode (LMB=RH, RMB=LH)

        private bool _rmbWasDown;
        private int _rmbCooldown;
        private int _rmbAttackIdx; // cycle through LH directions

        /// <summary>
        /// v7.45: SEPARATED mode.
        /// LMB = normal right-hand attack (engine handles it).
        /// RMB = left-hand attack via fist_left_stance on ch1.
        /// LMB+RMB together = block (TODO).
        /// </summary>
        private void ProcessSeparatedMode(Agent agent)
        {
            if (_rmbCooldown > 0) { _rmbCooldown--; }

            bool mmbDown = Input.IsKeyDown(InputKey.MiddleMouseButton);

            // MMB pressed (not held from last tick)
            if (mmbDown && !_rmbWasDown && _rmbCooldown <= 0)
            {
                // v7.45b: Only SwingL and Uppercut actually move the LEFT hand.
                // SwingR and Direct still animate the right hand despite left_stance name.
                ActionIndexCache leftAction;
                string label;
                if (_rmbAttackIdx % 2 == 0)
                {
                    leftAction = LH_SwingL; label = "SwingL";
                }
                else
                {
                    leftAction = LH_Uppercut; label = "Uppercut";
                }
                _rmbAttackIdx++;

                Patches.DualWieldAnimationPatches.IsRedirecting = true;
                try
                {
                    bool ok = agent.SetActionChannel(1, in leftAction, true, 0);
                    _followUpCount++;
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"[MMB→LH] {label}: {(ok ? "OK" : "FAIL")}",
                            ok ? Colors.Green : Colors.Red));
                }
                finally
                {
                    Patches.DualWieldAnimationPatches.IsRedirecting = false;
                }
                _rmbCooldown = 30; // ~0.5s between LH attacks
            }
            _rmbWasDown = mmbDown;
        }

        #endregion

        #region Alternating Mode (LMB alternates R/L)

        private int _diagLogCount;
        private bool _nextIsLeft;

        /// <summary>
        /// v7.44: ALTERNATING mode.
        /// 1st LMB = right hand (normal), 2nd LMB = left hand, 3rd = right, etc.
        /// Detects RH release on ch1 → on every OTHER attack, replaces it with LH.
        /// </summary>
        private void ProcessAlternatingMode(Agent agent)
        {
            // Cooldown prevents rapid re-triggering
            if (_followUpCooldown > 0) { _followUpCooldown--; }

            for (int ch = 0; ch < 4; ch++)
            {
                ActionIndexCache chAction;
                try { chAction = agent.GetCurrentAction(ch); }
                catch { continue; }

                if (chAction == _lastChannelActions[ch]) continue;

                // Channel changed — update tracking
                _lastChannelActions[ch] = chAction;
                _diagLogCount++;

                string chName;
                try { chName = chAction.GetName(); }
                catch { chName = $"idx={chAction.Index}"; }

                // Diagnostic display (first 40 changes)
                if (_diagLogCount <= 40)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"[ch{ch}] {chName}",
                            ch == 0 ? Colors.Yellow : Colors.White));
                }

                // Detect RH release attack on any channel
                if (_followUpCooldown <= 0 && chName != null)
                {
                    var dir = ClassifyRightHandAttack(chName);
                    if (dir != AttackDir.None)
                    {
                        // v7.44b: Sync ALL channel trackers to prevent multi-trigger.
                        // ch1/2/3 show the same action — without this, each channel
                        // triggers separately across consecutive ticks.
                        for (int sync = 0; sync < 4; sync++)
                        {
                            try { _lastChannelActions[sync] = agent.GetCurrentAction(sync); }
                            catch { /* ignore */ }
                        }

                        if (_nextIsLeft)
                        {
                            _detectedDir = dir;
                            FireLeftFollowUp(agent);
                            _followUpCooldown = FOLLOW_UP_COOLDOWN;
                            InformationManager.DisplayMessage(
                                new InformationMessage(
                                    $"[LH] {dir} → LEFT!",
                                    Colors.Green));
                        }
                        else
                        {
                            InformationManager.DisplayMessage(
                                new InformationMessage(
                                    $"[RH] {dir} → right",
                                    Colors.Cyan));
                        }
                        _nextIsLeft = !_nextIsLeft;
                        break;
                    }
                }
            }

            // Legacy phase 2 — disabled, kept for cleanup
            if (_followUpQueued)
            {
                _followUpDelay--;
                if (_followUpDelay <= 0)
                {
                    FireLeftFollowUp(agent);
                    _followUpQueued = false;
                    _followUpCooldown = FOLLOW_UP_COOLDOWN;
                }
            }
        }

        /// <summary>
        /// Classify the right-hand attack direction from the action name.
        /// Uses substring matching for robustness across weapon variants.
        /// Returns None for non-release, non-1h, or left_stance actions.
        /// </summary>
        private AttackDir ClassifyRightHandAttack(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return AttackDir.None;

            // Only detect release actions (the actual damaging swing)
            if (!actionName.Contains("release_")) return AttackDir.None;

            // Only 1h weapons
            if (!actionName.Contains("_1h")) return AttackDir.None;

            // Skip our own left_stance follow-ups
            if (actionName.Contains("left_stance")) return AttackDir.None;

            // Skip 2h and polearm
            if (actionName.Contains("_2h") || actionName.Contains("_lance") ||
                actionName.Contains("_staff") || actionName.Contains("_pike"))
                return AttackDir.None;

            if (actionName.Contains("thrust"))     return AttackDir.Thrust;
            if (actionName.Contains("overswing"))  return AttackDir.Overswing;
            if (actionName.Contains("slashright")) return AttackDir.SlashRight;
            if (actionName.Contains("slashleft"))  return AttackDir.SlashLeft;

            return AttackDir.None;
        }

        /// <summary>
        /// Fire the left-hand follow-up action based on detected RH direction.
        /// Applies direction mapping + anti-repetition + quick/full alternation.
        /// </summary>
        private void FireLeftFollowUp(Agent agent)
        {
            if (!agent.IsActive()) return;

            // v7.45b: Only SwingL and Uppercut actually move the LEFT hand.
            // Alternate between them based on RH direction.
            //   RH Thrust/SlashRight → LH SwingL  (horizontal sweep)
            //   RH Overswing/SlashLeft → LH Uppercut (vertical strike)
            ActionIndexCache leftAction;
            switch (_detectedDir)
            {
                case AttackDir.Thrust:     leftAction = LH_SwingL;   break;
                case AttackDir.Overswing:  leftAction = LH_Uppercut; break;
                case AttackDir.SlashRight: leftAction = LH_SwingL;   break;
                case AttackDir.SlashLeft:  leftAction = LH_Uppercut; break;
                default:
                    leftAction = (_followUpCount % 2 == 0) ? LH_Uppercut : LH_SwingL;
                    break;
            }

            // v7.43i: Fire fist_left_stance on CH1 with fast actionSpeed (3x)
            // so the LH animation completes before the engine starts the next
            // RH attack cycle on ch1. Prevents choppy right-hand animations.
            Patches.DualWieldAnimationPatches.IsRedirecting = true;
            try
            {
                bool ok = agent.SetActionChannel(
                    1,                      // channel
                    in leftAction,          // action
                    ignorePriority: true,
                    additionalFlags: 0UL,
                    blendWithNextActionFactor: 0f,
                    actionSpeed: 1.0f,      // normal speed
                    blendInPeriod: 0f,
                    blendOutPeriodToNoAnim: 0.1f);

                _followUpCount++;

                string dirLabel = leftAction.GetName();
                DualWieldLog.Log($"[DW] LH #{_followUpCount}: {dirLabel} on ch1 ← RH {_detectedDir} → {ok}");

                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"[LH-ch1] ← {_detectedDir}: {(ok ? "OK" : "FAIL")}",
                        ok ? Colors.Green : Colors.Red));
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"[DW] LH FOLLOW-UP ERROR: {ex.Message}");
            }
            finally
            {
                Patches.DualWieldAnimationPatches.IsRedirecting = false;
            }
        }

        #endregion

        #region V-Key Manual Test (diagnostic — same as v7.38 test rig)

        private bool _vWasDown;
        private int _vTestIdx;
        private int _vTestCooldown;

        // v7.42c: Test fist_left_stance on CH1 (v7.16 proven mechanism)
        // NOT 1h_left_stance on ch0 (native engine ignores managed left_stance state)
        private static readonly string[] VTestActions = new[]
        {
            "act_release_uppercut_fist_left_stance",    // 0: thrust equivalent
            "act_release_swingleft_fist_left_stance",   // 1: slashleft equivalent
            "act_release_swingright_fist_left_stance",  // 2: slashright equivalent
            "act_release_direct_fist_left_stance",      // 3: overswing equivalent
        };

        /// <summary>
        /// V key = manual LH attack (diagnostic comparison to auto follow-up).
        /// Cycles through the 4 confirmed left-hand actions.
        /// If V works but auto doesn't, the issue is timing/state.
        /// </summary>
        private void ProcessManualTest(Agent agent)
        {
            bool vDown = Input.IsKeyDown(InputKey.V);
            if (vDown && !_vWasDown && _vTestCooldown == 0)
            {
                string actName = VTestActions[_vTestIdx];
                var action = ActionIndexCache.Create(actName);

                DualWieldLog.Log($"[V-TEST] Firing '{actName}' on CH1 idx={action.Index}");

                // v7.43g: Back to ch1 — fist_left_stance only works on ch1
                bool ok = agent.SetActionChannel(1, in action, true, 0);
                _vTestCooldown = 25;

                bool isDW = DualWieldStateManager.IsDualWielding(agent);
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"[V] {_vTestIdx}: {(ok ? "OK" : "FAIL")} LS={agent.GetIsLeftStance()} DW={isDW}",
                        ok ? Colors.Green : Colors.Red));

                _vTestIdx = (_vTestIdx + 1) % VTestActions.Length;
            }
            if (_vTestCooldown > 0) _vTestCooldown--;
            _vWasDown = vDown;
        }

        #endregion


        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent != null)
            {
                _agentsWithAttachment.Remove(affectedAgent.Index);
                _offHandSlots.Remove(affectedAgent.Index);
                Patches.DualWieldAnimationPatches.ClearAgentState(affectedAgent.Index);
            }
            DualWieldStateManager.Unregister(affectedAgent);
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            IsActive = false;
            RestoreAllItemFlags();
            _agentsWithAttachment.Clear();
            _offHandSlots.Clear();
            _indicatorShown = false;
            _ikActive = false;
            _followUpQueued = false;
            _followUpCooldown = 0;
            _followUpCount = 0;
            _lastLeftWasThrust = false;
            _nextIsLeft = false;
            _rmbWasDown = false;
            _rmbCooldown = 0;
            _rmbAttackIdx = 0;
            Patches.DualWieldAnimationPatches.ClearAll();
            Patches.DualWieldWieldingPatches.ClearTrackingState();
            DualWieldStateManager.Clear();
        }

        // v7.46: Rotation presets removed — engine handles offhand orientation natively
        // via hand_shield usage flags. No AttachWeaponToBone = no custom rotation needed.
    }
}
