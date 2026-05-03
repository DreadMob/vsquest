using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.GameContent;
using Vintagestory.API.Config;

namespace VsQuest.Harmony.Dialogue
{
    [HarmonyPatch(typeof(DlgTalkComponent), "genText")]
    public class DlgTalkComponent_genText_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var getSafeParamsMethod = typeof(LocalizationUtils).GetMethod("GetSafeStrictDomains", new[] { typeof(string), typeof(object[]) });

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt)
                {
                    if (codes[i].operand is MethodInfo called && called.DeclaringType == typeof(Lang) && called.Name == "Get")
                    {
                        codes[i] = new CodeInstruction(OpCodes.Call, getSafeParamsMethod);
                    }
                }
            }

            return codes;
        }
    }
}
