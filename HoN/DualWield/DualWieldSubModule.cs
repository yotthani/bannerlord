using System;
using System.Linq;
using System.Reflection;
using DualWield.Core;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DualWield
{
    public class DualWieldSubModule : MBSubModuleBase
    {
        private Harmony _harmony;
        private bool _patchesApplied;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // Clear the file log at start of each session
            DualWieldLog.Clear();
            DualWieldLog.Log("OnSubModuleLoad starting...");

            try
            {
                _harmony = new Harmony("mod.dualwield");
                var assembly = Assembly.GetExecutingAssembly();
                _harmony.PatchAll(assembly);
                _patchesApplied = true;
                DualWieldLog.Log("PatchAll completed successfully");
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"ERROR: PatchAll failed: {ex}");
                InformationManager.DisplayMessage(
                    new InformationMessage($"[DualWield] ERROR: Failed to apply patches: {ex.Message}", Colors.Red));
                _patchesApplied = false;
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll("mod.dualwield");
            DualWieldStateManager.Clear();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            if (!_patchesApplied) return;
            if (!DualWieldSettings.Get().EnableDualWield) return;

            // v7.46: Only add to combat-relevant missions.
            // Without this filter, preview missions (inventory character screen, etc.)
            // also get the behavior, which causes SpawnPatches to register preview agents
            // as dual wielders → broken character previews.
            var mode = mission.Mode;
            if (mode != MissionMode.Battle &&
                mode != MissionMode.Duel &&
                mode != MissionMode.Tournament &&
                mode != MissionMode.Stealth)
            {
                DualWieldLog.Log($"Skipping non-combat mission (mode={mode})");
                return;
            }

            mission.AddMissionBehavior(new DualWieldMissionBehavior());
            DualWieldLog.Log($"DualWieldMissionBehavior added to mission (mode={mode})");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (_patchesApplied)
            {
                // Enumerate ALL patched methods and log them to file
                var patchedMethods = _harmony.GetPatchedMethods().ToList();
                int count = patchedMethods.Count;

                DualWieldLog.Log($"=== Patch Verification: {count} methods patched ===");

                bool hasSetActionChannel = false;

                foreach (var method in patchedMethods)
                {
                    var patchInfo = Harmony.GetPatchInfo(method);
                    bool isOurs = patchInfo != null &&
                        (patchInfo.Prefixes.Any(p => p.owner == "mod.dualwield") ||
                         patchInfo.Postfixes.Any(p => p.owner == "mod.dualwield"));

                    if (!isOurs) continue;

                    string methodDesc = $"{method.DeclaringType?.Name}.{method.Name}";
                    string patchTypes = "";
                    if (patchInfo.Prefixes.Any(p => p.owner == "mod.dualwield")) patchTypes += "prefix ";
                    if (patchInfo.Postfixes.Any(p => p.owner == "mod.dualwield")) patchTypes += "postfix ";

                    DualWieldLog.Log($"  PATCHED: {methodDesc} ({patchTypes.Trim()})");

                    if (method.Name == "SetActionChannel")
                    {
                        hasSetActionChannel = true;

                        // Log the parameter details of the patched method
                        var paramInfo = method.GetParameters();
                        var paramStr = string.Join(", ", paramInfo.Select(p =>
                            $"{(p.ParameterType.IsByRef ? "ref " : "")}{p.ParameterType.Name} {p.Name}"));
                        DualWieldLog.Log($"    SetActionChannel signature: ({paramStr})");
                    }
                }

                if (hasSetActionChannel)
                {
                    DualWieldLog.Log("SetActionChannel: PATCHED (Harmony prefix active)");
                    DualWieldLog.Log("Animation redirect: Harmony prefix + OnMissionTick fallback");
                }
                else
                {
                    DualWieldLog.Log("WARNING: SetActionChannel NOT patched by Harmony!");
                    DualWieldLog.Log("Animation redirect: OnMissionTick fallback ONLY");
                }

                DualWieldLog.Log("=== End Patch Verification ===");

                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"[DualWield] v7.16 XML-ANIM-TEST. {count} patches. Testing XML item_usage_sets + offhand IK.",
                        hasSetActionChannel ? Colors.Green : Colors.Yellow));
            }
        }
    }
}
