using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace MixedFormations
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
        private readonly Dictionary<Formation, FormationLayoutType> _formationLayouts = new Dictionary<Formation, FormationLayoutType>();

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
            var ranged = agents.Where(IsRangedUnit).ToList();
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

        private static bool IsRangedUnit(Agent agent)
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
            int infantryRows = MixedFormationsSettings.Get().InfantryRowDepth;
            // TODO: apply layout via SetPositioning / ArrangementOrderRow etc.
            // Formation.ArrangementOrder is private-set in recent Bannerlord builds;
            // the proper API is Formation.SetControlledByAI + FormationOrder methods.
        }

        private void ArrangeRangedFrontInfantryBack(Formation formation, List<Agent> melee, List<Agent> ranged)
        {
            int rangedRows = MixedFormationsSettings.Get().RangedRowDepth;
            // TODO: see ArrangeInfantryFrontRangedBack note
        }

        private void ArrangeRangedWings(Formation formation, List<Agent> melee, List<Agent> ranged)
        {
            // TODO: see ArrangeInfantryFrontRangedBack note
        }

        private void ArrangeCheckerboard(Formation formation, List<Agent> melee, List<Agent> ranged)
        {
            // TODO: see ArrangeInfantryFrontRangedBack note
        }

        protected override void OnEndMission()
        {
            _formationLayouts.Clear();
            base.OnEndMission();
        }
    }
}
