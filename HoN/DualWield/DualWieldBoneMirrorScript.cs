using DualWield.Core;
using TaleWorlds.Core;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DualWield
{
    /// <summary>
    /// v10.6: Direct MirrorZ of local bone rotations.
    ///
    /// DISCOVERY: From rest pose data, LH and RH local rotations relate by:
    ///   LH_local = MirrorZ(RH_local)  where MirrorZ = M * R * M, M = diag(1,1,-1)
    ///
    /// This holds for BOTH rest pose AND animated pose. So the algorithm is:
    ///   1. Compute RH bone's current LOCAL rotation (from entitial transforms)
    ///   2. Apply MirrorZ → this IS the correct LH local rotation
    ///   3. Set via SetOutQuat
    ///
    /// No delta computation, no rest-pose subtraction needed.
    ///
    /// PROOF: Mz * Mz = I, so mirroring each bone independently produces
    /// the correct entity-space mirror for the entire chain:
    ///   S * Mz*A*Mz * Mz*B*Mz = S * Mz * A * B * Mz
    ///
    /// V key cycles: OFF → FREEZE → MIRROR_Z → OFF
    /// </summary>
    [ScriptComponentParams("dw_bone_mirror")]
    public class DualWieldBoneMirrorScript : ScriptComponentBehavior
    {
        public static int Mode { get; set; }
        public static readonly string[] MODE_NAMES = {
            "OFF",
            "FREEZE (T-pose)",
            "MIRROR_Z (direct)"
        };
        public const int MODE_COUNT = 3;

        public Skeleton AgentSkeleton { get; set; }
        public static bool CallbackFired { get; private set; }
        private static int _callbackCount;

        // ── All arm bone pairs, root-to-tip ──
        // Clavicle, Upperarm, UpperarmTwist1, Forearm, Forearm1, Hand
        private static readonly sbyte[] RH_ARM = { 21, 22, 23, 24, 25, 26 };
        private static readonly sbyte[] LH_ARM = { 14, 15, 16, 17, 18, 19 };
        private const int PAIR_COUNT = 6;

        // Cached LH rest rotations (for FREEZE mode only)
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
            DualWieldLog.Log("[BoneMirror] v10.6: Rest poses cached. MirrorZ direct mode.");
        }

        /// <summary>
        /// MirrorZ: M * R * M where M = diag(1, 1, -1).
        /// Derived from actual rest-pose data comparing LH vs RH bones.
        ///
        /// (M*R*M)[i][j] = M[i] * R[i][j] * M[j], M = diag(1,1,-1):
        ///   s' = ( s.x,  s.y, -s.z)   negate only z (row 2, col 0)
        ///   f' = ( f.x,  f.y, -f.z)   negate only z (row 2, col 1)
        ///   u' = (-u.x, -u.y,  u.z)   negate x,y (row 0,1 col 2) keep z (row 2, col 2)
        ///
        /// Verified against rest pose data:
        ///   RH[22] s=(0.834,0.051,0.549) → MirrorZ → (0.834,0.051,-0.549) = LH[15] ✓
        /// </summary>
        private static Mat3 MirrorZ(Mat3 r)
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
                return false;

            if (!_restCached)
                CacheRestPoses();

            try
            {
                if (Mode == 1)
                    return ApplyFreeze(animResult);

                // Mode 2: Direct MirrorZ
                return ApplyMirrorZ(animResult);
            }
            catch (System.Exception ex)
            {
                DualWieldLog.Log($"[BoneMirror] ERROR: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        private bool ApplyFreeze(AnimResult animResult)
        {
            for (int i = 0; i < PAIR_COUNT; i++)
                animResult.SetOutQuat(LH_ARM[i], _lhRestLocal[i], AgentSkeleton);
            return true;
        }

        private bool ApplyMirrorZ(AnimResult animResult)
        {
            for (int i = 0; i < PAIR_COUNT; i++)
            {
                sbyte rhBone = RH_ARM[i];
                sbyte lhBone = LH_ARM[i];

                // ── Step 1: Get RH bone's LOCAL rotation from entitial ──
                Mat3 rhLocalRot = GetBoneLocalRotation(animResult, rhBone);

                // ── Step 2: Mirror Z → correct LH local rotation ──
                Mat3 lhLocalRot = MirrorZ(rhLocalRot);

                // ── Step 3: Set on LH bone ──
                animResult.SetOutQuat(lhBone, lhLocalRot, AgentSkeleton);

                // Log first 2 frames
                if (_callbackCount <= 2)
                {
                    DualWieldLog.Log($"  [MirrorZ] [{i}] RH{rhBone}->LH{lhBone}" +
                        $" rh.s=({rhLocalRot.s.x:F3},{rhLocalRot.s.y:F3},{rhLocalRot.s.z:F3})" +
                        $" lh.s=({lhLocalRot.s.x:F3},{lhLocalRot.s.y:F3},{lhLocalRot.s.z:F3})");
                }
            }

            // Periodic position comparison
            if (_callbackCount % 120 == 1)
            {
                Transformation rhEnt = animResult.GetEntitialOutTransform(RH_ARM[1], AgentSkeleton);
                Transformation lhEnt = animResult.GetEntitialOutTransform(LH_ARM[1], AgentSkeleton);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[MirrorZ] RH=({rhEnt.Origin.x:F1},{rhEnt.Origin.y:F1},{rhEnt.Origin.z:F1})" +
                    $" LH=({lhEnt.Origin.x:F1},{lhEnt.Origin.y:F1},{lhEnt.Origin.z:F1})",
                    Colors.Cyan));
            }

            return true;
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
            _callbackCount = 0;
        }
    }
}
