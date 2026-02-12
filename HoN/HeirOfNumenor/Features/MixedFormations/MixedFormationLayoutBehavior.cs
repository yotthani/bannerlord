using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.MixedFormations
{
    public enum FormationLayoutType
    {
        Vanilla,
        InfantryFrontRangedBack,
        RangedFrontInfantryBack,
        RangedWingsInfantryCenter,
        Checkerboard
    }
    
    public class MixedFormationLayoutBehavior : MissionBehavior
    {
        private Dictionary<Formation, FormationLayoutType> _formationLayouts = new Dictionary<Formation, FormationLayoutType>();
        
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        
        public void SetFormationLayout(Formation formation, FormationLayoutType layout)
        {
            if (formation == null) return;
            _formationLayouts[formation] = layout;
            ApplyLayout(formation, layout);
        }
        
        public FormationLayoutType GetFormationLayout(Formation formation)
        {
            if (formation == null) return FormationLayoutType.Vanilla;
            return _formationLayouts.TryGetValue(formation, out var layout) ? layout : FormationLayoutType.Vanilla;
        }
        
        private void ApplyLayout(Formation formation, FormationLayoutType layout)
        {
            if (formation == null || formation.CountOfUnits == 0) return;
            
            var agents = formation.UnitsWithoutLooseDetachedOnes.Cast<Agent>().ToList();
            var ranged = agents.Where(a => IsRangedUnit(a)).ToList();
            var melee = agents.Where(a => !IsRangedUnit(a)).ToList();
            
            if (ranged.Count == 0 || melee.Count == 0)
                return; // Not a mixed formation
            
            switch (layout)
            {
                case FormationLayoutType.InfantryFrontRangedBack:
                    ArrangeInfantryFrontRangedBack(formation, melee, ranged);
                    break;
                case FormationLayoutType.RangedFrontInfantryBack:
                    ArrangeRangedFrontInfantryBack(formation, melee, ranged);
                    break;
                case FormationLayoutType.RangedWingsInfantryCenter:
                    ArrangeRangedWings(formation, melee, ranged);
                    break;
                case FormationLayoutType.Checkerboard:
                    ArrangeCheckerboard(formation, melee, ranged);
                    break;
            }
        }
        
        private bool IsRangedUnit(Agent agent)
        {
            if (agent?.Equipment == null) return false;
            for (int i = 0; i < 4; i++)
            {
                var weapon = agent.Equipment[(EquipmentIndex)i];
                if (!weapon.IsEmpty && weapon.Item?.PrimaryWeapon?.IsRangedWeapon == true)
                    return true;
            }
            return false;
        }
        
        private void ArrangeInfantryFrontRangedBack(Formation formation, List<Agent> melee, List<Agent> ranged)
        {
            int infantryRows = Settings.Instance?.InfantryRowDepth ?? 3;
            // Melee units get priority for front positions
            // This is handled by formation arrangement system
            formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
        }
        
        private void ArrangeRangedFrontInfantryBack(Formation formation, List<Agent> melee, List<Agent> ranged)
        {
            int rangedRows = Settings.Instance?.RangedRowDepth ?? 2;
            formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
        }
        
        private void ArrangeRangedWings(Formation formation, List<Agent> melee, List<Agent> ranged)
        {
            formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
        }
        
        private void ArrangeCheckerboard(Formation formation, List<Agent> melee, List<Agent> ranged)
        {
            formation.ArrangementOrder = ArrangementOrder.ArrangementOrderScatter;
        }
        
        public override void OnMissionEnded(IMission mission)
        {
            _formationLayouts.Clear();
            base.OnMissionEnded(mission);
        }
    }
}
