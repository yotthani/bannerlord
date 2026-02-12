using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.SmartCavalryAI
{
    public static class CavalryChargeController
    {
        public static void ExecuteCoordinatedCharge(Formation formation, Vec3 targetPosition)
        {
            if (formation == null || formation.CountOfUnits == 0)
                return;
            
            formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
            
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
            
            float spacing = Settings.Instance?.ChargeLineSpacing ?? 1.2f;
            float unitWidth = 2f;
            float totalWidth = (agents.Count - 1) * unitWidth * spacing;
            
            var sortedAgents = agents.OrderBy(a => 
                Vec3.DotProduct(a.Position - formationCenter, lineDirection)).ToList();
            
            for (int i = 0; i < sortedAgents.Count; i++)
            {
                float offset = -totalWidth / 2f + i * unitWidth * spacing;
                Vec3 targetPos = formationCenter + lineDirection * offset;
                
                // Formation system handles actual positioning
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
            formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
        }
        
        public static Vec3 CalculateReformPosition(Formation formation, Vec3 chargeEndPosition)
        {
            float reformDistance = Settings.Instance?.ReformDistanceAfterCharge ?? 25f;
            Vec3 chargeDir = formation.Direction.ToVec3();
            return chargeEndPosition + chargeDir * reformDistance;
        }
    }
}
