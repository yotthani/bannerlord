using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DualWield.Core;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Engine;
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

            // v10.29: Only activate mirror in mode 0. Async modes (1/2) use fist_left_stance + proximity damage.
            if (agent == Agent.Main && !_mirrorActive && _dwCombatMode == 0)
                _pendingMirrorActivation = true;
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

            // v10.26: Reset mirror when dual-wield is removed
            if (agent == Agent.Main)
                DeactivateMirror();

            DualWieldLog.Log($"OffHand removed for agent {agent.Name}");
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!DualWieldSettings.Get().EnableDualWield) return;

            if (!_tickLoggedOnce)
            {
                _tickLoggedOnce = true;
                DualWieldLog.Log("OnMissionTick: v10.30 — Mode system + usage diagnostics");
                Patches.ForceLeftStancePatch.ForceLeftStance = false;

                // v10.30: Initialize combat mode from settings (0=Mirror, 1=Alternating, 2=Separated)
                _dwCombatMode = Math.Max(0, Math.Min(2, DualWieldSettings.Get().AttackMode));
                string[] modeNames = { "Spiegel (Mirror)", "Alternierend (LMB)", "Getrennt (MMB/B)" };
                DualWieldLog.Log($"[DW] Initial mode from settings: {_dwCombatMode} = {modeNames[_dwCombatMode]}");
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[DW] Modus: {modeNames[_dwCombatMode]} (N = wechseln)", Colors.Green));

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

            var agent = Agent.Main;

            // v10.27: Always sync native bones if callback is attached but no active DW —
            // EnableScriptDrivenPostIntegrateCallback permanently breaks engine weapon auto-sync.
            if (agent != null && agent.IsActive() && _scriptAttached && !_mirrorActive
                && !_agentsWithAttachment.Contains(agent.Index))
            {
                SyncWeaponEntitiesToNativeBones(agent);
            }

            if (_agentsWithAttachment.Count == 0) return;

            // Process attack system for player
            if (agent != null && agent.IsActive() && _agentsWithAttachment.Contains(agent.Index))
            {
                if (_ikActive) { agent.ClearHandInverseKinematics(); _ikActive = false; }

                if (_dwCombatMode == 0)
                {
                    // ── Spiegel-Modus: Arme spiegeln sich, LH-Schaden via OnAgentHit ──
                    if (_pendingMirrorActivation && !_mirrorActive)
                    {
                        _pendingMirrorActivation = false;
                        ActivateMirror(agent);
                    }
                }
                else
                {
                    // ── Async-Modi 1/2: Mirror-Toggle für LH-Angriffe ──
                    // Mirror wird von ProcessAlternatingMode/ProcessSeparatedMode gesteuert.
                    // LH-Schaden kommt via OnAgentHit (bonus blow wenn _mirrorActive).
                    if (_dwCombatMode == 1)
                        ProcessAlternatingMode(agent);
                    else
                        ProcessSeparatedMode(agent);
                }

                ProcessManualTest(agent); // V key = manueller Mirror-Fallback, N key = Modus wechseln

                // Detect attack type for shoulder correction (non-slash = thrust/overswing)
                UpdateNonSlashFlag(agent);

                // Retry offset capture if it hasn't succeeded yet (sanity check rejects
                // garbage offsets until native weapon attachment has propagated).
                if (_mirrorActive && !_offsetsCaptured)
                    CaptureWeaponBoneOffsets(agent);

                // v10.10: Manually sync weapon entity positions to mirrored bone frames.
                // The PostIntegrate callback handles mesh rendering (arm mirroring).
                // We handle weapon entities here using stored bone frames from the callback.
                if (_mirrorActive)
                    SyncWeaponEntitiesToMirroredBones(agent);
                else if (_scriptAttached)
                    SyncWeaponEntitiesToNativeBones(agent);
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

        private int _separatedMirrorTicks; // countdown: Mode=2 active after MMB strike
        private ActionIndexCache _mmbTriggeredAction; // tracks which action MMB set on ch0

        // Vanilla direction → 1h release action mapping
        private static readonly ActionIndexCache _atkOverswing  = ActionIndexCache.Create("act_release_overswing_1h");
        private static readonly ActionIndexCache _atkThrust     = ActionIndexCache.Create("act_release_thrust_1h");
        private static readonly ActionIndexCache _atkSlashLeft  = ActionIndexCache.Create("act_release_slashleft_1h");
        private static readonly ActionIndexCache _atkSlashRight = ActionIndexCache.Create("act_release_slashright_1h");

        /// <summary>
        /// Maps vanilla PlayerAttackDirection to the matching 1h release action.
        /// Same direction system as RH LMB — mouse direction determines swing type.
        /// </summary>
        /// <summary>
        /// Maps vanilla PlayerAttackDirection to the matching 1h release action.
        /// L/R swapped because MirrorZ inverts the swing direction when mirroring RH→LH.
        /// </summary>
        private ActionIndexCache DirectionToReleaseAction(Agent.UsageDirection dir)
        {
            switch (dir)
            {
                case Agent.UsageDirection.AttackUp:    return _atkOverswing;
                case Agent.UsageDirection.AttackDown:  return _atkThrust;
                case Agent.UsageDirection.AttackLeft:  return _atkSlashRight; // Mirror flips L↔R
                case Agent.UsageDirection.AttackRight: return _atkSlashLeft;  // Mirror flips L↔R
                default:                               return _atkSlashRight; // fallback
            }
        }

        private void ProcessSeparatedMode(Agent agent)
        {
            // v10.31c: MMB/B = direkter LH-Schlag mit Vanilla-Richtungserkennung.
            // PlayerAttackDirection() liest Maus-Richtung → gleiche Steuerung wie LMB für RH.
            // Aktiviert Mode=2 (LH-only mirror) und triggert passende 1h-Release auf ch0.

            // Mirror window countdown — cancel if engine started a different action (LMB)
            if (_separatedMirrorTicks > 0)
            {
                // Check if ch0 action changed from our MMB-triggered action
                try
                {
                    ActionIndexCache currentCh0 = agent.GetCurrentAction(0);
                    if (_mmbTriggeredAction != ActionIndexCache.act_none
                        && currentCh0 != _mmbTriggeredAction)
                    {
                        // Engine overwrote our action (player pressed LMB) → cancel mirror
                        _separatedMirrorTicks = 0;
                        SetMirrorForAsync(false, agent);
                    }
                    else
                    {
                        _separatedMirrorTicks--;
                        SetMirrorForAsync(true, agent);
                    }
                }
                catch
                {
                    _separatedMirrorTicks--;
                    SetMirrorForAsync(true, agent);
                }
            }
            else
            {
                SetMirrorForAsync(false, agent);
            }

            bool mmbDown = Input.IsKeyDown(InputKey.MiddleMouseButton);
            bool bDown = Input.IsKeyDown(InputKey.B);
            bool triggerDown = mmbDown || bDown;

            if (triggerDown && !_rmbWasDown && _rmbCooldown <= 0)
            {
                // Read vanilla mouse direction + flip L↔R (MirrorZ flips it back on LH)
                Agent.UsageDirection dir = agent.PlayerAttackDirection();
                ActionIndexCache atkAction = DirectionToReleaseAction(dir);

                // Activate LH-only mirror
                SetMirrorForAsync(true, agent);
                _separatedMirrorTicks = 50;

                // Trigger flipped release on ch0 — callback mirrors to LH (correct direction)
                _mmbTriggeredAction = atkAction;
                bool ok = agent.SetActionChannel(0, atkAction, ignorePriority: true, additionalFlags: 0UL);

                string label = dir.ToString().Replace("Attack", "");
                InformationManager.DisplayMessage(new InformationMessage(
                    ok ? $"[LH] {label}" : "[LH] FAIL", ok ? Colors.Green : Colors.Red));

                _rmbCooldown = 35;
                _followUpCount++;
            }
            _rmbWasDown = triggerDown;

            if (_rmbCooldown > 0) _rmbCooldown--;
        }

        #endregion

        #region Alternating Mode (LMB alternates R/L)

        private bool _nextIsLeft;
        private int _altMirrorTicks; // countdown: keep mirror active for LH attack
        private bool _altLmbWasDown;
        private int _altCooldown;

        /// <summary>
        /// v10.33: ALTERNATING mode — input-basiert statt action-detection.
        /// Jeder LMB-Press alterniert: RH (mirror off) → LH (Mode=2 für ~1s) → RH → LH ...
        /// Engine spielt Angriff normal auf ch0, Callback spiegelt auf LH und setzt RH idle.
        /// </summary>
        private void ProcessAlternatingMode(Agent agent)
        {
            // Mirror window countdown
            if (_altMirrorTicks > 0)
            {
                _altMirrorTicks--;
                SetMirrorForAsync(true, agent);
            }
            else
            {
                SetMirrorForAsync(false, agent);
            }

            // Detect LMB press → alternate between RH and LH
            bool lmbDown = Input.IsKeyDown(InputKey.LeftMouseButton);
            if (lmbDown && !_altLmbWasDown)
            {
                if (_nextIsLeft)
                {
                    // LH-Turn: activate Mode=2 for release animation duration
                    SetMirrorForAsync(true, agent);
                    _altMirrorTicks = 35; // ~0.58s — covers release + early recovery
                    _followUpCount++;
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[LH]", Colors.Green));
                }
                else
                {
                    // RH-Turn: ensure mirror is off
                    _altMirrorTicks = 0;
                    SetMirrorForAsync(false, agent);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[RH]", Colors.Cyan));
                }
                _nextIsLeft = !_nextIsLeft;
            }
            _altLmbWasDown = lmbDown;
        }

        /// <summary>
        /// Update IsNonSlashAttack flag. Attacks play on ch1-ch3 (not ch0).
        /// Scan all non-zero channels for the first non-none action name.
        /// </summary>
        private string _lastFlagName = "";

        private void UpdateNonSlashFlag(Agent agent)
        {
            try
            {
                string name = null;
                for (int ch = 1; ch < 4; ch++)
                {
                    try
                    {
                        var n = agent.GetCurrentAction(ch).GetName();
                        if (!string.IsNullOrEmpty(n) && n != "act_none") { name = n; break; }
                    }
                    catch { }
                }

                // DEBUG: show action name in HUD whenever it changes
                if (name != null && name != _lastFlagName)
                {
                    _lastFlagName = name;
                    InformationManager.DisplayMessage(new InformationMessage($"[ACT] {name}", Colors.Yellow));
                    DualWieldLog.Log($"[UpdateNonSlash] action={name}");
                }

                bool isNonSlash = !string.IsNullOrEmpty(name) &&
                                  (name.Contains("thrust") || name.Contains("overswing"));
                // Overswing uses MirrorZ (like slash). Only thrust needs the SLERP treatment.
                bool isThrust = !string.IsNullOrEmpty(name) && name.Contains("thrust");
                bool isSlashRight = !string.IsNullOrEmpty(name) && name.Contains("slashright");
                bool isSlashLeft  = !string.IsNullOrEmpty(name) && name.Contains("slashleft");
                DualWieldBoneMirrorScript.IsNonSlashAttack = isNonSlash;
                DualWieldBoneMirrorScript.IsThrustAttack = isThrust;
                DualWieldBoneMirrorScript.IsSlashRightAttack = isSlashRight;
                DualWieldBoneMirrorScript.IsSlashLeftAttack = isSlashLeft;
            }
            catch
            {
                DualWieldBoneMirrorScript.IsNonSlashAttack = false;
                DualWieldBoneMirrorScript.IsThrustAttack = false;
                DualWieldBoneMirrorScript.IsSlashRightAttack = false;
                DualWieldBoneMirrorScript.IsSlashLeftAttack = false;
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
                StartLHAsyncDamageWindow(); // v10.29: open proximity damage window for this follow-up

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

        #region Bone Mirror (auto-activate on dual wield, v10.26)

        private bool _vWasDown;
        private bool _hWasDown;
        private bool _jWasDown;
        private bool _kWasDown;
        private bool _tabWasDown;
        private bool _shiftTabWasDown;
        private bool _gWasDown;
        private bool _nWasDown;
        private bool _mirrorActive;
        private bool _scriptAttached;
        private bool _pendingMirrorActivation; // delayed by 1 tick so weapon entities are initialized

        /// <summary>
        /// Active DW combat mode.
        /// 0 = Spiegel  — both arms mirror permanently, LH damage via OnAgentHit bonus blow
        /// 1 = Alternierend — every other LMB activates mirror → dual strike → OnAgentHit bonus
        /// 2 = Getrennt — MMB/B toggles mirror → while active, all attacks are dual strikes
        /// </summary>
        private int _dwCombatMode; // default 0 = Mirror

        /// <summary>
        /// Activates bone mirroring for the given agent. Called automatically when
        /// dual-wield is set up, or manually via V key for testing.
        /// One-way: cannot be deactivated (PostIntegrate callback is permanent).
        /// </summary>
        private void ActivateMirror(Agent agent)
        {
            if (_mirrorActive) return;

            // Always re-capture offsets — the item-ID guard inside skips if unchanged.
            // Different weapons (sword vs axe) have different grip geometry; stale offsets
            // cause weapons to float disconnected from the hand.
            CaptureWeaponBoneOffsets(agent);

            if (!_scriptAttached)
            {
                AttachBoneMirrorScript(agent);
            }

            _mirrorActive = true;
            DualWieldBoneMirrorScript.Mode = 1;
            InformationManager.DisplayMessage(new InformationMessage(
                "[DW] Bone Mirror: ON", Colors.Green));
            DualWieldLog.Log("[BoneMirror] Auto-activated for dual wield");
        }

        /// <summary>
        /// Resets mirror state. Called when dual-wield is removed (wield change).
        /// The PostIntegrate callback stays registered but Mode=0 makes it return false.
        /// Next AttachOffHandWeapon will re-activate with fresh offsets.
        /// </summary>
        private void DeactivateMirror()
        {
            if (!_mirrorActive) return;

            _mirrorActive = false;
            _pendingMirrorActivation = false;
            // _scriptAttached intentionally NOT reset:
            // EnableScriptDrivenPostIntegrateCallback is permanent — re-attaching the script
            // would add a second instance and re-enabling the callback causes double firing.
            // Offsets are re-captured on ActivateMirror (item-ID-guarded, so same-item re-equips
            // are cheap, different items get fresh offsets).
            DualWieldBoneMirrorScript.Mode = 0;
            InformationManager.DisplayMessage(new InformationMessage("[DW] Dual Wield: AUS", Colors.Yellow));
            DualWieldLog.Log("[BoneMirror] Deactivated (wield change)");
        }

        /// <summary>
        /// Lightweight mirror toggle for async modes (1=Alternierend, 2=Getrennt).
        /// No chat messages. Ensures script is attached on first use.
        /// OnAgentHit checks _mirrorActive to fire LH bonus damage.
        /// </summary>
        private void SetMirrorForAsync(bool active, Agent agent)
        {
            if (active == _mirrorActive) return;

            if (active && !_scriptAttached)
            {
                CaptureWeaponBoneOffsets(agent);
                AttachBoneMirrorScript(agent);
            }

            _mirrorActive = active;
            DualWieldBoneMirrorScript.Mode = active ? 2 : 0; // Mode 2 = LH-only (RH stays idle)
        }

        // v10.11 restored: Weapon-to-bone offsets, captured BEFORE enabling the callback.
        // weapon_frame = bone_entitial_frame * offset
        // → offset = bone_frame^-1 * weapon_frame (= bone_frame.TransformToLocal(weapon_frame))
        private MatrixFrame _rhWeaponOffset;
        private MatrixFrame _lhWeaponOffset;
        private bool _offsetsCaptured;

        /// <summary>
        /// v10.26: V key as manual fallback to activate mirror (e.g. for testing).
        /// Normally mirror activates automatically via AttachOffHandWeapon.
        /// </summary>
        private void ProcessManualTest(Agent agent)
        {
            // V key: manual mirror activate in mode 0 (fallback if auto-activate failed)
            if (_dwCombatMode == 0 && !_mirrorActive)
            {
                bool vDown = Input.IsKeyDown(InputKey.V);
                if (vDown && !_vWasDown)
                    ActivateMirror(agent);
                _vWasDown = vDown;
            }
            else
            {
                _vWasDown = Input.IsKeyDown(InputKey.V); // track without acting
            }

            // N key: cycle DW combat modes 0→1→2→0
            bool nDown = Input.IsKeyDown(InputKey.N);
            if (nDown && !_nWasDown)
                CycleDWMode(agent);
            _nWasDown = nDown;

            // G key: cycle the mirror-type for slashright's change quaternion.
            // 0=MirrorZ-style (flip X,Y)  1=MirrorY-style (flip X,Z)  2=MirrorX-style (flip Y,Z)  3=Identity
            bool gDown = Input.IsKeyDown(InputKey.G);
            if (gDown && !_gWasDown)
            {
                DualWieldBoneMirrorScript.MirrorVariant =
                    (DualWieldBoneMirrorScript.MirrorVariant + 1) % DualWieldBoneMirrorScript.MIRROR_VARIANT_COUNT;
                string variantName = DualWieldBoneMirrorScript.MirrorVariant switch
                {
                    0 => "MirrorZ-style (flip X,Y)",
                    1 => "MirrorY-style (flip X,Z)",
                    2 => "MirrorX-style (flip Y,Z)",
                    3 => "Identity",
                    _ => "?"
                };
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[slashright mirror] {DualWieldBoneMirrorScript.MirrorVariant}: {variantName}",
                    Colors.Magenta));
                DualWieldLog.Log($"[slashright mirror] cycled to {DualWieldBoneMirrorScript.MirrorVariant}={variantName}");
            }
            _gWasDown = gDown;

            // H key: dump current state for diagnostics
            bool hDown = Input.IsKeyDown(InputKey.H);
            if (hDown && !_hWasDown)
                DumpDiagnostics(agent);
            _hWasDown = hDown;

            // J key: dump per-bone rotation axes + angles for current frame.
            bool jDown = Input.IsKeyDown(InputKey.J);
            if (jDown && !_jWasDown)
            {
                string currentAction = _lastFlagName ?? "(unknown)";
                DualWieldBoneMirrorScript.DumpBoneAxes(currentAction);
            }
            _jWasDown = jDown;

            // Tab / Shift+Tab: cycle the probe state (isolated axis test).
            // Requires bind pose captured (K key) first.
            // 30 states: 5 bones (upperArm..hand) × 3 axes × 2 directions + OFF = 31
            const int PROBE_STATE_COUNT = 31;
            bool tabDown = Input.IsKeyDown(InputKey.Tab);
            bool shiftHeld = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
            if (tabDown && !_tabWasDown && !shiftHeld)
            {
                DualWieldBoneMirrorScript.ProbeState =
                    (DualWieldBoneMirrorScript.ProbeState + 1) % PROBE_STATE_COUNT;
                string desc = DualWieldBoneMirrorScript.ProbeStateDescription();
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Probe {DualWieldBoneMirrorScript.ProbeState}/{PROBE_STATE_COUNT - 1}] {desc}",
                    Colors.Cyan));
                DualWieldLog.Log($"[Probe] state={DualWieldBoneMirrorScript.ProbeState} {desc}");
            }
            if (tabDown && !_tabWasDown && shiftHeld && !_shiftTabWasDown)
            {
                DualWieldBoneMirrorScript.ProbeState =
                    (DualWieldBoneMirrorScript.ProbeState + PROBE_STATE_COUNT - 1) % PROBE_STATE_COUNT;
                string desc = DualWieldBoneMirrorScript.ProbeStateDescription();
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Probe {DualWieldBoneMirrorScript.ProbeState}/{PROBE_STATE_COUNT - 1}] {desc}",
                    Colors.Cyan));
                DualWieldLog.Log($"[Probe] state={DualWieldBoneMirrorScript.ProbeState} {desc}");
            }
            _tabWasDown = tabDown;
            _shiftTabWasDown = shiftHeld && tabDown;

            // K key: capture current bone world orientations as the "bind pose reference".
            // Press when the character is standing still in idle pose (no attack).
            // This reference is used by the bind-pose-aware slashright mirror.
            bool kDown = Input.IsKeyDown(InputKey.K);
            if (kDown && !_kWasDown)
            {
                DualWieldBoneMirrorScript.CaptureBindPose();
                InformationManager.DisplayMessage(new InformationMessage(
                    "[BindPose] Referenz-Pose erfasst — slashright wird jetzt die gemessene Spiegelung verwenden",
                    Colors.Cyan));
            }
            _kWasDown = kDown;
        }

        private void CycleDWMode(Agent agent)
        {
            _dwCombatMode = (_dwCombatMode + 1) % 3;
            string[] names = { "Spiegel (Mirror)", "Alternierend (LMB)", "Getrennt (MMB/B)" };
            InformationManager.DisplayMessage(new InformationMessage(
                $"[DW] Modus {_dwCombatMode}: {names[_dwCombatMode]}", new Color(1f, 0.6f, 0f)));
            DualWieldLog.Log($"[DW] Mode → {_dwCombatMode}: {names[_dwCombatMode]}");

            // Persist to MCM settings so it's remembered across missions
            try { DualWieldSettings.Get().AttackMode = _dwCombatMode; } catch { }

            if (_dwCombatMode == 0)
            {
                // Switching TO mirror: activate if dual-wielding
                if (_agentsWithAttachment.Contains(agent.Index))
                    _pendingMirrorActivation = true;
            }
            else
            {
                // Switching away from mirror: deactivate + reset async state
                _pendingMirrorActivation = false;
                _separatedMirrorTicks = 0;
                _altMirrorTicks = 0;
                _nextIsLeft = false;
                if (_mirrorActive) DeactivateMirror();
            }
        }

        private void DumpDiagnostics(Agent agent)
        {
            var mainSlot = agent?.GetPrimaryWieldedItemIndex() ?? EquipmentIndex.None;
            var offSlot  = agent?.GetOffhandWieldedItemIndex() ?? EquipmentIndex.None;
            bool inSet   = agent != null && _agentsWithAttachment.Contains(agent.Index);
            bool rhEnt   = agent != null && mainSlot != EquipmentIndex.None
                           && agent.GetWeaponEntityFromEquipmentSlot(mainSlot) != null;
            bool lhEnt   = agent != null && offSlot != EquipmentIndex.None
                           && agent.GetWeaponEntityFromEquipmentSlot(offSlot) != null;

            string line1 = $"[DW-Diag] mirror={_mirrorActive} script={_scriptAttached} offsets={_offsetsCaptured}";
            string line2 = $"[DW-Diag] inSet={inSet} mode={DualWieldBoneMirrorScript.Mode} hasRH={DualWieldBoneMirrorScript.HasRHFrame} hasWep={DualWieldBoneMirrorScript.HasWeaponFrames}";
            string line3 = $"[DW-Diag] main={mainSlot} off={offSlot} rhEnt={rhEnt} lhEnt={lhEnt}";

            InformationManager.DisplayMessage(new InformationMessage(line1, Colors.Yellow));
            InformationManager.DisplayMessage(new InformationMessage(line2, Colors.Yellow));
            InformationManager.DisplayMessage(new InformationMessage(line3, Colors.Yellow));
            DualWieldLog.Log(line1);
            DualWieldLog.Log(line2);
            DualWieldLog.Log(line3);
        }

        /// <summary>
        /// v10.11: Capture the local offset between weapon entities and their attachment bones.
        /// Must be called BEFORE EnableScriptDrivenPostIntegrateCallback (which breaks auto-sync).
        /// offset = bone_entitial_frame.TransformToLocal(weapon_entity_frame)
        /// </summary>
        // Track which items we captured offsets for — triggers re-capture when items change.
        private string _capturedMainItemId = "";
        private string _capturedOffItemId = "";

        private void CaptureWeaponBoneOffsets(Agent agent)
        {
            try
            {
                var skeleton = agent.AgentVisuals?.GetSkeleton();
                if (skeleton == null) return;

                const sbyte RH_WEAPON_BONE = 27;
                const sbyte LH_WEAPON_BONE = 20;

                var mainSlot = agent.GetPrimaryWieldedItemIndex();
                var offSlot = agent.GetOffhandWieldedItemIndex();

                // Only skip if the same items are still wielded. Different weapons (sword vs axe)
                // have different grip offsets and need separate capture.
                string mainId = "";
                string offId = "";
                try
                {
                    if (mainSlot != EquipmentIndex.None)
                        mainId = agent.Equipment?[mainSlot].Item?.StringId ?? "";
                    if (offSlot != EquipmentIndex.None)
                        offId = agent.Equipment?[offSlot].Item?.StringId ?? "";
                }
                catch { }
                if (_offsetsCaptured && mainId == _capturedMainItemId && offId == _capturedOffItemId)
                    return;
                _capturedMainItemId = mainId;
                _capturedOffItemId = offId;

                // GENERAL sanity check: weapon must NOT be at world origin (0,0,0).
                // That means it's not natively attached yet — offset would be garbage.
                // Works for all weapons (crafted polearms, sturgia_axe, wooden_sword, etc.)
                // without hardcoded per-weapon data.
                bool rhOk = false, lhOk = false;

                if (mainSlot != EquipmentIndex.None)
                {
                    var weaponEntity = agent.GetWeaponEntityFromEquipmentSlot(mainSlot);
                    if (weaponEntity != null)
                    {
                        MatrixFrame boneFrame = skeleton.GetBoneEntitialFrame(RH_WEAPON_BONE);
                        MatrixFrame weaponFrame = weaponEntity.GetFrame();
                        // Reject if weapon is at world origin (not attached yet)
                        bool atOrigin = weaponFrame.origin.Length < 0.01f;
                        if (!atOrigin)
                        {
                            _rhWeaponOffset = boneFrame.TransformToLocal(weaponFrame);
                            rhOk = true;
                            DualWieldLog.Log($"[WeaponOffset] RH CAPT id={mainId} bone={boneFrame.origin} weapon={weaponFrame.origin} offset={_rhWeaponOffset.origin}");
                        }
                        else
                        {
                            DualWieldLog.Log($"[WeaponOffset] RH SKIP id={mainId} weapon at world origin — native attach not ready, retry");
                        }
                    }
                }
                else rhOk = true;

                if (offSlot != EquipmentIndex.None)
                {
                    var weaponEntity = agent.GetWeaponEntityFromEquipmentSlot(offSlot);
                    if (weaponEntity != null)
                    {
                        MatrixFrame boneFrame = skeleton.GetBoneEntitialFrame(LH_WEAPON_BONE);
                        MatrixFrame weaponFrame = weaponEntity.GetFrame();
                        bool atOrigin = weaponFrame.origin.Length < 0.01f;
                        if (!atOrigin)
                        {
                            _lhWeaponOffset = boneFrame.TransformToLocal(weaponFrame);
                            lhOk = true;
                            DualWieldLog.Log($"[WeaponOffset] LH CAPT id={offId} bone={boneFrame.origin} weapon={weaponFrame.origin} offset={_lhWeaponOffset.origin}");
                        }
                        else
                        {
                            DualWieldLog.Log($"[WeaponOffset] LH SKIP id={offId} weapon at world origin — native attach not ready, retry");
                        }
                    }
                }
                else lhOk = true;

                if (!(rhOk && lhOk)) return; // will retry next tick
                _offsetsCaptured = true;
                DualWieldLog.Log("[WeaponOffset] v10.16: Offsets captured. LH offset is base, G-key cycles rotation fixes.");
                DualWieldLog.Log($"[WeaponOffset] RH offset origin={_rhWeaponOffset.origin}");
                DualWieldLog.Log($"[WeaponOffset] LH offset origin={_lhWeaponOffset.origin}");
            }
            catch (System.Exception ex)
            {
                DualWieldLog.Log($"[WeaponOffset] Capture error: {ex.Message}");
            }
        }

        /// <summary>
        /// Attach DualWieldBoneMirrorScript to the agent's GameEntity.
        /// Enables PostIntegrate callback for mesh bone mirroring.
        /// </summary>
        private void AttachBoneMirrorScript(Agent agent)
        {
            if (_scriptAttached) return;

            try
            {
                var agentVisuals = agent.AgentVisuals;
                if (agentVisuals == null) return;

                var entity = agentVisuals.GetEntity();
                var skeleton = agentVisuals.GetSkeleton();
                if (entity == null || skeleton == null) return;

                string scriptName = nameof(DualWieldBoneMirrorScript);
                entity.CreateAndAddScriptComponent(scriptName, true);

                var script = entity.GetFirstScriptOfType<DualWieldBoneMirrorScript>();
                if (script == null)
                {
                    DualWieldLog.Log("[BoneMirror] ERROR: Script not found after CreateAndAdd!");
                    return;
                }

                script.AgentSkeleton = skeleton;
                skeleton.EnableScriptDrivenPostIntegrateCallback();

                _scriptAttached = true;
                DualWieldLog.Log("[BoneMirror] v10.11: Script attached + callback enabled.");
            }
            catch (System.Exception ex)
            {
                DualWieldLog.Log($"[BoneMirror] AttachScript ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// v10.20: PreMirror approach.
        /// RH: boneFrame.TransformToParent(rhOffset) — works perfectly.
        /// LH: Use bone 20's entitial BEFORE mirroring bone 20 (parents 14-19 already mirrored).
        /// This gives det=+1 rotation (correct handedness) at the mirrored arm position.
        /// </summary>
        private void SyncWeaponEntitiesToMirroredBones(Agent agent)
        {
            if (!DualWieldBoneMirrorScript.HasWeaponFrames) return;
            // Removed `if (!_offsetsCaptured) return;` — fallback to bone-position rendering
            // below ensures weapons stay visible while we wait for valid capture.

            try
            {
                var mainSlot = agent.GetPrimaryWieldedItemIndex();
                var offSlot = agent.GetOffhandWieldedItemIndex();

                // RH weapon: bone frame × offset. If no valid offset captured yet, use bone directly.
                if (mainSlot != EquipmentIndex.None)
                {
                    var entity = agent.GetWeaponEntityFromEquipmentSlot(mainSlot);
                    if (entity != null)
                    {
                        MatrixFrame boneFrame = DualWieldBoneMirrorScript.LastRHWeaponBoneFrame;
                        MatrixFrame weaponFrame = _offsetsCaptured
                            ? boneFrame.TransformToParent(_rhWeaponOffset)
                            : boneFrame;
                        entity.SetFrame(ref weaponFrame);
                    }
                }

                // LH weapon: v10.20 PreMirror approach.
                // Use bone 20's entitial captured BEFORE mirroring bone 20 itself.
                // Parents mirrored → correct position. Bone 20 original → det=+1 → no flip.
                if (offSlot != EquipmentIndex.None)
                {
                    var entity = agent.GetWeaponEntityFromEquipmentSlot(offSlot);
                    if (entity != null)
                    {
                        // All modes use PreMirror approach for weapon positioning
                        {
                            MatrixFrame preMirrorBone = DualWieldBoneMirrorScript.PreMirrorLHWeaponBoneFrame;
                            MatrixFrame weaponFrame = _offsetsCaptured
                                ? preMirrorBone.TransformToParent(_lhWeaponOffset)
                                : preMirrorBone;
                            entity.SetFrame(ref weaponFrame);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (_tickCount % 300 == 0)
                    DualWieldLog.Log($"[WeaponSync] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// v10.27: Sync weapon entities to their native bone frames when mirror is inactive
        /// but the PostIntegrate callback is still registered (which breaks engine auto-sync).
        /// Uses LastRHWeaponBoneFrame from the callback — skeleton.GetBoneEntitialFrame() returns
        /// stale data after EnableScriptDrivenPostIntegrateCallback when callback returns false.
        /// Only syncs the main hand — offhand has been unwielded in single-weapon mode.
        /// </summary>
        private void SyncWeaponEntitiesToNativeBones(Agent agent)
        {
            if (!DualWieldBoneMirrorScript.HasRHFrame) return;

            try
            {
                // RH weapon
                var mainSlot = agent.GetPrimaryWieldedItemIndex();
                if (mainSlot != EquipmentIndex.None)
                {
                    var rhEntity = agent.GetWeaponEntityFromEquipmentSlot(mainSlot);
                    if (rhEntity != null)
                    {
                        MatrixFrame boneFrame = DualWieldBoneMirrorScript.LastRHWeaponBoneFrame;
                        MatrixFrame weaponFrame = _offsetsCaptured
                            ? boneFrame.TransformToParent(_rhWeaponOffset)
                            : boneFrame;
                        rhEntity.SetFrame(ref weaponFrame);
                    }
                }

                // LH weapon — only when dual-wielding in async mode (mirror handles LH when mirror is active)
                if (_agentsWithAttachment.Contains(agent.Index)
                    && DualWieldBoneMirrorScript.HasNativeLHFrame
                    && _offHandSlots.TryGetValue(agent.Index, out var offSlot))
                {
                    var lhEntity = agent.GetWeaponEntityFromEquipmentSlot(offSlot);
                    if (lhEntity != null)
                    {
                        MatrixFrame lhBoneFrame = DualWieldBoneMirrorScript.NativeLHWeaponBoneFrame;
                        MatrixFrame lhWeaponFrame = _offsetsCaptured
                            ? lhBoneFrame.TransformToParent(_lhWeaponOffset)
                            : lhBoneFrame;
                        lhEntity.SetFrame(ref lhWeaponFrame);
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (_tickCount % 300 == 0)
                    DualWieldLog.Log($"[NativeSync] Error: {ex.Message}");
            }
        }

        #endregion


        #region LH Weapon Damage (v10.21: Synchron mirror mode bonus blow)

        // Recursion guard: OnAgentHit → victim.RegisterBlow → OnAgentHit
        private bool _applyingLHBlow;
        private int _lhBlowCount;

        // v10.29: Async follow-up damage window — proximity check when fist_left_stance fires
        private bool _lhAsyncAttackActive;
        private int _lhAsyncWindowTicks;
        private readonly HashSet<int> _lhAsyncHitThisSwing = new HashSet<int>();
        private const int LH_ASYNC_WINDOW_TICKS = 15; // ~0.25 s at 60 Hz
        private const float LH_ASYNC_HIT_RADIUS = 1.8f; // meters

        /// <summary>
        /// v10.21: When mirror mode is active and the RH weapon hits, apply a bonus
        /// blow from the LH weapon. Both arms swing in unison, so both should deal damage.
        /// Damage scaled by OffHandDamageMultiplier (default 0.85).
        /// </summary>
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent,
            in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);

            // Guards: recursion, mirror mode, dual-wield state, valid target
            if (_applyingLHBlow) return;
            if (!_mirrorActive) return;
            // LH bonus blow only in Mode=1 (Spiegel: both arms visually attack → both deal damage).
            // Mode=2 (LH-only): engine damage IS the LH attack, no bonus needed.
            if (DualWieldBoneMirrorScript.Mode != 1) return;
            if (affectorAgent == null || affectedAgent == null) return;
            if (!affectedAgent.IsActive()) return;
            if (!_agentsWithAttachment.Contains(affectorAgent.Index)) return;
            if (!_offHandSlots.TryGetValue(affectorAgent.Index, out var offSlot)) return;

            // Only trigger on melee hits (not missiles, not blocked)
            if (blow.IsMissile) return;
            if (attackCollisionData.AttackBlockedWithShield) return;
            if (blow.InflictedDamage <= 0) return;

            // Get LH weapon data
            var lhWeapon = affectorAgent.Equipment[offSlot];
            if (lhWeapon.IsEmpty) return;

            try
            {
                _applyingLHBlow = true;

                float offHandMult = DualWieldSettings.Get().OffHandDamageMultiplier;

                // Create LH blow based on RH blow (struct = value type copy)
                Blow lhBlow = new Blow(affectorAgent.Index);
                lhBlow.VictimBodyPart = blow.VictimBodyPart;
                lhBlow.AttackType = blow.AttackType;
                lhBlow.StrikeType = blow.StrikeType;
                lhBlow.DamageType = blow.DamageType;
                lhBlow.GlobalPosition = blow.GlobalPosition;
                lhBlow.BoneIndex = blow.BoneIndex;
                lhBlow.Direction = blow.Direction;
                lhBlow.SwingDirection = blow.SwingDirection;
                lhBlow.BlowFlag = blow.BlowFlag;
                lhBlow.NoIgnore = blow.NoIgnore;
                lhBlow.DamageCalculated = true;

                // Scale damage (minimum 1 to avoid zero-damage hits)
                lhBlow.BaseMagnitude = blow.BaseMagnitude * offHandMult;
                lhBlow.InflictedDamage = Math.Max(1, (int)(blow.InflictedDamage * offHandMult));
                lhBlow.SelfInflictedDamage = 0;
                lhBlow.AbsorbedByArmor = blow.AbsorbedByArmor * offHandMult;
                lhBlow.MovementSpeedDamageModifier = blow.MovementSpeedDamageModifier;
                lhBlow.DefenderStunPeriod = blow.DefenderStunPeriod * 0.5f;
                lhBlow.AttackerStunPeriod = 0f;

                // Fill weapon record with LH weapon data
                sbyte lhAttachBone = (sbyte)(lhWeapon.Item != null
                    ? affectorAgent.Monster.GetBoneToAttachForItemFlags(lhWeapon.Item.ItemFlags)
                    : -1);
                lhBlow.WeaponRecord.FillAsMeleeBlow(
                    lhWeapon.Item,
                    lhWeapon.CurrentUsageItem,
                    (int)offSlot,
                    lhAttachBone);

                // Apply the blow — need mutable copy of collisionData for RegisterBlow
                AttackCollisionData lhCollision = attackCollisionData;
                affectedAgent.RegisterBlow(lhBlow, in lhCollision);
                _lhBlowCount++;

                if (_lhBlowCount <= 5 || _lhBlowCount % 50 == 0)
                {
                    DualWieldLog.Log($"[LH-Damage] #{_lhBlowCount}: {lhBlow.InflictedDamage} dmg to {affectedAgent.Name} (RH was {blow.InflictedDamage}, mult={offHandMult})");
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[DW] LH hit: {lhBlow.InflictedDamage} dmg ({offHandMult:P0})",
                        Colors.Cyan));
                }
            }
            catch (Exception ex)
            {
                if (_lhBlowCount % 100 == 0)
                    DualWieldLog.Log($"[LH-Damage] Error: {ex.Message}");
            }
            finally
            {
                _applyingLHBlow = false;
            }
        }

        // ── v10.29: Async LH follow-up damage ──────────────────────────────────────

        private void StartLHAsyncDamageWindow()
        {
            _lhAsyncAttackActive = true;
            _lhAsyncWindowTicks = LH_ASYNC_WINDOW_TICKS;
            _lhAsyncHitThisSwing.Clear();
        }

        /// <summary>
        /// Called every tick while a LH follow-up is active.
        /// Checks proximity every 3rd tick to find enemies in range.
        /// </summary>
        private void TickLHAsyncDamage(Agent agent)
        {
            if (!_lhAsyncAttackActive) return;

            _lhAsyncWindowTicks--;
            if (_lhAsyncWindowTicks <= 0)
            {
                _lhAsyncAttackActive = false;
                _lhAsyncHitThisSwing.Clear();
                return;
            }

            // Only check every 3rd tick — reduces overhead without missing hits
            if (_lhAsyncWindowTicks % 3 != 0) return;

            if (!_offHandSlots.TryGetValue(agent.Index, out var offSlot)) return;
            var lhWeapon = agent.Equipment[offSlot];
            if (lhWeapon.IsEmpty) return;

            // Use chest-level position as hit origin
            Vec3 origin = agent.Position + new Vec3(0f, 0f, 1.0f);

            foreach (var victim in Mission.Current.Agents)
            {
                if (victim == agent) continue;
                if (victim.Team != null && victim.Team.IsFriendOf(agent.Team)) continue;
                if (!victim.IsActive()) continue;
                if (_lhAsyncHitThisSwing.Contains(victim.Index)) continue;

                float distSq = (victim.Position - origin).LengthSquared;
                if (distSq < LH_ASYNC_HIT_RADIUS * LH_ASYNC_HIT_RADIUS)
                {
                    ApplyLHAsyncBlow(agent, victim, offSlot, lhWeapon);
                    _lhAsyncHitThisSwing.Add(victim.Index);
                }
            }
        }

        /// <summary>
        /// Applies a proximity-based LH blow during the async follow-up window.
        /// Uses DamageCalculated=true to bypass engine damage calc.
        /// Shares _applyingLHBlow guard to prevent OnAgentHit from adding a second bonus blow.
        /// </summary>
        private void ApplyLHAsyncBlow(Agent attacker, Agent victim, EquipmentIndex offSlot, MissionWeapon lhWeapon)
        {
            try
            {
                var usageItem = lhWeapon.CurrentUsageItem;
                if (usageItem == null) return;

                // Follow-up is weaker than a direct synchronized hit
                int baseWeaponDmg = usageItem.SwingDamage > 0 ? usageItem.SwingDamage : usageItem.ThrustDamage;
                float mult = DualWieldSettings.Get().OffHandDamageMultiplier * 0.6f;
                int inflicted = Math.Max(1, (int)(baseWeaponDmg * mult));

                Vec3 dirRaw = victim.Position - attacker.Position;
                float dirLen = dirRaw.Length;
                Vec3 dir = dirLen > 0.01f ? dirRaw * (1f / dirLen) : Vec3.Forward;

                Blow blow = new Blow(attacker.Index);
                blow.DamageType = usageItem.SwingDamage > 0 ? usageItem.SwingDamageType : usageItem.ThrustDamageType;
                blow.StrikeType = StrikeType.Swing;
                blow.AttackType = AgentAttackType.Standard;
                blow.BlowFlag = BlowFlags.None;
                blow.NoIgnore = false;
                blow.BoneIndex = victim.Monster.ThoraxLookDirectionBoneIndex;
                blow.VictimBodyPart = BoneBodyPartType.Chest;
                blow.GlobalPosition = victim.Position + new Vec3(0f, 0f, 1.0f);
                blow.Direction = dir;
                blow.SwingDirection = Vec3.Up;
                blow.BaseMagnitude = baseWeaponDmg * 0.6f;
                blow.MovementSpeedDamageModifier = 1f;
                blow.InflictedDamage = inflicted;
                blow.SelfInflictedDamage = 0;
                blow.AbsorbedByArmor = 0;
                blow.DefenderStunPeriod = 0.1f;
                blow.AttackerStunPeriod = 0f;
                blow.DamageCalculated = true;

                sbyte attachBone = lhWeapon.Item != null
                    ? (sbyte)attacker.Monster.GetBoneToAttachForItemFlags(lhWeapon.Item.ItemFlags)
                    : (sbyte)(-1);
                blow.WeaponRecord.FillAsMeleeBlow(lhWeapon.Item, usageItem, (int)offSlot, attachBone);

                AttackCollisionData acd = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                    false, false, false,                        // shield, correctShield, isAlternative
                    true,                                       // isColliderAgent
                    false, false, false, false, false,          // collidedWithShieldOnBack, isMissile, isMissileBlocked, hasPhysics, entityExists
                    false, false, false,                        // thrustTipHit, missileUnderWater, missileOutOfBorder
                    CombatCollisionResult.StrikeAgent,
                    (int)offSlot,
                    (int)StrikeType.Swing,
                    (int)blow.DamageType,
                    victim.Monster.ThoraxLookDirectionBoneIndex,
                    BoneBodyPartType.Chest,
                    (sbyte)(-1),
                    Agent.UsageDirection.AttackUp,
                    0,
                    CombatHitResultFlags.NormalHit,
                    0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                    Vec3.Up,
                    dir,
                    blow.GlobalPosition,
                    Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero);

                _applyingLHBlow = true;
                victim.RegisterBlow(blow, in acd);
                _lhBlowCount++;

                DualWieldLog.Log($"[LH-Async] #{_lhBlowCount}: {inflicted} dmg to {victim.Name} (base={baseWeaponDmg}, mult={mult:F2})");
                if (_lhBlowCount <= 5)
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[DW] LH async: {inflicted} dmg", Colors.Cyan));
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"[LH-Async] Error: {ex.Message}");
            }
            finally
            {
                _applyingLHBlow = false;
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
            _lhAsyncAttackActive = false;
            _lhAsyncWindowTicks = 0;
            _lhAsyncHitThisSwing.Clear();
            _nextIsLeft = false;
            _altMirrorTicks = 0;
            _altLmbWasDown = false;
            _altCooldown = 0;
            _rmbWasDown = false;
            _rmbCooldown = 0;
            _rmbAttackIdx = 0;
            _separatedMirrorTicks = 0;
            _mirrorActive = false;
            _pendingMirrorActivation = false;
            _scriptAttached = false;
            _vWasDown = false;
            _hWasDown = false;
            _jWasDown = false;
            _kWasDown = false;
            _tabWasDown = false;
            _shiftTabWasDown = false;
            _gWasDown = false;
            DualWieldBoneMirrorScript.MirrorVariant = 0;
            _nWasDown = false;
            _dwCombatMode = 0;
            _offsetsCaptured = false;
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
