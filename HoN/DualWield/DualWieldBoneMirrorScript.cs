using DualWield.Core;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace DualWield
{
    /// <summary>
    /// v10.0 PoC: ScriptComponentBehavior attached to an Agent's GameEntity.
    ///
    /// When EnableScriptDrivenPostIntegrateCallback() is active on the skeleton,
    /// the native engine calls SkeletonPostIntegrateCallback AFTER animation computation
    /// but BEFORE GPU rendering. This is the ONLY hook point where bone transforms
    /// can be modified without being overwritten by the native pipeline.
    ///
    /// SetBoneLocalFrame (per-tick in OnMissionTick) does NOT work — confirmed dead end.
    /// SetOutQuat/SetOutBoneDisplacement (in this callback) write to the animation RESULT.
    /// </summary>
    [ScriptComponentParams("dw_bone_mirror")]
    public class DualWieldBoneMirrorScript : ScriptComponentBehavior
    {
        /// <summary>
        /// When true, the callback actively mirrors RH arm bones onto LH arm bones.
        /// Toggled by the V-key in DualWieldMissionBehavior.
        /// </summary>
        public static bool MirrorEnabled { get; set; }

        /// <summary>
        /// Reference to the agent's skeleton. Set when attaching the script.
        /// Needed by AnimResult methods (GetEntitialOutTransform, SetOutQuat, etc.)
        /// </summary>
        public Skeleton AgentSkeleton { get; set; }

        /// <summary>
        /// Tracks whether the callback has ever fired (diagnostic).
        /// </summary>
        public static bool CallbackFired { get; private set; }
        private static int _callbackCount;

        /// <summary>
        /// Called by native engine AFTER animation integration, BEFORE rendering.
        /// This is where we can safely modify bone transforms without being overwritten.
        ///
        /// Return true = we modified the animation result, engine should use our values.
        /// Return false = no modification, use normal animation result.
        /// </summary>
        protected override bool SkeletonPostIntegrateCallback(AnimResult animResult)
        {
            _callbackCount++;
            CallbackFired = true;

            if (!MirrorEnabled || AgentSkeleton == null)
                return false;

            // Log first callback
            if (_callbackCount == 1)
            {
                DualWieldLog.Log("[BoneMirror] SkeletonPostIntegrateCallback FIRED for first time!");
            }

            try
            {
                // ── PHASE 1: Mirror UpperarmR → UpperarmL and ForearmR → ForearmL ──
                // Read the current RH arm transforms from the animation result
                sbyte upperarmR = 22; // HumanBone.UpperarmR
                sbyte upperarmL = 15; // HumanBone.UpperarmL

                Transformation rhTransform = animResult.GetEntitialOutTransform(upperarmR, AgentSkeleton);

                // Mirror position: negate X (mirror across sagittal/YZ plane)
                Vec3 mirroredPos = new Vec3(-rhTransform.Origin.x, rhTransform.Origin.y, rhTransform.Origin.z);
                animResult.SetOutBoneDisplacement(upperarmL, mirroredPos, AgentSkeleton);

                // Mirror rotation across sagittal (YZ) plane:
                // For a proper reflection, negate the entire side vector (X-axis)
                // and negate X components of forward and up vectors
                Mat3 rhRot = rhTransform.Rotation;
                Mat3 mirroredRot = new Mat3(
                    new Vec3(-rhRot.s.x, -rhRot.s.y, -rhRot.s.z), // side: fully negated
                    new Vec3(-rhRot.f.x,  rhRot.f.y,  rhRot.f.z), // forward: negate X
                    new Vec3(-rhRot.u.x,  rhRot.u.y,  rhRot.u.z)  // up: negate X
                );
                animResult.SetOutQuat(upperarmL, mirroredRot, AgentSkeleton);

                // Also mirror forearm for more visible effect
                sbyte forearmR = 24; // HumanBone.ForearmR
                sbyte forearmL = 17; // HumanBone.ForearmL

                Transformation rhForearm = animResult.GetEntitialOutTransform(forearmR, AgentSkeleton);
                Vec3 mirroredFPos = new Vec3(-rhForearm.Origin.x, rhForearm.Origin.y, rhForearm.Origin.z);
                animResult.SetOutBoneDisplacement(forearmL, mirroredFPos, AgentSkeleton);

                Mat3 rhFRot = rhForearm.Rotation;
                Mat3 mirroredFRot = new Mat3(
                    new Vec3(-rhFRot.s.x, -rhFRot.s.y, -rhFRot.s.z),
                    new Vec3(-rhFRot.f.x,  rhFRot.f.y,  rhFRot.f.z),
                    new Vec3(-rhFRot.u.x,  rhFRot.u.y,  rhFRot.u.z)
                );
                animResult.SetOutQuat(forearmL, mirroredFRot, AgentSkeleton);

                // Log periodically
                if (_callbackCount % 60 == 0)
                {
                    DualWieldLog.Log($"[BoneMirror] Callback #{_callbackCount}: mirror applied, " +
                        $"RH_upperarm=({rhTransform.Origin.x:F2},{rhTransform.Origin.y:F2},{rhTransform.Origin.z:F2})");
                }

                return true; // We modified the result
            }
            catch (System.Exception ex)
            {
                DualWieldLog.Log($"[BoneMirror] Callback ERROR: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reset state (called when mission ends).
        /// </summary>
        public static void ResetState()
        {
            MirrorEnabled = false;
            CallbackFired = false;
            _callbackCount = 0;
        }
    }
}
