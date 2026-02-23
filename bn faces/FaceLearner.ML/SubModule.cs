using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using FaceLearner.Core;

namespace FaceLearner.ML
{
    /// <summary>
    /// FaceLearner ML Addon - Provides machine learning capabilities.
    /// 
    /// This addon is OPTIONAL. The main FaceLearner mod works without it,
    /// but with reduced functionality (no ML training, simpler face generation).
    /// 
    /// What this addon provides:
    /// - LEARN tab functionality
    /// - Full ML-based face generation from images
    /// - Landmark detection with ONNX models
    /// - Knowledge training from datasets
    /// - CMA-ES optimization
    /// 
    /// Loading order: This loads AFTER FaceLearner (Core).
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        // ML Addon has its own version (independent of Core)
        public const string VERSION = "3.0.41";
        public const string MOD_NAME = "FaceLearner.ML";
        
        public static string ModulePath { get; private set; }
        
        private MLAddonImplementation _addon;
        
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            // Get module path - Assembly is in bin\Win64_Shipping_Client\
            // Need to go up TWO levels to get to module root (FaceLearner.ML\)
            string assemblyDir = Path.GetDirectoryName(typeof(SubModule).Assembly.Location);
            ModulePath = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..")) + Path.DirectorySeparatorChar;
            
            Log($"{MOD_NAME} v{VERSION} loaded");
            Log($"Module path: {ModulePath}");
        }
        
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            try
            {
                // Create and register addon with Core
                // Note: Initialize() will be called later by Core with FaceController
                _addon = new MLAddonImplementation();
                MLAddonRegistry.Register(_addon);
                
                Log($"{MOD_NAME} registered with FaceLearner Core");
            }
            catch (Exception ex)
            {
                Log($"Error registering addon: {ex.Message}");
            }
        }
        
        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            
            try
            {
                _addon?.Dispose();
                MLAddonRegistry.Unregister();
            }
            catch { }
        }
        
        public static void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[{MOD_NAME}] {message}");
            
            try
            {
                FaceLearner.SubModule.Log($"[ML] {message}");
            }
            catch
            {
                // Core might not be loaded yet
            }
        }
    }
}
