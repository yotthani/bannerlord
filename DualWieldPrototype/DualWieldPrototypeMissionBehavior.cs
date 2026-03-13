using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DualWieldPrototype
{
    public sealed class DualWieldPrototypeMissionBehavior : MissionBehavior
    {
        internal enum ControlMode
        {
            AutoAlternate,
            SplitMouse
        }

        internal enum PlaybackMode
        {
            Channel0Combat,
            Channel1Overlay
        }

        internal enum RmbTriggerMode
        {
            DirectSlash,
            ReleaseFollowUp,
            PrimedSlashLeft,
            LegacyCycle
        }

        internal enum AttackVariant
        {
            SlashLeft,
            Thrust,
            FistSwingLeft,
            SlashRightProbe
        }

        internal enum TestActionMode
        {
            Sequence,
            SlashLeftOnly,
            ThrustOnly,
            FistLeftOnly
        }

        internal sealed class PlayerState
        {
            public Agent Agent;
            public EquipmentIndex MainSlot = EquipmentIndex.None;
            public EquipmentIndex OffhandSlot = EquipmentIndex.None;
            public ItemObject MainhandItem;
            public ItemObject OffhandItem;
            public string MainhandUsageId;
            public string OffhandUsageId;
            public int OffhandUsageIndex;
            public bool NextAttackUsesOffhand;
            public bool ConsumePrimaryAttackUntilRelease;
            public int ManualSequenceIndex;
            public int AlternateSequenceIndex;
            public float CooldownUntil;
            public int AttachConfigHash;
            public int LastChannel0ActionIndex = int.MinValue;
            public int LastChannel1ActionIndex = int.MinValue;
            public string LastLoggedLoadoutSignature;
            public bool PendingRightMouseSlash;
            public bool PendingRightMouseBootstrap;
            public int PendingRightMouseBootstrapTicks;
            public bool LastObservedLeftStance;
            public bool PendingRightMouseFollowUp;
            public int PendingRightMouseFollowUpDelay;
            public string LastQueuedReleaseActionName;
            public bool PendingRightMousePrimedSlash;
            public int PendingRightMousePrimedSlashDelay;
            public int LegacyCycleIndex;
            public string LastForcedOffhandActionName;
            public string PreviousForcedOffhandActionName;
        }

        private static readonly AttackVariant[] DefaultWeaponSequence =
        {
            AttackVariant.SlashLeft,
            AttackVariant.Thrust
        };

        private static readonly AttackVariant[] DaggerSequence =
        {
            AttackVariant.Thrust,
            AttackVariant.SlashLeft,
            AttackVariant.Thrust
        };

        private static readonly AttackVariant[] ThrustDominantSequence =
        {
            AttackVariant.Thrust,
            AttackVariant.SlashLeft,
            AttackVariant.Thrust
        };

        private static readonly AttackVariant[] FistSequence =
        {
            AttackVariant.FistSwingLeft
        };

        private static readonly AttackVariant[] LegacyDiagnosticSequence =
        {
            AttackVariant.SlashRightProbe,
            AttackVariant.Thrust,
            AttackVariant.SlashLeft
        };

        private readonly ActionIndexCache _quickSlashLeft = ActionIndexCache.Create("act_quick_release_slashleft_1h_left_stance");
        private readonly ActionIndexCache _quickSlashRightProbe = ActionIndexCache.Create("act_quick_release_slashright_1h_left_stance");
        private readonly ActionIndexCache _quickThrust = ActionIndexCache.Create("act_quick_release_thrust_1h_left_stance");
        private readonly ActionIndexCache _quickFistSwingLeft = ActionIndexCache.Create("act_quick_release_swingleft_fist_left_stance");
        private const int ReleaseFollowUpDelayTicks = 18;
        private const int PrimedSlashDelayTicks = 8;

        private ControlMode _controlMode = ControlMode.SplitMouse;
        private PlaybackMode _playbackMode = PlaybackMode.Channel0Combat;
        private RmbTriggerMode _rmbTriggerMode = RmbTriggerMode.DirectSlash;
        private TestActionMode _testActionMode = TestActionMode.Sequence;
        private PlayerState _playerState;
        private bool _loggedForcedChannel0;
        private bool _vWasDown;
        private string _lastLoggedSettingsSignature;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void AfterStart()
        {
            base.AfterStart();
            _playerState = new PlayerState();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!DualWieldPrototypeMissionFilters.IsSupportedMission(Mission))
            {
                ClearCurrentAttachment();
                return;
            }

            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            if (!settings.EnablePrototype)
            {
                ClearCurrentAttachment();
                return;
            }

            Agent mainAgent = Agent.Main;
            if (mainAgent == null || !mainAgent.IsActive())
            {
                return;
            }

            EnsurePlayerState(mainAgent);
            ApplySettings(settings);

            if (!TryRefreshLoadout(_playerState))
            {
                ClearCurrentAttachment();
                return;
            }

            EnsureOffhandAttached(_playerState);
            TrackActionChanges(_playerState);
            TrackLeftStanceTransitions(_playerState);
            TryQueuePendingRightMouseFollowUp(_playerState);
            ProcessPendingRightMouseFollowUp(_playerState);
            ProcessPendingRightMousePrimedSlash(_playerState);
            ProcessPendingRightMouseSlash(_playerState);
            ProcessManualComparisonInput(_playerState);
        }

        private void EnsurePlayerState(Agent mainAgent)
        {
            if (_playerState == null || _playerState.Agent != mainAgent)
            {
                _playerState = new PlayerState
                {
                    Agent = mainAgent
                };
            }
        }

        private void ApplySettings(DualWieldPrototypeSettings settings)
        {
            _controlMode = settings.ControlMode?.SelectedValue == "AutoAlternate"
                ? ControlMode.AutoAlternate
                : ControlMode.SplitMouse;

            _rmbTriggerMode = settings.RmbTriggerMode?.SelectedValue == "ReleaseFollowUp"
                ? RmbTriggerMode.ReleaseFollowUp
                : settings.RmbTriggerMode?.SelectedValue == "PrimedSlashLeft"
                    ? RmbTriggerMode.PrimedSlashLeft
                    : settings.RmbTriggerMode?.SelectedValue == "LegacyCycle"
                        ? RmbTriggerMode.LegacyCycle
                        : RmbTriggerMode.DirectSlash;

            _testActionMode = settings.OffHandTestAction?.SelectedValue switch
            {
                "SlashLeftOnly" => TestActionMode.SlashLeftOnly,
                "ThrustOnly" => TestActionMode.ThrustOnly,
                "FistLeftOnly" => TestActionMode.FistLeftOnly,
                _ => TestActionMode.Sequence
            };

            _playbackMode = PlaybackMode.Channel0Combat;
            if (settings.PlaybackMode?.SelectedValue == "Channel1Overlay" && !_loggedForcedChannel0)
            {
                _loggedForcedChannel0 = true;
                DualWieldPrototypeLogger.Log("playback_mode_forced channel=0 reason=left_stance_actions_only_work_on_channel0");
            }

            string settingsSignature =
                $"control={_controlMode} rmb={_rmbTriggerMode} testAction={_testActionMode} cooldown={settings.OffHandCooldownSeconds:0.00} " +
                $"ignorePriority={settings.IgnoreActionPriority} fallbackOverlay={settings.FallbackToOverlay}";
            if (settingsSignature != _lastLoggedSettingsSignature)
            {
                _lastLoggedSettingsSignature = settingsSignature;
                DualWieldPrototypeLogger.Log($"settings_applied {settingsSignature}");
            }
        }

        private bool TryRefreshLoadout(PlayerState state)
        {
            EquipmentIndex mainSlot = state.Agent.GetPrimaryWieldedItemIndex();
            if (mainSlot < EquipmentIndex.WeaponItemBeginSlot || mainSlot >= EquipmentIndex.ExtraWeaponSlot)
            {
                return false;
            }

            MissionWeapon mainWeapon = state.Agent.Equipment[mainSlot];
            if (!IsEligibleOffhandWeapon(mainWeapon))
            {
                return false;
            }

            EquipmentIndex offhandSlot = FindOffhandCandidate(state.Agent, mainSlot);
            if (offhandSlot == EquipmentIndex.None)
            {
                return false;
            }

            MissionWeapon offhandWeapon = state.Agent.Equipment[offhandSlot];
            if (!IsEligibleOffhandWeapon(offhandWeapon))
            {
                return false;
            }

            if (state.OffhandItem != null &&
                (state.OffhandSlot != offhandSlot || state.OffhandItem != offhandWeapon.Item || state.OffhandUsageIndex != offhandWeapon.CurrentUsageIndex))
            {
                RemoveManagedAttachment(state.Agent, state.OffhandItem);
            }

            state.MainSlot = mainSlot;
            state.MainhandItem = mainWeapon.Item;
            state.OffhandSlot = offhandSlot;
            state.OffhandItem = offhandWeapon.Item;
            state.MainhandUsageId = mainWeapon.CurrentUsageItem.ItemUsage ?? string.Empty;
            state.OffhandUsageId = offhandWeapon.CurrentUsageItem.ItemUsage ?? string.Empty;
            state.OffhandUsageIndex = offhandWeapon.CurrentUsageIndex;
            LogLoadoutIfChanged(state);
            return true;
        }

        private static void LogLoadoutIfChanged(PlayerState state)
        {
            string signature =
                $"{state.MainhandItem?.StringId}|{state.MainhandUsageId}|{state.OffhandItem?.StringId}|{state.OffhandUsageId}|leftStance={state.Agent.GetIsLeftStance()}";
            if (signature == state.LastLoggedLoadoutSignature)
            {
                return;
            }

            state.LastLoggedLoadoutSignature = signature;
            DualWieldPrototypeLogger.Log(
                $"loadout mainSlot={(int)state.MainSlot} mainItem={state.MainhandItem?.StringId ?? "none"} mainUsage={state.MainhandUsageId ?? "none"} " +
                $"offSlot={(int)state.OffhandSlot} offItem={state.OffhandItem?.StringId ?? "none"} offUsage={state.OffhandUsageId ?? "none"} " +
                $"leftStance={state.Agent.GetIsLeftStance()}");
        }

        private EquipmentIndex FindOffhandCandidate(Agent agent, EquipmentIndex mainSlot)
        {
            EquipmentIndex nativeOffhand = agent.GetOffhandWieldedItemIndex();
            if (nativeOffhand >= EquipmentIndex.WeaponItemBeginSlot &&
                nativeOffhand < EquipmentIndex.ExtraWeaponSlot &&
                nativeOffhand != mainSlot &&
                IsEligibleOffhandWeapon(agent.Equipment[nativeOffhand]))
            {
                return nativeOffhand;
            }

            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.ExtraWeaponSlot; slot++)
            {
                if (slot == mainSlot)
                {
                    continue;
                }

                MissionWeapon weapon = agent.Equipment[slot];
                if (IsEligibleOffhandWeapon(weapon))
                {
                    return slot;
                }
            }

            return EquipmentIndex.None;
        }

        private static bool IsEligibleOffhandWeapon(MissionWeapon weapon)
        {
            if (weapon.IsEmpty || weapon.CurrentUsageItem == null)
            {
                return false;
            }

            WeaponComponentData usage = weapon.CurrentUsageItem;
            if (!usage.IsMeleeWeapon || !usage.IsOneHanded || usage.IsShield || usage.IsConsumable)
            {
                return false;
            }

            return true;
        }

        private void EnsureOffhandAttached(PlayerState state)
        {
            int currentAttachHash = BuildAttachConfigHash();
            int existingAttachmentIndex = FindManagedAttachmentIndex(state.Agent, state.OffhandItem);
            if (existingAttachmentIndex >= 0 && state.AttachConfigHash == currentAttachHash)
            {
                return;
            }

            if (existingAttachmentIndex >= 0)
            {
                state.Agent.DeleteAttachedWeapon(existingAttachmentIndex);
            }

            MissionWeapon offhandWeapon = state.Agent.Equipment[state.OffhandSlot];
            if (offhandWeapon.IsEmpty)
            {
                return;
            }

            MatrixFrame attachFrame = offhandWeapon.CurrentUsageItem.Frame;
            ApplyAttachSettings(ref attachFrame);
            sbyte boneIndex = GetPreferredOffhandBone(state.Agent, offhandWeapon);
            state.Agent.AttachWeaponToBone(offhandWeapon, null, boneIndex, ref attachFrame);
            state.AttachConfigHash = currentAttachHash;
            DualWieldPrototypeSettings.DebugLog($"Attached off hand: {offhandWeapon.Item.Name}");
            DualWieldPrototypeLogger.Log($"attach item={offhandWeapon.Item.StringId} bone={boneIndex} preset={DualWieldPrototypeSettings.Get().RotationPreset} offset=({DualWieldPrototypeSettings.Get().OffsetX:0.00},{DualWieldPrototypeSettings.Get().OffsetY:0.00},{DualWieldPrototypeSettings.Get().OffsetZ:0.00})");
        }

        private static sbyte GetPreferredOffhandBone(Agent agent, MissionWeapon weapon)
        {
            ItemFlags attachmentMask = weapon.Item.ItemFlags & ItemFlags.AttachmentMask;
            if (attachmentMask != 0)
            {
                return agent.Monster.GetBoneToAttachForItemFlags(attachmentMask);
            }

            if (agent.Monster.OffHandItemBoneIndex >= 0)
            {
                return agent.Monster.OffHandItemBoneIndex;
            }

            return agent.Monster.OffHandBoneIndex;
        }

        internal bool ShouldAllowVanillaGameKeyDown(IInputContext inputContext, int gameKey)
        {
            bool isDown = inputContext.IsGameKeyDown(gameKey);
            if (gameKey != 9 && gameKey != 10)
            {
                return isDown;
            }

            if (!DualWieldPrototypeMissionFilters.IsSupportedMission(Mission))
            {
                return isDown;
            }

            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            if (!settings.EnablePrototype)
            {
                return isDown;
            }

            Agent mainAgent = Agent.Main;
            if (mainAgent == null || !mainAgent.IsActive())
            {
                return isDown;
            }

            EnsurePlayerState(mainAgent);
            ApplySettings(settings);
            if (!TryRefreshLoadout(_playerState))
            {
                _playerState.ConsumePrimaryAttackUntilRelease = false;
                return isDown;
            }

            if (gameKey == 9)
            {
                return ShouldAllowVanillaMainhandAttack(inputContext, isDown);
            }

            return ShouldAllowVanillaDefend(inputContext, isDown);
        }

        private bool ShouldAllowVanillaMainhandAttack(IInputContext inputContext, bool leftDown)
        {
            if (!leftDown)
            {
                _playerState.ConsumePrimaryAttackUntilRelease = false;
                return false;
            }

            if (_controlMode != ControlMode.AutoAlternate)
            {
                return true;
            }

            bool rightDown = inputContext.IsGameKeyDown(10);
            if (rightDown)
            {
                _playerState.ConsumePrimaryAttackUntilRelease = false;
                return true;
            }

            if (_playerState.ConsumePrimaryAttackUntilRelease)
            {
                return false;
            }

            bool leftPressed = inputContext.IsGameKeyPressed(9);
            if (!leftPressed)
            {
                return true;
            }

            if (!_playerState.NextAttackUsesOffhand)
            {
                _playerState.NextAttackUsesOffhand = true;
                DualWieldPrototypeLogger.Log("controltick_override mode=AutoAlternate arm_next_offhand=true");
                return true;
            }

            if (!TryStartOffhandAttack(_playerState, isAutoAlternate: true))
            {
                return true;
            }

            _playerState.NextAttackUsesOffhand = false;
            _playerState.ConsumePrimaryAttackUntilRelease = true;
            DualWieldPrototypeLogger.Log("controltick_override mode=AutoAlternate consume_mainhand=true");
            return false;
        }

        private bool ShouldAllowVanillaDefend(IInputContext inputContext, bool rightDown)
        {
            if (!rightDown)
            {
                _playerState.PendingRightMouseSlash = false;
                _playerState.PendingRightMouseBootstrap = false;
                _playerState.PendingRightMouseBootstrapTicks = 0;
                _playerState.PendingRightMouseFollowUp = false;
                _playerState.PendingRightMouseFollowUpDelay = 0;
                _playerState.PendingRightMousePrimedSlash = false;
                _playerState.PendingRightMousePrimedSlashDelay = 0;
                return false;
            }

            if (_controlMode != ControlMode.SplitMouse)
            {
                return true;
            }

            bool leftDown = inputContext.IsGameKeyDown(9);
            if (leftDown)
            {
                _playerState.PendingRightMouseSlash = false;
                _playerState.PendingRightMouseBootstrap = false;
                _playerState.PendingRightMouseBootstrapTicks = 0;
                _playerState.PendingRightMouseFollowUp = false;
                _playerState.PendingRightMouseFollowUpDelay = 0;
                _playerState.PendingRightMousePrimedSlash = false;
                _playerState.PendingRightMousePrimedSlashDelay = 0;
                return true;
            }

            if (_rmbTriggerMode == RmbTriggerMode.LegacyCycle)
            {
                if (!inputContext.IsGameKeyPressed(10))
                {
                    return false;
                }

                bool started = TryStartLegacyCycleAttack(_playerState);
                DualWieldPrototypeLogger.Log($"controltick_override mode=SplitMouse success={started} legacy=true");
                return !started;
            }

            if (inputContext.IsGameKeyPressed(10))
            {
                _playerState.PendingRightMouseSlash = true;
                _playerState.PendingRightMouseBootstrap = true;
                _playerState.PendingRightMouseBootstrapTicks = 6;
                _playerState.PendingRightMouseFollowUp = false;
                _playerState.PendingRightMouseFollowUpDelay = 0;
                _playerState.LastQueuedReleaseActionName = null;
                _playerState.PendingRightMousePrimedSlash = false;
                _playerState.PendingRightMousePrimedSlashDelay = 0;
                DualWieldPrototypeLogger.Log($"controltick_override mode=SplitMouse arm_rmb_slash leftStance={_playerState.Agent.GetIsLeftStance()} bootstrap_ticks={_playerState.PendingRightMouseBootstrapTicks}");
            }

            if (_playerState.PendingRightMouseBootstrap &&
                !_playerState.Agent.GetIsLeftStance() &&
                _playerState.PendingRightMouseBootstrapTicks > 0)
            {
                _playerState.PendingRightMouseBootstrapTicks--;
                DualWieldPrototypeLogger.Log($"controltick_override mode=SplitMouse bootstrap_vanilla_defend=true ticks_left={_playerState.PendingRightMouseBootstrapTicks}");
                return true;
            }

            _playerState.PendingRightMouseBootstrap = false;
            _playerState.PendingRightMouseBootstrapTicks = 0;

            return false;
        }

        private bool TryStartLegacyCycleAttack(PlayerState state)
        {
            if (state?.Agent == null || !state.Agent.IsActive())
            {
                return false;
            }

            int cycleStep = state.LegacyCycleIndex % LegacyDiagnosticSequence.Length;
            AttackVariant variant = LegacyDiagnosticSequence[cycleStep];
            string actionName = ResolveAction(variant).GetName();
            DualWieldPrototypeLogger.Log(
                $"legacy_cycle_probe step={cycleStep + 1}/{LegacyDiagnosticSequence.Length} action={actionName} " +
                $"leftStance={state.Agent.GetIsLeftStance()} ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()} " +
                $"prevForced={state.PreviousForcedOffhandActionName ?? "none"} lastForced={state.LastForcedOffhandActionName ?? "none"}");
            bool started = TryStartSpecificOffhandAttack(state, variant, $"legacy_cycle_step_{cycleStep + 1}");
            DualWieldPrototypeLogger.Log(
                $"legacy_cycle_resolved step={cycleStep + 1}/{LegacyDiagnosticSequence.Length} action={actionName} started={started} " +
                $"leftStance={state.Agent.GetIsLeftStance()}");
            if (started)
            {
                state.LegacyCycleIndex = (cycleStep + 1) % LegacyDiagnosticSequence.Length;
            }

            return started;
        }

        private bool TryStartOffhandAttack(PlayerState state, bool isAutoAlternate)
        {
            if (state == null || state.Agent == null || !state.Agent.IsActive())
            {
                return false;
            }

            if (Mission.CurrentTime < state.CooldownUntil)
            {
                return false;
            }

            ActionIndexCache action = GetNextAction(state, isAutoAlternate);
            string phaseTag = isAutoAlternate ? "auto" : "manual";
            return TryStartResolvedOffhandAttack(state, action, phaseTag);
        }

        private bool TryStartSpecificOffhandAttack(PlayerState state, AttackVariant variant, string phaseTag)
        {
            ActionIndexCache action = ResolveAction(variant);
            return TryStartResolvedOffhandAttack(state, action, phaseTag);
        }

        private bool TryStartResolvedOffhandAttack(PlayerState state, ActionIndexCache action, string phaseTag)
        {
            if (state == null || state.Agent == null || !state.Agent.IsActive())
            {
                return false;
            }

            if (Mission.CurrentTime < state.CooldownUntil)
            {
                return false;
            }

            if (action == ActionIndexCache.act_none)
            {
                return false;
            }

            string actionName = action.GetName();
            const int channel = 0;
            ActionIndexCache channel1Action = state.Agent.GetCurrentAction(1);
            DualWieldPrototypeLogger.Log(
                $"attack_request mode={_controlMode} playback={_playbackMode} channel={channel} action={actionName} " +
                $"mainItem={state.MainhandItem?.StringId ?? "none"} mainUsage={state.MainhandUsageId ?? "none"} " +
                $"offItem={state.OffhandItem?.StringId ?? "none"} offUsage={state.OffhandUsageId ?? "none"} " +
                $"leftStance={state.Agent.GetIsLeftStance()} ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={channel1Action.GetName()} " +
                $"prevForced={state.PreviousForcedOffhandActionName ?? "none"} lastForced={state.LastForcedOffhandActionName ?? "none"}");
            LogAttackDiagnostics(state, actionName, phaseTag, "pre");
            bool isLegacyCycleCall = _rmbTriggerMode == RmbTriggerMode.LegacyCycle && phaseTag.StartsWith("legacy_cycle");
            bool preserveChannel1 = isLegacyCycleCall;
            if (preserveChannel1)
            {
                DualWieldPrototypeLogger.Log($"channel1_preserve mode=LegacyCycle action={channel1Action.GetName()}");
            }
            else if (channel1Action != ActionIndexCache.act_none)
            {
                ActionIndexCache clearAction = ActionIndexCache.act_none;
                bool cleared = state.Agent.SetActionChannel(1, in clearAction, true, 0);
                DualWieldPrototypeLogger.Log($"channel1_clear before={channel1Action.GetName()} cleared={cleared}");
            }

            bool started = isLegacyCycleCall
                ? state.Agent.SetActionChannel(channel, in action, true, 0)
                : state.Agent.SetActionChannel(
                    channel,
                    in action,
                    true,
                    0,
                    0f,
                    1f,
                    0.03f,
                    0.15f,
                    0f);
            if (isLegacyCycleCall)
            {
                DualWieldPrototypeLogger.Log("legacy_cycle_call signature=minimal");
            }
            if (!started)
            {
                DualWieldPrototypeSettings.DebugLog($"Off-hand action failed: {action.GetName()}");
                DualWieldPrototypeLogger.Log($"attack_failed channel={channel} action={actionName}");
                return false;
            }

            state.CooldownUntil = Mission.CurrentTime + DualWieldPrototypeSettings.Get().OffHandCooldownSeconds;
            state.PreviousForcedOffhandActionName = state.LastForcedOffhandActionName;
            state.LastForcedOffhandActionName = actionName;
            DualWieldPrototypeLogger.Log($"attack_started action={actionName} cooldown_until={state.CooldownUntil:0.000}");
            LogAttackDiagnostics(state, actionName, phaseTag, "post");
            return true;
        }

        private ActionIndexCache GetNextAction(PlayerState state, bool isAutoAlternate)
        {
            if (_testActionMode != TestActionMode.Sequence)
            {
                return ResolveAction(_testActionMode switch
                {
                    TestActionMode.SlashLeftOnly => AttackVariant.SlashLeft,
                    TestActionMode.ThrustOnly => AttackVariant.Thrust,
                    TestActionMode.FistLeftOnly => AttackVariant.FistSwingLeft,
                    _ => AttackVariant.SlashLeft
                });
            }

            AttackVariant[] sequence = BuildAttackSequence(state.Agent.Equipment[state.OffhandSlot]);
            if (sequence.Length == 0)
            {
                sequence = FistSequence;
            }

            int index = isAutoAlternate ? state.AlternateSequenceIndex : state.ManualSequenceIndex;
            AttackVariant variant = sequence[index % sequence.Length];

            if (isAutoAlternate)
            {
                state.AlternateSequenceIndex = (index + 1) % sequence.Length;
            }
            else
            {
                state.ManualSequenceIndex = (index + 1) % sequence.Length;
            }

            return ResolveAction(variant);
        }

        private static AttackVariant[] BuildAttackSequence(MissionWeapon weapon)
        {
            if (weapon.IsEmpty || weapon.CurrentUsageItem == null)
            {
                return FistSequence;
            }

            string usage = weapon.CurrentUsageItem.ItemUsage ?? string.Empty;
            int swingDamage = weapon.GetModifiedSwingDamageForCurrentUsage();
            int thrustDamage = weapon.GetModifiedThrustDamageForCurrentUsage();

            if (usage.Contains("dagger"))
            {
                return DaggerSequence;
            }

            if (usage.Contains("rapier") || usage.Contains("degen") || swingDamage <= 0 || thrustDamage > swingDamage + 8)
            {
                return ThrustDominantSequence;
            }

            return DefaultWeaponSequence;
        }

        private static void LogAttackDiagnostics(PlayerState state, string actionName, string phaseTag, string edge)
        {
            if (!DualWieldPrototypeSettings.Get().DeepActionLogging || state?.Agent == null)
            {
                return;
            }

            Agent agent = state.Agent;
            DualWieldPrototypeLogger.Log(
                $"attack_diag phase={phaseTag} edge={edge} action={actionName} leftStance={agent.GetIsLeftStance()} " +
                $"move={agent.MovementFlags} defend={agent.GetDefendMovementFlag()} attackDir={agent.GetAttackDirection()} " +
                $"ch0Action={agent.GetCurrentAction(0).GetName()} ch0Type={agent.GetCurrentActionType(0)} ch0Stage={agent.GetCurrentActionStage(0)} " +
                $"ch0Prog={agent.GetCurrentActionProgress(0):0.00} ch0W={agent.GetActionChannelWeight(0):0.00} ch0CW={agent.GetActionChannelCurrentActionWeight(0):0.00} " +
                $"ch1Action={agent.GetCurrentAction(1).GetName()} ch1Type={agent.GetCurrentActionType(1)} ch1Stage={agent.GetCurrentActionStage(1)} " +
                $"ch1Prog={agent.GetCurrentActionProgress(1):0.00} ch1W={agent.GetActionChannelWeight(1):0.00} ch1CW={agent.GetActionChannelCurrentActionWeight(1):0.00}");
        }

        private void ProcessManualComparisonInput(PlayerState state)
        {
            if (_controlMode != ControlMode.SplitMouse || state?.Agent == null)
            {
                _vWasDown = false;
                return;
            }

            bool vDown = Input.IsKeyDown(InputKey.V);
            if (vDown && !_vWasDown)
            {
                bool started = TryStartSpecificOffhandAttack(state, AttackVariant.Thrust, "v_thrust");
                DualWieldPrototypeLogger.Log($"manual_compare key=V action=Thrust started={started}");
            }

            _vWasDown = vDown;
        }

        private void ProcessPendingRightMouseSlash(PlayerState state)
        {
            if (_controlMode != ControlMode.SplitMouse || state?.Agent == null || !state.PendingRightMouseSlash)
            {
                return;
            }

            if (_rmbTriggerMode == RmbTriggerMode.LegacyCycle)
            {
                return;
            }

            if (_rmbTriggerMode != RmbTriggerMode.DirectSlash)
            {
                return;
            }

            if (!Input.IsKeyDown(InputKey.RightMouseButton))
            {
                state.PendingRightMouseSlash = false;
                state.PendingRightMouseBootstrap = false;
                state.PendingRightMouseBootstrapTicks = 0;
                state.PendingRightMouseFollowUp = false;
                state.PendingRightMouseFollowUpDelay = 0;
                state.PendingRightMousePrimedSlash = false;
                state.PendingRightMousePrimedSlashDelay = 0;
                DualWieldPrototypeLogger.Log("pending_rmb_slash canceled reason=button_released");
                return;
            }

            bool leftStance = state.Agent.GetIsLeftStance();
            string channel1Action = state.Agent.GetCurrentAction(1).GetName();
            if (!leftStance)
            {
                return;
            }

            bool started = TryStartSpecificOffhandAttack(state, AttackVariant.SlashLeft, "rmb_slashleft");
            DualWieldPrototypeLogger.Log(
                $"pending_rmb_slash resolved started={started} leftStance={leftStance} ch1={channel1Action}");
            state.PendingRightMouseSlash = false;
            state.PendingRightMouseBootstrap = false;
            state.PendingRightMouseBootstrapTicks = 0;
        }

        private void TryQueuePendingRightMouseFollowUp(PlayerState state)
        {
            if (_controlMode != ControlMode.SplitMouse ||
                _rmbTriggerMode != RmbTriggerMode.ReleaseFollowUp ||
                state?.Agent == null ||
                !state.PendingRightMouseSlash ||
                state.PendingRightMouseFollowUp)
            {
                return;
            }

            if (!Input.IsKeyDown(InputKey.RightMouseButton))
            {
                return;
            }

            string channel1Action = state.Agent.GetCurrentAction(1).GetName();
            if (TryClassifyRightHandRelease(channel1Action) &&
                !string.Equals(channel1Action, state.LastQueuedReleaseActionName, System.StringComparison.Ordinal))
            {
                state.PendingRightMouseFollowUp = true;
                state.PendingRightMouseFollowUpDelay = ReleaseFollowUpDelayTicks;
                state.LastQueuedReleaseActionName = channel1Action;
                DualWieldPrototypeLogger.Log($"rmb_followup_queued source=ch1 action={channel1Action} delay={ReleaseFollowUpDelayTicks}");
                return;
            }

            string channel0Action = state.Agent.GetCurrentAction(0).GetName();
            if (TryClassifyRightHandRelease(channel0Action) &&
                !string.Equals(channel0Action, state.LastQueuedReleaseActionName, System.StringComparison.Ordinal))
            {
                state.PendingRightMouseFollowUp = true;
                state.PendingRightMouseFollowUpDelay = ReleaseFollowUpDelayTicks;
                state.LastQueuedReleaseActionName = channel0Action;
                DualWieldPrototypeLogger.Log($"rmb_followup_queued source=ch0 action={channel0Action} delay={ReleaseFollowUpDelayTicks}");
            }
        }

        private void ProcessPendingRightMouseFollowUp(PlayerState state)
        {
            if (_controlMode != ControlMode.SplitMouse ||
                _rmbTriggerMode != RmbTriggerMode.ReleaseFollowUp ||
                state?.Agent == null ||
                !state.PendingRightMouseFollowUp)
            {
                return;
            }

            if (!Input.IsKeyDown(InputKey.RightMouseButton))
            {
                state.PendingRightMouseSlash = false;
                state.PendingRightMouseBootstrap = false;
                state.PendingRightMouseBootstrapTicks = 0;
                state.PendingRightMouseFollowUp = false;
                state.PendingRightMouseFollowUpDelay = 0;
                state.PendingRightMousePrimedSlash = false;
                state.PendingRightMousePrimedSlashDelay = 0;
                DualWieldPrototypeLogger.Log("pending_rmb_followup canceled reason=button_released");
                return;
            }

            if (state.PendingRightMouseFollowUpDelay > 0)
            {
                state.PendingRightMouseFollowUpDelay--;
                return;
            }

            bool started = TryStartSpecificOffhandAttack(state, AttackVariant.SlashLeft, "rmb_followup");
            DualWieldPrototypeLogger.Log($"pending_rmb_followup resolved started={started} leftStance={state.Agent.GetIsLeftStance()}");
            state.PendingRightMouseSlash = false;
            state.PendingRightMouseBootstrap = false;
            state.PendingRightMouseBootstrapTicks = 0;
            state.PendingRightMouseFollowUp = false;
            state.PendingRightMouseFollowUpDelay = 0;
        }

        private void ProcessPendingRightMousePrimedSlash(PlayerState state)
        {
            if (_controlMode != ControlMode.SplitMouse ||
                _rmbTriggerMode != RmbTriggerMode.PrimedSlashLeft ||
                state?.Agent == null ||
                !state.PendingRightMouseSlash)
            {
                return;
            }

            if (!Input.IsKeyDown(InputKey.RightMouseButton))
            {
                state.PendingRightMouseSlash = false;
                state.PendingRightMouseBootstrap = false;
                state.PendingRightMouseBootstrapTicks = 0;
                state.PendingRightMousePrimedSlash = false;
                state.PendingRightMousePrimedSlashDelay = 0;
                DualWieldPrototypeLogger.Log("pending_rmb_primed canceled reason=button_released");
                return;
            }

            if (!state.PendingRightMousePrimedSlash)
            {
                if (!state.Agent.GetIsLeftStance())
                {
                    return;
                }

                bool thrustStarted = TryStartSpecificOffhandAttack(state, AttackVariant.Thrust, "rmb_prime_thrust");
                DualWieldPrototypeLogger.Log($"pending_rmb_primed thrust_started={thrustStarted} leftStance={state.Agent.GetIsLeftStance()}");
                if (!thrustStarted)
                {
                    return;
                }

                state.PendingRightMousePrimedSlash = true;
                state.PendingRightMousePrimedSlashDelay = PrimedSlashDelayTicks;
                state.CooldownUntil = Mission.CurrentTime;
                return;
            }

            if (state.PendingRightMousePrimedSlashDelay > 0)
            {
                state.PendingRightMousePrimedSlashDelay--;
                return;
            }

            bool slashStarted = TryStartSpecificOffhandAttack(state, AttackVariant.SlashLeft, "rmb_primed_slashleft");
            DualWieldPrototypeLogger.Log($"pending_rmb_primed slash_started={slashStarted} leftStance={state.Agent.GetIsLeftStance()}");
            state.PendingRightMouseSlash = false;
            state.PendingRightMouseBootstrap = false;
            state.PendingRightMouseBootstrapTicks = 0;
            state.PendingRightMousePrimedSlash = false;
            state.PendingRightMousePrimedSlashDelay = 0;
        }

        private static bool TryClassifyRightHandRelease(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                return false;
            }

            if (!actionName.Contains("release_") || !actionName.Contains("_1h"))
            {
                return false;
            }

            if (actionName.Contains("left_stance"))
            {
                return false;
            }

            if (actionName.Contains("_2h") || actionName.Contains("_lance") ||
                actionName.Contains("_staff") || actionName.Contains("_pike"))
            {
                return false;
            }

            return true;
        }

        private ActionIndexCache ResolveAction(AttackVariant variant)
        {
            switch (variant)
            {
                case AttackVariant.SlashLeft:
                    return _quickSlashLeft;
                case AttackVariant.SlashRightProbe:
                    return _quickSlashRightProbe;
                case AttackVariant.Thrust:
                    return _quickThrust;
                case AttackVariant.FistSwingLeft:
                    return _quickFistSwingLeft;
                default:
                    return ActionIndexCache.act_none;
            }
        }

        private int FindManagedAttachmentIndex(Agent agent, ItemObject item)
        {
            if (agent == null || item == null)
            {
                return -1;
            }

            sbyte offhandBone = agent.Monster.OffHandItemBoneIndex >= 0 ? agent.Monster.OffHandItemBoneIndex : agent.Monster.OffHandBoneIndex;
            for (int i = 0; i < agent.GetAttachedWeaponsCount(); i++)
            {
                MissionWeapon attachedWeapon = agent.GetAttachedWeapon(i);
                if (!attachedWeapon.IsEmpty &&
                    attachedWeapon.Item == item &&
                    agent.GetAttachedWeaponBoneIndex(i) == offhandBone)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RemoveManagedAttachment(Agent agent, ItemObject item)
        {
            int index = FindManagedAttachmentIndex(agent, item);
            if (index >= 0)
            {
                agent.DeleteAttachedWeapon(index);
            }
        }

        private void ClearCurrentAttachment()
        {
            if (_playerState?.Agent == null || _playerState.OffhandItem == null)
            {
                return;
            }

            RemoveManagedAttachment(_playerState.Agent, _playerState.OffhandItem);
            _playerState.OffhandItem = null;
            _playerState.OffhandSlot = EquipmentIndex.None;
            _playerState.AttachConfigHash = 0;
            DualWieldPrototypeLogger.Log("attachment_cleared");
        }

        private static int BuildAttachConfigHash()
        {
            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + settings.RotationPreset;
                hash = hash * 31 + settings.OffsetX.GetHashCode();
                hash = hash * 31 + settings.OffsetY.GetHashCode();
                hash = hash * 31 + settings.OffsetZ.GetHashCode();
                return hash;
            }
        }

        private static void ApplyAttachSettings(ref MatrixFrame attachFrame)
        {
            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            RotateByPreset(ref attachFrame, settings.RotationPreset);
            attachFrame.origin += new Vec3(settings.OffsetX, settings.OffsetY, settings.OffsetZ);
        }

        private static void RotateByPreset(ref MatrixFrame frame, int preset)
        {
            switch (preset)
            {
                case 1:
                    frame.rotation.RotateAboutSide((float)System.Math.PI);
                    break;
                case 2:
                    frame.rotation.RotateAboutForward((float)System.Math.PI);
                    break;
                case 3:
                    frame.rotation.RotateAboutUp((float)System.Math.PI);
                    break;
                case 4:
                    frame.rotation.RotateAboutForward((float)System.Math.PI / 2f);
                    break;
                case 5:
                    frame.rotation.RotateAboutForward(-(float)System.Math.PI / 2f);
                    break;
                case 6:
                    frame.rotation.RotateAboutSide((float)System.Math.PI / 2f);
                    break;
                case 7:
                    frame.rotation.RotateAboutSide(-(float)System.Math.PI / 2f);
                    break;
                case 8:
                    frame.rotation.RotateAboutUp((float)System.Math.PI / 2f);
                    break;
                case 9:
                    frame.rotation.RotateAboutUp(-(float)System.Math.PI / 2f);
                    break;
            }
        }

        private static void TrackActionChanges(PlayerState state)
        {
            ActionIndexCache current0 = state.Agent.GetCurrentAction(0);
            ActionIndexCache current1 = state.Agent.GetCurrentAction(1);

            if (current0.Index != state.LastChannel0ActionIndex || current1.Index != state.LastChannel1ActionIndex)
            {
                state.LastChannel0ActionIndex = current0.Index;
                state.LastChannel1ActionIndex = current1.Index;
                DualWieldPrototypeLogger.Log(
                    $"action_change ch0={current0.GetName()} p0={state.Agent.GetCurrentActionProgress(0):0.00} ch1={current1.GetName()} p1={state.Agent.GetCurrentActionProgress(1):0.00}");
            }
        }

        private static void TrackLeftStanceTransitions(PlayerState state)
        {
            bool leftStance = state.Agent.GetIsLeftStance();
            if (leftStance == state.LastObservedLeftStance)
            {
                return;
            }

            state.LastObservedLeftStance = leftStance;
            DualWieldPrototypeLogger.Log(
                $"leftstance_change value={leftStance} ch0={state.Agent.GetCurrentAction(0).GetName()} p0={state.Agent.GetCurrentActionProgress(0):0.00} " +
                $"ch1={state.Agent.GetCurrentAction(1).GetName()} p1={state.Agent.GetCurrentActionProgress(1):0.00} " +
                $"defend={state.Agent.GetDefendMovementFlag()} attackDir={state.Agent.GetAttackDirection()} " +
                $"prevForced={state.PreviousForcedOffhandActionName ?? "none"} lastForced={state.LastForcedOffhandActionName ?? "none"}");
        }

    }
}
