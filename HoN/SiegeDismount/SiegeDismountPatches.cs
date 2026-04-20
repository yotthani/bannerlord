using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace SiegeDismount
{
    /// <summary>
    /// Harmony patches for Siege Dismount.
    ///
    /// NOTE: The original HoN had a `Mission.SpawnAgent` prefix that called
    /// `AgentBuildData.MountAgent(null)` to support a "DismountKeepOnMap" mode
    /// where the horse spawns separately. That API surface differs in the
    /// current Bannerlord version, so the patch was removed.
    ///
    /// The two main behaviors (DismountToInventory + AutoRemountAfter) work
    /// purely by removing the mount from the player's BattleEquipment in
    /// SiegeDismountBehavior.OnBehaviorInitialize — no patch required.
    ///
    /// "DismountKeepOnMap" mode currently behaves the same as Vanilla; can be
    /// re-implemented when the correct AgentBuildData API is identified.
    /// </summary>
    [HarmonyPatch]
    public static class SiegeDismountPatches
    {
        // Reserved for future patch points (see class comment).
    }
}
