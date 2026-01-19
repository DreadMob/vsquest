using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VsQuest.Harmony
{
    [HarmonyPatch]
    public static class EntityPlayerBotInteractPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Vintagestory.API.Common.Entities.EntityPlayerBot");
            return type == null ? null : AccessTools.Method(type, "OnInteract");
        }

        public static bool Prefix(object __instance, EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            try
            {
                if (__instance is not EntityAgent entity || entity?.Properties?.Attributes?[
                        "alegacyvsquestNoUndress"].AsBool(false) != true)
                {
                    return true;
                }

                if (byEntity is not EntityPlayer eplr) return true;
                if (!eplr.Controls.Sneak) return true;
                if (mode != EnumInteractMode.Interact) return true;
                if (byEntity.World.Side != EnumAppSide.Server) return true;
                if (eplr.Player?.WorldData?.CurrentGameMode != EnumGameMode.Creative) return true;

                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
