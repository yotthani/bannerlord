using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem.Party;

namespace HeirOfNumenor.Features.SiegeDismount
{
    /// <summary>
    /// Handles automatic dismounting when entering siege battles.
    /// Stores mount state and can restore it after the siege.
    /// </summary>
    public class SiegeDismountBehavior : MissionBehavior
    {
        private static readonly string FEATURE_NAME = "SiegeDismount";
        
        // Stored mount state for auto-remount after siege
        private static EquipmentElement _storedMount;
        private static EquipmentElement _storedHarness;
        private static bool _wasPlayerMounted = false;
        private static bool _pendingRemount = false;
        
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        
        /// <summary>
        /// Called when mission starts. Checks if this is a siege and applies dismount behavior.
        /// </summary>
        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            
            if (!IsSiegeMission())
            {
                return;
            }
            
            var behavior = Settings.Instance?.GetSiegeMountBehavior() ?? SiegeMountBehavior.Vanilla;
            
            if (behavior == SiegeMountBehavior.Vanilla)
            {
                return;
            }
            
            Log($"Siege detected. Mount behavior: {behavior}");
            ApplyDismountBehavior(behavior);
        }
        
        /// <summary>
        /// Called when mission ends. Handles auto-remount if configured.
        /// </summary>
        public override void OnMissionEnded(IMission mission)
        {
            base.OnMissionEnded(mission);
            
            if (_pendingRemount && _wasPlayerMounted)
            {
                RestorePlayerMount();
                _pendingRemount = false;
                _wasPlayerMounted = false;
            }
        }
        
        /// <summary>
        /// Determines if the current mission is a siege battle.
        /// </summary>
        private bool IsSiegeMission()
        {
            if (Mission.Current == null)
                return false;
            
            // Check mission mode
            var mode = Mission.Current.Mode;
            if (mode == MissionMode.Battle)
            {
                // Check for siege-specific combat type
                var combatType = Mission.Current.CombatType;
                if (combatType == Mission.MissionCombatType.Combat)
                {
                    // Additional check: siege missions typically have siege engines or specific tags
                    // Check scene name or mission descriptor
                    var sceneName = Mission.Current.SceneName?.ToLower() ?? "";
                    if (sceneName.Contains("siege") || sceneName.Contains("wall") || sceneName.Contains("gate"))
                    {
                        return true;
                    }
                    
                    // Check if there's a siege event in campaign
                    if (Campaign.Current?.CurrentMissionData != null)
                    {
                        // Check campaign siege state
                        bool isCampaignSiege = false;
                        try 
                        {
                            if (Campaign.Current?.CurrentMenuContext != null)
                            {
                                var menuId = Campaign.Current.CurrentMenuContext.GameMenu?.StringId ?? "";
                                isCampaignSiege = menuId.Contains("siege") || menuId.Contains("assault");
                            }
                        }
                        catch { /* Campaign may not be active */ }
                        
                        if (isCampaignSiege)
                        {
                            Log("Campaign siege detected - dismount rules apply");
                        }
                    }
                }
            }
            
            // Check for assault/defense missions
            if (Mission.Current.IsSiegeBattle)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Applies the configured dismount behavior.
        /// </summary>
        private void ApplyDismountBehavior(SiegeMountBehavior behavior)
        {
            var player = Hero.MainHero;
            if (player == null) return;
            
            var equipment = player.BattleEquipment;
            if (equipment == null) return;
            
            // Check if player has a mount equipped
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
                case SiegeMountBehavior.DismountKeepOnMap:
                    // Horse will spawn in battle but player won't be mounted
                    // This is handled by spawn logic patch
                    Log("Mount will spawn on map but player will be on foot.");
                    break;
                    
                case SiegeMountBehavior.DismountToInventory:
                    // Temporarily remove mount from battle equipment
                    RemoveMountFromBattleEquipment(equipment);
                    _pendingRemount = true;
                    Log("Mount moved to inventory for siege duration.");
                    break;
                    
                case SiegeMountBehavior.AutoRemountAfter:
                    // Same as DismountToInventory but with auto-restore flag
                    RemoveMountFromBattleEquipment(equipment);
                    _pendingRemount = true;
                    Log("Mount moved to inventory. Will restore after siege.");
                    break;
            }
        }
        
        /// <summary>
        /// Removes the mount from battle equipment (moves to party inventory).
        /// </summary>
        private void RemoveMountFromBattleEquipment(Equipment equipment)
        {
            try
            {
                // Clear mount slots
                equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
                equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
                
                // Add to party inventory so it's not lost
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
        
        /// <summary>
        /// Restores the player's mount after siege ends.
        /// </summary>
        private void RestorePlayerMount()
        {
            try
            {
                var player = Hero.MainHero;
                if (player == null) return;
                
                var equipment = player.BattleEquipment;
                if (equipment == null) return;
                
                // Restore mount
                if (!_storedMount.IsEmpty)
                {
                    equipment[EquipmentIndex.Horse] = _storedMount;
                    
                    // Remove from inventory
                    var party = MobileParty.MainParty;
                    if (party != null)
                    {
                        party.ItemRoster.AddToCounts(_storedMount.Item, -1);
                    }
                }
                
                // Restore harness
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
        
        /// <summary>
        /// Clears stored mount state (call when switching save games, etc.)
        /// </summary>
        public static void ClearStoredState()
        {
            _storedMount = EquipmentElement.Invalid;
            _storedHarness = EquipmentElement.Invalid;
            _wasPlayerMounted = false;
            _pendingRemount = false;
        }
        
        private void Log(string message)
        {
            if (Settings.Instance?.DebugMode ?? false)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] {message}", Colors.Cyan));
            }
        }
    }
}
