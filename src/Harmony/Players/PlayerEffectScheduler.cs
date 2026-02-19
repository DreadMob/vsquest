using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using VsQuest.Systems.Performance;

namespace VsQuest.Harmony.Players
{
    public static class PlayerEffectScheduler
    {
        private const string RepulseStunUntilKey = "alegacyvsquest:bossrepulsestun:until";
        private const string RepulseStunMultKey = "alegacyvsquest:bossrepulsestun:mult";
        private const string RepulseStunStatKey = "alegacyvsquest:bossrepulsestun:stat";
        private const string BossGrabNoSneakUntilKey = "alegacyvsquest:bossgrab:nosneakuntil";
        private const string BossGrabWalkSpeedStatKey = "alegacyvsquest:bossgrab";
        private const string SecondChanceDebuffUntilKey = "alegacyvsquest:secondchance:debuffuntil";
        private const string SecondChanceDebuffStatKey = "alegacyvsquest:secondchance:debuff";
        private const float SecondChanceDebuffWalkspeed = -0.2f;
        private const float SecondChanceDebuffHungerRate = 0.4f;
        private const float SecondChanceDebuffHealing = -0.3f;

        public static void ScheduleRepulseStunEffect(ICoreServerAPI api, EntityPlayer player, int durationMs)
        {
            long until = api.World.ElapsedMilliseconds + durationMs;
            player.WatchedAttributes.SetLong(RepulseStunUntilKey, until);

            // Apply immediately
            player.Stats.Set("walkspeed", RepulseStunStatKey, player.WatchedAttributes.GetFloat(RepulseStunMultKey, 0.5f), true);

            // Schedule cleanup with ZeroPollEffectSystem
            ZeroPollEffectSystem.ApplyTimedEffect(
                api,
                player,
                "repulsestun",
                durationMs,
                onApply: (p) => { /* Already applied above */ },
                onExpire: (p) =>
                {
                    p.WatchedAttributes.SetLong(RepulseStunUntilKey, 0);
                    float currentMult = p.WatchedAttributes.GetFloat(RepulseStunMultKey, 1f);
                    if (currentMult != 1f)
                    {
                        p.WatchedAttributes.SetFloat(RepulseStunMultKey, 1f);
                        p.WatchedAttributes.MarkPathDirty(RepulseStunMultKey);
                    }
                    p.WatchedAttributes.MarkPathDirty(RepulseStunUntilKey);
                    p.Stats.Remove("walkspeed", RepulseStunStatKey);
                }
            );
        }

        public static void ScheduleBossGrabEffect(ICoreServerAPI api, EntityPlayer player, int durationMs)
        {
            long until = api.World.ElapsedMilliseconds + durationMs;
            player.WatchedAttributes.SetLong(BossGrabNoSneakUntilKey, until);
            player.WatchedAttributes.MarkPathDirty(BossGrabNoSneakUntilKey);

            // Apply walk speed penalty immediately
            player.Stats.Set("walkspeed", BossGrabWalkSpeedStatKey, -0.5f, true);

            // Schedule cleanup with ZeroPollEffectSystem
            ZeroPollEffectSystem.ApplyTimedEffect(
                api,
                player,
                "bossgrab",
                durationMs,
                onApply: (p) => { /* Already applied above */ },
                onExpire: (p) =>
                {
                    p.WatchedAttributes.SetLong(BossGrabNoSneakUntilKey, 0);
                    p.WatchedAttributes.MarkPathDirty(BossGrabNoSneakUntilKey);
                    p.Stats.Remove("walkspeed", BossGrabWalkSpeedStatKey);
                }
            );
        }

        public static void ScheduleSecondChanceDebuff(ICoreServerAPI api, EntityPlayer player)
        {
            double durationHours = 2d / 60d; // 2 minutes
            double until = player.World.Calendar.TotalHours + durationHours;
            player.WatchedAttributes.SetDouble(SecondChanceDebuffUntilKey, until);

            // Apply debuff immediately
            ApplyDebuffStats(player);

            // Convert game hours to milliseconds (approximate: 1 game day = 24 real minutes = 1440000 ms)
            int durationMs = (int)(durationHours * 1440000 / 24);

            // Schedule cleanup with ZeroPollEffectSystem
            ZeroPollEffectSystem.ApplyTimedEffect(
                api,
                player,
                "secondchancedebuff",
                durationMs,
                onApply: (p) => { /* Already applied above */ },
                onExpire: (p) =>
                {
                    p.WatchedAttributes.SetDouble(SecondChanceDebuffUntilKey, 0);
                    ClearDebuff(p);
                }
            );
        }

        private static void ApplyDebuffStats(EntityPlayer player)
        {
            player.Stats.Set("walkspeed", SecondChanceDebuffStatKey, SecondChanceDebuffWalkspeed, true);
            player.Stats.Set("hungerrate", SecondChanceDebuffStatKey, SecondChanceDebuffHungerRate, true);
            player.Stats.Set("healingeffectivness", SecondChanceDebuffStatKey, SecondChanceDebuffHealing, true);
            player.walkSpeed = player.Stats.GetBlended("walkspeed");
        }

        private static void ClearDebuff(EntityPlayer player)
        {
            player.Stats.Set("walkspeed", SecondChanceDebuffStatKey, 0f, true);
            player.Stats.Set("hungerrate", SecondChanceDebuffStatKey, 0f, true);
            player.Stats.Set("healingeffectivness", SecondChanceDebuffStatKey, 0f, true);
            player.walkSpeed = player.Stats.GetBlended("walkspeed");
        }
    }
}
