using System;
using System.Collections.Generic;
using System.Linq;
using HeirOfNumenor.Features.EquipPresets.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace HeirOfNumenor.Features.EquipPresets
{
    /// <summary>
    /// Campaign behavior that persists equipment presets with the save game.
    /// Presets are stored per-hero using their StringId as key.
    /// </summary>
    public class EquipmentPresetCampaignBehavior : CampaignBehaviorBase
    {
        private const string FEATURE_NAME = "EquipPresets";
        
        // Key: Hero.StringId, Value: List of presets for that hero
        private Dictionary<string, List<HoNEquipmentPreset>> _heroPresets;

        public static EquipmentPresetCampaignBehavior Instance { get; private set; }

        /// <summary>
        /// Check if Equipment Presets are enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableEquipmentPresets; }
                catch { return true; }
            }
        }

        public EquipmentPresetCampaignBehavior()
        {
            _heroPresets = new Dictionary<string, List<HoNEquipmentPreset>>();
            Instance = this;
        }

        public override void RegisterEvents()
        {
            SafeExecutor.Execute(FEATURE_NAME, "RegisterEvents", () =>
            {
                CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            });
        }

        public override void SyncData(IDataStore dataStore)
        {
            SafeExecutor.Execute(FEATURE_NAME, "SyncData", () =>
            {
                dataStore.SyncData("EquipPresets_HeroPresets", ref _heroPresets);
            });

            // Ensure dictionary exists after load
            _heroPresets ??= new Dictionary<string, List<HoNEquipmentPreset>>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            Instance = this;

            if (!IsEnabled)
            {
                ModSettings.DebugLog($"{FEATURE_NAME} is disabled in settings.");
                return;
            }

            SafeExecutor.Execute(FEATURE_NAME, "SessionLaunched", () =>
            {
                int totalPresets = _heroPresets?.Values.Sum(p => p?.Count ?? 0) ?? 0;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] Loaded. {totalPresets} presets across {_heroPresets?.Count ?? 0} heroes.",
                    Colors.Cyan));
            });
        }

        #region Preset Access

        /// <summary>
        /// Gets all presets for a specific hero.
        /// </summary>
        public List<HoNEquipmentPreset> GetPresetsForHero(Hero hero)
        {
            try
            {
                if (!IsEnabled || hero == null)
                    return new List<HoNEquipmentPreset>();

                string heroId = hero.StringId;
                if (_heroPresets?.TryGetValue(heroId, out var presets) == true)
                {
                    return presets ?? new List<HoNEquipmentPreset>();
                }
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "GetPresetsForHero", ex);
            }

            return new List<HoNEquipmentPreset>();
        }

        /// <summary>
        /// Gets all presets for the current player hero.
        /// </summary>
        public List<HoNEquipmentPreset> GetPresetsForPlayer()
        {
            return GetPresetsForHero(Hero.MainHero);
        }

        /// <summary>
        /// Adds a preset for a hero.
        /// </summary>
        public void AddPreset(Hero hero, HoNEquipmentPreset preset)
        {
            SafeExecutor.Execute(FEATURE_NAME, "AddPreset", () =>
            {
                if (!IsEnabled || hero == null || preset == null)
                    return;

                var settings = ModSettings.Get();
                string heroId = hero.StringId;
                
                if (!_heroPresets.ContainsKey(heroId))
                {
                    _heroPresets[heroId] = new List<HoNEquipmentPreset>();
                }

                // Check max presets limit
                if (_heroPresets[heroId].Count >= settings.MaxPresetsPerCharacter)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Maximum presets ({settings.MaxPresetsPerCharacter}) reached for this character!",
                        Colors.Yellow));
                    return;
                }

                _heroPresets[heroId].Add(preset);
                
                ModSettings.DebugLog($"Added preset '{preset.Name}' for {hero.Name}");
            });
        }

        /// <summary>
        /// Removes a preset from a hero.
        /// </summary>
        public bool RemovePreset(Hero hero, HoNEquipmentPreset preset)
        {
            return SafeExecutor.Execute(FEATURE_NAME, "RemovePreset", () =>
            {
                if (!IsEnabled || hero == null || preset == null)
                    return false;

                string heroId = hero.StringId;
                if (_heroPresets?.TryGetValue(heroId, out var presets) == true)
                {
                    return presets?.Remove(preset) == true;
                }

                return false;
            }, false);
        }

        /// <summary>
        /// Removes a preset by ID from a hero.
        /// </summary>
        public bool RemovePresetById(Hero hero, string presetId)
        {
            return SafeExecutor.Execute(FEATURE_NAME, "RemovePresetById", () =>
            {
                if (!IsEnabled || hero == null || string.IsNullOrEmpty(presetId))
                    return false;

                string heroId = hero.StringId;
                if (_heroPresets?.TryGetValue(heroId, out var presets) == true)
                {
                    return presets?.RemoveAll(p => p?.Id == presetId) > 0;
                }

                return false;
            }, false);
        }

        /// <summary>
        /// Updates an existing preset (by replacing it).
        /// </summary>
        public void UpdatePreset(Hero hero, HoNEquipmentPreset preset)
        {
            SafeExecutor.Execute(FEATURE_NAME, "UpdatePreset", () =>
            {
                if (!IsEnabled || hero == null || preset == null)
                    return;

                RemovePresetById(hero, preset.Id);
                AddPreset(hero, preset);
            });
        }

        /// <summary>
        /// Checks if any preset for a hero contains the given item.
        /// Used to determine if an item should stay locked.
        /// </summary>
        public bool IsItemInAnyPreset(Hero hero, string itemStringId)
        {
            try
            {
                if (!IsEnabled) return false;
                
                var presets = GetPresetsForHero(hero);
                foreach (var preset in presets)
                {
                    if (preset?.ContainsItemById(itemStringId) == true)
                        return true;
                }
            }
            catch { }
            
            return false;
        }

        /// <summary>
        /// Gets all unique item StringIds across all presets for a hero.
        /// </summary>
        public HashSet<string> GetAllPresetItemIds(Hero hero)
        {
            var itemIds = new HashSet<string>();

            SafeExecutor.Execute(FEATURE_NAME, "GetAllPresetItemIds", () =>
            {
                if (!IsEnabled) return;
                
                var presets = GetPresetsForHero(hero);

                foreach (var preset in presets)
                {
                    if (preset == null) continue;
                    
                    foreach (var itemId in preset.GetItemStringIds())
                    {
                        if (!string.IsNullOrEmpty(itemId))
                            itemIds.Add(itemId);
                    }
                }
            });

            return itemIds;
        }

        #endregion
    }
}
