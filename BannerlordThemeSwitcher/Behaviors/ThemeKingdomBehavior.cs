using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace BannerlordThemeSwitcher.Behaviors
{
    /// <summary>
    /// Campaign behavior that detects kingdom changes and triggers theme switches.
    /// </summary>
    public class ThemeKingdomBehavior : CampaignBehaviorBase
    {
        private static ThemeKingdomBehavior _instance;
        public static ThemeKingdomBehavior Instance => _instance;

        private string _lastKingdomId = null;

        public ThemeKingdomBehavior()
        {
            _instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No data to save - theme preference is in MCM settings
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Debug.Print("[ThemeSwitcher] Campaign session launched");
            CheckCurrentKingdom();
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            Debug.Print("[ThemeSwitcher] New game created");
            ApplyThemeForKingdom(null);
        }

        private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
        {
            // Only care about player's clan
            if (clan != Clan.PlayerClan)
                return;

            var newKingdomId = newKingdom?.StringId;
            Debug.Print($"[ThemeSwitcher] Player kingdom changed: {oldKingdom?.StringId ?? "none"} -> {newKingdomId ?? "none"}");
            
            ApplyThemeForKingdom(newKingdomId);
        }

        /// <summary>
        /// Checks current kingdom and applies theme. Called on load and settings change.
        /// </summary>
        public void CheckCurrentKingdom()
        {
            try
            {
                if (Clan.PlayerClan == null)
                {
                    Debug.Print("[ThemeSwitcher] No player clan yet");
                    return;
                }

                var kingdom = Clan.PlayerClan.Kingdom;
                var kingdomId = kingdom?.StringId;
                
                Debug.Print($"[ThemeSwitcher] Current kingdom: {kingdomId ?? "none"}");
                ApplyThemeForKingdom(kingdomId);
            }
            catch (Exception ex)
            {
                Debug.Print($"[ThemeSwitcher] Error checking kingdom: {ex.Message}");
            }
        }

        private void ApplyThemeForKingdom(string kingdomId)
        {
            // Avoid redundant switches
            if (_lastKingdomId == kingdomId)
                return;
            _lastKingdomId = kingdomId;

            ThemeManager.Instance?.OnKingdomChanged(kingdomId);
        }
    }
}
