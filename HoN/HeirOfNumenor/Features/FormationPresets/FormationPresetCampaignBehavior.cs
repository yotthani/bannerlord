using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using HeirOfNumenor.Features.FormationPresets.Data;

namespace HeirOfNumenor.Features.FormationPresets
{
    /// <summary>
    /// Campaign behavior that persists formation presets across game saves.
    /// </summary>
    public class FormationPresetCampaignBehavior : CampaignBehaviorBase
    {
        private const string FEATURE_NAME = "FormationPresets";
        
        private static FormationPresetCampaignBehavior _instance;
        public static FormationPresetCampaignBehavior Instance => _instance;

        /// <summary>
        /// Check if Formation Presets are enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try { return ModSettings.Get().EnableFormationPresets; }
                catch { return true; }
            }
        }

        [SaveableField(1)]
        private List<HoNFormationPreset> _savedPresets = new List<HoNFormationPreset>();

        public FormationPresetCampaignBehavior()
        {
            _instance = this;
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
                dataStore.SyncData("FormationPresets", ref _savedPresets);

                if (dataStore.IsLoading)
                {
                    // Load presets into the manager
                    FormationPresetManager.LoadFromCampaignData(_savedPresets ?? new List<HoNFormationPreset>());
                }
                else if (dataStore.IsSaving)
                {
                    // Save presets from the manager
                    _savedPresets = FormationPresetManager.GetPresetsForSaving();
                }
            });

            _savedPresets ??= new List<HoNFormationPreset>();
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
                // Ensure presets are loaded
                if (_savedPresets != null && _savedPresets.Count > 0)
                {
                    FormationPresetManager.LoadFromCampaignData(_savedPresets);
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] Loaded. {_savedPresets?.Count ?? 0} presets available.",
                    Colors.Cyan));
            });
        }

        /// <summary>
        /// Marks presets as needing to be saved.
        /// Updates the internal list from the manager.
        /// </summary>
        public void MarkDirty()
        {
            SafeExecutor.Execute(FEATURE_NAME, "MarkDirty", () =>
            {
                _savedPresets = FormationPresetManager.GetPresetsForSaving();
            });
        }

        /// <summary>
        /// Gets all saved presets.
        /// </summary>
        public List<HoNFormationPreset> GetPresets()
        {
            try
            {
                return new List<HoNFormationPreset>(_savedPresets ?? new List<HoNFormationPreset>());
            }
            catch
            {
                return new List<HoNFormationPreset>();
            }
        }

        /// <summary>
        /// Checks if auto-assign is enabled.
        /// </summary>
        public bool IsAutoAssignEnabled()
        {
            try
            {
                return IsEnabled && ModSettings.Get().AutoAssignHeroes;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Gets the maximum number of presets allowed.
        /// </summary>
        public int GetMaxPresets()
        {
            try
            {
                return ModSettings.Get().MaxFormationPresets;
            }
            catch
            {
                return 10;
            }
        }
    }
}
