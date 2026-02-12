using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace HeirOfNumenor.Features.BattleActionBar
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
    
    public static class TroopStanceManager
    {
        private static Dictionary<Formation, TroopStance> _stances = new();
        private static Dictionary<Formation, float> _stanceTimes = new();
        
        public static void SetStance(Formation formation, TroopStance stance)
        {
            if (formation == null) return;
            
            // Toggle off if same stance
            if (GetStance(formation) == stance)
            {
                ClearStance(formation);
                return;
            }
            
            _stances[formation] = stance;
            _stanceTimes[formation] = 0f;
            ApplyStanceBehavior(formation, stance);
            Log($"{formation.RepresentativeClass} stance: {stance}");
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
            ResetFormationBehavior(formation);
            Log($"{formation.RepresentativeClass} stance cleared");
        }
        
        public static void ClearAllStances()
        {
            _stances.Clear();
            _stanceTimes.Clear();
        }
        
        public static void Tick(float dt)
        {
            foreach (var kvp in _stanceTimes)
            {
                _stanceTimes[kvp.Key] = kvp.Value + dt;
            }
            
            // Auto-cancel on movement if configured
            if (Settings.Instance?.CancelStanceOnMove ?? true)
            {
                var toRemove = new List<Formation>();
                foreach (var kvp in _stances)
                {
                    if (IsFormationMoving(kvp.Key))
                        toRemove.Add(kvp.Key);
                }
                foreach (var f in toRemove)
                    ClearStance(f);
            }
        }
        
        private static void ApplyStanceBehavior(Formation formation, TroopStance stance)
        {
            switch (stance)
            {
                case TroopStance.BracedForCavalry:
                    formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
                    formation.FormOrder = FormOrder.FormOrderDeep;
                    SetAgentsDefensive(formation, true);
                    break;
                    
                case TroopStance.PikeWall:
                    formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
                    formation.FormOrder = FormOrder.FormOrderWide;
                    SetAgentsDefensive(formation, true);
                    break;
                    
                case TroopStance.Testudo:
                    formation.ArrangementOrder = ArrangementOrder.ArrangementOrderShieldWall;
                    formation.FormOrder = FormOrder.FormOrderDeep;
                    SetAgentsDefensive(formation, true);
                    break;
                    
                case TroopStance.LineCharge:
                    formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
                    formation.FormOrder = FormOrder.FormOrderWide;
                    SetAgentsDefensive(formation, false);
                    break;
                    
                case TroopStance.Skirmish:
                    formation.ArrangementOrder = ArrangementOrder.ArrangementOrderSkein;
                    SetAgentsDefensive(formation, false);
                    break;
            }
        }
        
        private static void SetAgentsDefensive(Formation formation, bool defensive)
        {
            foreach (var unit in formation.UnitsWithoutLooseDetachedOnes)
            {
                if (unit is Agent agent)
                {
                    agent.SetIsDefending(defensive);
                }
            }
        }
        
        private static void ResetFormationBehavior(Formation formation)
        {
            formation.ArrangementOrder = ArrangementOrder.ArrangementOrderLine;
            SetAgentsDefensive(formation, false);
        }
        
        private static bool IsFormationMoving(Formation formation)
        {
            if (formation?.MovementOrder == null) return false;
            return formation.MovementOrder.OrderType == OrderType.Move ||
                   formation.MovementOrder.OrderType == OrderType.Charge ||
                   formation.MovementOrder.OrderType == OrderType.FollowMe;
        }
        
        private static void Log(string msg)
        {
            if (Settings.Instance?.DebugMode ?? false)
                InformationManager.DisplayMessage(new InformationMessage($"[Stance] {msg}", Colors.Yellow));
        }
    }
}
