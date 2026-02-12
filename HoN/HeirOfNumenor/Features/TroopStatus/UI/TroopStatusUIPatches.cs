using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace HeirOfNumenor.Features.TroopStatus.UI
{
    /// <summary>
    /// Harmony patches to inject troop status indicators into party screen.
    /// </summary>
    [HarmonyPatch]
    public static class TroopStatusUIPatches
    {
        private const string FEATURE_NAME = "TroopStatusUI";
        
        // Cache of status VMs per troop ID to avoid recreation
        private static Dictionary<string, TroopStatusIndicatorsVM> _statusVMCache;
        private static bool _isInitialized = false;

        /// <summary>
        /// Check if TroopStatus UI is enabled.
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                try 
                { 
                    var settings = ModSettings.Get();
                    return settings.EnableTroopStatus && settings.EnableTroopStatusUI; 
                }
                catch { return true; }
            }
        }

        public static void Initialize()
        {
            SafeExecutor.Execute(FEATURE_NAME, "Initialize", () =>
            {
                if (_isInitialized) return;
                _statusVMCache = new Dictionary<string, TroopStatusIndicatorsVM>();
                StatusIconMapper.Initialize();
                _isInitialized = true;
            });
        }

        /// <summary>
        /// Gets or creates a status indicators VM for a troop.
        /// </summary>
        public static TroopStatusIndicatorsVM GetStatusIndicatorsVM(string troopId, bool isHero)
        {
            try
            {
                if (!IsEnabled) return null;
                if (!_isInitialized) Initialize();

                if (isHero || string.IsNullOrEmpty(troopId))
                    return null;

                if (!_statusVMCache.TryGetValue(troopId, out var vm))
                {
                    vm = new TroopStatusIndicatorsVM(troopId, isHero);
                    _statusVMCache[troopId] = vm;
                }
                else
                {
                    vm.RefreshFromData();
                }

                return vm;
            }
            catch (Exception ex)
            {
                ModSettings.ErrorLog(FEATURE_NAME, "GetStatusIndicatorsVM", ex);
                return null;
            }
        }

        /// <summary>
        /// Clears the VM cache (call when party screen closes).
        /// </summary>
        public static void ClearCache()
        {
            SafeExecutor.Execute(FEATURE_NAME, "ClearCache", () =>
            {
                _statusVMCache?.Clear();
            });
        }

        /// <summary>
        /// Refreshes all cached status VMs.
        /// </summary>
        public static void RefreshAll()
        {
            SafeExecutor.Execute(FEATURE_NAME, "RefreshAll", () =>
            {
                if (!IsEnabled) return;
                if (_statusVMCache == null) return;

                foreach (var vm in _statusVMCache.Values)
                {
                    vm?.RefreshFromData();
                }
            });
        }
    }

    /// <summary>
    /// Mixin class to extend PartyCharacterVM with status indicators.
    /// Uses reflection to add dynamic properties.
    /// </summary>
    public static class PartyCharacterVMExtensions
    {
        private static Dictionary<object, TroopStatusIndicatorsVM> _vmStatusMap = new Dictionary<object, TroopStatusIndicatorsVM>();

        /// <summary>
        /// Gets the status indicators for a PartyCharacterVM.
        /// </summary>
        public static TroopStatusIndicatorsVM GetStatusIndicators(this PartyCharacterVM vm)
        {
            if (vm == null) return null;

            if (!_vmStatusMap.TryGetValue(vm, out var indicators))
            {
                var character = vm.Character;
                if (character != null && !character.IsHero)
                {
                    indicators = new TroopStatusIndicatorsVM(character.StringId, false);
                    _vmStatusMap[vm] = indicators;
                }
            }

            return indicators;
        }

        /// <summary>
        /// Refreshes status indicators for a PartyCharacterVM.
        /// </summary>
        public static void RefreshStatusIndicators(this PartyCharacterVM vm)
        {
            var indicators = vm.GetStatusIndicators();
            indicators?.RefreshFromData();
        }

        /// <summary>
        /// Clears the extension map.
        /// </summary>
        public static void ClearExtensions()
        {
            _vmStatusMap.Clear();
        }
    }

    /// <summary>
    /// Patch to refresh status indicators when party roster changes.
    /// </summary>
    [HarmonyPatch(typeof(PartyVM))]
    public static class PartyVMPatches
    {
        [HarmonyPatch("RefreshValues")]
        [HarmonyPostfix]
        public static void RefreshValues_Postfix(PartyVM __instance)
        {
            try
            {
                TroopStatusUIPatches.RefreshAll();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TroopStatus] RefreshValues error: {ex.Message}", Colors.Red));
            }
        }

        [HarmonyPatch("OnFinalize")]
        [HarmonyPostfix]
        public static void OnFinalize_Postfix()
        {
            try
            {
                TroopStatusUIPatches.ClearCache();
                PartyCharacterVMExtensions.ClearExtensions();
            }
            catch { }
        }
    }

    /// <summary>
    /// Patch to add status properties to PartyCharacterVM.
    /// </summary>
    [HarmonyPatch(typeof(PartyCharacterVM))]
    public static class PartyCharacterVMPatches
    {
        /* DISABLED - Constructor signature doesn't match this game version
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(PartyVM), typeof(PartyScreenLogic.PartyRosterSide), typeof(TroopRosterElement), typeof(PartyScreenLogic.TroopType), typeof(Action<PartyCharacterVM>), typeof(Action<PartyCharacterVM>) })]
        [HarmonyPostfix]
        public static void Constructor_Postfix(PartyCharacterVM __instance, TroopRosterElement troop)
        {
            try
            {
                if (troop.Character != null && !troop.Character.IsHero)
                {
                    // Initialize status indicators for this troop
                    __instance.GetStatusIndicators();
                }
            }
            catch { }
        }
        */

        [HarmonyPatch("RefreshValues")]
        [HarmonyPostfix]
        public static void RefreshValues_Postfix(PartyCharacterVM __instance)
        {
            try
            {
                __instance.RefreshStatusIndicators();
            }
            catch { }
        }
    }
}
