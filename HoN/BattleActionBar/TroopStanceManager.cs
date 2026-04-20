using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace BattleActionBar
{
    public enum TroopStance
    {
        None,
        BracedForCavalry,
        PikeWall,
        Testudo,
        LineCharge,
        Skirmish
    }

    /// <summary>
    /// Tracks active stances per formation. Note: actual formation-changing
    /// API calls (ArrangementOrder, FormOrder) were private-set in current
    /// Bannerlord; this manager keeps state and the UI hint text but does not
    /// directly mutate formation arrangement. Hook into FormationOrder system
    /// to wire actual behavior.
    /// </summary>
    public static class TroopStanceManager
    {
        private static readonly Dictionary<Formation, TroopStance> _stances = new Dictionary<Formation, TroopStance>();
        private static readonly Dictionary<Formation, float> _stanceTimes = new Dictionary<Formation, float>();

        public static void SetStance(Formation formation, TroopStance stance)
        {
            if (formation == null) return;

            if (GetStance(formation) == stance)
            {
                ClearStance(formation);
                return;
            }

            _stances[formation] = stance;
            _stanceTimes[formation] = 0f;
            BattleActionBarLog.Debug($"{formation.RepresentativeClass} stance: {stance}");
        }

        public static TroopStance GetStance(Formation formation)
        {
            return formation != null && _stances.TryGetValue(formation, out var stance)
                ? stance : TroopStance.None;
        }

        public static void ClearStance(Formation formation)
        {
            if (formation == null) return;
            _stances.Remove(formation);
            _stanceTimes.Remove(formation);
            BattleActionBarLog.Debug($"{formation.RepresentativeClass} stance cleared");
        }

        public static void ClearAllStances()
        {
            _stances.Clear();
            _stanceTimes.Clear();
        }

        public static void Tick(float dt)
        {
            // Update timers
            var keys = new List<Formation>(_stanceTimes.Keys);
            foreach (var key in keys)
            {
                _stanceTimes[key] += dt;
            }

            // Auto-cancel on movement: API access for Formation.MovementOrder differs
            // across Bannerlord versions (sometimes private). The setting is preserved
            // but auto-cancel is currently disabled — manual stance clearing still works.
        }
    }
}
