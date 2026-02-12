using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace HeirOfNumenor.Features.BattleActionBar
{
    /// <summary>
    /// Mission view for Battle Action Bar.
    /// Registered via SubModule.OnMissionBehaviorInitialize, not [DefaultView].
    /// </summary>
    public class BattleActionBarMissionView : MissionView
    {
        private static readonly string FEATURE_NAME = "BattleActionBar";
        
        private GauntletLayer _gauntletLayer;
        private IGauntletMovie _movie;
        private BattleActionBarVM _dataSource;
        private bool _isInitialized;
        
        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            
            try
            {
                if (!(Settings.Instance?.EnableBattleActionBar ?? false))
                    return;
                
                // Only show in field battles, not sieges/arena
                if (!IsFieldBattle())
                    return;
                
                _dataSource = new BattleActionBarVM();
                _gauntletLayer = new GauntletLayer(100);
                _movie = _gauntletLayer.LoadMovie("BattleActionBar", _dataSource);
                MissionScreen.AddLayer(_gauntletLayer);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Log($"Init failed: {ex.Message}");
            }
        }
        
        private bool IsFieldBattle()
        {
            if (Mission.Current == null) return false;
            return Mission.Current.Mode == MissionMode.Battle && 
                   !Mission.Current.IsSiegeBattle;
        }
        
        private void Log(string msg)
        {
            if (Settings.Instance?.DebugMode ?? false)
                InformationManager.DisplayMessage(new InformationMessage($"[{FEATURE_NAME}] {msg}", Colors.Cyan));
        }
        
        public override void OnMissionScreenFinalize()
        {
            if (_isInitialized)
            {
                try
                {
                    MissionScreen.RemoveLayer(_gauntletLayer);
                    _gauntletLayer = null;
                    _movie = null;
                    _dataSource = null;
                    TroopStanceManager.ClearAllStances();
                }
                catch { }
            }
            base.OnMissionScreenFinalize();
        }
        
        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            
            if (!_isInitialized) return;
            
            try
            {
                TroopStanceManager.Tick(dt);
                
                var selectedFormation = GetPlayerSelectedFormation();
                _dataSource?.UpdateForFormation(selectedFormation);
                
                HandleHotkeyInput();
            }
            catch { }
        }
        
        private Formation GetPlayerSelectedFormation()
        {
            var controller = Mission.Current?.PlayerTeam?.PlayerOrderController;
            if (controller == null) return null;
            
            return controller.SelectedFormations.Count > 0 
                ? controller.SelectedFormations[0] 
                : null;
        }
        
        private void HandleHotkeyInput()
        {
            if (_dataSource?.ActionButtons == null) return;
            if (_gauntletLayer?.Input == null) return;
            
            for (int i = 0; i < _dataSource.ActionButtons.Count && i < 9; i++)
            {
                var key = (InputKey)(InputKey.D1 + i);
                if (_gauntletLayer.Input.IsKeyPressed(key))
                {
                    _dataSource.ActionButtons[i].ExecuteAction();
                }
            }
        }
    }
}
