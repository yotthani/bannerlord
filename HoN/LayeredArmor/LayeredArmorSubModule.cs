using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace LayeredArmor
{
    /// <summary>
    /// Standalone entry point. No MissionBehavior needed — the feature works
    /// entirely through the Harmony patch on Agent.GetBaseArmorEffectivenessForBodyPart
    /// and a static per-Hero dictionary in LayeredArmorManager.
    /// </summary>
    public class LayeredArmorSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.layeredarmor.patch";
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll();
            }
            catch { /* Patch sites fall back to vanilla via try/catch in each patch. */ }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll(HarmonyId);
            LayeredArmorManager.ClearAll();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage(
                "[LayeredArmor] loaded",
                Colors.Green));
        }
    }
}
