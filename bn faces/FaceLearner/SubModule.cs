using System;
using System.Xml;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;

namespace FaceLearner
{
    public class SubModule : MBSubModuleBase
    {
        // VERSION is read from SubModule.xml at runtime - single source of truth!
        private static string _version = null;
        public static string VERSION 
        {
            get 
            {
                if (_version == null)
                {
                    try
                    {
                        // Read version from SubModule.xml using standard XmlDocument
                        string xmlPath = BasePath.Name + "Modules/FaceLearner/SubModule.xml";
                        if (IOFile.Exists(xmlPath))
                        {
                            var doc = new XmlDocument();
                            doc.Load(xmlPath);
                            var versionNode = doc.SelectSingleNode("//Version");
                            string versionStr = versionNode?.Attributes?["value"]?.InnerText ?? "unknown";
                            // Remove 'v' prefix if present (v2.7.13 → 2.7.13)
                            _version = versionStr.StartsWith("v") ? versionStr.Substring(1) : versionStr;
                        }
                        else
                        {
                            _version = "unknown";
                        }
                    }
                    catch
                    {
                        _version = "unknown";
                    }
                }
                return _version;
            }
        }
        
        public static string ModulePath { get; private set; }
        private Harmony _harmony;
        
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            ModulePath = BasePath.Name + "Modules/FaceLearner/";
            Log("FaceLearner loading...");
            
            try
            {
                _harmony = new Harmony("com.facelearner.patch");
                _harmony.PatchAll();
                Log("Harmony patches applied");
            }
            catch (Exception ex)
            {
                Log($"Harmony error: {ex.Message}");
            }
        }
        
        protected override void OnSubModuleUnloaded()
        {
            _harmony?.UnpatchAll("com.facelearner.patch");
            base.OnSubModuleUnloaded();
        }
        
        private static bool _menuButtonsAdded = false;
        
        // This is the key - add our buttons to the main menu!
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            if (_menuButtonsAdded) return;
            
            try
            {
                // Add "FaceLearner" option to main menu
                Module.CurrentModule.AddInitialStateOption(new InitialStateOption(
                    "FaceLearner",
                    new TextObject("{=FaceLearner}FaceLearner", null),
                    9990,  // Order/priority
                    () => OpenFaceLearner(),
                    () => (false, new TextObject("", null))  // Always enabled
                ));
                
                _menuButtonsAdded = true;
                Log("Main menu button added!");
            }
            catch (Exception ex)
            {
                Log($"Failed to add menu button: {ex.Message}");
            }
        }
        
        private static void OpenFaceLearner()
        {
            try
            {
                Log("Opening FaceLearner...");
                // Use GameManager approach - this properly initializes the game environment
                // and creates the FaceLearnerState which FaceLearnerScreen needs
                MBGameManager.StartNewGame(new FaceLearner.Core.FaceLearnerGameManager());
                Log("FaceLearnerGameManager started successfully");
            }
            catch (Exception ex)
            {
                Log($"Error opening FaceLearner: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        public static void Log(string message)
        {
            string fullMsg = $"[FaceLearner] {message}";
            TaleWorlds.Library.Debug.Print(fullMsg);
            
            try
            {
                var logPath = IOPath.Combine(ModulePath, "Data", "facelearner.log");
                var dir = IOPath.GetDirectoryName(logPath);
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                IOFile.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }
        
        public static void LogDebug(string message)
        {
            // Debug logging - can be toggled
            Log($"[DEBUG] {message}");
        }
    }
}
