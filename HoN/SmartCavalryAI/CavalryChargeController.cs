using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SmartCavalryAI
{
    /// <summary>
    /// Helpers for coordinated cavalry charges. NOTE: Formation.ArrangementOrder
    /// is private-set in current Bannerlord — true line-formation enforcement
    /// must be handled via SetControlledByAI / Formation.SetPositioning instead.
    /// These helpers compute target positions; actual repositioning is left to
    /// the engine via SetMovementOrder.
    /// </summary>
    public static class CavalryChargeController
    {
        public static void ExecuteCoordinatedCharge(Formation formation, Vec3 targetPosition)
        {
            if (formation == null || formation.CountOfUnits == 0)
                return;

            var agents = formation.UnitsWithoutLooseDetachedOnes.Cast<Agent>().ToList();
            if (agents.Count == 0) return;

            Vec3 formationCenter = Vec3.Zero;
            foreach (var agent in agents)
            {
                formationCenter += agent.Position;
            }
            formationCenter /= agents.Count;

            Vec3 chargeDirection = (targetPosition - formationCenter).NormalizedCopy();
            Vec3 lineDirection = Vec3.CrossProduct(chargeDirection, Vec3.Up).NormalizedCopy();

            float spacing = SmartCavalryAISettings.Get().ChargeLineSpacing;
            float unitWidth = 2f;
            float totalWidth = (agents.Count - 1) * unitWidth * spacing;

            var sortedAgents = agents.OrderBy(a =>
                Vec3.DotProduct(a.Position - formationCenter, lineDirection)).ToList();

            for (int i = 0; i < sortedAgents.Count; i++)
            {
                float offset = -totalWidth / 2f + i * unitWidth * spacing;
                Vec3 targetPos = formationCenter + lineDirection * offset;
                // TODO: write targetPos via SetPositioning when API is wired.
            }
        }

        public static bool IsChargeLineFormed(Formation formation, float tolerance = 5f)
        {
            var agents = formation.UnitsWithoutLooseDetachedOnes.Cast<Agent>().ToList();
            if (agents.Count < 2) return true;

            var positions = agents.Select(a => a.Position).ToList();
            Vec3 center = Vec3.Zero;
            foreach (var pos in positions) center += pos;
            center /= positions.Count;

            Vec3 facing = formation.Direction.ToVec3();
            Vec3 lineDir = Vec3.CrossProduct(facing, Vec3.Up).NormalizedCopy();

            float maxForwardDeviation = 0f;
            foreach (var pos in positions)
            {
                Vec3 offset = pos - center;
                float forwardDev = Math.Abs(Vec3.DotProduct(offset, facing));
                if (forwardDev > maxForwardDeviation)
                    maxForwardDeviation = forwardDev;
            }

            return maxForwardDeviation < tolerance;
        }

        public static void ReformAfterCharge(Formation formation)
        {
            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
        }

        public static Vec3 CalculateReformPosition(Formation formation, Vec3 chargeEndPosition)
        {
            float reformDistance = SmartCavalryAISettings.Get().ReformDistanceAfterCharge;
            Vec3 chargeDir = formation.Direction.ToVec3();
            return chargeEndPosition + chargeDir * reformDistance;
        }
    }
}
