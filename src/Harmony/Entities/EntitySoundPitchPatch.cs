using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Entity), "PlayEntitySound")]
    public static class EntitySoundPitchPatch
    {
        public static bool Prefix(Entity __instance, string type, IPlayer dualCallByPlayer, bool randomizePitch, float range)
        {
            try
            {
                if (__instance?.Properties?.Attributes == null) return true;

                float mult = 1f;
                try
                {
                    mult = __instance.Properties.Attributes["vsquestSoundPitchMul"].AsFloat(1f);
                }
                catch
                {
                }

                if (mult <= 0f || Math.Abs(mult - 1f) < 0.0001f) return true;

                if (__instance.Properties.ResolvedSounds == null
                    || !__instance.Properties.ResolvedSounds.TryGetValue(type, out var locations)
                    || locations.Length == 0)
                {
                    return true;
                }

                var location = locations[__instance.World.Rand.Next(locations.Length)];
                float pitch = randomizePitch ? (float)__instance.World.Rand.NextDouble() * 0.5f + 0.75f : 1f;
                pitch *= mult;

                __instance.World.PlaySoundAt(location, (float)__instance.SidedPos.X, (float)__instance.SidedPos.InternalY, (float)__instance.SidedPos.Z, dualCallByPlayer, pitch, range);
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
