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
            public bool PendingSlashProxy;
            public float PendingSlashProxyArmedAt;
        }

        private readonly ActionIndexCache _leftFistProxy = ActionIndexCache.Create("act_quick_release_swingleft_fist_left_stance");
        private readonly ActionIndexCache _rightFistProxy = ActionIndexCache.Create("act_quick_release_swingright_fist");
        private readonly ActionIndexCache _leftSlashProxy = ActionIndexCache.Create("act_quick_release_slashleft_1h_left_stance");

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
                TrackActionChanges(_playerState);
                TrackLeftStanceTransitions(_playerState);
                return;
            }

            if (!TryRefreshLoadout(_playerState))
            {
                ClearCurrentAttachment();
                return;
            }

            EnsureOffhandAttached(_playerState);
            ProcessPendingSlashProxy(_playerState);
            TrackActionChanges(_playerState);
            TrackLeftStanceTransitions(_playerState);
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
                _playerState.PendingSlashProxy = false;
                _playerState.PendingSlashProxyArmedAt = 0f;
                _playerState.CooldownUntil = 0f;
                _playerState.LastChannel0ActionIndex = int.MinValue;
                _playerState.LastChannel1ActionIndex = int.MinValue;
                _playerState.LastLoggedLoadoutSignature = null;
                _playerState.LastLoggedUnarmedTraceSignature = null;
                _playerState.LastObservedLeftStance = false;
            }

            DualWieldPrototypeLogger.Log($"runtime_reset reason={reason}");
        }

        private void LogSettingsIfChanged(DualWieldPrototypeSettings settings)
        {
            string settingsSignature =
                $"proxyAction={settings.ProxyAttackAction?.SelectedValue ?? "LeftFistSwing"} cooldown={settings.OffHandCooldownSeconds:0.00} traceNative={settings.TraceNativeChannelCalls} " +
                $"unarmedTrace={settings.UnarmedTraceMode} live={settings.LiveMessages} gateSlash={settings.GateSlashProxyToLeftStance} fistCompare={settings.FistCompareMode}";
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
            if (!TryRefreshLoadout(_playerState))
            {
                _playerState.RightMouseConsumedUntilRelease = false;
                return isDown;
            }

            if (gameKey == 9)
            {
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

        private bool TryStartProxyAttack(PlayerState state, string phaseTag)
        {
            ActionIndexCache action = ResolveProxyAction();
            string proxyKind = DualWieldPrototypeSettings.Get().ProxyAttackAction?.SelectedValue ?? "LeftFistSwing";
            return TryStartSpecificProxyAttack(state, action, proxyKind, phaseTag);
        }

        private bool TryStartSpecificProxyAttack(PlayerState state, in ActionIndexCache action, string proxyKind, string phaseTag)
        {
            if (state == null || state.Agent == null || !state.Agent.IsActive())
            {
                return false;
            }

            if (action == ActionIndexCache.act_none)
            {
                return false;
            }

            if (Mission.CurrentTime < state.CooldownUntil)
            {
                return false;
            }

            string actionName = action.GetName();
            state.Agent.GetOldWieldedItemInfo(out int oldRightSlot, out int oldRightUsage, out int oldLeftSlot, out int oldLeftUsage);
            DualWieldPrototypeLogger.Log(
                $"attack_request mode=Proxy proxyKind={proxyKind} channel=0 action={actionName} mainItem={state.MainhandItem?.StringId ?? "none"} " +
                $"mainUsage={state.MainhandUsageId ?? "none"} offItem={state.OffhandItem?.StringId ?? "none"} offUsage={state.OffhandUsageId ?? "none"} " +
                $"leftStance={state.Agent.GetIsLeftStance()} actionSet={state.Agent.ActionSet.GetHashCode()} " +
                $"primaryWielded={(int)state.Agent.GetPrimaryWieldedItemIndex()} offhandWielded={(int)state.Agent.GetOffhandWieldedItemIndex()} " +
                $"oldRightSlot={oldRightSlot} oldRightUsage={oldRightUsage} oldLeftSlot={oldLeftSlot} oldLeftUsage={oldLeftUsage} " +
                $"ch0={state.Agent.GetCurrentAction(0).GetName()} ch1={state.Agent.GetCurrentAction(1).GetName()}");
            LogAttackDiagnostics(state, actionName, phaseTag, "pre");

            bool started;
            using (DualWieldPrototypeTraceContext.Push($"behavior:{phaseTag}:set_ch0"))
            {
                started = state.Agent.SetActionChannel(0, in action, true, 0);
            }

            if (!started)
            {
                DualWieldPrototypeLogger.Log($"attack_failed channel=0 action={actionName}");
                return false;
            }

            state.CooldownUntil = Mission.CurrentTime + DualWieldPrototypeSettings.Get().OffHandCooldownSeconds;
            DualWieldPrototypeLogger.Log($"attack_started action={actionName} cooldown_until={state.CooldownUntil:0.000}");
            LogAttackDiagnostics(state, actionName, phaseTag, "post");
            return true;
        }

        private ActionIndexCache ResolveProxyAction()
        {
            string selected = DualWieldPrototypeSettings.Get().ProxyAttackAction?.SelectedValue ?? "LeftFistSwing";
            if (selected == "SlashLeft1hLeftStance")
            {
                return _leftSlashProxy;
            }

            return _leftFistProxy;
        }

        private static bool ShouldGateSlashProxy()
        {
            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            return settings.GateSlashProxyToLeftStance &&
                   string.Equals(settings.ProxyAttackAction?.SelectedValue, "SlashLeft1hLeftStance", System.StringComparison.Ordinal);
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
    }
}
