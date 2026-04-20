using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace FiefManagement
{
    public class FiefManagementSubModule : MBSubModuleBase
    {
        private const string HarmonyId = "com.fiefmanagement.patch";
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
                "[FiefManagement] loaded", Colors.Green));
        }
    }
}
