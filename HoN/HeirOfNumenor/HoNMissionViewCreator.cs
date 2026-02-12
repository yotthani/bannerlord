using System.Collections.Generic;
using HeirOfNumenor.Features.BattleActionBar;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace HeirOfNumenor
{
    /// <summary>
    /// Registers custom MissionViews for HeirOfNumenor.
    /// This is the proper way to add UI overlays to missions.
    /// </summary>
    [ViewCreatorModule]
    public static class HoNMissionViewCreator
    {
        /// <summary>
        /// Creates mission views for field battles.
        /// </summary>
        [ViewMethod("FieldBattle")]
        public static MissionView[] CreateFieldBattleViews(Mission mission)
        {
            var views = new List<MissionView>();
            
            // Add Battle Action Bar if enabled
            if (Settings.Instance?.EnableBattleActionBar ?? false)
            {
                views.Add(new BattleActionBarMissionView());
            }
            
            return views.ToArray();
        }
        
        /// <summary>
        /// Creates mission views for siege battles.
        /// Note: Action bar disabled in sieges by default.
        /// </summary>
        [ViewMethod("SiegeBattle")]
        public static MissionView[] CreateSiegeBattleViews(Mission mission)
        {
            var views = new List<MissionView>();
            
            // Siege-specific views could go here
            
            return views.ToArray();
        }
    }
}
