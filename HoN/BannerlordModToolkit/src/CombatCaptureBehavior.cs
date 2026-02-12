using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BannerlordCommonLib.Diagnostics;
using BannerlordCommonLib.Utilities;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BannerlordModToolkit
{
    public class CombatCaptureBehavior : MissionBehavior
    {
        private List<CombatEvent> _events = new();
        private string _sessionId = Guid.NewGuid().ToString("N")[..8];
        
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
        
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, 
            in MissionWeapon weapon, in Blow blow, in AttackCollisionData collision)
        {
            try
            {
                _events.Add(new CombatEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Damage = blow.InflictedDamage,
                    Absorbed = blow.AbsorbedByArmor,
                    DamageType = blow.DamageType.ToString(),
                    WeaponId = weapon.Item?.StringId,
                    AttackerTroop = affectorAgent?.Character?.Name?.ToString(),
                    VictimTroop = affectedAgent?.Character?.Name?.ToString(),
                    BodyPart = collision.VictimHitBodyPart.ToString()
                });
            }
            catch { }
        }
        
        public override void OnMissionEnded(IMission mission)
        {
            if (_events.Count == 0) return;
            var folder = Path.Combine(BasePath.Name, "Modules", "BannerlordModToolkit", "CapturedData");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"combat_{_sessionId}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(_events));
            Log.Info("BMT", $"Captured {_events.Count} combat events");
        }
    }
    
    public class CombatEvent
    {
        public DateTime Timestamp { get; set; }
        public int Damage { get; set; }
        public int Absorbed { get; set; }
        public string DamageType { get; set; }
        public string WeaponId { get; set; }
        public string AttackerTroop { get; set; }
        public string VictimTroop { get; set; }
        public string BodyPart { get; set; }
    }
}
