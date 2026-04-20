using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SmartCavalryAI
{
    public enum CavalryState
    {
        Idle,
        Forming,
        Charging,
        PassingThrough,
        Reforming
    }

    public class CavalryFormationState
    {
        public CavalryState State = CavalryState.Idle;
        public Vec3 ChargeTarget;
        public Vec3 ChargeStartPosition;
        public float ChargeStartTime;
        public bool IsCoordinatedCharge;
    }

    public class SmartCavalryAIBehavior : MissionBehavior
    {
        private readonly Dictionary<Formation, CavalryFormationState> _cavalryStates = new Dictionary<Formation, CavalryFormationState>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (!SmartCavalryAISettings.Get().EnableSmartCavalryAI)
                return;

            UpdateCavalryFormations(dt);
        }

        private void UpdateCavalryFormations(float dt)
        {
            if (Mission.Current?.PlayerTeam == null) return;

            var settings = SmartCavalryAISettings.Get();

            foreach (var formation in Mission.Current.PlayerTeam.FormationsIncludingEmpty)
            {
                if (formation == null || formation.CountOfUnits == 0) continue;
                if (!IsCavalryFormation(formation)) continue;

                if (!_cavalryStates.ContainsKey(formation))
                    _cavalryStates[formation] = new CavalryFormationState();

                var state = _cavalryStates[formation];

                switch (state.State)
                {
                    case CavalryState.Forming:
                        UpdateFormingState(formation, state, dt);
                        break;
                    case CavalryState.Charging:
                        UpdateChargingState(formation, state, dt);
                        break;
                    case CavalryState.PassingThrough:
                        UpdatePassingThroughState(formation, state, dt);
                        break;
                    case CavalryState.Reforming:
                        UpdateReformingState(formation, state, dt);
                        break;
                }

                if (settings.EnableFriendlyCollisionAvoidance)
                {
                    ApplyCollisionAvoidance(formation);
                }
            }
        }

        public void InitiateLineCharge(Formation formation, Vec3 target)
        {
            if (!_cavalryStates.ContainsKey(formation))
                _cavalryStates[formation] = new CavalryFormationState();

            var state = _cavalryStates[formation];
            state.State = CavalryState.Forming;
            state.ChargeTarget = target;
            state.ChargeStartPosition = formation.CurrentPosition.ToVec3();
            state.IsCoordinatedCharge = true;

            // ArrangementOrder is private-set in current Bannerlord; use SetMovementOrder only.
            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
        }

        private void UpdateFormingState(Formation formation, CavalryFormationState state, float dt)
        {
            float strictness = SmartCavalryAISettings.Get().ChargeFormationStrictness;

            if (IsFormationAligned(formation, strictness))
            {
                state.State = CavalryState.Charging;
                state.ChargeStartTime = Mission.Current.CurrentTime;

                var nearestEnemy = GetNearestEnemyFormation(formation);
                if (nearestEnemy != null)
                {
                    var chargeOrder = MovementOrder.MovementOrderChargeToTarget(nearestEnemy);
                    formation.SetMovementOrder(chargeOrder);
                }
            }
        }

        private void UpdateChargingState(Formation formation, CavalryFormationState state, float dt)
        {
            float distanceToTarget = (formation.CurrentPosition.ToVec3() - state.ChargeTarget).Length;
            if (distanceToTarget < 10f)
            {
                state.State = CavalryState.PassingThrough;
            }
        }

        private void UpdatePassingThroughState(Formation formation, CavalryFormationState state, float dt)
        {
            float reformDistance = SmartCavalryAISettings.Get().ReformDistanceAfterCharge;
            float distanceFromTarget = (formation.CurrentPosition.ToVec3() - state.ChargeTarget).Length;

            if (distanceFromTarget > reformDistance)
            {
                state.State = CavalryState.Reforming;
                formation.SetMovementOrder(MovementOrder.MovementOrderStop);
            }
        }

        private void UpdateReformingState(Formation formation, CavalryFormationState state, float dt)
        {
            if (IsFormationAligned(formation, 0.5f))
            {
                state.State = CavalryState.Idle;
                state.IsCoordinatedCharge = false;
            }
        }

        private static bool IsFormationAligned(Formation formation, float strictness)
        {
            if (formation.CountOfUnits < 2) return true;

            var agents = formation.UnitsWithoutLooseDetachedOnes.Cast<Agent>().ToList();
            if (agents.Count < 2) return true;

            var positions = agents.Select(a => a.Position).ToList();
            var avgY = positions.Average(p => p.y);
            var maxDeviation = positions.Max(p => Math.Abs(p.y - avgY));

            float threshold = 5f * (1f - strictness);
            return maxDeviation < threshold;
        }

        private static readonly MBList<Agent> _nearbyBuffer = new MBList<Agent>();

        private static void ApplyCollisionAvoidance(Formation formation)
        {
            var playerTeam = Mission.Current.PlayerTeam;
            foreach (var unit in formation.UnitsWithoutLooseDetachedOnes)
            {
                var agent = unit as Agent;
                if (agent == null || !agent.HasMount) continue;

                _nearbyBuffer.Clear();
                Mission.Current.GetNearbyAgents(agent.Position.AsVec2, 5f, _nearbyBuffer);

                foreach (var friendly in _nearbyBuffer)
                {
                    if (friendly == agent) continue;
                    if (friendly.HasMount) continue;       // Only avoid infantry
                    if (friendly.Team != playerTeam) continue;

                    Vec3 avoidDir = (agent.Position - friendly.Position).NormalizedCopy();
                    // Agent movement is engine-driven; this is informational stub for future work.
                }
            }
        }

        private static bool IsCavalryFormation(Formation formation)
        {
            if (formation.CountOfUnits == 0) return false;

            int mounted = 0;
            foreach (var unit in formation.UnitsWithoutLooseDetachedOnes)
            {
                var agent = unit as Agent;
                if (agent?.HasMount == true) mounted++;
            }

            return mounted > formation.CountOfUnits / 2;
        }

        private static Formation GetNearestEnemyFormation(Formation ownFormation)
        {
            Formation nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var team in Mission.Current.Teams)
            {
                if (team.IsFriendOf(Mission.Current.PlayerTeam)) continue;

                foreach (var formation in team.FormationsIncludingEmpty)
                {
                    if (formation == null || formation.CountOfUnits == 0) continue;

                    float dist = ownFormation.CurrentPosition.Distance(formation.CurrentPosition);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = formation;
                    }
                }
            }

            return nearest;
        }

        protected override void OnEndMission()
        {
            _cavalryStates.Clear();
            base.OnEndMission();
        }
    }
}
