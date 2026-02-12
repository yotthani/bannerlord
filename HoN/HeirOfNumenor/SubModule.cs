using HarmonyLib;
using HeirOfNumenor.Features.CustomResourceSystem;
using HeirOfNumenor.Features.EquipPresets;
using HeirOfNumenor.Features.FormationPresets;
using HeirOfNumenor.Features.MemorySystem;
using HeirOfNumenor.Features.RingSystem;
using HeirOfNumenor.Features.SmithingExtended;
using HeirOfNumenor.Features.TransferbuttonMenu;
using HeirOfNumenor.Features.TroopStatus;
using HeirOfNumenor.Features.SiegeDismount;
using HeirOfNumenor.Features.MixedFormations;
using HeirOfNumenor.Features.SmartCavalryAI;
using HeirOfNumenor.Features.BattleActionBar;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using System;

namespace HeirOfNumenor
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;
        private InventorySearchBehavior _searchBehavior;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            var logPath = System.IO.Path.Combine(BasePath.Name, "Modules", "HeirOfNumenor", "harmony_debug.txt");
            var logLines = new System.Collections.Generic.List<string>();
            logLines.Add($"HeirOfNumenor Harmony Debug Log - {DateTime.Now}");
            logLines.Add("");
            
            try
            {
                // Initialize Harmony
                _harmony = new Harmony("com.heirofnumenor.patch");
                
                // Patches that must be DISABLED due to causing crashes during registration
                // (They trigger CampaignUIHelper static init before Campaign.Current exists)
                var disabledPatches = new System.Collections.Generic.HashSet<string>
                {
                    // RingBearerIndicators - crashes during patch registration
                    "RingBearerIndicators",
                    "PartyNameplate_Patches",
                    "EncyclopediaHeroPage_Patches",
                    "PartyCharacterVM_RingIndicator_Patches",
                    "PartyTroopTupleWidget_RingIndicator_Patch",
                };
                
                // Get all types with HarmonyPatch attribute
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var patchTypes = new System.Collections.Generic.List<Type>();
                
                foreach (var type in assembly.GetTypes())
                {
                    if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                    {
                        patchTypes.Add(type);
                    }
                    
                    foreach (var nestedType in type.GetNestedTypes())
                    {
                        if (nestedType.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
                        {
                            patchTypes.Add(nestedType);
                        }
                    }
                }
                
                logLines.Add($"Found {patchTypes.Count} patch classes");
                logLines.Add("");
                
                int successCount = 0;
                int failCount = 0;
                int skippedCount = 0;
                
                foreach (var patchType in patchTypes)
                {
                    // Check if this patch should be skipped
                    bool shouldSkip = false;
                    foreach (var skipName in disabledPatches)
                    {
                        if (patchType.Name.Contains(skipName) || patchType.FullName.Contains(skipName))
                        {
                            shouldSkip = true;
                            break;
                        }
                    }
                    
                    if (shouldSkip)
                    {
                        skippedCount++;
                        logLines.Add($"[SKIP] {patchType.FullName}");
                        continue;
                    }
                    
                    try
                    {
                        var processor = _harmony.CreateClassProcessor(patchType);
                        processor.Patch();
                        successCount++;
                        logLines.Add($"[OK] {patchType.FullName}");
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        logLines.Add($"[FAIL] {patchType.FullName}: {ex.Message}");
                    }
                }
                
                logLines.Add("");
                logLines.Add($"=== SUMMARY ===");
                logLines.Add($"Successful: {successCount}");
                logLines.Add($"Failed: {failCount}");
                logLines.Add($"Skipped: {skippedCount}");
            }
            catch (Exception ex)
            {
                logLines.Add($"CRITICAL ERROR: {ex}");
            }
            
            // Write log
            try
            {
                System.IO.File.WriteAllLines(logPath, logLines);
            }
            catch { }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll("com.heirofnumenor.patch");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            // Register CampaignBehaviors (save/load persistence)
            if (game.GameType is Campaign)
            {
                var campaignStarter = gameStarterObject as CampaignGameStarter;
                if (campaignStarter != null)
                {
                    // Register custom models FIRST (they override native calculations)
                    try
                    {
                        // Custom morale model - makes TroopStatus visible in native UI
                        campaignStarter.AddModel(new HeirOfNumenorPartyMoraleModel());
                        
                        // Custom speed model - Ring/Fear effects show in speed tooltip  
                        campaignStarter.AddModel(new HeirOfNumenorPartySpeedModel());
                        
                        // Custom clan tier model - increases companion limit for promoted captains
                        campaignStarter.AddModel(new HeirOfNumenorClanTierModel());
                    }
                    catch (Exception ex)
                    {
                        ModSettings.ErrorLog("SubModule", "Model registration failed", ex);
                    }
                    
                    // Core features
                    campaignStarter.AddBehavior(new EquipmentPresetCampaignBehavior());
                    campaignStarter.AddBehavior(new FormationPresetCampaignBehavior());
                    
                    // Inventory Search Enabler
                    _searchBehavior = new InventorySearchBehavior();
                    campaignStarter.AddBehavior(_searchBehavior);
                    
                    // Ring System
                    campaignStarter.AddBehavior(new RingSystemCampaignBehavior());
                    
                    // Troop Status & Memory System
                    campaignStarter.AddBehavior(new TroopStatusCampaignBehavior());
                    campaignStarter.AddBehavior(new MemorySystemCampaignBehavior());
                    
                    // Custom Resource System (cultural needs)
                    campaignStarter.AddBehavior(new CustomResourceCampaignBehavior());
                    
                    // Smithing Extended
                    campaignStarter.AddBehavior(new SmithingExtendedCampaignBehavior());
                }
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            
            // Inventory search enabler needs to run every tick to detect inventory screen
            _searchBehavior?.OnTick();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            // This message appears in the bottom-left corner when you reach main menu
            InformationManager.DisplayMessage(new InformationMessage(
                "=== Heir of Numenor Extension LOADED ===", 
                Colors.Green));
            
            // Report Harmony patch status
            try
            {
                var patchedMethods = Harmony.GetAllPatchedMethods();
                int honPatchCount = 0;
                foreach (var method in patchedMethods)
                {
                    var patches = Harmony.GetPatchInfo(method);
                    if (patches.Owners.Contains("com.heirofnumenor.patch"))
                    {
                        honPatchCount++;
                    }
                }
                
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[HoN] Harmony patches applied: {honPatchCount}", 
                    honPatchCount > 0 ? Colors.Green : Colors.Red));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[HoN] Harmony status check failed: {ex.Message}", Colors.Red));
            }
        }
        
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            
            // Add mission behaviors for new features
            
            // Siege Dismount
            if (Settings.Instance?.GetSiegeMountBehavior() != SiegeMountBehavior.Vanilla)
            {
                mission.AddMissionBehavior(new SiegeDismountBehavior());
            }
            
            // Mixed Formations
            if (Settings.Instance?.EnableMixedFormationLayouts ?? false)
            {
                var layoutBehavior = new MixedFormationLayoutBehavior();
                mission.AddMissionBehavior(layoutBehavior);
                FormationLayoutPatches.SetBehavior(layoutBehavior);
            }
            
            // Smart Cavalry AI
            if (Settings.Instance?.EnableSmartCavalryAI ?? false)
            {
                mission.AddMissionBehavior(new SmartCavalryAIBehavior());
            }
            
            // Battle Action Bar (MissionView, not MissionBehavior)
            // Note: MissionViews are added differently - need to add to MissionScreen
            // This is handled by BattleActionBarMissionView.OnMissionScreenInitialize
        }
        
        /// <summary>
        /// Override to add custom MissionViews to battles.
        /// </summary>
        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            base.OnBeforeMissionBehaviorInitialize(mission);
            
            // Add MissionViews for UI overlays
            if (Settings.Instance?.EnableBattleActionBar ?? false)
            {
                // MissionViews are added via AddMissionView in newer API
                // or via ViewCreator modules
            }
        }
    }
}
