using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TaleWorlds.MountAndBlade.CustomBattle;
using TaleWorlds.ObjectSystem;

namespace FaceLearner.Core
{
    /// <summary>
    /// EXACT COPY of CustomGameManager - no modifications
    /// </summary>
    public class FaceLearnerGameManager : MBGameManager
    {
        protected override void DoLoadingForGameManager(GameManagerLoadingSteps gameManagerLoadingStep, out GameManagerLoadingSteps nextStep)
        {
            nextStep = GameManagerLoadingSteps.None;
            switch (gameManagerLoadingStep)
            {
                case GameManagerLoadingSteps.PreInitializeZerothStep:
                    FaceLearner.SubModule.Log("GameManager: PreInitializeZerothStep");
                    MBGameManager.LoadModuleData(isLoadGame: false);
                    MBGlobals.InitializeReferences();
                    Game.CreateGame(new FaceLearnerGame(), this).DoLoading();
                    nextStep = GameManagerLoadingSteps.FirstInitializeFirstStep;
                    break;
                case GameManagerLoadingSteps.FirstInitializeFirstStep:
                    {
                        FaceLearner.SubModule.Log("GameManager: FirstInitializeFirstStep");
                        bool flag = true;
                        foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
                        {
                            flag = flag && item.DoLoading(Game.Current);
                        }
                        nextStep = ((!flag) ? GameManagerLoadingSteps.FirstInitializeFirstStep : GameManagerLoadingSteps.WaitSecondStep);
                        FaceLearner.SubModule.Log($"  SubModules done: {flag}");
                        break;
                    }
                case GameManagerLoadingSteps.WaitSecondStep:
                    FaceLearner.SubModule.Log("GameManager: WaitSecondStep");
                    MBGameManager.StartNewGame();
                    nextStep = GameManagerLoadingSteps.SecondInitializeThirdState;
                    break;
                case GameManagerLoadingSteps.SecondInitializeThirdState:
                    {
                        bool done = Game.Current.DoLoading();
                        FaceLearner.SubModule.Log($"GameManager: SecondInitializeThirdState, done={done}");
                        nextStep = (done ? GameManagerLoadingSteps.PostInitializeFourthState : GameManagerLoadingSteps.SecondInitializeThirdState);
                        break;
                    }
                case GameManagerLoadingSteps.PostInitializeFourthState:
                    FaceLearner.SubModule.Log("GameManager: PostInitializeFourthState");
                    nextStep = GameManagerLoadingSteps.FinishLoadingFifthStep;
                    break;
                case GameManagerLoadingSteps.FinishLoadingFifthStep:
                    FaceLearner.SubModule.Log("GameManager: FinishLoadingFifthStep - DONE");
                    nextStep = GameManagerLoadingSteps.None;
                    break;
            }
        }

        public override void OnAfterCampaignStart(Game game)
        {
            // CustomGameManager calls MultiplayerMain.Initialize(new GameNetworkHandler()) here
            // but we skip it as those types are internal to CustomBattle DLL
        }

        public override void OnLoadFinished()
        {
            FaceLearner.SubModule.Log("GameManager: OnLoadFinished called!");
            base.OnLoadFinished();
            FaceLearner.SubModule.Log("GameManager: Pushing FaceLearnerState...");
            Game.Current.GameStateManager.CleanAndPushState(Game.Current.GameStateManager.CreateState<FaceLearnerState>());
            FaceLearner.SubModule.Log("GameManager: State pushed!");
        }
    }

    /// <summary>
    /// EXACT COPY of CustomGame - no modifications except removed scene loading
    /// </summary>
    public class FaceLearnerGame : GameType
    {
        public override bool IsCoreOnlyGameMode => true;
        
        // Set IsDevelopment = true to bypass XML gameType filtering
        // This makes ignoreGameTypeInclusionCheck = true in LoadXML
        public override bool IsDevelopment => true;

        protected override void OnInitialize()
        {
            Game currentGame = CurrentGame;
            IGameStarter gameStarter = new BasicGameStarter();
            InitializeGameModels(gameStarter);
            GameManager.InitializeGameStarter(currentGame, gameStarter);
            GameManager.OnGameStart(CurrentGame, gameStarter);
            MBObjectManager objectManager = currentGame.ObjectManager;
            currentGame.SetBasicModels(gameStarter.Models);
            currentGame.CreateGameManager();
            GameManager.BeginGameStart(CurrentGame);
            currentGame.InitializeDefaultGameObjects();
            
            // IsDevelopment = true bypasses XML gameType filtering
            currentGame.LoadBasicFiles();
            LoadCustomGameXmls();
            
            objectManager.UnregisterNonReadyObjects();
            currentGame.SetDefaultEquipments(new Dictionary<string, Equipment>());
            objectManager.UnregisterNonReadyObjects();
            GameManager.OnNewCampaignStart(CurrentGame, null);
            GameManager.OnAfterCampaignStart(CurrentGame);
            GameManager.OnGameInitializationFinished(CurrentGame);
        }

        private void InitializeGameModels(IGameStarter basicGameStarter)
        {
            // EXACT same models as CustomGame
            basicGameStarter.AddModel(new CustomBattleAgentStatCalculateModel());
            basicGameStarter.AddModel(new CustomAgentApplyDamageModel());
            basicGameStarter.AddModel(new CustomBattleApplyWeatherEffectsModel());
            basicGameStarter.AddModel(new CustomBattleAutoBlockModel());
            basicGameStarter.AddModel(new CustomBattleMoraleModel());
            basicGameStarter.AddModel(new CustomBattleInitializationModel());
            basicGameStarter.AddModel(new CustomBattleSpawnModel());
            basicGameStarter.AddModel(new DefaultAgentDecideKilledOrUnconsciousModel());
            basicGameStarter.AddModel(new DefaultMissionDifficultyModel());
            basicGameStarter.AddModel(new DefaultRidingModel());
            basicGameStarter.AddModel(new DefaultStrikeMagnitudeModel());
            basicGameStarter.AddModel(new CustomBattleBannerBearersModel());
            basicGameStarter.AddModel(new DefaultFormationArrangementModel());
            basicGameStarter.AddModel(new DefaultDamageParticleModel());
            basicGameStarter.AddModel(new DefaultItemPickupModel());
            basicGameStarter.AddModel(new DefaultItemValueModel());
            basicGameStarter.AddModel(new DefaultSiegeEngineCalculationModel());
        }

        private void LoadCustomGameXmls()
        {
            ObjectManager.LoadXML("Items");
            ObjectManager.LoadXML("EquipmentRosters");
            ObjectManager.LoadXML("NPCCharacters");
            ObjectManager.LoadXML("SPCultures");
        }

        protected override void BeforeRegisterTypes(MBObjectManager objectManager)
        {
        }

        protected override void OnRegisterTypes(MBObjectManager objectManager)
        {
            // EXACT same as CustomGame
            objectManager.RegisterType<BasicCharacterObject>("NPCCharacter", "NPCCharacters", 43u);
            objectManager.RegisterType<BasicCultureObject>("Culture", "SPCultures", 17u);
        }

        protected override void DoLoadingForGameType(GameTypeLoadingStates gameTypeLoadingState, out GameTypeLoadingStates nextState)
        {
            // EXACT same as CustomGame
            nextState = GameTypeLoadingStates.None;
            switch (gameTypeLoadingState)
            {
                case GameTypeLoadingStates.InitializeFirstStep:
                    CurrentGame.Initialize();
                    nextState = GameTypeLoadingStates.WaitSecondStep;
                    break;
                case GameTypeLoadingStates.WaitSecondStep:
                    nextState = GameTypeLoadingStates.LoadVisualsThirdState;
                    break;
                case GameTypeLoadingStates.LoadVisualsThirdState:
                    nextState = GameTypeLoadingStates.PostInitializeFourthState;
                    break;
                case GameTypeLoadingStates.PostInitializeFourthState:
                    break;
            }
        }

        public override void OnStateChanged(GameState oldState)
        {
        }

        public override void OnDestroy()
        {
        }
    }

    /// <summary>
    /// EXACT COPY of CustomBattleState
    /// </summary>
    public class FaceLearnerState : GameState
    {
        public override bool IsMusicMenuState => true;

        protected override void OnInitialize()
        {
            FaceLearner.SubModule.Log("State: OnInitialize");
            base.OnInitialize();
            FaceLearner.SubModule.Log("State: OnInitialize done");
        }
    }
}
