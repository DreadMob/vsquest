using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Players
{
    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "OnGameTick")]
    public class EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth_Patch
    {
        // Cached reflection fields - initialized once
        private static readonly FieldInfo ClothingBonusField;
        private static readonly FieldInfo LastWearableHoursField;
        
        static EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth_Patch()
        {
            ClothingBonusField = AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "clothingBonus");
            LastWearableHoursField = AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "lastWearableHoursTotalUpdate");
        }

        public static void Prefix(EntityBehaviorBodyTemperature __instance)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth)) return;
            
            // Fast fail: check fields once
            if (ClothingBonusField == null || LastWearableHoursField == null) return;
            
            var entity = __instance?.entity as EntityPlayer;
            if (entity?.WatchedAttributes == null) return;

            // Batch read WatchedAttributes
            var wa = entity.WatchedAttributes;
            float desiredBonus = wa.GetFloat("vsquestadmin:attr:warmth", 0f);
            float appliedBonus = wa.GetFloat("vsquestadmin:attr:warmth:applied", 0f);
            
            // Early exit: no change needed
            float delta = desiredBonus - appliedBonus;
            if (delta == 0f) return;

            try
            {
                double lastWearableHours = (double)LastWearableHoursField.GetValue(__instance);
                double storedLastWearableHours = wa.GetDouble("vsquestadmin:attr:warmth:lastwearablehours", double.NaN);

                if (double.IsNaN(storedLastWearableHours) || storedLastWearableHours != lastWearableHours)
                {
                    wa.SetDouble("vsquestadmin:attr:warmth:lastwearablehours", lastWearableHours);
                    wa.SetFloat("vsquestadmin:attr:warmth:applied", 0f);
                    appliedBonus = 0f;
                    delta = desiredBonus; // Recalculate delta
                    if (delta == 0f) return;
                }

                float cur = (float)ClothingBonusField.GetValue(__instance);
                ClothingBonusField.SetValue(__instance, cur + delta);
                wa.SetFloat("vsquestadmin:attr:warmth:applied", desiredBonus);
            }
            catch (Exception e)
            {
                entity?.Api?.Logger?.Error($"[vsquest] EntityBehaviorBodyTemperature.OnGameTick Prefix warmth bonus: {e}");
            }
        }
    }
}
