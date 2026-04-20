using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace FormationPresets
{
    public class FormationPresetsSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.formationpresets.patch";
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try { _harmony = new Harmony(HarmonyId); _harmony.PatchAll(); }
            catch { }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            _harmony?.UnpatchAll(HarmonyId);
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage(
                "[FormationPresets] loaded", Colors.Green));
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter cgs)
            {
                cgs.AddBehavior(new FormationPresetCampaignBehavior());
            }
        }
    }
}
