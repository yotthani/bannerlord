using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace DualWieldPrototype
{
    public sealed class DualWieldPrototypeSubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _harmony = new Harmony("DualWieldPrototype");
            _harmony.PatchAll(typeof(DualWieldPrototypeSubModule).Assembly);
            DualWieldPrototypeLogger.Log("submodule_load harmony_patched=true scope=MissionMainAgentController.ControlTick");
        }

        protected override void OnSubModuleUnloaded()
        {
            _harmony?.UnpatchAll("DualWieldPrototype");
            DualWieldPrototypeLogger.Log("submodule_unload harmony_unpatched=true");
            base.OnSubModuleUnloaded();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            string missionDescription = DualWieldPrototypeMissionFilters.DescribeMission(mission);
            bool supported = DualWieldPrototypeMissionFilters.IsSupportedMission(mission);
            DualWieldPrototypeLogger.Log($"mission_init supported={supported} {missionDescription}");
            mission.AddMissionBehavior(new DualWieldPrototypeMissionBehavior());
        }
    }
}
