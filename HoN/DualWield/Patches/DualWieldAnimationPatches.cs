using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DualWield.Core;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DualWield.Patches
{
    /// <summary>
    /// Harmony prefix on Agent.SetActionChannel — v7.17 OVERLAY approach.
    ///
    /// PROVEN MECHANISM (v7.16 X-key test):
    ///   SetActionChannel(channel=1, fist_left_stance_anim) moves the LEFT arm
    ///   with the off-hand weapon visible. The fist animations without "shield"
    ///   in the name are left-hand attacks. Channel 1 overlays on top of channel 0.
    ///
    /// v7.17: When the engine sets a mainhand combat animation on channel 0,
    /// we ALSO trigger the corresponding fist_left_stance animation on channel 1.
    /// This makes both hands attack — mainhand on ch0, offhand overlay on ch1.
    /// </summary>
    [HarmonyPatch]
    public static class DualWieldAnimationPatches
    {
        [System.ThreadStatic]
        private static bool _isRedirecting;

        // Log throttle
        private static int _prefixCallCount;
        private static int _offhandTriggerCount;

        // Track last offhand action per agent to prevent duplicate triggers
        private static readonly Dictionary<int, string> _lastOffhandAction = new Dictionary<int, string>();

        internal static bool IsRedirecting
        {
            get => _isRedirecting;
            set => _isRedirecting = value;
        }

        internal static void ClearAgentState(int agentIndex)
        {
            _lastOffhandAction.Remove(agentIndex);
        }

        internal static void ClearAll()
        {
            _lastOffhandAction.Clear();
            _prefixCallCount = 0;
            _offhandTriggerCount = 0;
        }

        static MethodBase TargetMethod()
        {
            try
            {
                var methods = typeof(Agent).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "SetActionChannel")
                    .ToArray();

                DualWieldLog.Log($"TargetMethod: found {methods.Length} SetActionChannel overload(s)");

                if (methods.Length == 0)
                {
                    DualWieldLog.Log("TargetMethod: FATAL — no SetActionChannel found!");
                    return null;
                }

                for (int i = 0; i < methods.Length; i++)
                {
                    var pars = methods[i].GetParameters();
                    var sig = string.Join(", ", pars.Select(p =>
                        $"{(p.ParameterType.IsByRef ? "ref " : "")}{p.ParameterType.Name} {p.Name}"));
                    DualWieldLog.Log($"  Overload {i}: {methods[i].ReturnType.Name} SetActionChannel({sig})");
                }

                MethodInfo target;
                if (methods.Length == 1)
                {
                    target = methods[0];
                }
                else
                {
                    target = methods.FirstOrDefault(m =>
                        m.GetParameters().Any(p =>
                        {
                            var pType = p.ParameterType;
                            return pType == typeof(ActionIndexCache) ||
                                   pType == typeof(ActionIndexCache).MakeByRefType();
                        }));

                    if (target == null)
                    {
                        DualWieldLog.Log("TargetMethod: WARNING — no ActionIndexCache overload, using first");
                        target = methods[0];
                    }
                }

                DualWieldLog.Log($"TargetMethod: selected {target.DeclaringType?.Name}.{target.Name}");
                return target;
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"TargetMethod: EXCEPTION — {ex}");
                return null;
            }
        }

        /// <summary>
        /// v7.17: OVERLAY prefix.
        ///
        /// Watches channel 0 for mainhand combat animations. When detected on a
        /// dual-wielding agent, overlays the corresponding fist_left_stance animation
        /// on channel 1 to make the left arm attack with the off-hand weapon.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(
            Agent __instance,
            ref bool __result,
            int channelNo,
            ref ActionIndexCache actionIndexCache,
            bool ignorePriority,
            AnimFlags additionalFlags,
            float blendWithNextActionFactor,
            float actionSpeed,
            float blendInPeriod,
            float blendOutPeriodToNoAnim,
            float startProgress,
            bool useLinearSmoothing,
            float blendOutPeriod,
            int actionShift,
            bool forceFaceMorphRestart)
        {
            // Guard: skip during preview/non-combat missions
            if (!DualWieldMissionBehavior.IsActive) return true;

            // Guard: skip our own overlay calls to prevent recursion
            if (_isRedirecting) return true;

            _prefixCallCount++;

            // Only process channel 0 (main body combat channel)
            if (channelNo != 0) return true;

            // Only process dual-wielding agents
            if (!DualWieldStateManager.IsDualWielding(__instance)) return true;

            // Get action name
            string actionName;
            try
            {
                actionName = actionIndexCache.GetName();
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"[Prefix] GetName() THREW: {ex.GetType().Name}: {ex.Message}");
                return true;
            }

            if (string.IsNullOrEmpty(actionName)) return true;

            // Diagnostic: log ch0 combat actions for dual-wielders
            if (_prefixCallCount <= 100 || _prefixCallCount % 200 == 0)
            {
                DualWieldLog.Log($"[Prefix] #{_prefixCallCount} ch0: {actionName} (agent={__instance.Name})");
            }

            // Map mainhand combat action to offhand fist_left_stance
            string offhandAnim = MapToOffhandAnimation(actionName);
            if (offhandAnim == null) return true;

            // Prevent duplicate triggers for same action
            int agentIdx = __instance.Index;
            if (_lastOffhandAction.TryGetValue(agentIdx, out var last) && last == offhandAnim)
                return true;
            _lastOffhandAction[agentIdx] = offhandAnim;

            // Overlay offhand animation on channel 1
            _isRedirecting = true;
            try
            {
                var offhandAction = ActionIndexCache.Create(offhandAnim);
                if (offhandAction.Index >= 0)
                {
                    __instance.SetActionChannel(1, offhandAction, ignorePriority: true, blendInPeriod: 0f);
                    _offhandTriggerCount++;
                    DualWieldLog.Log($"[Prefix] OFFHAND #{_offhandTriggerCount}: {actionName} → {offhandAnim} on ch1");

                    // v7.42b: On-screen diagnostic — show when overlay fires
                    if (_offhandTriggerCount <= 20)
                    {
                        InformationManager.DisplayMessage(
                            new InformationMessage(
                                $"[CH1] {offhandAnim}",
                                Colors.Cyan));
                    }
                }
                else
                {
                    DualWieldLog.Log($"[Prefix] OFFHAND: '{offhandAnim}' invalid index, skipped");
                }
            }
            catch (Exception ex)
            {
                DualWieldLog.Log($"[Prefix] OFFHAND ERROR: {ex.Message}");
            }
            finally
            {
                _isRedirecting = false;
            }

            return true; // Let mainhand animation proceed on channel 0
        }

        #region Animation Mapping

        /// <summary>
        /// Maps a mainhand 1H combat action to the corresponding fist_left_stance animation.
        /// Returns null if the action is not a mappable combat action.
        ///
        /// Mainhand 1H uses: slashright, slashleft, direct (overhead), thrust
        /// Fist uses: swingright, swingleft, direct, uppercut
        ///
        /// v7.39: Skips left_stance actions (they ARE left-hand follow-ups from the auto system).
        /// </summary>
        private static string MapToOffhandAnimation(string actionName)
        {
            // v7.39: Don't overlay on left_stance actions — they're already LH follow-ups
            if (actionName.Contains("left_stance")) return null;

            // Only map release/ready actions (the actual attack animations)
            bool isRelease = actionName.StartsWith("act_release_") || actionName.StartsWith("act_quick_release_");
            bool isReady = actionName.StartsWith("act_ready_");
            if (!isRelease && !isReady) return null;

            // Must be a 1H action (contains _1h)
            if (!actionName.Contains("_1h")) return null;

            // Don't map excluded weapon types
            if (actionName.Contains("_2h") || actionName.Contains("_lance") ||
                actionName.Contains("_staff") || actionName.Contains("_pike"))
                return null;

            // Determine attack direction from the mainhand action name
            string prefix = isReady ? "act_ready_" : (actionName.StartsWith("act_quick_release_") ? "act_quick_release_" : "act_release_");

            if (actionName.Contains("slashright") || actionName.Contains("swingright"))
            {
                return isReady
                    ? "act_ready_swingright_fist_left_stance"
                    : "act_release_swingright_fist_left_stance";
            }
            if (actionName.Contains("slashleft") || actionName.Contains("swingleft"))
            {
                return isReady
                    ? "act_ready_swingleft_fist_left_stance"
                    : "act_release_swingleft_fist_left_stance";
            }
            if (actionName.Contains("thrust") || actionName.Contains("uppercut"))
            {
                return isReady
                    ? "act_ready_uppercut_fist_left_stance"
                    : "act_release_uppercut_fist_left_stance";
            }
            if (actionName.Contains("direct") || actionName.Contains("overhead"))
            {
                return isReady
                    ? "act_ready_direct_fist_left_stance"
                    : "act_release_direct_fist_left_stance";
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// v7.42: Force GetIsLeftStance() = true for dual-wield agents.
    /// The engine only maps _left_stance animations to the LEFT arm when
    /// this returns true. Without it, left_stance actions play on the right arm.
    /// </summary>
    [HarmonyPatch(typeof(Agent), nameof(Agent.GetIsLeftStance))]
    public static class ForceLeftStancePatch
    {
        /// <summary>
        /// When true, GetIsLeftStance() returns true for the player agent.
        /// This makes the engine use left_stance usages for LMB attacks
        /// and correctly map left_stance animations to the left arm.
        /// </summary>
        public static bool ForceLeftStance;

        [HarmonyPostfix]
        public static void Postfix(Agent __instance, ref bool __result)
        {
            if (!DualWieldMissionBehavior.IsActive) return;
            if (!ForceLeftStance) return;
            if (__instance != Agent.Main) return;
            if (!DualWieldStateManager.IsDualWielding(__instance)) return;
            __result = true;
        }
    }
}
