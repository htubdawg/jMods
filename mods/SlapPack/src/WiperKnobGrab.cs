using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace HalfGripSlapPack
{
    // Reuse native grip snapping, held-hand poses, cursor movement and release rebasing.
    // Only this control's right-button grab needs a different release button.
    [HarmonyPatch(typeof(Grabber), "Update")]
    internal static class WiperKnobGrab
    {
        private static readonly MethodInfo tryInteract = AccessTools.Method(typeof(Grabber), "TryInteract");

        internal static void Begin(Grabber hand) { tryInteract.Invoke(hand, new object[] { false }); }

        private static bool HandUp(int button, Grabber hand) => GameInput.HandUp(
            button == 0 && hand.Held is WiperStalk stalk && stalk.RotatingKnob ? 1 : button);

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(GameInput), nameof(GameInput.HandUp));
            MethodInfo replacement = AccessTools.Method(typeof(WiperKnobGrab), nameof(HandUp));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    // Copy branch labels through Harmony's helper; direct Label collection
                    // access is incompatible with the SDK's netstandard reference facade.
                    var instance = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                    yield return instance;
                    instruction.opcode = OpCodes.Call; instruction.operand = replacement;
                }
                yield return instruction;
            }
        }
    }
}
