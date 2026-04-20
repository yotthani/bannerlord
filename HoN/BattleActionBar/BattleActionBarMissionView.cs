using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace BattleActionBar
{
    /// <summary>
    /// Mission view for the action bar. Currently relies on a "BattleActionBar"
    /// Gauntlet movie XML which is NOT shipped with this module — the C# state
    /// runs but no visible bar will appear without those GUI assets. The hotkey
    /// fallback (digits 1-9) still works once the data source is initialized.
    /// </summary>
    public class BattleActionBarMissionView : MissionView
    {
        private GauntletLayer _gauntletLayer;
        private BattleActionBarVM _dataSource;
        private bool _isInitialized;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();

            try
            {
                if (!BattleActionBarSettings.Get().EnableBattleActionBar) return;
                if (!IsFieldBattle()) return;

                _dataSource = new BattleActionBarVM();
                _gauntletLayer = new GauntletLayer("BattleActionBar", 100, true);

                try
                {
                    _gauntletLayer.LoadMovie("BattleActionBar", _dataSource);
                }
                catch
                {
                    // Movie XML not shipped with this module — hotkeys still work
                    // because _dataSource updates regardless of UI presence.
                }

                MissionScreen.AddLayer(_gauntletLayer);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                BattleActionBarLog.Error("Init", ex);
            }
        }

        private static bool IsFieldBattle()
        {
            if (Mission.Current == null) return false;
            return Mission.Current.Mode == MissionMode.Battle &&
                   !Mission.Current.IsSiegeBattle;
        }

        public override void OnMissionScreenFinalize()
        {
            if (_isInitialized)
            {
                try
                {
                    MissionScreen.RemoveLayer(_gauntletLayer);
                    _gauntletLayer = null;
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

        private static Formation GetPlayerSelectedFormation()
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

            // Hotkey input via global InputManager (GauntletLayer.Input differs across versions)
            for (int i = 0; i < _dataSource.ActionButtons.Count && i < 9; i++)
            {
                var key = (InputKey)(InputKey.D1 + i);
                if (Input.IsKeyPressed(key))
                {
                    _dataSource.ActionButtons[i].ExecuteAction();
                }
            }
        }
    }
}
