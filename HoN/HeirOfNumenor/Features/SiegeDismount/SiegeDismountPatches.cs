using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.SiegeDismount
{
    /// <summary>
    /// Harmony patches for siege dismount functionality.
    /// Intercepts player spawn to enforce dismount behavior.
    /// </summary>
    [HarmonyPatch]
    public static class SiegeDismountPatches
    {
        private static readonly string FEATURE_NAME = "SiegeDismount";
        
        /// <summary>
        /// Patch: Intercept player agent spawn to apply dismount behavior.
        /// Target: MissionAgentSpawnLogic or similar spawn method.
        /// </summary>
        [HarmonyPatch(typeof(Mission), "SpawnAgent")]
        [HarmonyPrefix]
        public static bool SpawnAgent_Prefix(
            AgentBuildData agentBuildData,
            ref Agent __result)
        {
            try
            {
                // Only process if this is the main hero in a siege
                if (!IsMainHeroSpawn(agentBuildData))
                    return true; // Continue normal execution
                
                if (!IsSiegeMission())
                    return true;
                
                var behavior = Settings.Instance?.GetSiegeMountBehavior() ?? SiegeMountBehavior.Vanilla;
                
                if (behavior == SiegeMountBehavior.Vanilla)
                    return true;
                
                // Apply dismount modifications to spawn data
                if (behavior == SiegeMountBehavior.DismountKeepOnMap)
                {
                    // Spawn horse separately, player on foot
                    // Modify agentBuildData to not mount the horse
                    var mountAgent = agentBuildData.AgentMountAgent;
                    if (mountAgent != null)
                    {
                        // Clear the mount agent from spawn data
                        // Player will spawn on foot, horse spawns nearby
                        agentBuildData.MountAgent(null);
                        Log("Player spawn modified: dismounted, horse on map.");
                    }
                }
                // DismountToInventory and AutoRemountAfter are handled by SiegeDismountBehavior
                // which removes the mount from equipment before spawn
            }
            catch (Exception ex)
            {
                Log($"SpawnAgent patch error: {ex.Message}");
            }
            
            return true; // Continue normal execution
        }
        
        /// <summary>
        /// Alternative patch point: Agent constructor or InitializeAgentProperties
        /// </summary>
        [HarmonyPatch(typeof(Agent), "InitializeAgentProperties")]
        [HarmonyPostfix]
        public static void InitializeAgentProperties_Postfix(Agent __instance)
        {
            try
            {
                // Check if this is the main hero
                if (__instance?.Character == null)
                    return;
                
                if (!__instance.IsMainAgent)
                    return;
                
                // Check siege mission and behavior
                if (!IsSiegeMission())
                    return;
                
                var behavior = Settings.Instance?.GetSiegeMountBehavior() ?? SiegeMountBehavior.Vanilla;
                
                if (behavior == SiegeMountBehavior.DismountKeepOnMap)
                {
                    // Force dismount if somehow mounted
                    if (__instance.HasMount)
                    {
                        // Get the mount and dismount
                        var mount = __instance.MountAgent;
                        if (mount != null)
                        {
                            __instance.SetMountAgent(null);
                            Log("Forced dismount applied to player.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"InitializeAgentProperties patch error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Helper: Determines if the spawn data is for the main hero.
        /// </summary>
        private static bool IsMainHeroSpawn(AgentBuildData agentBuildData)
        {
            if (agentBuildData == null)
                return false;
            
            // Check if this is the main hero's character
            var character = agentBuildData.AgentCharacter;
            if (character == null)
                return false;
            
            // Check if it matches the main hero
            if (Hero.MainHero?.CharacterObject == character)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Helper: Determines if the current mission is a siege.
        /// </summary>
        private static bool IsSiegeMission()
        {
            if (Mission.Current == null)
                return false;
            
            // Use Mission's built-in siege detection
            if (Mission.Current.IsSiegeBattle)
                return true;
            
            // Additional checks for siege-like missions
            var sceneName = Mission.Current.SceneName?.ToLower() ?? "";
            if (sceneName.Contains("siege") || sceneName.Contains("assault") || 
                sceneName.Contains("defense") || sceneName.Contains("breach"))
            {
                return true;
            }
            
            return false;
        }
        
        private static void Log(string message)
        {
            if (Settings.Instance?.DebugMode ?? false)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[{FEATURE_NAME}] {message}", Colors.Cyan));
            }
        }
    }
}
