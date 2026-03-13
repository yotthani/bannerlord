using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace DualWieldPrototype
{
    public sealed class DualWieldPrototypeSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "DualWieldPrototype";
        private static Harmony _harmony;
        private static bool _runtimePatchesApplied;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _harmony = new Harmony(HarmonyId);
            _runtimePatchesApplied = false;
            DualWieldPrototypeLogger.Log("submodule_load harmony_ready=true runtime_patches=false");
        }

        protected override void OnSubModuleUnloaded()
        {
            DisableRuntimePatches("submodule_unload");
            _harmony?.UnpatchAll(HarmonyId);
            DualWieldPrototypeLogger.Log("submodule_unload harmony_unpatched=true");
            base.OnSubModuleUnloaded();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            string missionDescription = DualWieldPrototypeMissionFilters.DescribeMission(mission);
            bool supported = DualWieldPrototypeMissionFilters.IsSupportedMission(mission);
            DualWieldPrototypeLogger.Log($"mission_init supported={supported} {missionDescription}");
            if (supported)
            {
                EnsureRuntimePatches("mission_init_supported");
            }
            else
            {
                DisableRuntimePatches("mission_init_unsupported");
            }

            mission.AddMissionBehavior(new DualWieldPrototypeMissionBehavior());
        }

        internal static void EnsureRuntimePatches(string reason)
        {
            if (_harmony == null || _runtimePatchesApplied)
            {
                return;
            }

            _harmony.Patch(
                GetControlTickTarget(),
                transpiler: new HarmonyMethod(typeof(DualWieldPrototypeMissionMainAgentControllerPatch), "Transpiler"));

            _harmony.Patch(
                GetAgentSetActionChannelTarget(),
                prefix: new HarmonyMethod(typeof(DualWieldPrototypeAgentSetActionChannelTracePatch), "Prefix"),
                postfix: new HarmonyMethod(typeof(DualWieldPrototypeAgentSetActionChannelTracePatch), "Postfix"));

            _runtimePatchesApplied = true;
            DualWieldPrototypeLogger.Log($"runtime_patches_enabled reason={reason}");
        }

        internal static void DisableRuntimePatches(string reason)
        {
            if (_harmony == null || !_runtimePatchesApplied)
            {
                return;
            }

            _harmony.Unpatch(GetControlTickTarget(), HarmonyPatchType.All, HarmonyId);
            _harmony.Unpatch(GetAgentSetActionChannelTarget(), HarmonyPatchType.All, HarmonyId);
            _runtimePatchesApplied = false;
            DualWieldPrototypeLogger.Log($"runtime_patches_disabled reason={reason}");
        }

        private static MethodInfo GetControlTickTarget()
        {
            return AccessTools.DeclaredMethod(typeof(MissionMainAgentController), "ControlTick");
        }

        private static MethodInfo GetAgentSetActionChannelTarget()
        {
            return AccessTools.DeclaredMethod(
                typeof(Agent),
                nameof(Agent.SetActionChannel),
                new[]
                {
                    typeof(int),
                    typeof(ActionIndexCache).MakeByRefType(),
                    typeof(bool),
                    typeof(AnimFlags),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(bool),
                    typeof(float),
                    typeof(int),
                    typeof(bool)
                });
        }
    }
}
