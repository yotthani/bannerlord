using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DualWieldPrototype
{
    public sealed class DualWieldPrototypeMissionBehavior : MissionBehavior
    {
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
            public float CooldownUntil;
            public int AttachConfigHash;
            public int LastChannel0ActionIndex = int.MinValue;
            public int LastChannel1ActionIndex = int.MinValue;
            public string LastLoggedLoadoutSignature;
            public string LastLoggedUnarmedTraceSignature;
            public bool LastObservedLeftStance;
            public bool RightMouseConsumedUntilRelease;
            public bool LeftMouseConsumedUntilRelease;
            public bool VConsumedUntilRelease;
            public bool PendingSlashProxy;
            public float PendingSlashProxyArmedAt;
            public bool PendingFistThenSlash;
            public float PendingFistThenSlashAt;
            public bool PendingReadyRelease;
            public float PendingReadyReleaseAt;
            public ActionIndexCache PendingReadyReleaseAction;
            public string PendingReadyReleaseProxyKind;
            public string PendingReadyReleasePhaseTag;
            public string PendingReadyReleaseTrigger;
            public int PendingReadyReleaseChannel;
            public int FlowTraceSequence;
            public float FlowTraceUntil;
            public string FlowTraceLabel;
            public string LastFlowTraceSignature;
            public string LastLoggedNativeFistSelectionSignature;
            public string LastLoggedWieldSignature;
            public bool OffhandWieldProbeAttempted;
            public string LastOffhandWieldProbeMode;
        }

        private readonly ActionIndexCache _leftFistProxy = ActionIndexCache.Create("act_quick_release_swingleft_fist_left_stance");
        private readonly ActionIndexCache _rightFistProxy = ActionIndexCache.Create("act_quick_release_swingright_fist");
        private readonly ActionIndexCache _leftSlashProxy = ActionIndexCache.Create("act_quick_release_slashleft_1h_left_stance");
        private readonly ActionIndexCache _rightSlashLeftStanceProxy = ActionIndexCache.Create("act_quick_release_slashright_1h_left_stance");
        private readonly ActionIndexCache _rotDualLeftSlashProxy = ActionIndexCache.Create("act_dual_quick_release_slashleft_1h_left_stance");
        private readonly ActionIndexCache _rotDualLeftThrustProxy = ActionIndexCache.Create("act_dual_quick_release_thrust_1h_left_stance");

        private PlayerState _playerState;
        private string _lastLoggedSettingsSignature;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void AfterStart()
        {
            base.AfterStart();
            _playerState = new PlayerState();
        }

        public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
        {
            base.OnMissionModeChange(oldMissionMode, atStart);
            DualWieldPrototypeLogger.Log($"mission_mode_change old={oldMissionMode} current={Mission?.Mode} atStart={atStart}");
            if (DualWieldPrototypeMissionFilters.IsSupportedMission(Mission))
            {
                DualWieldPrototypeSubModule.EnsureRuntimePatches("mode_change_supported");
            }
            else
            {
                DualWieldPrototypeSubModule.DisableRuntimePatches("mode_change_unsupported");
                ResetRuntimeState(clearAttachment: true, reason: "mode_change_unsupported");
            }
        }

        public override void OnMissionStateDeactivated()
        {
            base.OnMissionStateDeactivated();
            DualWieldPrototypeSubModule.DisableRuntimePatches("mission_state_deactivated");
            ResetRuntimeState(clearAttachment: true, reason: "mission_state_deactivated");
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            DualWieldPrototypeSubModule.DisableRuntimePatches("clear_scene");
            ResetRuntimeState(clearAttachment: true, reason: "clear_scene");
        }

        protected override void OnEndMission()
        {
            DualWieldPrototypeSubModule.DisableRuntimePatches("end_mission");
            ResetRuntimeState(clearAttachment: true, reason: "end_mission");
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            DualWieldPrototypeSubModule.DisableRuntimePatches("remove_behavior");
            ResetRuntimeState(clearAttachment: true, reason: "remove_behavior");
            base.OnRemoveBehavior();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            if (!settings.EnablePrototype)
            {
                ClearCurrentAttachment();
                return;
            }

            Agent mainAgent = Agent.Main;
            if (!DualWieldPrototypeMissionFilters.IsSupportedCombatContext(Mission, mainAgent))
            {
                ClearCurrentAttachment();
                return;
            }

            EnsurePlayerState(mainAgent);
            LogSettingsIfChanged(settings);

            if (settings.UnarmedTraceMode)
            {
                ClearCurrentAttachment();
                LogUnarmedTraceStateIfChanged(_playerState);
                ProcessNativeUnarmedTrace(_playerState);
                TrackActionChanges(_playerState);
                TrackWieldStateChanges(_playerState);
                TrackLeftStanceTransitions(_playerState);
                TrackFlowTrace(_playerState);
                return;
            }

            if (!TryRefreshLoadout(_playerState))
            {
                ClearCurrentAttachment();
                return;
            }

            ProcessOffhandWieldProbe(_playerState);
            EnsureOffhandAttached(_playerState);
            HandleManualCompareInput(_playerState);
            ProcessPendingSlashProxy(_playerState);
            ProcessPendingFistThenSlash(_playerState);
            ProcessPendingReadyRelease(_playerState);
            TrackActionChanges(_playerState);
            TrackWieldStateChanges(_playerState);
            TrackLeftStanceTransitions(_playerState);
            TrackFlowTrace(_playerState);
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

        private void ResetRuntimeState(bool clearAttachment, string reason)
        {
            if (clearAttachment)
            {
                ClearCurrentAttachment();
            }

            if (_playerState != null)
            {
                _playerState.RightMouseConsumedUntilRelease = false;
                _playerState.LeftMouseConsumedUntilRelease = false;
                _playerState.VConsumedUntilRelease = false;
                _playerState.PendingSlashProxy = false;
                _playerState.PendingSlashProxyArmedAt = 0f;
                _playerState.PendingFistThenSlash = false;
                _playerState.PendingFistThenSlashAt = 0f;
                _playerState.PendingReadyRelease = false;
                _playerState.PendingReadyReleaseAt = 0f;
                _playerState.PendingReadyReleaseAction = ActionIndexCache.act_none;
                _playerState.PendingReadyReleaseProxyKind = null;
                _playerState.PendingReadyReleasePhaseTag = null;
                _playerState.PendingReadyReleaseTrigger = null;
                _playerState.PendingReadyReleaseChannel = 0;
                _playerState.FlowTraceUntil = 0f;
                _playerState.FlowTraceLabel = null;
                _playerState.LastFlowTraceSignature = null;
                _playerState.CooldownUntil = 0f;
                _playerState.LastChannel0ActionIndex = int.MinValue;
                _playerState.LastChannel1ActionIndex = int.MinValue;
                _playerState.LastLoggedLoadoutSignature = null;
                _playerState.LastLoggedUnarmedTraceSignature = null;
                _playerState.LastLoggedNativeFistSelectionSignature = null;
                _playerState.LastLoggedWieldSignature = null;
                _playerState.LastObservedLeftStance = false;
                _playerState.OffhandWieldProbeAttempted = false;
                _playerState.LastOffhandWieldProbeMode = null;
            }

            DualWieldPrototypeLogger.Log($"runtime_reset reason={reason}");
        }

        private void LogSettingsIfChanged(DualWieldPrototypeSettings settings)
        {
              string settingsSignature =
                  $"proxyAction={settings.ProxyAttackAction?.SelectedValue ?? "LeftFistSwing"} cooldown={settings.OffHandCooldownSeconds:0.00} traceNative={settings.TraceNativeChannelCalls} " +
                  $"unarmedTrace={settings.UnarmedTraceMode} live={settings.LiveMessages} gateSlash={settings.GateSlashProxyToLeftStance} " +
                  $"left1hCompareChannel={settings.Left1hCompareChannel?.SelectedValue ?? "Channel0"} " +
                  $"offhandProbe={settings.OffhandWieldProbeMode?.SelectedValue ?? "Disabled"} " +
                  $"resolverMainUsage={settings.ResolverMainUsage?.SelectedValue ?? "CurrentMainhandUsage"} resolverDirection={settings.ResolverDirection?.SelectedValue ?? "AttackLeft"} resolverForceLeftStance={settings.ResolverForceLeftStance} " +
                  $"primeSlashFlags={settings.PrimeSlashWithLeftFlags} slashAnimFlagMode={settings.SlashAnimFlagMode?.SelectedValue ?? "None"} " +
                  $"slashTransitionMode={settings.SlashTransitionMode?.SelectedValue ?? "Default"} slashFlowMode={settings.SlashFlowMode?.SelectedValue ?? "DirectQuickRelease"} " +
                  $"fistThenSlashDelay={settings.FistThenSlashDelaySeconds:0.000} " +
                  $"fistCompare={settings.FistCompareMode} leftProxyCompare={settings.LeftProxyCompareMode} left1hSlashCompare={settings.Left1hSlashCompareMode}";
            if (settingsSignature == _lastLoggedSettingsSignature)
            {
                return;
            }

            _lastLoggedSettingsSignature = settingsSignature;
            DualWieldPrototypeLogger.Log($"settings_applied {settingsSignature}");
        }

        internal bool ShouldAllowVanillaGameKeyDown(IInputContext inputContext, int gameKey)
        {
            bool isDown = inputContext.IsGameKeyDown(gameKey);
            if (gameKey != 9 && gameKey != 10)
            {
                return isDown;
            }

            Agent mainAgent = Agent.Main;
            if (!DualWieldPrototypeMissionFilters.IsSupportedCombatContext(Mission, mainAgent))
            {
                return isDown;
            }

            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            if (!settings.EnablePrototype || settings.UnarmedTraceMode)
            {
                return isDown;
            }

            EnsurePlayerState(mainAgent);
            // The input hook can run before the main mission tick has built a safe equipment state.
            // Only act on already-cached dual-wield state; otherwise fall back to vanilla input.
            if (_playerState == null ||
                _playerState.Agent != mainAgent ||
                _playerState.MainhandItem == null ||
                _playerState.OffhandItem == null ||
                _playerState.MainSlot < EquipmentIndex.WeaponItemBeginSlot ||
                _playerState.OffhandSlot < EquipmentIndex.WeaponItemBeginSlot)
            {
                _playerState.RightMouseConsumedUntilRelease = false;
                return isDown;
            }

            if (gameKey == 9)
            {
                if (settings.LeftProxyCompareMode)
                {
                    return ShouldAllowLeftProxyCompareAttack(inputContext, isDown);
                }

                if (settings.Left1hSlashCompareMode)
                {
                    return ShouldAllowLeft1hSlashCompareAttack(inputContext, isDown);
                }

                if (settings.FistCompareMode)
                {
                    return ShouldAllowVanillaAttack(inputContext, isDown);
                }

                return isDown;
            }

            return ShouldAllowVanillaDefend(inputContext, isDown);
        }

        private bool ShouldAllowVanillaAttack(IInputContext inputContext, bool leftDown)
        {
            if (!leftDown)
            {
                _playerState.LeftMouseConsumedUntilRelease = false;
                return false;
            }

            if (inputContext.IsGameKeyDown(10))
            {
                _playerState.LeftMouseConsumedUntilRelease = false;
                return true;
            }

            if (_playerState.LeftMouseConsumedUntilRelease)
            {
                return false;
            }

            if (!inputContext.IsGameKeyPressed(9))
            {
                return false;
            }

            bool started = TryStartSpecificProxyAttack(_playerState, _rightFistProxy, "RightFistSwing", "lmb_right_fist_proxy");
            DualWieldPrototypeLogger.Log($"controltick_override mode=FistCompare side=right success={started}");
            if (started)
            {
                _playerState.LeftMouseConsumedUntilRelease = true;
            }

            return false;
        }

        private bool ShouldAllowLeftProxyCompareAttack(IInputContext inputContext, bool leftDown)
        {
            if (!leftDown)
            {
                _playerState.LeftMouseConsumedUntilRelease = false;
                return false;
            }

            if (inputContext.IsGameKeyDown(10))
            {
                _playerState.LeftMouseConsumedUntilRelease = false;
                return true;
            }

            if (_playerState.LeftMouseConsumedUntilRelease)
            {
                return false;
            }

            if (!inputContext.IsGameKeyPressed(9))
            {
                return false;
            }

            bool started = TryStartSpecificProxyAttack(_playerState, _leftFistProxy, "LeftFistSwingCompare", "lmb_left_fist_compare");
            BeginFlowTrace(_playerState, "left_fist_compare");
            DualWieldPrototypeLogger.Log($"controltick_override mode=LeftProxyCompare side=leftFist success={started}");
            if (started)
            {
                _playerState.LeftMouseConsumedUntilRelease = true;
            }

            return false;
        }

        private bool ShouldAllowLeft1hSlashCompareAttack(IInputContext inputContext, bool leftDown)
        {
            if (!leftDown)
            {
                _playerState.LeftMouseConsumedUntilRelease = false;
                return false;
            }

            if (inputContext.IsGameKeyDown(10))
            {
                _playerState.LeftMouseConsumedUntilRelease = false;
                return true;
            }

            if (_playerState.LeftMouseConsumedUntilRelease)
            {
                return false;
            }

            if (!inputContext.IsGameKeyPressed(9))
            {
                return false;
            }

            bool started = TryStartSpecificProxyAttack(_playerState, _leftSlashProxy, "Left1hSlashLeftCompare", "lmb_left1h_slashleft_compare", targetChannel: ResolveLeft1hCompareChannel());
            BeginFlowTrace(_playerState, "left1h_slashleft_compare");
            DualWieldPrototypeLogger.Log($"controltick_override mode=Left1hSlashCompare side=slashleft channel={ResolveLeft1hCompareChannel()} success={started}");
            if (started)
            {
                _playerState.LeftMouseConsumedUntilRelease = true;
            }

            return false;
        }

        private bool ShouldAllowVanillaDefend(IInputContext inputContext, bool rightDown)
        {
            if (!rightDown)
            {
                _playerState.RightMouseConsumedUntilRelease = false;
                _playerState.PendingSlashProxy = false;
                _playerState.PendingSlashProxyArmedAt = 0f;
                return false;
            }

            if (inputContext.IsGameKeyDown(9))
            {
                _playerState.RightMouseConsumedUntilRelease = false;
                _playerState.PendingSlashProxy = false;
                _playerState.PendingSlashProxyArmedAt = 0f;
                return true;
            }

            if (_playerState.RightMouseConsumedUntilRelease)
            {
                return false;
            }

            if (!inputContext.IsGameKeyPressed(10))
            {
                return _playerState.PendingSlashProxy;
            }

            if (DualWieldPrototypeSettings.Get().LeftProxyCompareMode)
            {
                bool compareStarted = TryStartProxyAttack(_playerState, "rmb_left_slash_compare");
                BeginFlowTrace(_playerState, "left_slash_compare");
                DualWieldPrototypeLogger.Log(
                    $"controltick_override mode=LeftProxyCompare side={DualWieldPrototypeSettings.Get().ProxyAttackAction?.SelectedValue ?? "LeftFistSwing"} success={compareStarted}");
                if (compareStarted)
                {
                    _playerState.RightMouseConsumedUntilRelease = true;
                }

                return false;
            }

            if (DualWieldPrototypeSettings.Get().Left1hSlashCompareMode)
            {
                bool compareStarted = TryStartSpecificProxyAttack(_playerState, _rightSlashLeftStanceProxy, "Left1hSlashRightCompare", "rmb_left1h_slashright_compare", targetChannel: ResolveLeft1hCompareChannel());
                BeginFlowTrace(_playerState, "left1h_slashright_compare");
                DualWieldPrototypeLogger.Log("controltick_override mode=Left1hSlashCompare side=slashright channel=" + ResolveLeft1hCompareChannel() + " success=" + compareStarted);
                if (compareStarted)
                {
                    _playerState.RightMouseConsumedUntilRelease = true;
                }

                return false;
            }

            if (DualWieldPrototypeSettings.Get().FistCompareMode)
            {
                bool compareStarted = TryStartSpecificProxyAttack(_playerState, _leftFistProxy, "LeftFistSwingCompare", "rmb_left_fist_compare");
                DualWieldPrototypeLogger.Log($"controltick_override mode=FistCompare side=left success={compareStarted}");
                if (compareStarted)
                {
                    _playerState.RightMouseConsumedUntilRelease = true;
                }

                return false;
            }

            if (ShouldGateSlashProxy())
            {
                _playerState.PendingSlashProxy = true;
                _playerState.PendingSlashProxyArmedAt = Mission.CurrentTime;
                DualWieldPrototypeLogger.Log(
                    $"controltick_override mode=Proxy arm_pending_slash leftStance={_playerState.Agent.GetIsLeftStance()} " +
                    $"ch1={_playerState.Agent.GetCurrentAction(1).GetName()}");
                return true;
            }

            bool started = TryStartProxyAttack(_playerState, "rmb_proxy");
            DualWieldPrototypeLogger.Log($"controltick_override mode=Proxy success={started}");
            if (started)
            {
                _playerState.RightMouseConsumedUntilRelease = true;
            }

            return false;
        }

        private void HandleManualCompareInput(PlayerState state)
        {
            if (state?.Agent == null || !DualWieldPrototypeSettings.Get().LeftProxyCompareMode)
            {
                return;
            }

            if (!Input.IsKeyDown(InputKey.V))
            {
                state.VConsumedUntilRelease = false;
                state.PendingFistThenSlash = false;
                state.PendingFistThenSlashAt = 0f;
                return;
            }

            if (state.VConsumedUntilRelease || !Input.IsKeyPressed(InputKey.V))
            {
                return;
            }

            bool started = TryStartSpecificProxyAttack(state, _leftFistProxy, "LeftFistPrimeForSlashV", "v_left_fist_prime");
            BeginFlowTrace(state, "v_fist_then_slash");
            DualWieldPrototypeLogger.Log($"manual_compare key=V action=leftFistPrime success={started}");
            if (started)
            {
                float delay = DualWieldPrototypeSettings.Get().FistThenSlashDelaySeconds;
                state.PendingFistThenSlash = true;
                state.PendingFistThenSlashAt = Mission.CurrentTime + delay;
                DualWieldPrototypeLogger.Log(
                    $"pending_fist_then_slash armed at={state.PendingFistThenSlashAt:0.000} delay={delay:0.000} " +
                    $"leftStance={state.Agent.GetIsLeftStance()} ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()}");
                state.VConsumedUntilRelease = true;
            }
        }

        private void ProcessPendingSlashProxy(PlayerState state)
        {
            if (state == null || !state.PendingSlashProxy)
            {
                return;
            }

            if (!Input.IsKeyDown(InputKey.RightMouseButton))
            {
                state.PendingSlashProxy = false;
                state.PendingSlashProxyArmedAt = 0f;
                DualWieldPrototypeLogger.Log("pending_slash_proxy canceled reason=button_released");
                return;
            }

            bool leftStanceReady = state.Agent.GetIsLeftStance();
            float pendingAge = Mission.CurrentTime - state.PendingSlashProxyArmedAt;
            bool timeoutFallback = pendingAge >= 0.20f;

            if (!leftStanceReady && !timeoutFallback)
            {
                return;
            }

            if (leftStanceReady)
            {
                ClearChannel1PassiveDefendState(state, "pending_slash_proxy");
            }

            bool started = TryStartProxyAttack(state, "pending_slash_proxy");
            DualWieldPrototypeLogger.Log(
                $"pending_slash_proxy resolved started={started} leftStance={state.Agent.GetIsLeftStance()} " +
                $"timeoutFallback={timeoutFallback} age={pendingAge:0.000} ch1={state.Agent.GetCurrentAction(1).GetName()}");
            if (started)
            {
                state.RightMouseConsumedUntilRelease = true;
            }

            state.PendingSlashProxy = false;
            state.PendingSlashProxyArmedAt = 0f;
        }

        private void ProcessPendingFistThenSlash(PlayerState state)
        {
            if (state == null || !state.PendingFistThenSlash)
            {
                return;
            }

            if (!Input.IsKeyDown(InputKey.V))
            {
                state.PendingFistThenSlash = false;
                state.PendingFistThenSlashAt = 0f;
                DualWieldPrototypeLogger.Log("pending_fist_then_slash canceled reason=button_released");
                return;
            }

            if (Mission.CurrentTime < state.PendingFistThenSlashAt)
            {
                return;
            }

            bool started = TryStartSpecificProxyAttack(
                state,
                _leftSlashProxy,
                "SlashAfterLeftFistV",
                "v_fist_then_slash",
                suppressSlashAugment: true,
                ignoreCooldown: true);
            DualWieldPrototypeLogger.Log(
                $"pending_fist_then_slash resolved started={started} leftStance={state.Agent.GetIsLeftStance()} " +
                $"ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()}");
            state.PendingFistThenSlash = false;
            state.PendingFistThenSlashAt = 0f;
        }

        private void ProcessPendingReadyRelease(PlayerState state)
        {
            if (state == null || !state.PendingReadyRelease)
            {
                return;
            }

            bool stillHeld = IsTriggerStillHeld(state.PendingReadyReleaseTrigger);
            bool timeoutFallback = Mission.CurrentTime - state.PendingReadyReleaseAt >= 0.20f;
            if (stillHeld && !timeoutFallback)
            {
                return;
            }

            bool started = TryStartSpecificProxyAttack(
                state,
                state.PendingReadyReleaseAction,
                state.PendingReadyReleaseProxyKind ?? "ReadyRelease",
                state.PendingReadyReleasePhaseTag ?? "ready_release",
                suppressSlashAugment: false,
                ignoreCooldown: true,
                targetChannel: state.PendingReadyReleaseChannel);
            DualWieldPrototypeLogger.Log(
                $"pending_ready_release resolved started={started} trigger={state.PendingReadyReleaseTrigger ?? "auto"} " +
                $"channel={state.PendingReadyReleaseChannel} timeoutFallback={timeoutFallback} leftStance={state.Agent.GetIsLeftStance()} " +
                $"ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()}");
            state.PendingReadyRelease = false;
            state.PendingReadyReleaseAt = 0f;
            state.PendingReadyReleaseAction = ActionIndexCache.act_none;
            state.PendingReadyReleaseProxyKind = null;
            state.PendingReadyReleasePhaseTag = null;
            state.PendingReadyReleaseTrigger = null;
            state.PendingReadyReleaseChannel = 0;
        }

        private static void ClearChannel1PassiveDefendState(PlayerState state, string reason)
        {
            if (state?.Agent == null)
            {
                return;
            }

            ActionIndexCache current = state.Agent.GetCurrentAction(1);
            if (!ShouldClearChannel1BeforeSlash(state.Agent, current))
            {
                return;
            }

            string before = current.GetName();
            using (DualWieldPrototypeTraceContext.Push($"behavior:{reason}:clear_ch1"))
            {
                state.Agent.SetActionChannel(1, ActionIndexCache.act_none, true, 0);
            }

            DualWieldPrototypeLogger.Log(
                $"slash_proxy_clear_ch1 reason={reason} before={before} after={state.Agent.GetCurrentAction(1).GetName()}");
        }

        private static bool ShouldClearChannel1BeforeSlash(Agent agent, ActionIndexCache current)
        {
            string currentName = current.GetName();
            if (string.IsNullOrEmpty(currentName) || current == ActionIndexCache.act_none)
            {
                return false;
            }

            if (currentName.Contains("_passive_left_stance"))
            {
                return true;
            }

            string actionTypeName = agent.GetCurrentActionType(1).ToString();
            return actionTypeName.Contains("DefendUp1h") ||
                   actionTypeName.Contains("DefendDown1h") ||
                   actionTypeName.Contains("DefendLeft1h") ||
                   actionTypeName.Contains("DefendRight1h") ||
                   actionTypeName.Contains("Guard");
        }

        private bool TryRefreshLoadout(PlayerState state)
        {
            EquipmentIndex mainSlot;
            EquipmentIndex offhandSlot;
            if (!TryResolvePreferredTestLoadout(state.Agent, out mainSlot, out offhandSlot))
            {
                mainSlot = state.Agent.GetPrimaryWieldedItemIndex();
                if (mainSlot < EquipmentIndex.WeaponItemBeginSlot || mainSlot >= EquipmentIndex.ExtraWeaponSlot)
                {
                    return false;
                }

                offhandSlot = FindOffhandCandidate(state.Agent, mainSlot);
            }

            MissionWeapon mainWeapon = state.Agent.Equipment[mainSlot];
            if (!IsEligibleOffhandWeapon(mainWeapon))
            {
                return false;
            }

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

            bool loadoutChanged =
                state.MainSlot != mainSlot ||
                state.OffhandSlot != offhandSlot ||
                state.MainhandItem != mainWeapon.Item ||
                state.OffhandItem != offhandWeapon.Item ||
                state.MainhandUsageId != (mainWeapon.CurrentUsageItem.ItemUsage ?? string.Empty) ||
                state.OffhandUsageId != (offhandWeapon.CurrentUsageItem.ItemUsage ?? string.Empty);

            state.MainSlot = mainSlot;
            state.MainhandItem = mainWeapon.Item;
            state.OffhandSlot = offhandSlot;
            state.OffhandItem = offhandWeapon.Item;
            state.MainhandUsageId = mainWeapon.CurrentUsageItem.ItemUsage ?? string.Empty;
            state.OffhandUsageId = offhandWeapon.CurrentUsageItem.ItemUsage ?? string.Empty;
            state.OffhandUsageIndex = offhandWeapon.CurrentUsageIndex;
            if (loadoutChanged)
            {
                state.OffhandWieldProbeAttempted = false;
                state.LastOffhandWieldProbeMode = null;
                state.LastLoggedWieldSignature = null;
            }
            LogLoadoutIfChanged(state);
            return true;
        }

        private static bool TryResolvePreferredTestLoadout(Agent agent, out EquipmentIndex mainSlot, out EquipmentIndex offhandSlot)
        {
            mainSlot = EquipmentIndex.None;
            offhandSlot = EquipmentIndex.None;

            MissionWeapon slot0 = agent.Equipment[EquipmentIndex.Weapon0];
            MissionWeapon slot1 = agent.Equipment[EquipmentIndex.Weapon1];
            string slot0Id = slot0.Item?.StringId ?? string.Empty;
            string slot1Id = slot1.Item?.StringId ?? string.Empty;

            bool slot0LooksOffhand = IsDualWieldTestOffhand(slot0Id);
            bool slot1LooksMainhand = IsDualWieldTestMainhand(slot1Id);
            if (slot0LooksOffhand && slot1LooksMainhand)
            {
                mainSlot = EquipmentIndex.Weapon1;
                offhandSlot = EquipmentIndex.Weapon0;
                DualWieldPrototypeLogger.Log($"test_loadout_rot_slots main={(int)mainSlot}:{slot1Id} off={(int)offhandSlot}:{slot0Id}");
                return true;
            }

            bool slot0LooksMainhand = IsDualWieldTestMainhand(slot0Id);
            bool slot1LooksOffhand = IsDualWieldTestOffhand(slot1Id);
            if (slot0LooksMainhand && slot1LooksOffhand)
            {
                DualWieldPrototypeLogger.Log($"test_loadout_slot_warning expected=offhand@0 mainhand@1 actualMain={(int)EquipmentIndex.Weapon0}:{slot0Id} actualOff={(int)EquipmentIndex.Weapon1}:{slot1Id}");
            }

            return false;
        }

        private static bool IsDualWieldTestMainhand(string itemId)
        {
            return itemId == "dwp_control_mainhand" ||
                   itemId == "dwp_xml_mainhand" ||
                   itemId == "dwp_mainctx_mainhand" ||
                   itemId == "dwp_offctx_mainhand";
        }

        private static bool IsDualWieldTestOffhand(string itemId)
        {
            return itemId == "dwp_control_offhand" ||
                   itemId == "dwp_xml_offhand" ||
                   itemId == "dwp_mainctx_offhand" ||
                   itemId == "dwp_offctx_offhand";
        }

        private static void LogLoadoutIfChanged(PlayerState state)
        {
            string signature =
                $"{state.MainhandItem?.StringId}|{state.MainhandUsageId}|{state.OffhandItem?.StringId}|{state.OffhandUsageId}|leftStance={state.Agent.GetIsLeftStance()}|" +
                $"actionSet={state.Agent.ActionSet.GetHashCode()}|primary={(int)state.Agent.GetPrimaryWieldedItemIndex()}|offhand={(int)state.Agent.GetOffhandWieldedItemIndex()}";
            if (signature == state.LastLoggedLoadoutSignature)
            {
                return;
            }

            state.LastLoggedLoadoutSignature = signature;
            state.Agent.GetOldWieldedItemInfo(out int oldRightSlot, out int oldRightUsage, out int oldLeftSlot, out int oldLeftUsage);
            DualWieldPrototypeLogger.Log(
                $"loadout mainSlot={(int)state.MainSlot} mainItem={state.MainhandItem?.StringId ?? "none"} mainUsage={state.MainhandUsageId ?? "none"} " +
                $"offSlot={(int)state.OffhandSlot} offItem={state.OffhandItem?.StringId ?? "none"} offUsage={state.OffhandUsageId ?? "none"} " +
                $"leftStance={state.Agent.GetIsLeftStance()} actionSet={state.Agent.ActionSet.GetHashCode()} " +
                $"primaryWielded={(int)state.Agent.GetPrimaryWieldedItemIndex()} offhandWielded={(int)state.Agent.GetOffhandWieldedItemIndex()} " +
                $"oldRightSlot={oldRightSlot} oldRightUsage={oldRightUsage} oldLeftSlot={oldLeftSlot} oldLeftUsage={oldLeftUsage}");
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

                if (IsEligibleOffhandWeapon(agent.Equipment[slot]))
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
            if (state.Agent.GetOffhandWieldedItemIndex() == state.OffhandSlot)
            {
                RemoveManagedAttachment(state.Agent, state.OffhandItem);
                return;
            }

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
            DualWieldPrototypeLogger.Log(
                $"attach item={offhandWeapon.Item.StringId} bone={boneIndex} preset={DualWieldPrototypeSettings.Get().RotationPreset} " +
                $"offset=({DualWieldPrototypeSettings.Get().OffsetX:0.00},{DualWieldPrototypeSettings.Get().OffsetY:0.00},{DualWieldPrototypeSettings.Get().OffsetZ:0.00})");
        }

        private void ProcessOffhandWieldProbe(PlayerState state)
        {
            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            string mode = settings.OffhandWieldProbeMode?.SelectedValue ?? "Disabled";
            if (mode != state.LastOffhandWieldProbeMode)
            {
                state.LastOffhandWieldProbeMode = mode;
                state.OffhandWieldProbeAttempted = false;
            }

            if (string.Equals(mode, "Disabled", System.StringComparison.Ordinal) || state.OffhandWieldProbeAttempted)
            {
                return;
            }

            if (state.MainSlot < EquipmentIndex.WeaponItemBeginSlot ||
                state.OffhandSlot < EquipmentIndex.WeaponItemBeginSlot ||
                state.Agent.GetPrimaryWieldedItemIndex() != state.MainSlot ||
                state.Agent.GetOffhandWieldedItemIndex() != EquipmentIndex.None)
            {
                return;
            }

            Agent.WeaponWieldActionType wieldType;
            switch (mode)
            {
                case "Instant":
                    wieldType = Agent.WeaponWieldActionType.Instant;
                    break;
                case "InstantAfterPickUp":
                    wieldType = Agent.WeaponWieldActionType.InstantAfterPickUp;
                    break;
                case "WithAnimation":
                    wieldType = Agent.WeaponWieldActionType.WithAnimation;
                    break;
                default:
                    return;
            }

            state.OffhandWieldProbeAttempted = true;
            state.Agent.GetOldWieldedItemInfo(out int oldRightSlotBefore, out int oldRightUsageBefore, out int oldLeftSlotBefore, out int oldLeftUsageBefore);
            DualWieldPrototypeLogger.Log(
                $"offhand_probe_begin mode={mode} mainSlot={(int)state.MainSlot} offSlot={(int)state.OffhandSlot} " +
                $"primary={(int)state.Agent.GetPrimaryWieldedItemIndex()} offhand={(int)state.Agent.GetOffhandWieldedItemIndex()} " +
                $"oldRightSlot={oldRightSlotBefore} oldRightUsage={oldRightUsageBefore} oldLeftSlot={oldLeftSlotBefore} oldLeftUsage={oldLeftUsageBefore}");

            state.Agent.TryToWieldWeaponInSlot(state.OffhandSlot, wieldType, isWieldedOnSpawn: false);

            state.Agent.GetOldWieldedItemInfo(out int oldRightSlotAfter, out int oldRightUsageAfter, out int oldLeftSlotAfter, out int oldLeftUsageAfter);
            DualWieldPrototypeLogger.Log(
                $"offhand_probe_end mode={mode} mainSlot={(int)state.MainSlot} offSlot={(int)state.OffhandSlot} " +
                $"primary={(int)state.Agent.GetPrimaryWieldedItemIndex()} offhand={(int)state.Agent.GetOffhandWieldedItemIndex()} " +
                $"oldRightSlot={oldRightSlotAfter} oldRightUsage={oldRightUsageAfter} oldLeftSlot={oldLeftSlotAfter} oldLeftUsage={oldLeftUsageAfter}");
        }

        private bool TryStartProxyAttack(PlayerState state, string phaseTag)
        {
            string proxyKind = DualWieldPrototypeSettings.Get().ProxyAttackAction?.SelectedValue ?? "LeftFistSwing";
            if (string.Equals(proxyKind, "ResolvedMainUsageAttackLeft", System.StringComparison.Ordinal))
            {
                return TryStartResolvedMainUsageAttack(state, ResolveConfiguredDirection(), proxyKind, phaseTag);
            }

            ActionIndexCache action = ResolveProxyAction();
            return TryStartSpecificProxyAttack(state, action, proxyKind, phaseTag);
        }

        private bool TryStartResolvedMainUsageAttack(PlayerState state, Agent.UsageDirection direction, string proxyKind, string phaseTag)
        {
            if (state == null || state.Agent == null || !state.Agent.IsActive())
            {
                return false;
            }

            if (Mission.CurrentTime < state.CooldownUntil)
            {
                return false;
            }

            string mainUsage = state.MainhandUsageId;
            string resolverMainUsage = ResolveConfiguredMainUsage(mainUsage);
            string offUsage = state.OffhandUsageId;
            int offUsageIndexProbe = string.IsNullOrEmpty(offUsage) ? -1 : MBItem.GetItemUsageIndex(offUsage);
            int leftHandUsageSetIndex = -1;
            bool isLeftStance = DualWieldPrototypeSettings.Get().ResolverForceLeftStance || state.Agent.GetIsLeftStance();
            ActionIndexCache resolvedAction = MBItem.GetItemUsageReloadActionCode(
                resolverMainUsage,
                (int)direction,
                state.Agent.HasMount,
                leftHandUsageSetIndex,
                isLeftStance,
                state.Agent.IsLookDirectionLow);
            int strikeType = MBItem.GetItemUsageStrikeType(
                resolverMainUsage,
                (int)direction,
                state.Agent.HasMount,
                leftHandUsageSetIndex,
                isLeftStance,
                state.Agent.IsLookDirectionLow);

            DualWieldPrototypeLogger.Log(
                $"usage_resolver mainUsage={mainUsage ?? "none"} resolverMainUsage={resolverMainUsage ?? "none"} " +
                $"offUsage={offUsage ?? "none"} direction={direction} " +
                $"offUsageIndexProbe={offUsageIndexProbe} leftHandUsageSetIndex={leftHandUsageSetIndex} " +
                $"isLeftStance={isLeftStance} lowLook={state.Agent.IsLookDirectionLow} " +
                $"resolvedAction={resolvedAction.GetName()} strikeType={strikeType}");

            if (resolvedAction == ActionIndexCache.act_none)
            {
                return false;
            }

            return TryStartSpecificProxyAttack(state, resolvedAction, proxyKind, $"{phaseTag}:resolved", suppressSlashAugment: true, ignoreCooldown: false);
        }

        private static string ResolveConfiguredMainUsage(string currentMainUsage)
        {
            string selected = DualWieldPrototypeSettings.Get().ResolverMainUsage?.SelectedValue ?? "CurrentMainhandUsage";
            if (string.Equals(selected, "onehanded_block_shield_swing", System.StringComparison.Ordinal))
            {
                return "onehanded_block_shield_swing";
            }

            if (string.Equals(selected, "onehanded_block_shield_swing_thrust", System.StringComparison.Ordinal))
            {
                return "onehanded_block_shield_swing_thrust";
            }

            if (string.Equals(currentMainUsage, "dwp_dual_swing_thrust", System.StringComparison.Ordinal))
            {
                return "onehanded_block_shield_swing_thrust";
            }

            if (string.Equals(currentMainUsage, "dwp_dual_swing", System.StringComparison.Ordinal))
            {
                return "onehanded_block_shield_swing";
            }

            return currentMainUsage;
        }

        private static Agent.UsageDirection ResolveConfiguredDirection()
        {
            string selected = DualWieldPrototypeSettings.Get().ResolverDirection?.SelectedValue ?? "AttackLeft";
            if (string.Equals(selected, "AttackUp", System.StringComparison.Ordinal))
            {
                return Agent.UsageDirection.AttackUp;
            }

            if (string.Equals(selected, "AttackDown", System.StringComparison.Ordinal))
            {
                return Agent.UsageDirection.AttackDown;
            }

            if (string.Equals(selected, "AttackRight", System.StringComparison.Ordinal))
            {
                return Agent.UsageDirection.AttackRight;
            }

            return Agent.UsageDirection.AttackLeft;
        }

        private static int ResolveLeft1hCompareChannel()
        {
            return string.Equals(
                DualWieldPrototypeSettings.Get().Left1hCompareChannel?.SelectedValue,
                "Channel1",
                System.StringComparison.Ordinal)
                ? 1
                : 0;
        }

        private bool TryStartSpecificProxyAttack(PlayerState state, in ActionIndexCache action, string proxyKind, string phaseTag)
        {
            return TryStartSpecificProxyAttack(state, action, proxyKind, phaseTag, suppressSlashAugment: false, ignoreCooldown: false, targetChannel: 0);
        }

        private bool TryStartSpecificProxyAttack(PlayerState state, in ActionIndexCache action, string proxyKind, string phaseTag, int targetChannel)
        {
            return TryStartSpecificProxyAttack(state, action, proxyKind, phaseTag, suppressSlashAugment: false, ignoreCooldown: false, targetChannel: targetChannel);
        }

        private bool TryStartSpecificProxyAttack(PlayerState state, in ActionIndexCache action, string proxyKind, string phaseTag, bool suppressSlashAugment)
        {
            return TryStartSpecificProxyAttack(state, action, proxyKind, phaseTag, suppressSlashAugment, ignoreCooldown: false, targetChannel: 0);
        }

        private bool TryStartSpecificProxyAttack(PlayerState state, in ActionIndexCache action, string proxyKind, string phaseTag, bool suppressSlashAugment, bool ignoreCooldown)
        {
            return TryStartSpecificProxyAttack(state, action, proxyKind, phaseTag, suppressSlashAugment, ignoreCooldown, targetChannel: 0);
        }

        private bool TryStartSpecificProxyAttack(PlayerState state, in ActionIndexCache action, string proxyKind, string phaseTag, bool suppressSlashAugment, bool ignoreCooldown, int targetChannel)
        {
            if (state == null || state.Agent == null || !state.Agent.IsActive())
            {
                return false;
            }

            if (action == ActionIndexCache.act_none)
            {
                return false;
            }

            if (!ignoreCooldown && Mission.CurrentTime < state.CooldownUntil)
            {
                return false;
            }

            string actionName = action.GetName();
            if (ShouldUseReadyThenRelease(actionName) &&
                TryGetReadyReleasePair(actionName, out ActionIndexCache readyAction, out ActionIndexCache releaseAction))
            {
                bool readyStarted = TryStartSpecificProxyAttack(state, readyAction, $"{proxyKind}Ready", $"{phaseTag}:ready", suppressSlashAugment: true, ignoreCooldown: ignoreCooldown, targetChannel: targetChannel);
                if (!readyStarted)
                {
                    return false;
                }

                state.PendingReadyRelease = true;
                state.PendingReadyReleaseAt = Mission.CurrentTime;
                state.PendingReadyReleaseAction = releaseAction;
                state.PendingReadyReleaseProxyKind = $"{proxyKind}Release";
                state.PendingReadyReleasePhaseTag = $"{phaseTag}:release";
                state.PendingReadyReleaseTrigger = ResolveTriggerSource(phaseTag);
                state.PendingReadyReleaseChannel = targetChannel;
                DualWieldPrototypeLogger.Log(
                    $"pending_ready_release armed trigger={state.PendingReadyReleaseTrigger ?? "auto"} action={releaseAction.GetName()} " +
                    $"channel={targetChannel} leftStance={state.Agent.GetIsLeftStance()} ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()}");
                return true;
            }

            AnimFlags additionalFlags = suppressSlashAugment ? 0 : ResolveAdditionalAnimFlags(actionName);
            bool useInstantTransition = ShouldUseInstantSlashTransition(actionName);
            if (!suppressSlashAugment)
            {
                PrimeSlashProxyStateIfNeeded(state, actionName, phaseTag);
            }
            state.Agent.GetOldWieldedItemInfo(out int oldRightSlot, out int oldRightUsage, out int oldLeftSlot, out int oldLeftUsage);
            DualWieldPrototypeLogger.Log(
                $"attack_request mode=Proxy proxyKind={proxyKind} channel={targetChannel} action={actionName} mainItem={state.MainhandItem?.StringId ?? "none"} " +
                $"mainUsage={state.MainhandUsageId ?? "none"} offItem={state.OffhandItem?.StringId ?? "none"} offUsage={state.OffhandUsageId ?? "none"} " +
                $"leftStance={state.Agent.GetIsLeftStance()} actionSet={state.Agent.ActionSet.GetHashCode()} " +
                $"primaryWielded={(int)state.Agent.GetPrimaryWieldedItemIndex()} offhandWielded={(int)state.Agent.GetOffhandWieldedItemIndex()} " +
                $"oldRightSlot={oldRightSlot} oldRightUsage={oldRightUsage} oldLeftSlot={oldLeftSlot} oldLeftUsage={oldLeftUsage} " +
                $"ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()} " +
                $"animFlags={additionalFlags} slashAnimFlagMode={DualWieldPrototypeSettings.Get().SlashAnimFlagMode?.SelectedValue ?? "None"} " +
                $"slashTransitionMode={DualWieldPrototypeSettings.Get().SlashTransitionMode?.SelectedValue ?? "Default"} " +
                $"instantTransition={useInstantTransition} suppressSlashAugment={suppressSlashAugment} ignoreCooldown={ignoreCooldown}");
            LogAttackDiagnostics(state, actionName, phaseTag, "pre");

            bool started;
            using (DualWieldPrototypeTraceContext.Push($"behavior:{phaseTag}:set_ch{targetChannel}"))
            {
                started = useInstantTransition
                    ? state.Agent.SetActionChannel(targetChannel, in action, true, additionalFlags, 0f, 1f, 0f, 0f, 0f)
                    : state.Agent.SetActionChannel(targetChannel, in action, true, additionalFlags);
            }

            if (!started)
            {
                DualWieldPrototypeLogger.Log($"attack_failed channel={targetChannel} action={actionName}");
                return false;
            }

            state.CooldownUntil = Mission.CurrentTime + DualWieldPrototypeSettings.Get().OffHandCooldownSeconds;
            DualWieldPrototypeLogger.Log($"attack_started channel={targetChannel} action={actionName} cooldown_until={state.CooldownUntil:0.000}");
            LogAttackDiagnostics(state, actionName, phaseTag, "post");
            return true;
        }

        private static AnimFlags ResolveAdditionalAnimFlags(string actionName)
        {
            if (!string.Equals(actionName, "act_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_quick_release_slashright_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_dual_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_dual_quick_release_thrust_1h_left_stance", System.StringComparison.Ordinal))
            {
                return 0;
            }

            string selected = DualWieldPrototypeSettings.Get().SlashAnimFlagMode?.SelectedValue ?? "None";
            if (string.Equals(selected, "UseLeftHandDuringAttack", System.StringComparison.Ordinal))
            {
                return AnimFlags.anf_use_left_hand_during_attack;
            }

            return 0;
        }

        private static bool ShouldUseInstantSlashTransition(string actionName)
        {
            if (!string.Equals(actionName, "act_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_quick_release_slashright_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_dual_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_dual_quick_release_thrust_1h_left_stance", System.StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(
                DualWieldPrototypeSettings.Get().SlashTransitionMode?.SelectedValue,
                "Instant",
                System.StringComparison.Ordinal);
        }

        private static bool ShouldUseReadyThenRelease(string actionName)
        {
            if (!string.Equals(actionName, "act_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_quick_release_slashright_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_dual_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_dual_quick_release_thrust_1h_left_stance", System.StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(
                DualWieldPrototypeSettings.Get().SlashFlowMode?.SelectedValue,
                "ReadyThenRelease",
                System.StringComparison.Ordinal);
        }

        private static bool TryGetReadyReleasePair(string actionName, out ActionIndexCache readyAction, out ActionIndexCache releaseAction)
        {
            if (string.Equals(actionName, "act_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal))
            {
                readyAction = ActionIndexCache.Create("act_ready_slashleft_1h_left_stance");
                releaseAction = ActionIndexCache.Create("act_release_slashleft_1h_left_stance");
                return true;
            }

            if (string.Equals(actionName, "act_quick_release_slashright_1h_left_stance", System.StringComparison.Ordinal))
            {
                readyAction = ActionIndexCache.Create("act_ready_slashright_1h_left_stance");
                releaseAction = ActionIndexCache.Create("act_release_slashright_1h_left_stance");
                return true;
            }

            if (string.Equals(actionName, "act_dual_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal))
            {
                readyAction = ActionIndexCache.Create("act_dual_ready_slashleft_1h_left_stance");
                releaseAction = ActionIndexCache.Create("act_dual_release_slashleft_1h_left_stance");
                return true;
            }

            if (string.Equals(actionName, "act_dual_quick_release_thrust_1h_left_stance", System.StringComparison.Ordinal))
            {
                readyAction = ActionIndexCache.Create("act_dual_ready_thrust_1h_left_stance");
                releaseAction = ActionIndexCache.Create("act_dual_release_thrust_1h_left_stance");
                return true;
            }

            readyAction = ActionIndexCache.act_none;
            releaseAction = ActionIndexCache.act_none;
            return false;
        }

        private static string ResolveTriggerSource(string phaseTag)
        {
            if (string.IsNullOrEmpty(phaseTag))
            {
                return "auto";
            }

            if (phaseTag.StartsWith("v_", System.StringComparison.Ordinal))
            {
                return "v";
            }

            if (phaseTag.IndexOf("rmb", System.StringComparison.Ordinal) >= 0 ||
                phaseTag.IndexOf("pending_slash_proxy", System.StringComparison.Ordinal) >= 0)
            {
                return "rmb";
            }

            return "auto";
        }

        private static bool IsTriggerStillHeld(string trigger)
        {
            if (string.Equals(trigger, "v", System.StringComparison.Ordinal))
            {
                return Input.IsKeyDown(InputKey.V);
            }

            if (string.Equals(trigger, "rmb", System.StringComparison.Ordinal))
            {
                return Input.IsKeyDown(InputKey.RightMouseButton);
            }

            return false;
        }

        private ActionIndexCache ResolveProxyAction()
        {
            string selected = DualWieldPrototypeSettings.Get().ProxyAttackAction?.SelectedValue ?? "LeftFistSwing";
            if (selected == "SlashLeft1hLeftStance")
            {
                return _leftSlashProxy;
            }

            if (selected == "ResolvedMainUsageAttackLeft")
            {
                return ActionIndexCache.act_none;
            }

            if (selected == "RotDualSlashLeft1hLeftStance")
            {
                return _rotDualLeftSlashProxy;
            }

            if (selected == "RotDualThrust1hLeftStance")
            {
                return _rotDualLeftThrustProxy;
            }

            return _leftFistProxy;
        }

        private static bool ShouldGateSlashProxy()
        {
            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            return settings.GateSlashProxyToLeftStance &&
                   (string.Equals(settings.ProxyAttackAction?.SelectedValue, "SlashLeft1hLeftStance", System.StringComparison.Ordinal) ||
                    string.Equals(settings.ProxyAttackAction?.SelectedValue, "ResolvedMainUsageAttackLeft", System.StringComparison.Ordinal) ||
                    string.Equals(settings.ProxyAttackAction?.SelectedValue, "RotDualSlashLeft1hLeftStance", System.StringComparison.Ordinal) ||
                    string.Equals(settings.ProxyAttackAction?.SelectedValue, "RotDualThrust1hLeftStance", System.StringComparison.Ordinal));
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
                $"primaryWielded={(int)agent.GetPrimaryWieldedItemIndex()} offhandWielded={(int)agent.GetOffhandWieldedItemIndex()} actionSet={agent.ActionSet.GetHashCode()} " +
                $"ch0Action={agent.GetCurrentAction(0).GetName()} ch0Type={agent.GetCurrentActionType(0)} ch0Stage={agent.GetCurrentActionStage(0)} " +
                $"ch0Prog={agent.GetCurrentActionProgress(0):0.00} ch0W={agent.GetActionChannelWeight(0):0.00} ch0CW={agent.GetActionChannelCurrentActionWeight(0):0.00} " +
                $"ch1Action={agent.GetCurrentAction(1).GetName()} ch1Type={agent.GetCurrentActionType(1)} ch1Stage={agent.GetCurrentActionStage(1)} " +
                $"ch1Prog={agent.GetCurrentActionProgress(1):0.00} ch1W={agent.GetActionChannelWeight(1):0.00} ch1CW={agent.GetActionChannelCurrentActionWeight(1):0.00}");
        }

        private static void PrimeSlashProxyStateIfNeeded(PlayerState state, string actionName, string phaseTag)
        {
            if (state?.Agent == null || !DualWieldPrototypeSettings.Get().PrimeSlashWithLeftFlags)
            {
                return;
            }

            if (!string.Equals(actionName, "act_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_dual_quick_release_slashleft_1h_left_stance", System.StringComparison.Ordinal) &&
                !string.Equals(actionName, "act_dual_quick_release_thrust_1h_left_stance", System.StringComparison.Ordinal))
            {
                return;
            }

            Agent agent = state.Agent;
            Agent.MovementControlFlag originalFlags = agent.MovementFlags;
            Agent.MovementControlFlag strippedFlags = originalFlags & ~(Agent.MovementControlFlag.AttackMask | Agent.MovementControlFlag.DefendMask);
            Agent.MovementControlFlag primedFlags =
                strippedFlags |
                agent.AttackDirectionToMovementFlag(Agent.UsageDirection.AttackLeft);

            if (primedFlags == originalFlags)
            {
                DualWieldPrototypeLogger.Log(
                    $"slash_prime_flags phase={phaseTag} unchanged=true before={originalFlags} after={primedFlags} " +
                    $"attackDir={agent.GetAttackDirection()} defend={agent.GetDefendMovementFlag()}");
                return;
            }

            agent.MovementFlags = primedFlags;
            DualWieldPrototypeLogger.Log(
                $"slash_prime_flags phase={phaseTag} unchanged=false before={originalFlags} after={primedFlags} " +
                $"attackDir={agent.GetAttackDirection()} defend={agent.GetDefendMovementFlag()}");
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

        private static void LogUnarmedTraceStateIfChanged(PlayerState state)
        {
            if (state?.Agent == null)
            {
                return;
            }

            EquipmentIndex primary = state.Agent.GetPrimaryWieldedItemIndex();
            EquipmentIndex offhand = state.Agent.GetOffhandWieldedItemIndex();
            string signature =
                $"primary={(int)primary} offhand={(int)offhand} leftStance={state.Agent.GetIsLeftStance()} " +
                $"ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()}";
            if (signature == state.LastLoggedUnarmedTraceSignature)
            {
                return;
            }

            state.LastLoggedUnarmedTraceSignature = signature;
            DualWieldPrototypeLogger.Log($"unarmed_trace_state {signature}");
        }

        private void ProcessNativeUnarmedTrace(PlayerState state)
        {
            if (state?.Agent == null || !DualWieldPrototypeSettings.Get().DeepActionLogging)
            {
                return;
            }

            if (Input.IsKeyPressed(InputKey.LeftMouseButton))
            {
                BeginFlowTrace(state, "native_unarmed_lmb");
            }

            if (Input.IsKeyPressed(InputKey.RightMouseButton))
            {
                BeginFlowTrace(state, "native_unarmed_rmb");
            }

            ActionIndexCache current0 = state.Agent.GetCurrentAction(0);
            ActionIndexCache current1 = state.Agent.GetCurrentAction(1);
            string action0 = current0.GetName();
            string action1 = current1.GetName();
            int trackedChannel = GetTrackedNativeFistAttackChannel(state.Agent, action0, action1);
            if (trackedChannel < 0)
            {
                return;
            }

            ActionIndexCache trackedAction = trackedChannel == 0 ? current0 : current1;
            string trackedActionName = trackedAction.GetName();
            string signature =
                $"ch={trackedChannel}|action={trackedActionName}|type={state.Agent.GetCurrentActionType(trackedChannel)}|stage={state.Agent.GetCurrentActionStage(trackedChannel)}|" +
                $"ls={state.Agent.GetIsLeftStance()}|move={state.Agent.GetMovementDirection()}|def={state.Agent.GetDefendMovementFlag()}|atk={state.Agent.GetAttackDirection()}";
            if (signature == state.LastLoggedNativeFistSelectionSignature)
            {
                return;
            }

            state.LastLoggedNativeFistSelectionSignature = signature;
            DualWieldPrototypeLogger.Log(
                $"native_fist_selection channel={trackedChannel} action={trackedActionName} type={state.Agent.GetCurrentActionType(trackedChannel)} stage={state.Agent.GetCurrentActionStage(trackedChannel)} " +
                $"ch0={action0} ch1={action1} leftStance={state.Agent.GetIsLeftStance()} " +
                $"move={state.Agent.GetMovementDirection()} defend={state.Agent.GetDefendMovementFlag()} attackDir={state.Agent.GetAttackDirection()} " +
                $"primary={(int)state.Agent.GetPrimaryWieldedItemIndex()} offhand={(int)state.Agent.GetOffhandWieldedItemIndex()} " +
                $"actionSet={state.Agent.ActionSet.GetHashCode()}");

            string label = IsLeftNativeFistAttackAction(trackedActionName)
                ? "native_left_fist_attack"
                : "native_right_fist_attack";
            BeginFlowTrace(state, label);
        }

        private static int GetTrackedNativeFistAttackChannel(Agent agent, string action0, string action1)
        {
            if (IsTrackedNativeFistAttackAction(action0, agent.GetCurrentActionType(0)))
            {
                return 0;
            }

            if (IsTrackedNativeFistAttackAction(action1, agent.GetCurrentActionType(1)))
            {
                return 1;
            }

            return -1;
        }

        private static bool IsTrackedNativeFistAttackAction(string actionName, Agent.ActionCodeType actionType)
        {
            if (string.IsNullOrEmpty(actionName) ||
                actionName.IndexOf("fist", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (actionName.IndexOf("guard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                actionName.IndexOf("passive", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                actionName.IndexOf("defend", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            string actionTypeName = actionType.ToString();
            if (actionTypeName.IndexOf("Guard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                actionTypeName.IndexOf("Defend", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return actionName.IndexOf("quick_release", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actionName.IndexOf("release_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actionName.IndexOf("ready_", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actionName.IndexOf("swing", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actionName.IndexOf("thrust", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actionName.IndexOf("uppercut", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actionName.IndexOf("direct_fist", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLeftNativeFistAttackAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                return false;
            }

            return actionName.IndexOf("left_stance", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actionName.IndexOf("swingleft_fist", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   actionName.IndexOf("_left_fist", System.StringComparison.OrdinalIgnoreCase) >= 0;
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

        private static void TrackWieldStateChanges(PlayerState state)
        {
            state.Agent.GetOldWieldedItemInfo(out int oldRightSlot, out int oldRightUsage, out int oldLeftSlot, out int oldLeftUsage);
            string signature =
                $"primary={(int)state.Agent.GetPrimaryWieldedItemIndex()}|offhand={(int)state.Agent.GetOffhandWieldedItemIndex()}|" +
                $"oldRightSlot={oldRightSlot}|oldRightUsage={oldRightUsage}|oldLeftSlot={oldLeftSlot}|oldLeftUsage={oldLeftUsage}";
            if (signature == state.LastLoggedWieldSignature)
            {
                return;
            }

            state.LastLoggedWieldSignature = signature;
            DualWieldPrototypeLogger.Log(
                $"wield_state primary={(int)state.Agent.GetPrimaryWieldedItemIndex()} offhand={(int)state.Agent.GetOffhandWieldedItemIndex()} " +
                $"oldRightSlot={oldRightSlot} oldRightUsage={oldRightUsage} oldLeftSlot={oldLeftSlot} oldLeftUsage={oldLeftUsage}");
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
                $"defend={state.Agent.GetDefendMovementFlag()} attackDir={state.Agent.GetAttackDirection()}");
        }

        private void BeginFlowTrace(PlayerState state, string label)
        {
            if (state?.Agent == null || !DualWieldPrototypeSettings.Get().DeepActionLogging)
            {
                return;
            }

            state.FlowTraceSequence++;
            state.FlowTraceLabel = label;
            state.FlowTraceUntil = Mission.CurrentTime + 0.65f;
            state.LastFlowTraceSignature = null;
            DualWieldPrototypeLogger.Log(
                $"flow_trace_begin id={state.FlowTraceSequence} label={label} until={state.FlowTraceUntil:0.000} " +
                $"leftStance={state.Agent.GetIsLeftStance()} ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()}");
            TrackFlowTrace(state, force: true);
        }

        private void TrackFlowTrace(PlayerState state, bool force = false)
        {
            if (state?.Agent == null || !DualWieldPrototypeSettings.Get().DeepActionLogging)
            {
                return;
            }

            if (!force && Mission.CurrentTime > state.FlowTraceUntil)
            {
                return;
            }

            Agent agent = state.Agent;
            string signature =
                $"leftStance={agent.GetIsLeftStance()} move={agent.MovementFlags} defend={agent.GetDefendMovementFlag()} attackDir={agent.GetAttackDirection()} " +
                $"primary={(int)agent.GetPrimaryWieldedItemIndex()} offhand={(int)agent.GetOffhandWieldedItemIndex()} " +
                $"ch0={agent.GetCurrentAction(0).GetName()}|{agent.GetCurrentActionType(0)}|{agent.GetCurrentActionStage(0)}|{agent.GetCurrentActionProgress(0):0.00}|{agent.GetActionChannelWeight(0):0.00}|{agent.GetActionChannelCurrentActionWeight(0):0.00} " +
                $"ch1={agent.GetCurrentAction(1).GetName()}|{agent.GetCurrentActionType(1)}|{agent.GetCurrentActionStage(1)}|{agent.GetCurrentActionProgress(1):0.00}|{agent.GetActionChannelWeight(1):0.00}|{agent.GetActionChannelCurrentActionWeight(1):0.00}";
            if (!force && signature == state.LastFlowTraceSignature)
            {
                return;
            }

            state.LastFlowTraceSignature = signature;
            DualWieldPrototypeLogger.Log(
                $"flow_trace id={state.FlowTraceSequence} label={state.FlowTraceLabel ?? "none"} t={Mission.CurrentTime:0.000} {signature}");
        }
    }
}
