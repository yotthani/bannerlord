using DualWield.Core;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace DualWield
{
    /// <summary>
    /// v10.11: Hybrid bone mirroring — PostIntegrate callback for mesh +
    /// stored weapon bone frames for manual weapon entity positioning.
    ///
    /// FINDINGS FROM v10.8-v10.9:
    /// - PostIntegrate callback (AnimResult.SetOutQuat) correctly mirrors mesh rendering
    /// - BUT EnableScriptDrivenPostIntegrateCallback breaks weapon entity auto-sync
    /// - SetBoneLocalFrame in OnMissionTick gets overwritten by animation before render
    ///
    /// SOLUTION: Use PostIntegrate callback for visual mirroring (mesh skinning),
    /// AND store the resulting weapon bone entitial frames so that
    /// DualWieldMissionBehavior can manually position weapon entities in OnMissionTick.
    ///
    /// Frame timing:
    /// 1. OnMissionTick → apply stored weapon bone frames to weapon entities (1 frame lag, invisible at 60fps)
    /// 2. Animation update → computes bone transforms
    /// 3. PostIntegrate callback → mirrors LH bones in AnimResult, stores weapon bone frames
    /// 4. Rendering → uses our mirrored AnimResult for mesh
    /// </summary>
    [ScriptComponentParams("dw_bone_mirror")]
    public class DualWieldBoneMirrorScript : ScriptComponentBehavior
    {
        public static int Mode { get; set; }
        public Skeleton AgentSkeleton { get; set; }
        public static bool CallbackFired { get; private set; }
        private static int _callbackCount;

        // ── All arm bone pairs including finger/item, root-to-tip ──
        private static readonly sbyte[] RH_ARM = { 21, 22, 23, 24, 25, 26, 27 };
        private static readonly sbyte[] LH_ARM = { 14, 15, 16, 17, 18, 19, 20 };
        private const int PAIR_COUNT = 7;

        // ── Weapon bone indices (finger/item bones) ──
        private const sbyte RH_WEAPON_BONE = 27; // r_finger0
        private const sbyte LH_WEAPON_BONE = 20; // l_finger0

        // ── Stored weapon bone entitial frames for OnMissionTick to consume ──
        // Computed during callback, used by MissionBehavior to position weapon entities.
        public static MatrixFrame LastRHWeaponBoneFrame { get; private set; }
        public static MatrixFrame LastLHWeaponBoneFrame { get; private set; }
        public static bool HasWeaponFrames { get; private set; }

        // Cached LH rest rotations (for FREEZE mode)
        private Mat3[] _lhRestLocal;
        private bool _restCached;

        private void CacheRestPoses()
        {
            if (_restCached || AgentSkeleton == null) return;
            _lhRestLocal = new Mat3[PAIR_COUNT];
            for (int i = 0; i < PAIR_COUNT; i++)
            {
                MatrixFrame lhRest = AgentSkeleton.GetBoneLocalRestFrame(LH_ARM[i], true);
                _lhRestLocal[i] = lhRest.rotation;
            }
            _restCached = true;
            DualWieldLog.Log("[BoneMirror] v10.11: Rest poses cached, 7 bone pairs.");
        }

        /// <summary>
        /// MirrorZ: M * R * M where M = diag(1, 1, -1).
        /// </summary>
        public static Mat3 MirrorZ(Mat3 r)
        {
            return new Mat3(
                new Vec3( r.s.x,  r.s.y, -r.s.z),
                new Vec3( r.f.x,  r.f.y, -r.f.z),
                new Vec3(-r.u.x, -r.u.y,  r.u.z)
            );
        }

        protected override bool SkeletonPostIntegrateCallback(AnimResult animResult)
        {
            _callbackCount++;
            CallbackFired = true;

            if (Mode == 0 || AgentSkeleton == null)
            {
                HasWeaponFrames = false;
                return false;
            }

            if (!_restCached)
                CacheRestPoses();

            try
            {
                // Apply bone mirroring to AnimResult (visual mesh)
                for (int i = 0; i < PAIR_COUNT; i++)
                {
                    sbyte rhBone = RH_ARM[i];
                    sbyte lhBone = LH_ARM[i];

                    Mat3 rhLocalRot = GetBoneLocalRotation(animResult, rhBone);
                    Mat3 lhLocalRot = MirrorZ(rhLocalRot);
                    animResult.SetOutQuat(lhBone, lhLocalRot, AgentSkeleton);
                }

                // Store weapon bone entitial frames for manual weapon entity positioning.
                // These are the post-mirror frames — exactly where weapons SHOULD be.
                // RH weapon bone: unchanged (we don't modify RH), read directly
                Transformation rhWeaponXform = animResult.GetEntitialOutTransform(RH_WEAPON_BONE, AgentSkeleton);
                LastRHWeaponBoneFrame = new MatrixFrame(rhWeaponXform.Rotation, rhWeaponXform.Origin);

                // LH weapon bone: we just set its rotation, so read the mirrored result
                Transformation lhWeaponXform = animResult.GetEntitialOutTransform(LH_WEAPON_BONE, AgentSkeleton);
                LastLHWeaponBoneFrame = new MatrixFrame(lhWeaponXform.Rotation, lhWeaponXform.Origin);

                HasWeaponFrames = true;

                if (_callbackCount == 1)
                {
                    DualWieldLog.Log($"[BoneMirror] v10.11: First callback — RH weapon bone frame origin={LastRHWeaponBoneFrame.origin}");
                    DualWieldLog.Log($"[BoneMirror] v10.11: First callback — LH weapon bone frame origin={LastLHWeaponBoneFrame.origin}");
                }

                return true; // Tell engine we modified the result
            }
            catch (System.Exception ex)
            {
                if (_callbackCount % 300 == 0)
                    DualWieldLog.Log($"[BoneMirror] ERROR: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Compute bone's LOCAL rotation: parent_entitial^T * bone_entitial
        /// </summary>
        private Mat3 GetBoneLocalRotation(AnimResult animResult, sbyte boneIndex)
        {
            Transformation boneEntitial = animResult.GetEntitialOutTransform(boneIndex, AgentSkeleton);
            sbyte parentIdx = AgentSkeleton.GetParentBoneIndex(boneIndex);

            if (parentIdx >= 0)
            {
                Transformation parentEntitial = animResult.GetEntitialOutTransform(parentIdx, AgentSkeleton);
                return parentEntitial.Rotation.TransformToLocal(in boneEntitial.Rotation);
            }

            return boneEntitial.Rotation;
        }

        public static void ResetState()
        {
            Mode = 0;
            CallbackFired = false;
            HasWeaponFrames = false;
            _callbackCount = 0;
        }
    }
}
