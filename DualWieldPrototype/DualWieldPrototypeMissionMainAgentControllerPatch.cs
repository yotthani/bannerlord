using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace DualWieldPrototype
{
    [HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
    internal static class DualWieldPrototypeMissionMainAgentControllerPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> patchedInstructions = new List<CodeInstruction>(instructions);
            MethodInfo isGameKeyDownMethod = AccessTools.Method(typeof(IInputContext), nameof(IInputContext.IsGameKeyDown));
            MethodInfo hookMethod = AccessTools.Method(typeof(DualWieldPrototypeMissionMainAgentControllerPatch), nameof(ShouldAllowGameKeyDown));
            int replacementCount = 0;

            for (int i = 1; i < patchedInstructions.Count; i++)
            {
                CodeInstruction instruction = patchedInstructions[i];
                if (!instruction.Calls(isGameKeyDownMethod))
                {
                    continue;
                }

                if (!TryGetLdcI4Value(patchedInstructions[i - 1], out int gameKey) || (gameKey != 9 && gameKey != 10))
                {
                    continue;
                }

                patchedInstructions.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                i++;

                patchedInstructions[i] = new CodeInstruction(OpCodes.Call, hookMethod)
                {
                    labels = instruction.labels,
                    blocks = instruction.blocks
                };

                replacementCount++;
            }

            if (replacementCount != 2)
            {
                throw new InvalidOperationException($"Expected to replace 2 IsGameKeyDown calls, but replaced {replacementCount}.");
            }

            return patchedInstructions;
        }

        private static bool ShouldAllowGameKeyDown(IInputContext inputContext, int gameKey, MissionMainAgentController controller)
        {
            bool isDown = inputContext.IsGameKeyDown(gameKey);
            if (gameKey != 9 && gameKey != 10)
            {
                return isDown;
            }

            if (controller?.Mission == null || controller.MissionScreen == null)
            {
                return isDown;
            }

            DualWieldPrototypeMissionBehavior behavior = controller.Mission.GetMissionBehavior<DualWieldPrototypeMissionBehavior>();
            if (behavior == null)
            {
                return isDown;
            }

            return behavior.ShouldAllowVanillaGameKeyDown(inputContext, gameKey);
        }

        private static bool TryGetLdcI4Value(CodeInstruction instruction, out int value)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_M1)
            {
                value = -1;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_0)
            {
                value = 0;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_1)
            {
                value = 1;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_2)
            {
                value = 2;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_3)
            {
                value = 3;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_4)
            {
                value = 4;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_5)
            {
                value = 5;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_6)
            {
                value = 6;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_7)
            {
                value = 7;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_8)
            {
                value = 8;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4_S)
            {
                value = (sbyte)instruction.operand;
                return true;
            }

            if (instruction.opcode == OpCodes.Ldc_I4)
            {
                value = (int)instruction.operand;
                return true;
            }

            value = 0;
            return false;
        }
    }
}
