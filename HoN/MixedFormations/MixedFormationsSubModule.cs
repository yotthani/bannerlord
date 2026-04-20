using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace MixedFormations
{
    /// <summary>
    /// Standalone module entry point. Mirrors the DualWieldSubModule pattern:
    /// - Own Harmony ID so it can be loaded/unloaded independently
    /// - Adds MissionBehavior gated by MCM toggle
    /// - Wires the static patches to the live behavior instance
    /// </summary>
    public class MixedFormationsSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.mixedformations.patch";
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll();
            }
            catch
            {
                // Patch failures should not prevent module load.
                // FormationLayoutPatches use try/catch internally so a failed
                // patch site simply degrades to vanilla behavior.
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll(HarmonyId);
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage(
                "[MixedFormations] loaded",
                Colors.Green));
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            if (!MixedFormationsSettings.Get().EnableMixedFormationLayouts)
                return;

            var behavior = new MixedFormationLayoutBehavior();
            mission.AddMissionBehavior(behavior);
            FormationLayoutPatches.SetBehavior(behavior);
        }
    }
}
