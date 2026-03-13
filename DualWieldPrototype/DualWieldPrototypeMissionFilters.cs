using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace DualWieldPrototype
{
    internal static class DualWieldPrototypeMissionFilters
    {
        public static bool IsSupportedMission(Mission mission)
        {
            if (mission == null)
            {
                return false;
            }

            if (!IsSupportedMode(mission.Mode))
            {
                return false;
            }

            string sceneName = mission.SceneName ?? string.Empty;
            if (sceneName.IndexOf("inventory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("character_menu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("facegen", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        public static bool IsSupportedCombatContext(Mission mission, Agent agent)
        {
            if (!IsSupportedMission(mission))
            {
                return false;
            }

            if (mission.CurrentState != Mission.State.Continuing)
            {
                return false;
            }

            if (agent == null || !agent.IsActive() || !agent.IsMainAgent)
            {
                return false;
            }

            if (mission.MainAgent != agent)
            {
                return false;
            }

            if (!agent.IsPlayerControlled || agent.Controller != AgentControllerType.Player)
            {
                return false;
            }

            return true;
        }

        public static string DescribeMission(Mission mission)
        {
            if (mission == null)
            {
                return "mission=null";
            }

            return $"mode={mission.Mode} scene={mission.SceneName ?? "<null>"} mainAgent={(mission.MainAgent != null ? "yes" : "no")} agents={mission.Agents.Count}";
        }

        private static bool IsSupportedMode(MissionMode mode)
        {
            switch (mode)
            {
                case MissionMode.Battle:
                case MissionMode.Duel:
                case MissionMode.Stealth:
                case MissionMode.Tournament:
                    return true;
                default:
                    return false;
            }
        }
    }
}
