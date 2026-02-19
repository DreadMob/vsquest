using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest.Harmony.Players
{
    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "OnGameTick")]
    public class EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth_Patch
    {
        public static void Prefix(EntityBehaviorBodyTemperature __instance)
        {
            if (!HarmonyPatchSwitches.PlayerEnabled(HarmonyPatchSwitches.Player_EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth)) return;
            var entity = __instance?.entity as EntityPlayer;

            if (entity?.WatchedAttributes == null) return;

            float desiredBonus = entity.WatchedAttributes.GetFloat("vsquestadmin:attr:warmth", 0f);

            const string AppliedKey = "vsquestadmin:attr:warmth:applied";
            const string LastWearableHoursKey = "vsquestadmin:attr:warmth:lastwearablehours";

            try
            {
                var clothingBonusField = AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "clothingBonus");
                var lastWearableHoursField = AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "lastWearableHoursTotalUpdate");

                if (clothingBonusField == null || lastWearableHoursField == null) return;

                double lastWearableHours = (double)lastWearableHoursField.GetValue(__instance);
                double storedLastWearableHours = entity.WatchedAttributes.GetDouble(LastWearableHoursKey, double.NaN);

                if (double.IsNaN(storedLastWearableHours) || storedLastWearableHours != lastWearableHours)
                {
                    entity.WatchedAttributes.SetDouble(LastWearableHoursKey, lastWearableHours);
                    entity.WatchedAttributes.SetFloat(AppliedKey, 0f);
                }

                float appliedBonus = entity.WatchedAttributes.GetFloat(AppliedKey, 0f);
                float delta = desiredBonus - appliedBonus;
                if (delta == 0f) return;

                float cur = (float)clothingBonusField.GetValue(__instance);
                clothingBonusField.SetValue(__instance, cur + delta);

                entity.WatchedAttributes.SetFloat(AppliedKey, desiredBonus);
            }
            catch (Exception e)
            {
                entity?.Api?.Logger?.Error($"[vsquest] EntityBehaviorBodyTemperature.OnGameTick Prefix warmth bonus: {e}");
            }
        }
    }
}
