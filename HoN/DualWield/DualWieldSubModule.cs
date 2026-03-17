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
        private static Harmony _harmony;
        private static bool _patchesApplied;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            DualWieldLog.Clear();
            DualWieldLog.Log("OnSubModuleLoad: v7.50 — patches in OnGameStart, lazy behavior injection");

            // Create Harmony instance but DO NOT PatchAll yet.
            // Patches are applied in OnGameStart — after save preview is complete.
            _harmony = new Harmony("mod.dualwield");
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            RemovePatches();
            DualWieldStateManager.Clear();
        }

        /// <summary>
        /// v7.50: Apply patches HERE — after the save is fully loaded.
        /// Timeline: OnSubModuleLoad → Save-Preview (dangerous!) → OnGameStart (safe!)
        /// The save-preview renders character portraits and creates agents while
        /// MBObjects aren't initialized. By patching here, we skip that phase entirely.
        ///
        /// Patches stay active for the entire campaign session. The IsActive guard
        /// on each patch prevents them from firing outside combat missions.
        /// </summary>
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            string gameType = game?.GameType?.GetType().Name ?? "null";
            DualWieldLog.Log($"OnGameStart: gameType={gameType}");

            if (!DualWieldSettings.Get().EnableDualWield)
            {
                DualWieldLog.Log("  → DualWield disabled in settings");
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"[DW v7.50] GameStart ({gameType}) — DW disabled in settings.",
                        Colors.Yellow));
                return;
            }

            ApplyPatches();

            InformationManager.DisplayMessage(
                new InformationMessage(
                    $"[DW v7.50] GameStart ({gameType}) — Patches applied. Ready for combat.",
                    Colors.Green));
        }

        /// <summary>
        /// v7.50: OnMissionBehaviorInitialize fires only for StartUp in BL 1.3.
        /// Combat missions don't trigger it. We keep it for diagnostics + StartUp behavior.
        /// The actual DualWieldMissionBehavior is added lazily via SpawnAgent patch.
        /// </summary>
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            var mode = mission.Mode;
            DualWieldLog.Log($"OnMissionBehaviorInitialize: mode={mode}");

            // Diagnostic: always show what mission modes we see
            InformationManager.DisplayMessage(
                new InformationMessage(
                    $"[DW v7.50] MissionInit: mode={mode}",
                    Colors.Yellow));

            // v7.50: Try to add behavior here too (belt-and-suspenders approach).
            // Works for StartUp and any modes that DO fire this hook.
            TryAddBehaviorToMission(mission);
        }

        /// <summary>
        /// Adds DualWieldMissionBehavior to a combat mission if not already present.
        /// Called from multiple hooks (OnMissionBehaviorInitialize + SpawnAgent patch).
        /// </summary>
        internal static void TryAddBehaviorToMission(Mission mission)
        {
            if (mission == null) return;
            if (!DualWieldSettings.Get().EnableDualWield) return;

            var mode = mission.Mode;
            bool isCombat = mode == MissionMode.Battle
                         || mode == MissionMode.Duel
                         || mode == MissionMode.Tournament
                         || mode == MissionMode.Stealth
                         || mode == MissionMode.Deployment;

            if (!isCombat) return;

            // Already has our behavior?
            var existing = mission.GetMissionBehavior<DualWieldMissionBehavior>();
            if (existing != null) return;

            mission.AddMissionBehavior(new DualWieldMissionBehavior());
            DualWieldLog.Log($"  → DualWieldMissionBehavior ADDED (mode={mode})");

            InformationManager.DisplayMessage(
                new InformationMessage(
                    $"[DW v7.50] COMBAT! mode={mode} — DW behavior active.",
                    Colors.Green));
        }

        /// <summary>
        /// Applies all Harmony patches. Safe to call multiple times — skips if already applied.
        /// </summary>
        internal static void ApplyPatches()
        {
            if (_patchesApplied || _harmony == null) return;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                _harmony.PatchAll(assembly);
                _patchesApplied = true;
                DualWieldLog.Log("ApplyPatches: PatchAll completed successfully");
                LogPatchedMethods();
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"ApplyPatches: ERROR — {ex}");
                InformationManager.DisplayMessage(
                    new InformationMessage($"[DualWield] ERROR: Failed to apply patches: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Removes all Harmony patches. Only called on module unload.
        /// v7.50: No longer called per-mission — patches stay active for the campaign session.
        /// The IsActive guard on each patch prevents them from firing outside combat.
        /// </summary>
        internal static void RemovePatches()
        {
            if (!_patchesApplied || _harmony == null) return;

            try
            {
                _harmony.UnpatchAll("mod.dualwield");
                _patchesApplied = false;
                DualWieldLog.Log("RemovePatches: UnpatchAll completed");
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"RemovePatches: ERROR — {ex}");
            }
        }

        private static void LogPatchedMethods()
        {
            if (_harmony == null) return;

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
                    var paramInfo = method.GetParameters();
                    var paramStr = string.Join(", ", paramInfo.Select(p =>
                        $"{(p.ParameterType.IsByRef ? "ref " : "")}{p.ParameterType.Name} {p.Name}"));
                    DualWieldLog.Log($"    SetActionChannel signature: ({paramStr})");
                }
            }

            DualWieldLog.Log($"SetActionChannel: {(hasSetActionChannel ? "PATCHED" : "NOT PATCHED")}");
            DualWieldLog.Log("=== End Patch Verification ===");

            InformationManager.DisplayMessage(
                new InformationMessage(
                    $"[DualWield] v7.50 — {count} patches applied.",
                    hasSetActionChannel ? Colors.Green : Colors.Yellow));
        }
    }
}
