using BannerlordCommonLib.Diagnostics;
using BannerlordCommonLib.Utilities;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace BannerlordModToolkit
{
    public class BMTSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ScreenshotCapture.Initialize("BannerlordModToolkit");
            ErrorCapture.Initialize();
        }
        
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new CombatCaptureBehavior());
        }
        
        protected override void OnApplicationTick(float dt)
        {
            // F12 = capture screenshot with description prompt
            if (TaleWorlds.InputSystem.Input.IsKeyPressed(TaleWorlds.InputSystem.InputKey.F12))
            {
                ErrorCapture.AddScreenshot("User triggered screenshot", "Manual");
                Log.Info("BMT", "Screenshot captured (F12)");
            }
        }
    }
}
