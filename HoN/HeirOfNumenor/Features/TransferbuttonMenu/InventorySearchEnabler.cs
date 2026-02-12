using System;
using System.Reflection;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.ScreenSystem;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.TransferbuttonMenu
{
    /// <summary>
    /// Campaign behavior that persists the inventory search state.
    /// The search boxes allow filtering items in both player and other inventory panels.
    /// Based on InventorySearchEnabler mod - integrated with MCM settings.
    /// </summary>
    public class InventorySearchBehavior : CampaignBehaviorBase
    {
        private const string FEATURE_NAME = "InventorySearch";

        private static InventorySearchBehavior _instance;
        public static InventorySearchBehavior Instance => _instance;

        // Persisted state - whether search is available
        [SaveableField(1)]
        internal bool IsSearchAvailable = true;

        // Reflection cache
        private readonly FieldInfo _dataSourceField;
        private GauntletInventoryScreen _lastScreen;

        public InventorySearchBehavior()
        {
            _instance = this;
            
            // Cache reflection field
            _dataSourceField = typeof(GauntletInventoryScreen).GetField(
                "_dataSource", 
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public override void RegisterEvents()
        {
            // No campaign events needed - we use OnApplicationTick from SubModule
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("HoN_IsSearchAvailable", ref IsSearchAvailable);
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "SyncData error", ex);
            }
        }

        /// <summary>
        /// Called every application tick to sync search state with inventory screen.
        /// </summary>
        public void OnTick()
        {
            // Check if feature is enabled in MCM
            if (!IsFeatureEnabled()) return;

            try
            {
                // Check if inventory screen is open
                if (!(ScreenManager.TopScreen is GauntletInventoryScreen topScreen))
                    return;

                if (_dataSourceField == null) return;

                // Get the ViewModel
                var spInventoryVm = _dataSourceField.GetValue(topScreen) as SPInventoryVM;
                if (spInventoryVm == null) return;

                // Sync state
                if (_lastScreen != topScreen)
                {
                    // Screen changed - apply our saved state to the VM
                    if (spInventoryVm.IsSearchAvailable != IsSearchAvailable)
                    {
                        spInventoryVm.IsSearchAvailable = IsSearchAvailable;
                        // Note: SetValue not needed since SPInventoryVM is a reference type
                    }
                    _lastScreen = topScreen;
                }
                else
                {
                    // Same screen - check if user toggled search (sync back to our state)
                    if (spInventoryVm.IsSearchAvailable != IsSearchAvailable)
                    {
                        IsSearchAvailable = spInventoryVm.IsSearchAvailable;
                        _lastScreen = null; // Reset to re-sync next tick
                    }
                }
            }
            catch (Exception ex)
            {
                ModSettings.DebugLog(FEATURE_NAME, $"Tick error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if the feature is enabled in MCM settings.
        /// </summary>
        private bool IsFeatureEnabled()
        {
            try
            {
                return ModSettings.Get()?.EnableInventorySearch ?? true;
            }
            catch
            {
                return true; // Default enabled
            }
        }

        /// <summary>
        /// Programmatically enable search.
        /// </summary>
        public void EnableSearch()
        {
            IsSearchAvailable = true;
            _lastScreen = null; // Force re-sync
        }

        /// <summary>
        /// Programmatically disable search.
        /// </summary>
        public void DisableSearch()
        {
            IsSearchAvailable = false;
            _lastScreen = null; // Force re-sync
        }

        /// <summary>
        /// Toggle search state.
        /// </summary>
        public void ToggleSearch()
        {
            IsSearchAvailable = !IsSearchAvailable;
            _lastScreen = null; // Force re-sync
        }
    }
}

