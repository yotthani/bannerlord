using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace DualWieldPrototype
{
    [HarmonyPatch]
    internal static class DualWieldPrototypeAgentSetActionChannelTracePatch
    {
        private sealed class TraceState
        {
            public bool ShouldLog;
            public int Channel;
            public string RequestedAction;
            public bool IgnorePriority;
            public AnimFlags AdditionalFlags;
            public float ActionSpeed;
            public float BlendInPeriod;
            public float BlendOutPeriodToNoAnim;
            public float StartProgress;
            public bool PreLeftStance;
            public string PreCh0;
            public string PreCh1;
            public string Scope;
            public string Caller;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.DeclaredMethod(
                typeof(Agent),
                nameof(Agent.SetActionChannel),
                new[]
                {
                    typeof(int),
                    typeof(ActionIndexCache).MakeByRefType(),
                    typeof(bool),
                    typeof(AnimFlags),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(bool),
                    typeof(float),
                    typeof(int),
                    typeof(bool)
                });
        }

        private static void Prefix(
            Agent __instance,
            int channelNo,
            ref ActionIndexCache actionIndexCache,
            bool ignorePriority,
            AnimFlags additionalFlags,
            float actionSpeed,
            float blendInPeriod,
            float blendOutPeriodToNoAnim,
            float startProgress,
            ref TraceState __state)
        {
            __state = null;

            if (__instance == null || Agent.Main == null || !ReferenceEquals(__instance, Agent.Main))
            {
                return;
            }

            Mission mission = Mission.Current;
            if (!DualWieldPrototypeMissionFilters.IsSupportedCombatContext(mission, __instance))
            {
                return;
            }

            DualWieldPrototypeSettings settings = DualWieldPrototypeSettings.Get();
            if (!settings.EnablePrototype || !settings.DebugFileLogging || !settings.TraceNativeChannelCalls)
            {
                return;
            }

            __state = new TraceState
            {
                ShouldLog = true,
                Channel = channelNo,
                RequestedAction = actionIndexCache.GetName(),
                IgnorePriority = ignorePriority,
                AdditionalFlags = additionalFlags,
                ActionSpeed = actionSpeed,
                BlendInPeriod = blendInPeriod,
                BlendOutPeriodToNoAnim = blendOutPeriodToNoAnim,
                StartProgress = startProgress,
                PreLeftStance = __instance.GetIsLeftStance(),
                PreCh0 = __instance.GetCurrentAction(0).GetName(),
                PreCh1 = __instance.GetCurrentAction(1).GetName(),
                Scope = DualWieldPrototypeTraceContext.CurrentScope ?? "external",
                Caller = BuildCallerFingerprint()
            };
        }

        private static void Postfix(Agent __instance, bool __result, TraceState __state)
        {
            if (__state == null || !__state.ShouldLog || __instance == null)
            {
                return;
            }

            DualWieldPrototypeLogger.Log(
                $"trace_setaction scope={__state.Scope} caller={__state.Caller} ch={__state.Channel} req={__state.RequestedAction} result={__result} " +
                $"ignorePriority={__state.IgnorePriority} flags={__state.AdditionalFlags} speed={__state.ActionSpeed:0.00} " +
                $"blendIn={__state.BlendInPeriod:0.00} blendOutNoAnim={__state.BlendOutPeriodToNoAnim:0.00} start={__state.StartProgress:0.00} " +
                $"leftStance={__state.PreLeftStance}->{__instance.GetIsLeftStance()} preCh0={__state.PreCh0} preCh1={__state.PreCh1} " +
                $"postCh0={__instance.GetCurrentAction(0).GetName()} postCh1={__instance.GetCurrentAction(1).GetName()}");
        }

        private static string BuildCallerFingerprint()
        {
            try
            {
                StackTrace trace = new StackTrace(2, false);
                List<string> parts = new List<string>(3);
                for (int i = 0; i < trace.FrameCount; i++)
                {
                    MethodBase method = trace.GetFrame(i)?.GetMethod();
                    if (method == null)
                    {
                        continue;
                    }

                    Type declaringType = method.DeclaringType;
                    string typeName = declaringType?.FullName ?? string.Empty;
                    if (typeName.StartsWith("System.", StringComparison.Ordinal) ||
                        typeName.StartsWith("HarmonyLib.", StringComparison.Ordinal) ||
                        typeName.StartsWith("MonoMod.", StringComparison.Ordinal) ||
                        typeName == typeof(DualWieldPrototypeAgentSetActionChannelTracePatch).FullName ||
                        typeName == typeof(DualWieldPrototypeTraceContext).FullName ||
                        typeName == typeof(DualWieldPrototypeLogger).FullName ||
                        typeName == typeof(Agent).FullName)
                    {
                        continue;
                    }

                    parts.Add($"{declaringType?.Name ?? "?"}.{method.Name}");
                    if (parts.Count >= 3)
                    {
                        break;
                    }
                }

                if (parts.Count == 0)
                {
                    return "unknown";
                }

                return string.Join(">", parts);
            }
            catch
            {
                return "error";
            }
        }
    }
}
