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
        ///
        /// v7.48: Set in CONSTRUCTOR, not OnMissionTick. Agents spawn BEFORE the first tick,
        /// so SpawnPatch needs IsActive=true during the spawn phase. The SubModule already
        /// filters by MissionMode before creating this behavior, so constructor is safe.
        /// </summary>
        public static bool IsActive { get; private set; }

        public DualWieldMissionBehavior()
        {
            IsActive = true;
        }

        private readonly HashSet<int> _agentsWithAttachment = new HashSet<int>();
        private readonly HashSet<int> _needsRotationFix = new HashSet<int>();
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
        private static FieldInfo _itemFlagsField;
        private static bool _fieldLookupDone;

        private static void InjectOffHandFlags(ItemObject item)
        {
            if (item == null)
            {
                DualWieldLog.Log("[FlagInject] item is NULL — skipping");
                return;
            }

            DualWieldLog.Log($"[FlagInject] Checking '{item.StringId}': flags={item.ItemFlags}");

            // Already has offhand flag → nothing to do (e.g., our custom dw_items)
            if ((item.ItemFlags & ItemFlags.ForceAttachOffHandSecondaryItemBone) != 0
                || (item.ItemFlags & ItemFlags.ForceAttachOffHandPrimaryItemBone) != 0)
            {
                DualWieldLog.Log($"[FlagInject] '{item.StringId}' ALREADY has offhand bone flag — skipping");
                return;
            }

            // v7.48: Use backing field directly — more reliable than PropertyInfo.SetValue.
            // Auto-property backing fields are named <PropertyName>k__BackingField.
            if (!_fieldLookupDone)
            {
                _fieldLookupDone = true;
                _itemFlagsField = typeof(ItemObject).GetField(
                    "<ItemFlags>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (_itemFlagsField == null)
                {
                    // Fallback: try common field name patterns
                    _itemFlagsField = typeof(ItemObject).GetField(
                        "_itemFlags",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                DualWieldLog.Log($"[FlagInject] Backing field: {(_itemFlagsField != null ? _itemFlagsField.Name : "NOT FOUND")}");
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DW] FlagInject field: {(_itemFlagsField != null ? _itemFlagsField.Name : "NOT FOUND")}",
                    _itemFlagsField != null ? Colors.Green : Colors.Red));
            }

            if (_itemFlagsField == null)
            {
                DualWieldLog.Log("[FlagInject] No backing field found — cannot inject flags");
                return;
            }

            // Save original and inject offhand flags
            if (!_originalItemFlags.ContainsKey(item))
                _originalItemFlags[item] = item.ItemFlags;

            var oldFlags = item.ItemFlags;
            var newFlags = oldFlags
                | ItemFlags.ForceAttachOffHandSecondaryItemBone
                | ItemFlags.HeldInOffHand;
            _itemFlagsField.SetValue(item, newFlags);

            // Verify the write actually took effect
            var verify = item.ItemFlags;
            bool success = (verify & ItemFlags.ForceAttachOffHandSecondaryItemBone) != 0;

            DualWieldLog.Log($"[FlagInject] '{item.StringId}': {oldFlags} → {verify} (success={success})");
            InformationManager.DisplayMessage(new InformationMessage(
                $"[DW] FlagInject '{item.StringId}': {(success ? "OK" : "FAIL")} ({oldFlags} → {verify})",
                success ? Colors.Green : Colors.Red));
        }

        private static void RestoreItemFlags(ItemObject item)
        {
            if (item == null) return;
            if (!_originalItemFlags.TryGetValue(item, out var original)) return;

            _itemFlagsField?.SetValue(item, original);
            _originalItemFlags.Remove(item);
            DualWieldLog.Log($"[FlagInject] Restored flags on '{item.StringId}': {original}");
        }

        private static void RestoreAllItemFlags()
        {
            if (_itemFlagsField == null || _originalItemFlags.Count == 0) return;
            foreach (var kvp in _originalItemFlags)
            {
                try { _itemFlagsField.SetValue(kvp.Key, kvp.Value); }
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
            if (weapon.IsEmpty)
            {
                DualWieldLog.Log($"AttachOffHandWeapon: slot {(int)offHandSlot} is EMPTY — aborting");
                return;
            }

            DualWieldLog.Log($"AttachOffHandWeapon: slot {(int)offHandSlot}, item={weapon.Item?.StringId ?? "NULL"}, flags={weapon.Item?.ItemFlags}");

            // v8.1: Back to SetWieldedItemIndexAsClient — renders exactly 1 weapon.
            // AttachWeaponToBone creates a NEW entity → duplicate weapon visual.
            // Rotation issue accepted for now; will solve via custom mesh or render hook.
            try
            {
                int mainUsageIndex = 0;
                var mainIdx = agent.GetPrimaryWieldedItemIndex();
                if (mainIdx != EquipmentIndex.None)
                {
                    var mainWpn = agent.Equipment[mainIdx];
                    if (!mainWpn.IsEmpty) mainUsageIndex = mainWpn.CurrentUsageIndex;
                }

                // v9.0: isWieldedOnSpawn=true so native engine initializes leftHandUsageSetIndex
                agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, offHandSlot, true, true, mainUsageIndex);
                DualWieldLog.Log($"SetWieldedItemIndexAsClient: offhand slot {(int)offHandSlot} for {agent.Name}");
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"SetWieldedItemIndexAsClient failed: {ex.Message}");
            }

            _agentsWithAttachment.Add(agent.Index);
            _offHandSlots[agent.Index] = offHandSlot;
        }

        /// <summary>
        /// Rotates the offhand weapon entity 180° around its forward (grip) axis.
        /// This compensates for right-hand mesh orientation on the left-hand bone.
        /// </summary>
        private void RotateOffHandWeaponEntity(Agent agent, EquipmentIndex offHandSlot)
        {
            try
            {
                var weaponEntity = agent.GetWeaponEntityFromEquipmentSlot(offHandSlot);
                if (weaponEntity == null)
                {
                    DualWieldLog.Log($"[Rotation] Weapon entity NULL for slot {(int)offHandSlot}");
                    _needsRotationFix.Add(agent.Index);
                    return;
                }

                var frame = weaponEntity.GetFrame();
                frame.rotation.RotateAboutForward((float)Math.PI);
                weaponEntity.SetFrame(ref frame);

                _needsRotationFix.Remove(agent.Index);
                DualWieldLog.Log($"[Rotation] Rotated offhand weapon 180° (RotateAboutForward) for {agent.Name}");
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DW] Rotation fix applied for {agent.Name}", Colors.Green));
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"[Rotation] Error: {ex.Message}");
                _needsRotationFix.Add(agent.Index);
            }
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

            // v8.1: Unwield offhand via SetWieldedItemIndexAsClient(None)
            if (agent.IsActive())
            {
                try
                {
                    agent.SetWieldedItemIndexAsClient(Agent.HandIndex.OffHand, EquipmentIndex.None, true, false, 0);
                    DualWieldLog.Log($"[RemoveOH] Unwielded offhand for {agent.Name}");
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
                DualWieldLog.Log("OnMissionTick: v9.0 — Usage set diagnostics");
                Patches.ForceLeftStancePatch.ForceLeftStance = false;

                // v9.0: Usage set registration diagnostics (delayed to first tick so agent is ready)
                try
                {
                    int dwOffIdx = MBItem.GetItemUsageIndex("dw_offhand");
                    int dwMainIdx = MBItem.GetItemUsageIndex("dw_mainhand_swing_thrust");
                    int shieldIdx = MBItem.GetItemUsageIndex("hand_shield");
                    int vanillaSwingThrust = MBItem.GetItemUsageIndex("onehanded_swing_thrust");

                    DualWieldLog.Log($"[UsageIdx] dw_offhand={dwOffIdx}, dw_mainhand_swing_thrust={dwMainIdx}");
                    DualWieldLog.Log($"[UsageIdx] hand_shield={shieldIdx}, onehanded_swing_thrust={vanillaSwingThrust}");

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[DW] UsageIdx: dw_off={dwOffIdx} dw_main={dwMainIdx} shield={shieldIdx}",
                        dwOffIdx >= 0 ? Colors.Green : Colors.Red));

                    // Also log player's actual weapon usages
                    var player = Agent.Main;
                    if (player != null && player.IsActive())
                    {
                        var mainIdx = player.GetPrimaryWieldedItemIndex();
                        var offIdx = player.GetOffhandWieldedItemIndex();
                        string mainUsage = "none";
                        string offUsage = "none";
                        int mainUsageNativeIdx = -1;
                        int offUsageNativeIdx = -1;

                        if (mainIdx != EquipmentIndex.None)
                        {
                            var w = player.Equipment[mainIdx];
                            if (!w.IsEmpty && w.CurrentUsageItem != null)
                            {
                                mainUsage = w.CurrentUsageItem.ItemUsage ?? "null";
                                mainUsageNativeIdx = string.IsNullOrEmpty(mainUsage) ? -1 : MBItem.GetItemUsageIndex(mainUsage);
                            }
                        }
                        if (offIdx != EquipmentIndex.None)
                        {
                            var w = player.Equipment[offIdx];
                            if (!w.IsEmpty && w.CurrentUsageItem != null)
                            {
                                offUsage = w.CurrentUsageItem.ItemUsage ?? "null";
                                offUsageNativeIdx = string.IsNullOrEmpty(offUsage) ? -1 : MBItem.GetItemUsageIndex(offUsage);
                            }
                        }

                        DualWieldLog.Log($"[Player] mainSlot={mainIdx} usage='{mainUsage}' nativeIdx={mainUsageNativeIdx}");
                        DualWieldLog.Log($"[Player] offSlot={offIdx} usage='{offUsage}' nativeIdx={offUsageNativeIdx}");

                        InformationManager.DisplayMessage(new InformationMessage(
                            $"[DW] Main: '{mainUsage}'={mainUsageNativeIdx} Off: '{offUsage}'={offUsageNativeIdx}",
                            offUsageNativeIdx >= 0 ? Colors.Green : Colors.Yellow));
                    }
                }
                catch (System.Exception ex)
                {
                    DualWieldLog.Log($"[UsageIdx] Error: {ex.Message}");
                }
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
        private int _mmbDiagCount; // diagnostic throttle

        /// <summary>
        /// v7.48: SEPARATED mode.
        /// LMB = normal right-hand attack (engine handles it).
        /// MMB (or B key fallback) = left-hand attack via fist_left_stance on ch1.
        /// RMB = block.
        /// </summary>
        private void ProcessSeparatedMode(Agent agent)
        {
            if (_rmbCooldown > 0) { _rmbCooldown--; }

            // v7.48: Try multiple input methods for MMB detection.
            // Input.IsKeyDown delegates to InputManager.IsKeyDown (native).
            // Also check IsKeyPressed (single-frame) and B key as fallback.
            bool mmbDown = Input.IsKeyDown(InputKey.MiddleMouseButton);
            bool mmbPressed = Input.IsKeyPressed(InputKey.MiddleMouseButton);
            bool bDown = Input.IsKeyDown(InputKey.B);

            // Diagnostic: show input detection state (first 30 ticks with attachment, then every 300)
            if (_mmbDiagCount < 30 || _tickCount % 300 == 0)
            {
                if (mmbDown || mmbPressed || bDown)
                {
                    _mmbDiagCount++;
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[Input] MMB.Down={mmbDown} MMB.Pressed={mmbPressed} B={bDown}",
                        Colors.Yellow));
                }
            }

            // Use either MMB or B key as trigger
            bool triggerDown = mmbDown || bDown;

            // MMB/B pressed (not held from last tick)
            if (triggerDown && !_rmbWasDown && _rmbCooldown <= 0)
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
            _rmbWasDown = triggerDown;
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

        #region Bone Mirror PoC (V-Key)

        private bool _vWasDown;
        private bool _mirrorActive;
        private int _mirrorFramesLeft;
        private bool _boneDumpDone;
        private const int MIRROR_DURATION = 120; // ~2 seconds

        // Bone index pairs: L = 14..20, R = 21..27 (HumanBone enum)
        private static readonly sbyte[] LH_BONES = { 14, 15, 16, 17, 18, 19, 20 }; // ShoulderL..ItemL
        private static readonly sbyte[] RH_BONES = { 21, 22, 23, 24, 25, 26, 27 }; // ShoulderR..ItemR

        /// <summary>
        /// v10.3 Bone Mirror Diagnostic:
        /// V key cycles through test modes:
        ///   Mode 0 = OFF
        ///   Mode 1 = FREEZE: LH arm locked to rest pose (verifies SetOutQuat works)
        ///   Mode 2 = COPY:   LH gets RH's local rotation (verifies local calc works)
        ///   Mode 3 = MIRROR: Full delta-from-rest sagittal mirroring
        /// </summary>
        private void ProcessManualTest(Agent agent)
        {
            bool vDown = Input.IsKeyDown(InputKey.V);

            if (vDown && !_vWasDown)
            {
                // First V press: attach script if not done yet
                if (!_scriptAttached)
                {
                    DumpBoneHierarchy(agent);
                    _boneDumpDone = true;
                    AttachBoneMirrorScript(agent);
                }

                // Cycle mode: 0 → 1 → 2 → 3 → 0
                int newMode = (DualWieldBoneMirrorScript.Mode + 1) % DualWieldBoneMirrorScript.MODE_COUNT;
                DualWieldBoneMirrorScript.Mode = newMode;

                string modeName = DualWieldBoneMirrorScript.MODE_NAMES[newMode];
                Color color = newMode == 0 ? Colors.Yellow : Colors.Magenta;

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DW] Bone Mirror: {modeName}", color));
                DualWieldLog.Log($"[BoneMirror] Mode changed to {newMode}: {modeName}");

                // Reset timer on any activation
                if (newMode > 0)
                {
                    _mirrorActive = true;
                    _mirrorFramesLeft = MIRROR_DURATION;
                }
                else
                {
                    _mirrorActive = false;
                    _mirrorFramesLeft = 0;
                }
            }
            _vWasDown = vDown;

            if (!_mirrorActive) return;

            _mirrorFramesLeft--;
            if (_mirrorFramesLeft <= 0)
            {
                _mirrorActive = false;
                DualWieldBoneMirrorScript.Mode = 0;
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DW] Bone Mirror: timeout → OFF", Colors.Yellow));
                return;
            }

            // Diagnostic after 30 frames
            if (_mirrorFramesLeft == MIRROR_DURATION - 30)
            {
                bool fired = DualWieldBoneMirrorScript.CallbackFired;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DW] Callback fired: {fired}",
                    fired ? Colors.Green : Colors.Red));
            }
        }

        private bool _scriptAttached;

        /// <summary>
        /// Attach DualWieldBoneMirrorScript to the agent's GameEntity.
        /// This enables the SkeletonPostIntegrateCallback hook.
        /// </summary>
        private void AttachBoneMirrorScript(Agent agent)
        {
            if (_scriptAttached) return;

            try
            {
                var agentVisuals = agent.AgentVisuals;
                if (agentVisuals == null)
                {
                    DualWieldLog.Log("[BoneMirror] AgentVisuals is NULL");
                    return;
                }

                var entity = agentVisuals.GetEntity();
                if (entity == null)
                {
                    DualWieldLog.Log("[BoneMirror] Agent Entity is NULL");
                    return;
                }

                var skeleton = agentVisuals.GetSkeleton();
                if (skeleton == null)
                {
                    DualWieldLog.Log("[BoneMirror] Skeleton is NULL");
                    return;
                }

                DualWieldLog.Log($"[BoneMirror] Entity valid, skeleton bones={skeleton.GetBoneCount()}");

                // Step 1: Add our ScriptComponentBehavior to the agent entity
                // CreateAndAddScriptComponent uses CLASS NAME (or NameOverride), NOT the tag!
                string scriptName = nameof(DualWieldBoneMirrorScript);
                entity.CreateAndAddScriptComponent(scriptName, true);
                DualWieldLog.Log($"[BoneMirror] CreateAndAddScriptComponent('{scriptName}') called");

                // Step 2: Get reference to the created script
                var script = entity.GetFirstScriptOfType<DualWieldBoneMirrorScript>();
                if (script == null)
                {
                    DualWieldLog.Log("[BoneMirror] ERROR: Script not found after CreateAndAdd!");
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[DW] BoneMirror script NOT FOUND!", Colors.Red));
                    return;
                }

                // Step 3: Give the script a reference to the skeleton
                script.AgentSkeleton = skeleton;

                // Step 4: Enable the PostIntegrate callback on the skeleton
                skeleton.EnableScriptDrivenPostIntegrateCallback();

                _scriptAttached = true;
                DualWieldLog.Log("[BoneMirror] Script attached + PostIntegrate callback enabled!");
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DW] BoneMirror script attached!", Colors.Green));
            }
            catch (System.Exception ex)
            {
                DualWieldLog.Log($"[BoneMirror] AttachScript ERROR: {ex.Message}\n{ex.StackTrace}");
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DW] BoneMirror attach FAILED: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Dump all bone names and indices for the agent's skeleton.
        /// This confirms the HumanBone enum matches the actual skeleton layout.
        /// </summary>
        private void DumpBoneHierarchy(Agent agent)
        {
            var skeleton = agent.AgentVisuals?.GetSkeleton();
            if (skeleton == null) { DualWieldLog.Log("[BoneDump] skeleton is NULL"); return; }

            sbyte count = skeleton.GetBoneCount();
            DualWieldLog.Log($"[BoneDump] Skeleton has {count} bones:");

            for (sbyte i = 0; i < count; i++)
            {
                string name = skeleton.GetBoneName(i);
                sbyte parent = skeleton.GetParentBoneIndex(i);
                DualWieldLog.Log($"  [{i:D2}] '{name}' parent={parent}");
            }

            // Also log the Monster's known bone indices for comparison
            var monster = agent.Monster;
            if (monster != null)
            {
                DualWieldLog.Log($"[BoneDump] Monster MainHandBone={monster.MainHandBoneIndex}" +
                    $" OffHandBone={monster.OffHandBoneIndex}" +
                    $" MainHandItemBone={monster.MainHandItemBoneIndex}" +
                    $" OffHandItemBone={monster.OffHandItemBoneIndex}");
            }
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
            _mirrorActive = false;
            _mirrorFramesLeft = 0;
            _scriptAttached = false;
            DualWieldBoneMirrorScript.ResetState();
            Patches.DualWieldAnimationPatches.ClearAll();
            Patches.DualWieldWieldingPatches.ClearTrackingState();
            DualWieldStateManager.Clear();

            // v7.50: Patches stay active for the campaign session (applied in OnGameStart).
            // The IsActive guard on each patch prevents firing outside combat.
            // No RemovePatches() here — that's only on module unload.
        }

        // v7.46: Rotation presets removed — engine handles offhand orientation natively
        // via hand_shield usage flags. No AttachWeaponToBone = no custom rotation needed.
    }
}
