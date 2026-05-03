using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Dialogue
{
    [HarmonyPatch(typeof(GuiDialogueDialog), "Compose")]
    public class GuiDialogueDialog_Compose_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var getSafeParamsMethod = typeof(LocalizationUtils).GetMethod("GetSafeStrictDomains", new[] { typeof(string), typeof(object[]) });
            var getSafeMatchingParamsMethod = typeof(LocalizationUtils).GetMethod("GetSafeMatchingStrictDomains", new[] { typeof(string), typeof(object[]) });

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt)
                {
                    if (codes[i].operand is not MethodInfo called || called.DeclaringType != typeof(Lang)) continue;

                    if (called.Name == "Get")
                    {
                        codes[i] = new CodeInstruction(OpCodes.Call, getSafeParamsMethod);
                        continue;
                    }

                    if (called.Name == "GetMatching")
                    {
                        codes[i] = new CodeInstruction(OpCodes.Call, getSafeMatchingParamsMethod);
                    }
                }
            }

            return codes;
        }
    }
}
