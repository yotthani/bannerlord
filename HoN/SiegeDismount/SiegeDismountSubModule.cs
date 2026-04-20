using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SiegeDismount
{
    public class SiegeDismountSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.siegedismount.patch";
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll();
            }
            catch { }
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
                "[SiegeDismount] loaded",
                Colors.Green));
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            // Always add — behavior re-checks settings at OnBehaviorInitialize so
            // runtime toggling from MCM works without needing a new mission.
            mission.AddMissionBehavior(new SiegeDismountBehavior());
        }
    }
}
