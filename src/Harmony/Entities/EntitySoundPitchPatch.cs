using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Entity), "PlayEntitySound")]
    public static class EntitySoundPitchPatch
    {
        public static bool Prefix(Entity __instance, string type, IPlayer dualCallByPlayer)
        {
            try
            {
                if (!HarmonyPatchSwitches.EntitySoundPitchEnabled(HarmonyPatchSwitches.EntitySoundPitch_Entity_PlayEntitySound)) return true;
                if (__instance?.Properties?.Sounds == null) return true;
                if (!__instance.Properties.Sounds.TryGetValue(type, out var sound) || sound.Location == null) return true;

                float mult = 1f;
                Dictionary<string, float> volumeBySound = null;
                try
                {
                    mult = __instance.Properties.Attributes?["vsquestSoundPitchMul"].AsFloat(1f) ?? 1f;
                }
                catch
                {
                }

                if (mult <= 0f || Math.Abs(mult - 1f) < 0.0001f)
                {
                    mult = 1f;
                }

                try
                {
                    volumeBySound = __instance.Properties.Attributes?["SoundVolumeMulBySound"].AsObject<Dictionary<string, float>>();
                }
                catch
                {
                }

                if (__instance.Properties.ResolvedSounds == null || !__instance.Properties.ResolvedSounds.TryGetValue(type, out var locations) || locations.Length == 0)
                {
                    return true;
                }

                var location = locations[__instance.World.Rand.Next(locations.Length)];
                
                // Always apply pitch randomization for entity sounds
                float pitch = (float)__instance.World.Rand.NextDouble() * 0.5f + 0.75f;
                if (mult != 1f)
                {
                    pitch *= mult;
                }

                float volume = 1f;
                if (volumeBySound != null && volumeBySound.Count > 0 && TryGetVolumeMultiplier(volumeBySound, location, out float volumeMult))
                {
                    if (volumeMult > 0f)
                    {
                        volume = volumeMult;
                    }
                }

                __instance.World.PlaySoundAt(location, (float)__instance.Pos.X, (float)__instance.Pos.InternalY, (float)__instance.Pos.Z, dualCallByPlayer, pitch, volume);
                return false;
            }
            catch
            {
                // On any error, let vanilla handle it
                return true;
            }
        }

        private static bool TryGetVolumeMultiplier(Dictionary<string, float> volumeBySound, AssetLocation location, out float mult)
        {
            mult = 1f;
            if (volumeBySound == null || location == null) return false;

            string fullKey = location.ToString();
            string pathKey = location.Path;

            foreach (var entry in volumeBySound)
            {
                if (string.IsNullOrWhiteSpace(entry.Key)) continue;

                if (string.Equals(entry.Key, fullKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry.Key, pathKey, StringComparison.OrdinalIgnoreCase))
                {
                    mult = entry.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
