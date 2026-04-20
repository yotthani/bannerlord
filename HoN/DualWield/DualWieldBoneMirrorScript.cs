using DualWield.Core;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace DualWield
{
    /// <summary>
    /// v10.26: Bone mirroring — MirrorZ for LH arm.
    ///
    /// PostIntegrate callback mirrors RH arm bones to LH using MirrorZ (M*R*M, M=diag(1,1,-1)).
    /// Slash animations mirror cleanly. Thrust/overswing have known limitations (Z-axis inversion).
    ///
    /// Weapon entity positioning: stores PreMirror bone frame (parents mirrored, bone 20 original)
    /// for DualWieldMissionBehavior to manually position weapon entities in OnMissionTick.
    ///
    /// Mode: 0=off, 1=both arms mirror (Spiegel), 2=LH-only mirror (async: LH attacks, RH idle)
    /// </summary>
    [ScriptComponentParams("dw_bone_mirror")]
    public class DualWieldBoneMirrorScript : ScriptComponentBehavior
    {
        public static int Mode { get; set; }
        public Skeleton AgentSkeleton { get; set; }
        public static bool CallbackFired { get; private set; }
        private static int _callbackCount;

        // ── Arm bone pairs, root-to-tip ──
        private static readonly sbyte[] RH_ARM = { 21, 22, 23, 24, 25, 26, 27 };
        private static readonly sbyte[] LH_ARM = { 14, 15, 16, 17, 18, 19, 20 };
        private const int PAIR_COUNT = 7;

        private const sbyte RH_WEAPON_BONE = 27;
        private const sbyte LH_WEAPON_BONE = 20;

        // ── RH idle pose for Mode=2 (LH-only: reset RH to idle) ──
        private static Mat3[] _rhIdleLocalRots = new Mat3[PAIR_COUNT];
        private static bool _hasRHIdlePose;

        // ── Stored weapon bone frames for OnMissionTick ──
        public static MatrixFrame LastRHWeaponBoneFrame { get; private set; }
        public static MatrixFrame LastLHWeaponBoneFrame { get; private set; }
        /// <summary>
        /// LH weapon bone entitial BEFORE mirroring bone 20 itself.
        /// Parents 14-19 mirrored, bone 20 original → det=+1 → correct weapon handedness.
        /// </summary>
        public static MatrixFrame PreMirrorLHWeaponBoneFrame { get; private set; }
        public static bool HasWeaponFrames { get; private set; }
        /// <summary>
        /// True after the first callback fires. LastRHWeaponBoneFrame is always current
        /// (updated even in Mode=0) so SyncWeaponEntitiesToNativeBones can use it safely.
        /// </summary>
        public static bool HasRHFrame { get; private set; }
        /// <summary>
        /// Native LH weapon bone frame — captured every tick BEFORE any mirroring.
        /// Use in Async modes (Mode=0) to position the LH weapon following natural animations.
        /// </summary>
        public static MatrixFrame NativeLHWeaponBoneFrame { get; private set; }
        public static bool HasNativeLHFrame { get; private set; }

        /// <summary>
        /// Runtime-cyclable mirror selection for slashright i=5 (hand/grip).
        /// Toggle with G key to test Identity/MirrorX/MirrorY/MirrorZ live.
        /// 0=Identity  1=MirrorX  2=MirrorY  3=MirrorZ
        /// </summary>
        public static int MirrorVariant { get; set; } = 0; // start at MirrorZ-style flip (matches slashleft convention)
        public const int MIRROR_VARIANT_COUNT = 4;

        /// <summary>True when current attack is thrust/overswing (set by MissionBehavior)</summary>
        public static bool IsNonSlashAttack { get; set; }
        /// <summary>True when current attack is specifically a thrust (not overswing). Set by MissionBehavior.
        /// Overswing uses MirrorZ (same as slash). Thrust needs SLERP due to lateral arm component.</summary>
        public static bool IsThrustAttack { get; set; }
        /// <summary>True when current attack is slashright. Set by MissionBehavior.</summary>
        public static bool IsSlashRightAttack { get; set; }
        /// <summary>True when current attack is slashleft. Set by MissionBehavior.</summary>
        public static bool IsSlashLeftAttack { get; set; }

        // Cache of RH arm bone local rotations from the most recent callback.
        private static readonly Mat3[] _cachedRHLocalRots = new Mat3[7];
        // World-space bone rotations (entitial) — columns are world directions of local X/Y/Z axes.
        private static readonly Mat3[] _cachedRHWorldRots = new Mat3[7];
        private static readonly Mat3[] _cachedLHWorldRots = new Mat3[7];
        // World-space bone POSITIONS (origins). Measuring these directly tells us where each
        // arm joint IS in world — much more interpretable than rotation matrices.
        private static readonly Vec3[] _cachedRHWorldPos = new Vec3[7];
        private static readonly Vec3[] _cachedLHWorldPos = new Vec3[7];
        // Position of the agent's body origin (root/pelvis) — used to express positions
        // relative to the character so character rotation/translation doesn't pollute.
        private static Vec3 _cachedAgentOrigin;
        private static Mat3 _cachedAgentRotation; // Body frame rotation (for converting to body-local)
        private static bool _hasCachedRotations;

        // ── PROBE MODE ──
        // Applies a known isolated rotation to ONE LH bone around ONE local axis.
        // Used to map out: "local axis X of bone Y = world-space effect Z".
        // ProbeState values: 0 = OFF (normal mirror). 1..30 = 5 bones × 3 axes × 2 directions.
        // bone = (ProbeState-1) / 6 + 1  (i=1..5, skip shoulder i=0)
        // axis = ((ProbeState-1) % 6) / 2  (0=X, 1=Y, 2=Z)
        // dir  = ((ProbeState-1) % 2) * 2 - 1  (+1 or -1)
        public static int ProbeState { get; set; }
        public const int PROBE_ANGLE_DEG = 30;

        public static string ProbeStateDescription()
        {
            if (ProbeState == 0) return "OFF";
            int idx = ProbeState - 1;
            int bone = idx / 6 + 1;
            int axis = (idx % 6) / 2;
            int dir = (idx % 2) == 0 ? +1 : -1;
            string[] axisName = { "X", "Y", "Z" };
            string[] boneName = { "?", "upperArm", "elbow", "forearm", "wrist", "hand" };
            return $"i={bone} {boneName[bone]} local-{axisName[axis]} {(dir > 0 ? "+" : "-")}{PROBE_ANGLE_DEG}°";
        }

        // Bind-pose references as LOCAL-TO-PARENT rotations (invariant of character orientation).
        // Captured by K key when character is idle. Used by slashright mirror to compute the
        // exact LH rotation that produces the sagittal mirror of RH's motion.
        public static readonly Mat3[] BindPoseRHLocal = new Mat3[7];
        public static readonly Mat3[] BindPoseLHLocal = new Mat3[7];
        public static bool BindPoseCaptured { get; private set; }
        // Also cache the last-measured RH local-to-parent rotations (for the formula).
        private static readonly Mat3[] _cachedLHLocalRots = new Mat3[7];

        /// <summary>Capture current bone local-to-parent rotations as "bind pose reference".
        /// Call this when the character is in a neutral idle pose (no attack, standing still).
        /// Also automatically dumps a body-frame analysis showing what each bone's local
        /// axes correspond to in the character body frame.</summary>
        public static void CaptureBindPose()
        {
            if (!_hasCachedRotations)
            {
                DualWieldLog.Log("[BindCapture] no cached rotations yet");
                return;
            }
            for (int i = 0; i < 6; i++)
            {
                BindPoseRHLocal[i] = _cachedRHLocalRots[i];
                BindPoseLHLocal[i] = _cachedLHLocalRots[i];
            }
            BindPoseCaptured = true;
            DualWieldLog.Log("[BindCapture] Bind poses (local-to-parent) captured for i=0..5");
            AnalyzeBindInBodyFrame();
        }

        /// <summary>Analyze the bind pose: for each bone, express the world rotation in the
        /// CHARACTER BODY FRAME (shoulder's parent = chest/spine). The columns of that matrix
        /// are the character-relative directions of each local axis.
        /// This tells us: what does "rotate bone i around its local X/Y/Z" actually DO
        /// in terms of the character's body — without needing manual probes.</summary>
        public static void AnalyzeBindInBodyFrame()
        {
            DualWieldLog.Log("================================================================");
            DualWieldLog.Log("[BodyFrame] Analysis — each bone's local X/Y/Z axes expressed in the character body frame");
            DualWieldLog.Log("            Body frame assumed: X=right, Y=forward, Z=up (to be verified)");
            DualWieldLog.Log("            Columns of local-in-body matrix = world direction of local X, Y, Z axes.");
            DualWieldLog.Log("            CRUCIAL: compare LH vs RH per axis — are they X-mirrored? Equal? Different?");
            DualWieldLog.Log("----------------------------------------------------------------");

            // Body frame = shoulder's parent bone world rotation.
            // For bone i, compute bone_in_body = body⁻¹ · bone_world.
            // Columns of bone_in_body = world directions of local X/Y/Z in body frame.
            // We already have _cachedRHWorldRots[i] and _cachedLHWorldRots[i] in WORLD.
            // We need body frame. Since we don't have direct access here to animResult,
            // we use the SHOULDER's world rotation as a proxy for body orientation.
            // The shoulder's rotation ≈ body orientation + T-pose offset; not perfect but indicative.
            //
            // BETTER: use the inverse of the SHOULDER's LOCAL rotation as reference, which is
            // parent(shoulder)_world⁻¹ · shoulder_world. Then body_world = shoulder_world · shoulder_local⁻¹.

            // Easier: for each bone i, compute its ENTITIAL ROTATION as-cached (in world).
            // Build body frame by going up: we don't have easy access, so we report the
            // world matrices and let the comparison itself show asymmetry.
            string[] boneName = { "shoulder", "upperArm", "elbow   ", "forearm ", "wrist   ", "hand    " };

            // First approach: compare world matrices directly. If LH column j = -RH column j
            // (X-flipped only), that axis is cleanly X-mirrored.
            for (int i = 0; i < 6; i++)
            {
                Mat3 rh = _cachedRHWorldRots[i];
                Mat3 lh = _cachedLHWorldRots[i];

                DualWieldLog.Log($"  i={i} {boneName[i]}");
                DualWieldLog.Log($"      RH world   local-X→({rh.s.x,5:0.00},{rh.s.y,5:0.00},{rh.s.z,5:0.00})  Y→({rh.f.x,5:0.00},{rh.f.y,5:0.00},{rh.f.z,5:0.00})  Z→({rh.u.x,5:0.00},{rh.u.y,5:0.00},{rh.u.z,5:0.00})");
                DualWieldLog.Log($"      LH world   local-X→({lh.s.x,5:0.00},{lh.s.y,5:0.00},{lh.s.z,5:0.00})  Y→({lh.f.x,5:0.00},{lh.f.y,5:0.00},{lh.f.z,5:0.00})  Z→({lh.u.x,5:0.00},{lh.u.y,5:0.00},{lh.u.z,5:0.00})");

                // For each axis, classify the LH-vs-RH relationship.
                AnalyzeAxisPair("local-X", rh.s, lh.s);
                AnalyzeAxisPair("local-Y", rh.f, lh.f);
                AnalyzeAxisPair("local-Z", rh.u, lh.u);
            }
            DualWieldLog.Log("================================================================");

            InformationManager.DisplayMessage(new InformationMessage(
                "[BindCapture] Body-frame analysis dumped to log.", Colors.Cyan));
        }

        /// <summary>Compare a single LH axis vector to the corresponding RH axis vector.
        /// For Bannerlord body frame (Z = lateral), sagittal mirror flips Z component.</summary>
        private static void AnalyzeAxisPair(string axisLabel, Vec3 rh, Vec3 lh)
        {
            // Z-mirror expected: lh = (rh.x, rh.y, -rh.z)   — Bannerlord body frame mirror
            Vec3 zMirrored = new Vec3(rh.x, rh.y, -rh.z);
            float dZMirror = AngleDegBetween(lh, zMirrored);
            // Identical: lh = rh
            float dIdentical = AngleDegBetween(lh, rh);
            // Fully negated: lh = -rh
            Vec3 negated = new Vec3(-rh.x, -rh.y, -rh.z);
            float dNegated = AngleDegBetween(lh, negated);

            string classification;
            if (dZMirror < 15f)        classification = $"Z-MIRRORED of RH ({dZMirror:0.0}° off)  → sagittal mirror works as-is";
            else if (dIdentical < 15f) classification = $"IDENTICAL to RH ({dIdentical:0.0}° off)  → needs Z-negation to mirror";
            else if (dNegated < 15f)   classification = $"FULLY-NEGATED of RH ({dNegated:0.0}° off)  → needs 180° rotation around Z";
            else                       classification = $"ARBITRARY (zMir={dZMirror:0.0}° id={dIdentical:0.0}° neg={dNegated:0.0}° — no simple relation)";
            DualWieldLog.Log($"        {axisLabel}: {classification}");
        }

        public static string MirrorVariantName => MirrorVariant switch
        {
            0 => "Identity",
            1 => "MirrorX",
            2 => "MirrorY",
            3 => "MirrorZ",
            _ => "?"
        };

        /// <summary>MirrorZ: M * R * M where M = diag(1, 1, -1).
        /// Works cleanly for slash animations.</summary>
        public static Mat3 MirrorZ(Mat3 r)
        {
            return new Mat3(
                new Vec3( r.s.x,  r.s.y, -r.s.z),
                new Vec3( r.f.x,  r.f.y, -r.f.z),
                new Vec3(-r.u.x, -r.u.y,  r.u.z)
            );
        }

        /// <summary>MirrorY: M * R * M where M = diag(1, -1, 1).
        /// Preserves Y (forward) rotations — thrust/overswing arm goes forward+inward.</summary>
        public static Mat3 MirrorY(Mat3 r)
        {
            return new Mat3(
                new Vec3( r.s.x, -r.s.y,  r.s.z),
                new Vec3(-r.f.x,  r.f.y, -r.f.z),
                new Vec3( r.u.x, -r.u.y,  r.u.z)
            );
        }

        /// <summary>MirrorX: M * R * M where M = diag(-1, 1, 1).
        /// Preserves X-axis rotations, reverses Y and Z rotations.
        /// Tested for slashright: slashright is a diagonal Z-dominated sweep — MirrorZ preserves Z,
        /// but MirrorX reverses Z → should correctly flip slashright direction.</summary>
        public static Mat3 MirrorX(Mat3 r)
        {
            return new Mat3(
                new Vec3( r.s.x, -r.s.y, -r.s.z),
                new Vec3(-r.f.x,  r.f.y,  r.f.z),
                new Vec3(-r.u.x,  r.u.y,  r.u.z)
            );
        }

        /// <summary>Selects mirror based on current attack type.
        /// Slash: MirrorZ (tested, works). Non-slash: no mirror (direct copy),
        /// because LH bones have mirrored rest pose → same local rotation = mirrored world motion.</summary>
        public static Mat3 Mirror(Mat3 r)
            => IsNonSlashAttack ? r : MirrorZ(r);

        protected override bool SkeletonPostIntegrateCallback(AnimResult animResult)
        {
            _callbackCount++;
            CallbackFired = true;

            if (AgentSkeleton == null)
            {
                HasWeaponFrames = false;
                return false;
            }

            // Capture RH idle pose ONCE (agent idle at mission start) — used by Mode=2 RH reset.
            if (!_hasRHIdlePose)
            {
                try
                {
                    for (int i = 0; i < PAIR_COUNT; i++)
                        _rhIdleLocalRots[i] = GetBoneLocalRotation(animResult, RH_ARM[i]);
                    _hasRHIdlePose = true;
                }
                catch { /* retry next frame */ }
            }

            // Always capture RH weapon frame — skeleton.GetBoneEntitialFrame() returns stale
            // data after EnableScriptDrivenPostIntegrateCallback when callback returns false.
            // Reading from animResult here is the only reliable way to get current bone data.
            try
            {
                Transformation rhEarly = animResult.GetEntitialOutTransform(RH_WEAPON_BONE, AgentSkeleton);
                LastRHWeaponBoneFrame = new MatrixFrame(rhEarly.Rotation, rhEarly.Origin);
                HasRHFrame = true;
            }
            catch { /* keep HasRHFrame as-is */ }

            // Always capture native LH frame before any mirroring (Async modes need this)
            try
            {
                Transformation lhEarly = animResult.GetEntitialOutTransform(LH_WEAPON_BONE, AgentSkeleton);
                NativeLHWeaponBoneFrame = new MatrixFrame(lhEarly.Rotation, lhEarly.Origin);
                HasNativeLHFrame = true;
            }
            catch { /* keep as-is */ }

            if (Mode == 0)
            {
                HasWeaponFrames = false;
                return false;
            }

            try
            {
                // Step 1: Mirror RH arm bones → LH arm bones
                //
                // Each mirror type preserves one axis and reverses the other two (as rotation conjugation):
                //   MirrorZ: reverses X+Y rotations, preserves Z
                //   MirrorX: reverses Y+Z rotations, preserves X — causes arm to go BEHIND (reverses Y=forward axis)
                //   MirrorY: reverses X+Z rotations, preserves Y — flips horizontal but keeps forward-back intact ✓
                //
                // slashleft:   MirrorZ all → confirmed working.
                // overswing:   MirrorZ all → arm overhead.
                // slashright:  i=0 MirrorZ (shoulder in front); i=1..5 MirrorY
                //              (MirrorY reverses Z-rotation = horizontal flip, preserves Y = keeps arm in front)
                // thrust:      i=0 MirrorZ (shoulder); i=1..4 SLERP(Identity,MirrorZ,0.5) (arm inward);
                //              i=5 MirrorZ (hand/grip: correct sword forward orientation, doesn't affect arm chain)
                // Probe mode: apply isolated axis rotation to ONE LH bone, else keep at bind pose.
                // Overrides all other mirror logic for the duration of the probe.
                int probeBone = -1, probeAxis = -1, probeDir = 0;
                if (ProbeState > 0)
                {
                    int idx = ProbeState - 1;
                    probeBone = idx / 6 + 1;               // i=1..5 (skip shoulder)
                    probeAxis = (idx % 6) / 2;             // 0=X, 1=Y, 2=Z
                    probeDir = (idx % 2) == 0 ? +1 : -1;   // +30° or -30°
                }

                for (int i = 0; i < PAIR_COUNT - 1; i++)
                {
                    Mat3 rhLocalRot = GetBoneLocalRotation(animResult, RH_ARM[i]);
                    _cachedRHLocalRots[i] = rhLocalRot; // cache for DumpBoneAxes
                    Mat3 outRot;

                    // === PROBE MODE ===
                    if (ProbeState > 0 && BindPoseCaptured)
                    {
                        // All other bones: hold in LH bind pose (neutral)
                        // Probed bone: bind pose · rotation around specified local axis
                        Mat3 probeLocal;
                        if (i == probeBone)
                        {
                            // Build a pure local-axis rotation (axis=probeAxis, angle=probeDir*30°)
                            float angleRad = probeDir * PROBE_ANGLE_DEG * (float)System.Math.PI / 180f;
                            float half = angleRad * 0.5f;
                            float s = (float)System.Math.Sin(half);
                            float c = (float)System.Math.Cos(half);
                            float qx = probeAxis == 0 ? s : 0f;
                            float qy = probeAxis == 1 ? s : 0f;
                            float qz = probeAxis == 2 ? s : 0f;
                            var qProbe = new TaleWorlds.Library.Quaternion(qx, qy, qz, c);
                            var qBind = BindPoseLHLocal[i].ToQuaternion();
                            // bind · probe: apply probe rotation in bind's local frame (standard animation order)
                            var qResult = Multiply(qBind, qProbe);
                            probeLocal = qResult.ToMat3();
                        }
                        else
                        {
                            probeLocal = BindPoseLHLocal[i];
                        }
                        animResult.SetOutQuat(LH_ARM[i], probeLocal, AgentSkeleton);
                        _cachedRHWorldRots[i] = animResult.GetEntitialOutTransform(RH_ARM[i], AgentSkeleton).Rotation;
                        _cachedLHWorldRots[i] = animResult.GetEntitialOutTransform(LH_ARM[i], AgentSkeleton).Rotation;
                        _cachedLHLocalRots[i] = probeLocal;
                        continue; // skip normal mirror logic for this bone
                    }

                    if (IsSlashRightAttack)
                    {
                        // Plain MirrorZ conjugation — same as slashleft.
                        // User confirmed: this produces the correct arm motion (both arms R→L parallel).
                        // Only the hand (i=5) rotation will need a separate grip-flip fix.
                        outRot = MirrorZ(rhLocalRot);
                    }
                    else if ((IsThrustAttack || IsNonSlashAttack) && BindPoseCaptured)
                    {
                        // THRUST + OVERSWING: SAGITTAL MIRROR (bind-pose-aware, Z-flip).
                        // Produces arm with forward extension and small 5-15cm left-drift — acceptable.
                        // Parallel motion was tested for thrust and made the arm stay at hip (no extension).
                        var qRH = rhLocalRot.ToQuaternion();
                        var qBindRH = BindPoseRHLocal[i].ToQuaternion();
                        var qBindRHInv = new TaleWorlds.Library.Quaternion(-qBindRH.X, -qBindRH.Y, -qBindRH.Z, qBindRH.W);
                        var qChange = Multiply(qBindRHInv, qRH);
                        // MirrorZ-style flip (X,Y flipped, Z preserved) = sagittal mirror in Bannerlord body frame
                        var qChangeMirrored = new TaleWorlds.Library.Quaternion(-qChange.X, -qChange.Y, qChange.Z, qChange.W);
                        var qBindLH = BindPoseLHLocal[i].ToQuaternion();
                        var qTargetLHLocal = Multiply(qBindLH, qChangeMirrored);
                        outRot = qTargetLHLocal.ToMat3();
                    }
                    // slashleft falls through to the else branch with plain MirrorZ.
                    // Tested MirrorX and MirrorY: both put the arm behind the back, because any
                    // mirror that flips slashleft's Z-dominant component also flips either
                    // forward (X) or up (Y) in Bannerlord's body frame. MirrorZ keeps the arm
                    // in front but produces a crossing visual (Z preserved → Identity-like for
                    // Z-dominant rotations → LH matches RH rotation). Accepted limitation.
                    else
                    {
                        // overswing (fallback without bind pose) / thrust hand / thrust shoulder: MirrorZ
                        outRot = MirrorZ(rhLocalRot);
                    }
                    animResult.SetOutQuat(LH_ARM[i], outRot, AgentSkeleton);

                    // Cache world rotations, positions, and local rotations for analysis.
                    var rhTrans = animResult.GetEntitialOutTransform(RH_ARM[i], AgentSkeleton);
                    var lhTrans = animResult.GetEntitialOutTransform(LH_ARM[i], AgentSkeleton);
                    _cachedRHWorldRots[i] = rhTrans.Rotation;
                    _cachedLHWorldRots[i] = lhTrans.Rotation;
                    _cachedRHWorldPos[i]  = rhTrans.Origin;
                    _cachedLHWorldPos[i]  = lhTrans.Origin;
                    _cachedLHLocalRots[i] = GetBoneLocalRotation(animResult, LH_ARM[i]);
                }
                // Also cache the agent's root / body frame (shoulder's parent ≈ chest).
                // Used to express arm bone positions relative to the character body so
                // character rotation/walking doesn't pollute the mirror analysis.
                sbyte rootIdx = AgentSkeleton.GetParentBoneIndex(LH_ARM[0]);
                if (rootIdx >= 0)
                {
                    var rootTrans = animResult.GetEntitialOutTransform(rootIdx, AgentSkeleton);
                    _cachedAgentOrigin = rootTrans.Origin;
                    _cachedAgentRotation = rootTrans.Rotation;
                }

                // === AUTO-TRACKING ===
                // During any detected attack, log position data each frame for the HAND bone (i=5).
                // User can review trajectory after combat — no manual key press needed.
                if (BindPoseCaptured && (IsSlashRightAttack || IsNonSlashAttack))
                {
                    string attackType = IsSlashRightAttack ? "SR"
                                      : IsThrustAttack     ? "TH"
                                      : IsNonSlashAttack   ? "OS"   // overswing
                                                           : "??";
                    // Hand position analysis
                    Vec3 rhPos = ToBodyLocal(_cachedRHWorldPos[5]);
                    Vec3 lhPos = ToBodyLocal(_cachedLHWorldPos[5]);
                    Vec3 expected = new Vec3(rhPos.x, rhPos.y, -rhPos.z); // Bannerlord: Z is lateral (flip Z for sagittal mirror)
                    float dx = lhPos.x - expected.x, dy = lhPos.y - expected.y, dz = lhPos.z - expected.z;
                    float delta = (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    // Upper arm position analysis (for shoulder-level drift detection)
                    Vec3 rhArm = ToBodyLocal(_cachedRHWorldPos[1]);
                    Vec3 lhArm = ToBodyLocal(_cachedLHWorldPos[1]);
                    Vec3 expectedArm = new Vec3(rhArm.x, rhArm.y, -rhArm.z); // Z-flip for sagittal mirror
                    float dxa = lhArm.x - expectedArm.x, dya = lhArm.y - expectedArm.y, dza = lhArm.z - expectedArm.z;
                    float deltaArm = (float)System.Math.Sqrt(dxa * dxa + dya * dya + dza * dza);

                    DualWieldLog.Log(
                        $"[Track {attackType}] armΔ={deltaArm:0.00} handΔ={delta:0.00}  " +
                        $"hand RH({rhPos.x,5:0.00},{rhPos.y,5:0.00},{rhPos.z,5:0.00})  " +
                        $"LH({lhPos.x,5:0.00},{lhPos.y,5:0.00},{lhPos.z,5:0.00})  " +
                        $"EXP({expected.x,5:0.00},{expected.y,5:0.00},{expected.z,5:0.00})");
                }
                _hasCachedRotations = true;

                // Step 2: Capture bone 20's entitial BEFORE mirroring it (for weapon positioning)
                Transformation preMirrorLH = animResult.GetEntitialOutTransform(LH_WEAPON_BONE, AgentSkeleton);
                PreMirrorLHWeaponBoneFrame = new MatrixFrame(preMirrorLH.Rotation, preMirrorLH.Origin);

                // Step 3: Mirror weapon bone
                {
                    Mat3 rhLocalRot = GetBoneLocalRotation(animResult, RH_WEAPON_BONE);
                    animResult.SetOutQuat(LH_WEAPON_BONE, Mirror(rhLocalRot), AgentSkeleton);
                }

                // Mode 2 only: Reset RH arm to captured idle pose
                if (Mode == 2 && _hasRHIdlePose)
                {
                    for (int i = 0; i < PAIR_COUNT; i++)
                        animResult.SetOutQuat(RH_ARM[i], _rhIdleLocalRots[i], AgentSkeleton);

                    Transformation rhAfterReset = animResult.GetEntitialOutTransform(RH_WEAPON_BONE, AgentSkeleton);
                    LastRHWeaponBoneFrame = new MatrixFrame(rhAfterReset.Rotation, rhAfterReset.Origin);
                }

                // Store final LH weapon bone frame
                Transformation lhXform = animResult.GetEntitialOutTransform(LH_WEAPON_BONE, AgentSkeleton);
                LastLHWeaponBoneFrame = new MatrixFrame(lhXform.Rotation, lhXform.Origin);

                HasWeaponFrames = true;
                return true;
            }
            catch (System.Exception ex)
            {
                if (_callbackCount % 300 == 0)
                    DualWieldLog.Log($"[BoneMirror] ERROR: {ex.Message}");
                return false;
            }
        }

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

        /// <summary>
        /// Dump per-bone rotation axis+angle for the last-captured frame,
        /// plus what each of the 4 mirrors (Identity/MirrorX/Y/Z) would produce.
        /// Writes to DualWieldLog and (abbreviated) to HUD.
        /// Call this from a hotkey DURING an attack to see which mirror reverses
        /// each bone's dominant rotation axis correctly.
        /// </summary>
        public static void DumpBoneAxes(string context)
        {
            if (!_hasCachedRotations)
            {
                DualWieldLog.Log("[BoneAxes] no cached rotations yet (callback not fired)");
                return;
            }

            DualWieldLog.Log("================================================================");
            DualWieldLog.Log($"[BoneAxes] context='{context}'  flags: NonSlash={IsNonSlashAttack} Thrust={IsThrustAttack} SlashRight={IsSlashRightAttack}");
            DualWieldLog.Log("  Each row: bone idx | RH axis (x,y,z) | angle° | dominant axis | mirrors that REVERSE this axis");
            DualWieldLog.Log("  MirrorX reverses Y,Z  |  MirrorY reverses X,Z  |  MirrorZ reverses X,Y  |  Identity reverses nothing");
            DualWieldLog.Log("----------------------------------------------------------------");

            string[] boneNames = { "shoulder", "upperArm", "elbow   ", "forearm ", "wrist   ", "hand    " };
            for (int i = 0; i < 6; i++)
            {
                Mat3 r = _cachedRHLocalRots[i];
                var q = r.ToQuaternion();
                // Ensure canonical q with w>=0 for consistent axis direction
                if (q.W < 0f) q = new TaleWorlds.Library.Quaternion(-q.X, -q.Y, -q.Z, -q.W);

                float wClamp = System.Math.Max(-1f, System.Math.Min(1f, q.W));
                float angleRad = 2f * (float)System.Math.Acos(wClamp);
                float angleDeg = angleRad * (180f / (float)System.Math.PI);
                float sinHalf = (float)System.Math.Sqrt(System.Math.Max(0f, 1f - wClamp * wClamp));

                float ax, ay, az;
                if (sinHalf < 1e-4f) { ax = 0f; ay = 0f; az = 0f; } // identity rotation — no axis
                else { ax = q.X / sinHalf; ay = q.Y / sinHalf; az = q.Z / sinHalf; }

                float absX = System.Math.Abs(ax), absY = System.Math.Abs(ay), absZ = System.Math.Abs(az);
                string dom; string reversedBy;
                if (absX >= absY && absX >= absZ)      { dom = "X"; reversedBy = "MirrorY, MirrorZ"; }
                else if (absY >= absZ)                  { dom = "Y"; reversedBy = "MirrorX, MirrorZ"; }
                else                                    { dom = "Z"; reversedBy = "MirrorX, MirrorY"; }

                DualWieldLog.Log(
                    $"  i={i} {boneNames[i]}  axis=({ax,6:0.00},{ay,6:0.00},{az,6:0.00})  angle={angleDeg,6:0.0}°  dominant={dom}  →reverse with: {reversedBy}");
            }
            DualWieldLog.Log("----------------------------------------------------------------");
            DualWieldLog.Log("  WORLD-SPACE AXES (columns of each bone's rotation matrix in world):");
            DualWieldLog.Log("    local-X = s-col (e.g. 'along arm out'?)  local-Y = f-col (e.g. 'forward'?)  local-Z = u-col (e.g. 'up'?)");
            DualWieldLog.Log("    EXPECTED LH = sagittal mirror of RH: x-component negated for all 3 axes (MirrorX conjugation)");
            DualWieldLog.Log("    DELTA shows angle between actual LH and expected LH — 0° means our mirror is correct for that axis.");
            for (int i = 0; i < 6; i++)
            {
                Mat3 rh = _cachedRHWorldRots[i];
                Mat3 lh = _cachedLHWorldRots[i];
                Mat3 expectedLH = MirrorX(rh); // sagittal mirror = negate x-components = MirrorX conjugation

                // Angle between actual LH and expected LH for each local axis
                float dX = AngleDegBetween(lh.s, expectedLH.s);
                float dY = AngleDegBetween(lh.f, expectedLH.f);
                float dZ = AngleDegBetween(lh.u, expectedLH.u);

                DualWieldLog.Log($"  i={i} {boneNames[i]}");
                DualWieldLog.Log($"      RH world  X→({rh.s.x,5:0.00},{rh.s.y,5:0.00},{rh.s.z,5:0.00})  Y→({rh.f.x,5:0.00},{rh.f.y,5:0.00},{rh.f.z,5:0.00})  Z→({rh.u.x,5:0.00},{rh.u.y,5:0.00},{rh.u.z,5:0.00})");
                DualWieldLog.Log($"      LH world  X→({lh.s.x,5:0.00},{lh.s.y,5:0.00},{lh.s.z,5:0.00})  Y→({lh.f.x,5:0.00},{lh.f.y,5:0.00},{lh.f.z,5:0.00})  Z→({lh.u.x,5:0.00},{lh.u.y,5:0.00},{lh.u.z,5:0.00})");
                DualWieldLog.Log($"      EXPECTED  X→({expectedLH.s.x,5:0.00},{expectedLH.s.y,5:0.00},{expectedLH.s.z,5:0.00})  Y→({expectedLH.f.x,5:0.00},{expectedLH.f.y,5:0.00},{expectedLH.f.z,5:0.00})  Z→({expectedLH.u.x,5:0.00},{expectedLH.u.y,5:0.00},{expectedLH.u.z,5:0.00})");
                DualWieldLog.Log($"      DELTA     X={dX,5:0.0}°  Y={dY,5:0.0}°  Z={dZ,5:0.0}°");
            }
            DualWieldLog.Log("----------------------------------------------------------------");
            DualWieldLog.Log("  POSITIONS in BODY-LOCAL frame (agent root inverted out):");
            DualWieldLog.Log("    Bannerlord body frame: X=forward, Y=up, Z=left.");
            DualWieldLog.Log("    If LH is perfect sagittal mirror of RH: LH_body = (RH.x, RH.y, -RH.z)");
            DualWieldLog.Log("    DELTA = distance between LH_actual and LH_expected. 0 = perfect mirror.");
            for (int i = 0; i < 6; i++)
            {
                // Convert world positions to body-local frame.
                Vec3 rhBodyPos = ToBodyLocal(_cachedRHWorldPos[i]);
                Vec3 lhBodyPos = ToBodyLocal(_cachedLHWorldPos[i]);
                Vec3 expectedLH = new Vec3(rhBodyPos.x, rhBodyPos.y, -rhBodyPos.z); // Z-flip for Bannerlord body frame
                Vec3 delta = new Vec3(lhBodyPos.x - expectedLH.x, lhBodyPos.y - expectedLH.y, lhBodyPos.z - expectedLH.z);
                float deltaLen = (float)System.Math.Sqrt(delta.x * delta.x + delta.y * delta.y + delta.z * delta.z);

                DualWieldLog.Log($"  i={i} {boneNames[i]}");
                DualWieldLog.Log($"      RH body-pos  ({rhBodyPos.x,6:0.000},{rhBodyPos.y,6:0.000},{rhBodyPos.z,6:0.000})");
                DualWieldLog.Log($"      LH body-pos  ({lhBodyPos.x,6:0.000},{lhBodyPos.y,6:0.000},{lhBodyPos.z,6:0.000})");
                DualWieldLog.Log($"      EXPECTED LH  ({expectedLH.x,6:0.000},{expectedLH.y,6:0.000},{expectedLH.z,6:0.000})   DELTA={deltaLen,6:0.000} units");
            }
            DualWieldLog.Log("================================================================");

            InformationManager.DisplayMessage(new InformationMessage(
                $"[BoneAxes] dumped to log for '{context}' — see DualWield_debug.log",
                Colors.Cyan));
        }

        /// <summary>Convert a world-space position to body-local frame (relative to agent root/pelvis).
        /// This removes character rotation and translation from the data.</summary>
        private static Vec3 ToBodyLocal(Vec3 worldPos)
        {
            Vec3 relative = new Vec3(
                worldPos.x - _cachedAgentOrigin.x,
                worldPos.y - _cachedAgentOrigin.y,
                worldPos.z - _cachedAgentOrigin.z);
            // Rotate into body frame: body_v = body_rotation⁻¹ · world_v
            // body_rotation has columns s, f, u = body X, Y, Z in world.
            // Inverse rotation = transpose for rotation matrices.
            // body_v.x = body.s · relative (dot product with body X column)
            Mat3 br = _cachedAgentRotation;
            return new Vec3(
                br.s.x * relative.x + br.s.y * relative.y + br.s.z * relative.z,
                br.f.x * relative.x + br.f.y * relative.y + br.f.z * relative.z,
                br.u.x * relative.x + br.u.y * relative.y + br.u.z * relative.z);
        }

        /// <summary>Quaternion multiplication q1 * q2.</summary>
        private static TaleWorlds.Library.Quaternion Multiply(TaleWorlds.Library.Quaternion q1, TaleWorlds.Library.Quaternion q2)
        {
            return new TaleWorlds.Library.Quaternion(
                q1.W * q2.X + q1.X * q2.W + q1.Y * q2.Z - q1.Z * q2.Y,
                q1.W * q2.Y - q1.X * q2.Z + q1.Y * q2.W + q1.Z * q2.X,
                q1.W * q2.Z + q1.X * q2.Y - q1.Y * q2.X + q1.Z * q2.W,
                q1.W * q2.W - q1.X * q2.X - q1.Y * q2.Y - q1.Z * q2.Z
            );
        }

        /// <summary>Angle in degrees between two 3-vectors (via dot product + acos).</summary>
        private static float AngleDegBetween(Vec3 a, Vec3 b)
        {
            float la = (float)System.Math.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
            float lb = (float)System.Math.Sqrt(b.x * b.x + b.y * b.y + b.z * b.z);
            if (la < 1e-6f || lb < 1e-6f) return 0f;
            float dot = (a.x * b.x + a.y * b.y + a.z * b.z) / (la * lb);
            if (dot > 1f) dot = 1f; if (dot < -1f) dot = -1f;
            return (float)System.Math.Acos(dot) * (180f / (float)System.Math.PI);
        }

        public static void ResetState()
        {
            Mode = 0;
            CallbackFired = false;
            HasWeaponFrames = false;
            HasRHFrame = false;
            HasNativeLHFrame = false;
            _hasRHIdlePose = false;
            _callbackCount = 0;
        }
    }
}
