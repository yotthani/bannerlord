using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace HeirOfNumenor.Features.SmithingExtended
{
    /// <summary>
    /// Campaign behavior for Smithing Extended feature.
    /// Handles save/load and initialization.
    /// Uses central ModSettings for configuration.
    /// </summary>
    public class SmithingExtendedCampaignBehavior : CampaignBehaviorBase
    {
        private const string FEATURE_NAME = "SmithingExtended";
        
        private static SmithingExtendedCampaignBehavior _instance;
        public static SmithingExtendedCampaignBehavior Instance => _instance;

        /// <summary>
        /// Check if Smithing Extended is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableSmithingExtended; }
                catch { return true; }
            }
        }

        /// <summary>
        /// Unique items data for save/load.
        /// </summary>
        [SaveableField(1)]
        private Dictionary<string, HoNUniqueItemData> _uniqueItems;

        /// <summary>
        /// Tracks total items crafted (for stats).
        /// </summary>
        [SaveableField(2)]
        private int _totalItemsCrafted;

        /// <summary>
        /// Tracks total unique items created.
        /// </summary>
        [SaveableField(3)]
        private int _totalUniquesCrafted;

        /// <summary>
        /// Tracks total repairs performed.
        /// </summary>
        [SaveableField(4)]
        private int _totalRepairs;

        public SmithingExtendedCampaignBehavior()
        {
            _instance = this;
            _uniqueItems = new Dictionary<string, HoNUniqueItemData>();
            _totalItemsCrafted = 0;
            _totalUniquesCrafted = 0;
            _totalRepairs = 0;
        }

        public override void RegisterEvents()
        {
            SafeExecutor.Execute(FEATURE_NAME, "RegisterEvents", () =>
            {
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
                CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            });
        }

        public override void SyncData(IDataStore dataStore)
        {
            SafeExecutor.Execute(FEATURE_NAME, "SyncData", () =>
            {
                dataStore.SyncData("SmithExt_UniqueItems", ref _uniqueItems);
                dataStore.SyncData("SmithExt_TotalCrafted", ref _totalItemsCrafted);
                dataStore.SyncData("SmithExt_TotalUniques", ref _totalUniquesCrafted);
                dataStore.SyncData("SmithExt_TotalRepairs", ref _totalRepairs);
            });

            _uniqueItems ??= new Dictionary<string, HoNUniqueItemData>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _instance = this;

            if (!IsEnabled)
            {
                ModSettings.DebugLog($"{FEATURE_NAME} is disabled in settings.");
                return;
            }

            SafeExecutor.Execute(FEATURE_NAME, "SessionLaunched", () =>
            {
                // Initialize systems
                ArmorSmithingManager.Initialize();
                UniqueItemTracker.LoadData(_uniqueItems);

                // Register settlement menu options
                SettlementMenuExtension.RegisterMenus(starter);

                var settings = ModSettings.Get();
                string staminaStatus = settings.DisableSmithingStamina ? "DISABLED" : 
                    (Math.Abs(settings.SmithingStaminaMultiplier - 1.0f) < 0.01f ? "Normal" : $"{settings.SmithingStaminaMultiplier:F1}x");

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] Loaded. Stamina: {staminaStatus}, Uniques: {_totalUniquesCrafted}, Repairs: {_totalRepairs}",
                    Colors.Cyan));
            });
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            SafeExecutor.Execute(FEATURE_NAME, "NewGame", () =>
            {
                _uniqueItems = new Dictionary<string, HoNUniqueItemData>();
                _totalItemsCrafted = 0;
                _totalUniquesCrafted = 0;
                _totalRepairs = 0;
            });
        }

        #region Statistics Tracking

        public void TrackCraftedItem(bool isUnique)
        {
            SafeExecutor.Execute(FEATURE_NAME, "TrackCrafted", () =>
            {
                _totalItemsCrafted++;
                if (isUnique) _totalUniquesCrafted++;
            });
        }

        public void TrackRepair()
        {
            SafeExecutor.Execute(FEATURE_NAME, "TrackRepair", () =>
            {
                _totalRepairs++;
            });
        }

        public (int total, int unique, int repairs) GetStatistics()
        {
            return (_totalItemsCrafted, _totalUniquesCrafted, _totalRepairs);
        }

        #endregion

        #region Unique Items Management

        public void RegisterUniqueItem(string itemId, HoNUniqueItemData data)
        {
            SafeExecutor.Execute(FEATURE_NAME, "RegisterUnique", () =>
            {
                if (string.IsNullOrEmpty(itemId) || data == null) return;
                
                _uniqueItems[itemId] = data;
                UniqueItemTracker.RegisterUniqueItem(itemId, data);
                _totalUniquesCrafted++;
            });
        }

        public HoNUniqueItemData GetHoNUniqueItemData(string itemId)
        {
            try
            {
                return _uniqueItems?.TryGetValue(itemId, out var data) == true ? data : null;
            }
            catch
            {
                return null;
            }
        }

        public IReadOnlyDictionary<string, HoNUniqueItemData> AllUniqueItems => _uniqueItems;

        #endregion

        #region Queries

        public string GetSummary()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Smithing Extended Statistics:");
                sb.AppendLine($"  Total Items Crafted: {_totalItemsCrafted}");
                sb.AppendLine($"  Unique Items: {_totalUniquesCrafted}");
                sb.AppendLine($"  Repairs Performed: {_totalRepairs}");
                
                if (_totalItemsCrafted > 0)
                {
                    float uniqueRate = (_totalUniquesCrafted / (float)_totalItemsCrafted) * 100f;
                    sb.AppendLine($"  Unique Rate: {uniqueRate:F1}%");
                }

                return sb.ToString();
            }
            catch
            {
                return "Smithing Extended data unavailable";
            }
        }

        #endregion
    }

    /// <summary>
    /// SaveableTypeDefiner for smithing extended data.
    /// </summary>
    public class SmithingExtendedSaveableTypeDefiner : SaveableTypeDefiner
    {
        public SmithingExtendedSaveableTypeDefiner() : base(726900401) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(HoNUniqueItemData), 101);
            AddClassDefinition(typeof(HoNUniqueBonus), 102);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<string, HoNUniqueItemData>));
            ConstructContainerDefinition(typeof(List<HoNUniqueBonus>));
        }
    }
}
