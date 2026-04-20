using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SiegeDismount
{
    /// <summary>
    /// Handles automatic dismounting when entering siege battles.
    /// Stores mount state and can restore it after the siege.
    /// </summary>
    public class SiegeDismountBehavior : MissionBehavior
    {
        private const string FEATURE_NAME = "SiegeDismount";

        // Stored mount state for auto-remount after siege
        private static EquipmentElement _storedMount;
        private static EquipmentElement _storedHarness;
        private static bool _wasPlayerMounted;
        private static bool _pendingRemount;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();

            if (!IsSiegeMission()) return;

            var behavior = SiegeDismountSettings.Get().GetSiegeMountBehavior();
            if (behavior == SiegeMountBehaviorType.Vanilla) return;

            Log($"Siege detected. Mount behavior: {behavior}");
            ApplyDismountBehavior(behavior);
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();

            if (_pendingRemount && _wasPlayerMounted)
            {
                RestorePlayerMount();
                _pendingRemount = false;
                _wasPlayerMounted = false;
            }
        }

        private static bool IsSiegeMission()
        {
            if (Mission.Current == null) return false;

            if (Mission.Current.IsSiegeBattle) return true;

            var sceneName = Mission.Current.SceneName?.ToLower() ?? "";
            if (sceneName.Contains("siege") || sceneName.Contains("wall") ||
                sceneName.Contains("gate") || sceneName.Contains("assault") ||
                sceneName.Contains("breach"))
            {
                return true;
            }

            return false;
        }

        private void ApplyDismountBehavior(SiegeMountBehaviorType behavior)
        {
            var player = Hero.MainHero;
            if (player == null) return;

            var equipment = player.BattleEquipment;
            if (equipment == null) return;

            var mountSlot = equipment[EquipmentIndex.Horse];
            var harnessSlot = equipment[EquipmentIndex.HorseHarness];

            if (mountSlot.IsEmpty)
            {
                Log("Player has no mount equipped. No action needed.");
                return;
            }

            _wasPlayerMounted = true;
            _storedMount = mountSlot;
            _storedHarness = harnessSlot;

            switch (behavior)
            {
                case SiegeMountBehaviorType.DismountKeepOnMap:
                    Log("Mount will spawn on map but player will be on foot.");
                    break;

                case SiegeMountBehaviorType.DismountToInventory:
                    RemoveMountFromBattleEquipment(equipment);
                    _pendingRemount = false;
                    Log("Mount moved to inventory for siege duration.");
                    break;

                case SiegeMountBehaviorType.AutoRemountAfter:
                    RemoveMountFromBattleEquipment(equipment);
                    _pendingRemount = true;
                    Log("Mount moved to inventory. Will restore after siege.");
                    break;
            }
        }

        private void RemoveMountFromBattleEquipment(Equipment equipment)
        {
            try
            {
                equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
                equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;

                var party = MobileParty.MainParty;
                if (party != null && !_storedMount.IsEmpty)
                {
                    party.ItemRoster.AddToCounts(_storedMount.Item, 1);
                    if (!_storedHarness.IsEmpty)
                    {
                        party.ItemRoster.AddToCounts(_storedHarness.Item, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error removing mount: {ex.Message}");
            }
        }

        private void RestorePlayerMount()
        {
            try
            {
                var player = Hero.MainHero;
                if (player == null) return;

                var equipment = player.BattleEquipment;
                if (equipment == null) return;

                if (!_storedMount.IsEmpty)
                {
                    equipment[EquipmentIndex.Horse] = _storedMount;
                    var party = MobileParty.MainParty;
                    if (party != null)
                    {
                        party.ItemRoster.AddToCounts(_storedMount.Item, -1);
                    }
                }

                if (!_storedHarness.IsEmpty)
                {
                    equipment[EquipmentIndex.HorseHarness] = _storedHarness;
                    var party = MobileParty.MainParty;
                    if (party != null)
                    {
                        party.ItemRoster.AddToCounts(_storedHarness.Item, -1);
                    }
                }

                Log("Mount restored after siege.");
            }
            catch (Exception ex)
            {
                Log($"Error restoring mount: {ex.Message}");
            }
        }

        public static void ClearStoredState()
        {
            _storedMount = EquipmentElement.Invalid;
            _storedHarness = EquipmentElement.Invalid;
            _wasPlayerMounted = false;
            _pendingRemount = false;
        }

        private static void Log(string message)
        {
            if (SiegeDismountSettings.Get().DebugMode)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] {message}", Colors.Cyan));
            }
        }
    }
}
