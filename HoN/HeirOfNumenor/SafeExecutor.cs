using System;
using TaleWorlds.Library;

namespace HeirOfNumenor
{
    /// <summary>
    /// Utility class for safe execution of code with error handling.
    /// Prevents crashes from propagating to the game.
    /// </summary>
    public static class SafeExecutor
    {
        /// <summary>
        /// Executes an action with error handling. Returns true if successful.
        /// </summary>
        public static bool Execute(string featureName, string operationName, Action action)
        {
            try
            {
                action?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                LogError(featureName, operationName, ex);
                return false;
            }
        }

        /// <summary>
        /// Executes a function with error handling. Returns default(T) on failure.
        /// </summary>
        public static T Execute<T>(string featureName, string operationName, Func<T> func, T defaultValue = default)
        {
            try
            {
                return func != null ? func() : defaultValue;
            }
            catch (Exception ex)
            {
                LogError(featureName, operationName, ex);
                return defaultValue;
            }
        }

        /// <summary>
        /// Executes an action only if a condition is met.
        /// </summary>
        public static bool ExecuteIf(bool condition, string featureName, string operationName, Action action)
        {
            if (!condition) return false;
            return Execute(featureName, operationName, action);
        }

        /// <summary>
        /// Executes an action only if the feature is enabled in settings.
        /// </summary>
        public static bool ExecuteIfEnabled(Func<bool> isEnabledCheck, string featureName, string operationName, Action action)
        {
            try
            {
                if (!isEnabledCheck()) return false;
            }
            catch
            {
                return false; // If we can't check, don't execute
            }

            return Execute(featureName, operationName, action);
        }

        /// <summary>
        /// Logs an error to the game message system.
        /// </summary>
        private static void LogError(string featureName, string operationName, Exception ex)
        {
            try
            {
                var settings = ModSettings.Get();
                
                if (settings.EnableDebugMessages)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[{featureName}] {operationName} error: {ex.Message}", Colors.Red));
                }
                else if (settings.SafeMode)
                {
                    // In safe mode, show simplified error
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[{featureName}] An error occurred (enable debug for details)", Colors.Yellow));
                }

                // Always log to debug
                ModSettings.DebugLog($"{featureName}.{operationName}: {ex}");
            }
            catch
            {
                // If even error logging fails, silently continue
            }
        }

        /// <summary>
        /// Wraps a Harmony patch method to prevent crashes.
        /// </summary>
        public static void WrapPatch(string featureName, string patchName, Action patchAction)
        {
            try
            {
                // Check if feature is enabled based on feature name
                if (!IsFeatureEnabled(featureName))
                {
                    return;
                }

                patchAction?.Invoke();
            }
            catch (Exception ex)
            {
                LogError(featureName, patchName, ex);
            }
        }

        /// <summary>
        /// Wraps a Harmony prefix patch. Returns true to continue, false to skip original.
        /// </summary>
        public static bool WrapPrefix(string featureName, string patchName, Func<bool> patchFunc)
        {
            try
            {
                if (!IsFeatureEnabled(featureName))
                {
                    return true; // Continue to original if disabled
                }

                return patchFunc?.Invoke() ?? true;
            }
            catch (Exception ex)
            {
                LogError(featureName, patchName, ex);
                return true; // Continue to original on error
            }
        }

        /// <summary>
        /// Checks if a feature is enabled based on feature name.
        /// </summary>
        private static bool IsFeatureEnabled(string featureName)
        {
            try
            {
                var settings = ModSettings.Get();
                
                return featureName switch
                {
                    "EquipmentPresets" => settings.EnableEquipmentPresets,
                    "FormationPresets" => settings.EnableFormationPresets,
                    "CompanionRoles" => settings.EnableCompanionRoles,
                    "RingSystem" => settings.EnableRingSystem,
                    "TroopStatus" => settings.EnableTroopStatus,
                    "MemorySystem" => settings.EnableMemorySystem,
                    "CulturalNeeds" => settings.EnableCulturalNeeds,
                    "FiefManagement" => settings.EnableFiefManagement,
                    "SmithingExtended" => settings.EnableSmithingExtended,
                    _ => true // Unknown features default to enabled
                };
            }
            catch
            {
                return true; // Default enabled if settings fail
            }
        }
    }

    /// <summary>
    /// Extension methods for null-safe operations.
    /// </summary>
    public static class NullSafeExtensions
    {
        /// <summary>
        /// Safely gets a value from a dictionary, returning default if not found or null.
        /// </summary>
        public static TValue SafeGet<TKey, TValue>(this System.Collections.Generic.Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default)
        {
            if (dict == null || key == null) return defaultValue;
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Safely iterates over a collection, skipping null entries.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<T> SafeWhere<T>(
            this System.Collections.Generic.IEnumerable<T> source, 
            Func<T, bool> predicate) where T : class
        {
            if (source == null) yield break;
            
            foreach (var item in source)
            {
                if (item != null)
                {
                    bool match = false;
                    try { match = predicate(item); } catch { }
                    if (match) yield return item;
                }
            }
        }

        /// <summary>
        /// Safely converts to list, handling null source.
        /// </summary>
        public static System.Collections.Generic.List<T> SafeToList<T>(
            this System.Collections.Generic.IEnumerable<T> source)
        {
            if (source == null) return new System.Collections.Generic.List<T>();
            try
            {
                return new System.Collections.Generic.List<T>(source);
            }
            catch
            {
                return new System.Collections.Generic.List<T>();
            }
        }
    }
}
