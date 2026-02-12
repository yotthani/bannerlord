using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace HeirOfNumenor.Features.FiefManagement
{
    /// <summary>
    /// Game state for the Fief Management screen.
    /// </summary>
    public class FiefManagementState : GameState
    {
        public override bool IsMenuState => true;

        public FiefManagementState()
        {
        }
    }

    /// <summary>
    /// Gauntlet screen for Fief Management.
    /// Note: GameStateScreen attribute doesn't work in mods - we use Harmony patch instead.
    /// </summary>
    public class GauntletFiefManagementScreen : ScreenBase, IGameStateListener
    {
        private FiefManagementState _state;
        private FiefManagementVM _dataSource;
        private GauntletLayer _gauntletLayer;

        public GauntletFiefManagementScreen(FiefManagementState state)
        {
            _state = state;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _dataSource = new FiefManagementVM(OnClose);
            _gauntletLayer = new GauntletLayer("FiefManagementLayer", 100, false);

            _gauntletLayer.LoadMovie("FiefManagement", _dataSource);

            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _gauntletLayer.IsFocusLayer = true;
            _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));

            AddLayer(_gauntletLayer);
            ScreenManager.TrySetFocus(_gauntletLayer);
        }

        protected override void OnFinalize()
        {
            RemoveLayer(_gauntletLayer);

            _gauntletLayer = null;
            _dataSource?.OnFinalize();
            _dataSource = null;

            base.OnFinalize();
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);

            if (_gauntletLayer.Input.IsHotKeyReleased("Exit") || 
                _gauntletLayer.Input.IsHotKeyReleased("ToggleEscapeMenu"))
            {
                OnClose();
            }
        }

        private void OnClose()
        {
            Game.Current.GameStateManager.PopState(0);
        }

        void IGameStateListener.OnActivate() { }
        void IGameStateListener.OnDeactivate() { }
        void IGameStateListener.OnInitialize() { }
        void IGameStateListener.OnFinalize() { }
    }
}
