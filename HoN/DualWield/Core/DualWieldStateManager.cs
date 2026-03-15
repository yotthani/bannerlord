using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace DualWield.Core
{
    /// <summary>
    /// Tracks per-agent dual wield state: whether they're dual wielding and
    /// which hand strikes next.
    ///
    /// Attack hand alternation:
    ///   BeginAttack() is called at animation start (act_ready_*) — advances the
    ///   counter and sets IsCurrentOffHand for the entire attack sequence.
    ///   IsCurrentStrikeOffHand() is called during release/damage — reads the
    ///   flag set by BeginAttack without advancing.
    ///
    ///   Previous design advanced on blow connection (ComputeBlowMagnitudeMelee),
    ///   which meant misses never toggled the hand.
    /// </summary>
    public static class DualWieldStateManager
    {
        private static readonly Dictionary<int, DualWieldState> _states = new Dictionary<int, DualWieldState>();

        public static void Register(Agent agent)
        {
            _states[agent.Index] = new DualWieldState();
        }

        public static void Unregister(Agent agent)
        {
            _states.Remove(agent.Index);
        }

        public static bool IsDualWielding(Agent agent)
        {
            return agent != null && _states.ContainsKey(agent.Index);
        }

        /// <summary>
        /// Called at the START of an attack (act_ready_*).
        /// Advances the attack counter and returns true if this attack should use off-hand.
        /// Sets IsCurrentOffHand for the rest of the attack sequence (release, damage).
        /// </summary>
        public static bool BeginAttack(Agent agent)
        {
            if (!_states.TryGetValue(agent.Index, out var state))
                return false;

            state.IsCurrentOffHand = state.AttackCount % 2 == 1;
            state.AttackCount++;
            return state.IsCurrentOffHand;
        }

        /// <summary>
        /// Called DURING an attack (act_release_*, damage calculation).
        /// Returns the hand decided by the most recent BeginAttack() — does NOT advance.
        /// </summary>
        public static bool IsCurrentStrikeOffHand(Agent agent)
        {
            if (!_states.TryGetValue(agent.Index, out var state))
                return false;
            return state.IsCurrentOffHand;
        }

        public static int GetCount() => _states.Count;

        public static string GetDebugInfo(Agent agent)
        {
            if (agent == null) return "agent=null";
            if (!_states.TryGetValue(agent.Index, out var state))
                return $"NOT registered (index={agent.Index})";
            return $"registered (attacks={state.AttackCount}, offHand={state.IsCurrentOffHand})";
        }

        public static void Clear()
        {
            _states.Clear();
        }

        private class DualWieldState
        {
            public int AttackCount;
            public bool IsCurrentOffHand;
        }
    }
}
