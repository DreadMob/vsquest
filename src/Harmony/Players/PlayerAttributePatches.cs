using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using VsQuest;

namespace VsQuest.Harmony
{
    public class PlayerAttributePatches
    {
        private const string SecondChanceDebuffUntilKey = "alegacyvsquest:secondchance:debuffuntil";
        private const string SecondChanceDebuffStatKey = "alegacyvsquest:secondchance:debuff";

        private const string BossGrabNoSneakUntilKey = "alegacyvsquest:bossgrab:nosneakuntil";
        private const string BossGrabWalkSpeedStatKey = "alegacyvsquest:bossgrab";

        private const string RepulseStunUntilKey = "alegacyvsquest:bossrepulsestun:until";
        private const string RepulseStunMultKey = "alegacyvsquest:bossrepulsestun:mult";
        private const string RepulseStunStatKey = "alegacyvsquest:bossrepulsestun:stat";

        private const string AshFloorNoJumpUntilKey = "alegacyvsquest:ashfloor:nojumpuntil";
        private const string AshFloorNoShiftUntilKey = "alegacyvsquest:ashfloor:noshiftuntil";
        private const string AshFloorUntilKey = "alegacyvsquest:ashfloor:until";
        private const string AshFloorWalkSpeedMultKey = "alegacyvsquest:ashfloor:walkspeedmult";
        private const string AshFloorWalkSpeedStatKey = "alegacyvsquest:ashfloor";

        private const string SecondChanceProcSound = "albase:sounds/atmospheric-metallic-swipe";
        private const float SecondChanceProcSoundRange = 24f;

        private const float SecondChanceDebuffWalkspeed = -0.2f;
        private const float SecondChanceDebuffHungerRate = 0.4f;
        private const float SecondChanceDebuffHealing = -0.3f;

        private const string UraniumMaskLastTickHoursKey = "alegacyvsquest:uraniummask:lasttickhours";

        private static bool IsBossTarget(EntityAgent target)
        {
            if (target == null) return false;

            return target.HasBehavior<EntityBehaviorBossHuntCombatMarker>()
                || target.HasBehavior<EntityBehaviorBossCombatMarker>()
                || target.HasBehavior<EntityBehaviorBossRespawn>()
                || target.HasBehavior<EntityBehaviorBossDespair>()
                || target.HasBehavior<EntityBehaviorQuestBoss>()
                || target.HasBehavior<EntityBehaviorBoss>();
        }

        [HarmonyPatch(typeof(ModSystemWearableStats), "handleDamaged")]
        public class ModSystemWearableStats_handleDamaged_PlayerAttributes_Patch
        {
            public static void Postfix(ModSystemWearableStats __instance, IPlayer player, float damage, DamageSource dmgSource, ref float __result)
            {
                if (__result <= 0f) return;

                if (player?.Entity?.WatchedAttributes == null) return;

                if (!IsProtectionApplicable(dmgSource)) return;

                float playerFlat = player.Entity.WatchedAttributes.GetFloat("vsquestadmin:attr:protection", 0f);
                float playerPerc = player.Entity.WatchedAttributes.GetFloat("vsquestadmin:attr:protectionperc", 0f);

                float newDamage = __result;
                newDamage = System.Math.Max(0f, newDamage - playerFlat);

                playerPerc = System.Math.Max(0f, System.Math.Min(0.95f, playerPerc));
                newDamage *= (1f - playerPerc);

                __result = newDamage;
            }
        }

        [HarmonyPatch(typeof(EntityAgent), "OnGameTick")]
        public class EntityAgent_OnGameTick_Unified_Patch
        {
            private static int _tickCounter = 0;
            private const int TickInterval = 5; // Run logic every 5 ticks instead of every tick
            private static float _cachedWalkSpeed = 0f;
            private static int _walkSpeedUpdateCounter = 0;
            private const int WalkSpeedUpdateInterval = 5; // Update walk speed every 5 ticks

            public static void Prefix(EntityAgent __instance)
            {
                if (__instance is not EntityPlayer player) return;
                
                // Skip processing for most ticks - only run every 5 ticks
                if (++_tickCounter % TickInterval != 0) return;
                
                if (player.World?.Side == EnumAppSide.Server)
                {
                    ProcessServerSide(player);
                }
                else
                {
                    ProcessClientSide(player);
                }
            }

            private static void ProcessServerSide(EntityPlayer player)
            {
                if (player.Stats == null || player.WatchedAttributes == null) return;

                long nowMs = 0;
                double nowHours = 0;
                try { nowMs = player.World.ElapsedMilliseconds; } catch { }
                try { nowHours = player.World.Calendar.TotalHours; } catch { }

                // Process debuffs and effects every N ticks to reduce load
                if (++_tickCounter % TickInterval == 0)
                {
                    ProcessRepulseStun(player, nowMs);
                    ProcessBossGrab(player, nowMs);
                    ProcessAshFloorServer(player, nowHours);
                    ProcessSecondChanceDebuff(player, nowHours);
                    ProcessUraniumMaskCharge(player, nowHours);
                }

                // Update walk speed every N ticks instead of every tick
                if (++_walkSpeedUpdateCounter % WalkSpeedUpdateInterval == 0)
                {
                    UpdateWalkSpeed(player);
                }
                else
                {
                    // Apply cached walk speed for smooth movement between updates
                    player.walkSpeed = _cachedWalkSpeed;
                }
            }

            private static void ProcessClientSide(EntityPlayer player)
            {
                if (player.WatchedAttributes == null) return;

                long nowMs = 0;
                double nowHours = 0;
                try { nowMs = player.World.ElapsedMilliseconds; } catch { }
                try { nowHours = player.World.Calendar.TotalHours; } catch { }

                ProcessBossGrabClient(player, nowMs);
                ProcessAshFloorClient(player, nowHours);
            }

            private static void ProcessRepulseStun(EntityPlayer player, long nowMs)
            {
                try
                {
                    long until = player.WatchedAttributes.GetLong(RepulseStunUntilKey, 0);
                    if (until <= 0)
                    {
                        // Only update stats if they need to be cleared
                        float currentWalkSpeed = player.Stats?.GetBlended("walkspeed") ?? 0f;
                        if (currentWalkSpeed != 0f)
                        {
                            player.Stats?.Set("walkspeed", RepulseStunStatKey, 0f, true);
                        }
                    }
                    else
                    {
                        bool clear = false;
                        if (nowMs > 0 && until - nowMs > 5L * 60L * 1000L) clear = true; // Expired way in future
                        if (nowMs > 0 && nowMs >= until) clear = true; // Actually expired

                        if (clear)
                        {
                            player.WatchedAttributes.SetLong(RepulseStunUntilKey, 0);
                            // Only mark dirty if we actually changed something
                            float currentMult = player.WatchedAttributes.GetFloat(RepulseStunMultKey, 1f);
                            if (currentMult != 1f)
                            {
                                player.WatchedAttributes.SetFloat(RepulseStunMultKey, 1f);
                                player.WatchedAttributes.MarkPathDirty(RepulseStunMultKey);
                            }
                            player.WatchedAttributes.MarkPathDirty(RepulseStunUntilKey);
                            player.Stats.Set("walkspeed", RepulseStunStatKey, 0f, true);
                        }
                    }
                }
                catch { }
            }

            private static void ProcessBossGrab(EntityPlayer player, long nowMs)
            {
                try
                {
                    long until = player.WatchedAttributes.GetLong(BossGrabNoSneakUntilKey, 0);
                    bool clear = false;
                    if (until <= 0) clear = true;
                    if (nowMs > 0 && until - nowMs > 5L * 60L * 1000L) clear = true;
                    if (nowMs > 0 && nowMs >= until) clear = true;

                    if (clear)
                    {
                        player.WatchedAttributes.SetLong(BossGrabNoSneakUntilKey, 0);
                        player.WatchedAttributes.MarkPathDirty(BossGrabNoSneakUntilKey);
                        player.Stats.Remove("walkspeed", BossGrabWalkSpeedStatKey);
                    }
                }
                catch { }
            }

            private static void ProcessAshFloorServer(EntityPlayer player, double nowHours)
            {
                try
                {
                    double until = player.WatchedAttributes.GetDouble(AshFloorUntilKey, 0);
                    bool ashActive = until > 0 && nowHours > 0 && nowHours < until;

                    if (!ashActive)
                    {
                        // Only clear stats if they were previously set
                        float currentMod = 0f;
                        try { currentMod = player.Stats?.GetBlended("walkspeed") ?? 0f; } catch { }
                        if (currentMod != 0f)
                        {
                            player.Stats?.Set("walkspeed", AshFloorWalkSpeedStatKey, 0f, true);
                        }
                        return;
                    }

                    // Ash is active - check if player is actually on ash floor block
                    // Only check block every 20 ticks (1 second) instead of every 5 ticks
                    long lastBlockCheck = player.WatchedAttributes.GetLong("alegacyvsquest:ashfloor:lastblockcheck", 0);
                    long nowTicks = player.World.ElapsedMilliseconds / 50; // Convert to ticks (~20ms per tick)
                    
                    if (nowTicks - lastBlockCheck < 20) return; // Skip block check this time
                    
                    player.WatchedAttributes.SetLong("alegacyvsquery:ashfloor:lastblockcheck", nowTicks);

                    bool onAshFloor = false;
                    try
                    {
                        int px = (int)Math.Floor(player.ServerPos.X);
                        int pz = (int)Math.Floor(player.ServerPos.Z);
                        int py = (int)Math.Floor(player.ServerPos.Y - 0.02);

                        var pos = new BlockPos(px, py, pz, player.ServerPos.Dimension);
                        var block = player.World.BlockAccessor.GetBlock(pos);
                        if (block?.Code != null && block.Code.Domain == "alegacyvsquest" && block.Code.Path == "ashfloor")
                        {
                            onAshFloor = true;
                        }
                    }
                    catch { }

                    // Only clear debuff if player is NOT on ash floor AND debuff is active
                    if (!onAshFloor)
                    {
                        player.WatchedAttributes.SetDouble(AshFloorUntilKey, 0);
                        // Only mark dirty if value actually changed
                        player.WatchedAttributes.MarkPathDirty(AshFloorUntilKey);
                        player.WatchedAttributes.SetDouble(AshFloorNoJumpUntilKey, 0);
                        player.WatchedAttributes.SetDouble(AshFloorNoShiftUntilKey, 0);
                        player.WatchedAttributes.SetFloat(AshFloorWalkSpeedMultKey, 0f);
                        player.Stats.Set("walkspeed", AshFloorWalkSpeedStatKey, 0f, true);
                    }
                }
                catch { }
            }

            private static void ProcessSecondChanceDebuff(EntityPlayer player, double nowHours)
            {
                try
                {
                    double until = player.WatchedAttributes.GetDouble(SecondChanceDebuffUntilKey, 0);
                    if (until <= 0)
                    {
                        ClearDebuff(player);
                        return;
                    }

                    if (nowHours >= until)
                    {
                        player.WatchedAttributes.SetDouble(SecondChanceDebuffUntilKey, 0);
                        ClearDebuff(player);
                        return;
                    }

                    ApplyDebuffStats(player);
                }
                catch { }
            }

            private static void ProcessUraniumMaskCharge(EntityPlayer player, double nowHours)
            {
                try
                {
                    if (nowHours <= 0) return;

                    double lastHours = player.WatchedAttributes.GetDouble(UraniumMaskLastTickHoursKey, 0);
                    if (lastHours <= 0)
                    {
                        player.WatchedAttributes.SetDouble(UraniumMaskLastTickHoursKey, nowHours);
                        return;
                    }

                    double dtHours = nowHours - lastHours;
                    if (dtHours <= 0) return;

                    player.WatchedAttributes.SetDouble(UraniumMaskLastTickHoursKey, nowHours);

                    var inv = player.Player?.InventoryManager?.GetOwnInventory("character");
                    if (inv == null) return;

                    string chargeKey = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrUraniumMaskChargeHours);
                    bool anyChanged = false;

                    foreach (ItemSlot slot in inv)
                    {
                        if (slot?.Empty != false) continue;
                        var stack = slot.Itemstack;
                        if (stack?.Item is not ItemWearable) continue;
                        if (stack.Attributes == null) continue;
                        if (!stack.Attributes.HasAttribute(chargeKey)) continue;

                        float hours = stack.Attributes.GetFloat(chargeKey, 0f);
                        if (hours <= 0f) continue;

                        float newHours = Math.Max(0f, hours - (float)dtHours);
                        if (Math.Abs(newHours - hours) > 0.0001f)
                        {
                            stack.Attributes.SetFloat(chargeKey, newHours);
                            anyChanged = true;
                        }
                    }

                    if (anyChanged)
                    {
                        // Slots are marked dirty individually above when modified
                    }
                }
                catch { }
            }

            private static void ProcessBossGrabClient(EntityPlayer player, long nowMs)
            {
                try
                {
                    long until = player.WatchedAttributes.GetLong(BossGrabNoSneakUntilKey, 0);
                    if (until <= 0) return;

                    if (until > 0 && nowMs > 0 && until - nowMs > 5L * 60L * 1000L)
                    {
                        player.WatchedAttributes.SetLong(BossGrabNoSneakUntilKey, 0);
                        return;
                    }

                    if (nowMs > 0 && nowMs < until)
                    {
                        player.Controls.Sneak = false;
                    }
                }
                catch { }
            }

            private static void ProcessAshFloorClient(EntityPlayer player, double nowHours)
            {
                try
                {
                    if (nowHours <= 0) return;

                    double untilJump = player.WatchedAttributes.GetDouble(AshFloorNoJumpUntilKey, 0);
                    if (untilJump > 0 && nowHours < untilJump)
                    {
                        player.Controls.Jump = false;
                    }

                    double untilShift = player.WatchedAttributes.GetDouble(AshFloorNoShiftUntilKey, 0);
                    if (untilShift > 0 && nowHours < untilShift)
                    {
                        player.Controls.ShiftKey = false;
                        player.Controls.Sneak = false;
                    }
                }
                catch { }
            }

            private static void UpdateWalkSpeed(EntityPlayer player)
            {
                try
                {
                    float targetWalkSpeed = player.Stats.GetBlended("walkspeed");
                    if (Math.Abs(player.walkSpeed - targetWalkSpeed) > 0.001f)
                    {
                        player.walkSpeed = targetWalkSpeed;
                    }
                    // Cache the value for use between updates
                    _cachedWalkSpeed = player.walkSpeed;
                }
                catch { }
            }
        }

        private static bool IsProtectionApplicable(DamageSource dmgSource)
        {
            EnumDamageType type;
            try
            {
                type = dmgSource?.Type ?? EnumDamageType.Injury;
            }
            catch
            {
                type = EnumDamageType.Injury;
            }

            // Apply custom armor only to direct physical damage.
            // Do not reduce suffocation/drowning, hunger, poison, fire, etc.
            return type == EnumDamageType.BluntAttack
                || type == EnumDamageType.SlashingAttack
                || type == EnumDamageType.PiercingAttack
                || type == EnumDamageType.Crushing
                || type == EnumDamageType.Injury;
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), "OnEntityReceiveDamage")]
        public class EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance_Patch
        {
            public static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
            {
                if (damageSource?.Type == EnumDamageType.Heal) return;
                if (__instance?.entity is not EntityPlayer player) return;
                if (player.World?.Side == EnumAppSide.Client) return;
                if (player.Player?.InventoryManager == null) return;
                if (damage <= 0f) return;

                float health = __instance.Health;
                if (health - damage > 0f) return;

                if (!TryGetSecondChanceSlot(player, out var slot)) return;

                float charges = GetSecondChanceCharges(slot.Itemstack);
                if (charges < 1f) return;

                float targetHealth = Math.Max(0.1f, __instance.MaxHealth * 0.7f);
                __instance.Health = Math.Max(targetHealth, __instance.Health);
                damage = 0f;

                SetSecondChanceCharges(slot.Itemstack, charges - 1f);
                slot.MarkDirty();

                ApplySecondChanceDebuff(player);
                TryPlaySecondChanceSound(player);
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorHealth), "OnEntityDeath")]
        public class EntityBehaviorHealth_OnEntityDeath_SecondChanceReset_Patch
        {
            public static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSourceForDeath)
            {
                if (__instance?.entity is not EntityPlayer player) return;
                if (player.Player?.InventoryManager == null) return;

                try
                {
                    if (player.Api?.Side != EnumAppSide.Server) return;
                    var sapi = player.Api as Vintagestory.API.Server.ICoreServerAPI;
                    var system = sapi?.ModLoader?.GetModSystem<VsQuest.BossHuntArenaSystem>();
                    system?.TryHandlePlayerDeath(player);
                }
                catch
                {
                }

                if (!TryGetSecondChanceSlot(player, out var slot)) return;
                SetSecondChanceCharges(slot.Itemstack, 0f);
                slot.MarkDirty();
            }
        }

        private static bool TryGetSecondChanceSlot(EntityPlayer player, out ItemSlot slot)
        {
            slot = null;
            var inv = player.Player?.InventoryManager?.GetOwnInventory("character");
            if (inv == null) return false;

            foreach (ItemSlot s in inv)
            {
                if (s?.Empty != false) continue;
                var stack = s.Itemstack;
                if (!ItemAttributeUtils.IsActionItem(stack)) continue;

                string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
                if (stack.Attributes.HasAttribute(key))
                {
                    slot = s;
                    return true;
                }
            }

            return false;
        }

        private static float GetSecondChanceCharges(ItemStack stack)
        {
            if (stack?.Attributes == null) return 0f;
            string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            return stack.Attributes.GetFloat(key, 0f);
        }

        private static void SetSecondChanceCharges(ItemStack stack, float value)
        {
            if (stack?.Attributes == null) return;
            string key = ItemAttributeUtils.GetKey(ItemAttributeUtils.AttrSecondChanceCharges);
            stack.Attributes.SetFloat(key, Math.Clamp(value, 0f, 3f));
        }

        private static void ApplySecondChanceDebuff(EntityPlayer player)
        {
            double until = player.World.Calendar.TotalHours + (2d / 60d);
            player.WatchedAttributes.SetDouble(SecondChanceDebuffUntilKey, until);
            ApplyDebuffStats(player);
        }

        private static void TryPlaySecondChanceSound(EntityPlayer player)
        {
            if (player?.World == null) return;
            if (string.IsNullOrWhiteSpace(SecondChanceProcSound)) return;

            try
            {
                AssetLocation soundLoc = AssetLocation.Create(SecondChanceProcSound, "game").WithPathPrefixOnce("sounds/");
                player.World.PlaySoundAt(soundLoc, player.ServerPos.X, player.ServerPos.Y, player.ServerPos.Z, null, randomizePitch: true, SecondChanceProcSoundRange);
            }
            catch
            {
            }
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

        [HarmonyPatch(typeof(EntityAgent), "ReceiveDamage")]
        public class EntityAgent_ReceiveDamage_PlayerAttackPower_Patch
        {
            public static void Prefix(EntityAgent __instance, DamageSource damageSource, ref float damage)
            {
                if (__instance?.WatchedAttributes != null)
                {
                    try
                    {
                        if (damageSource != null && IsBossTarget(__instance))
                        {
                            damageSource.KnockbackStrength = 0f;
                        }

                        if (__instance.WatchedAttributes.GetBool("alegacyvsquest:bossclone:invulnerable", false))
                        {
                            damage = 0f;
                            if (damageSource != null)
                            {
                                damageSource.KnockbackStrength = 0f;
                            }
                            return;
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        var sourceEntity = damageSource.SourceEntity;
                        var causeEntity = damageSource.GetCauseEntity() ?? sourceEntity;
                        if (causeEntity is EntityPlayer attacker && attacker.Player?.InventoryManager != null)
                        {
                            var inv = attacker.Player.InventoryManager.GetOwnInventory("character");
                            if (inv != null)
                            {
                                float knockbackBonus = 0f;
                                foreach (ItemSlot slot in inv)
                                {
                                    if (!slot.Empty && slot.Itemstack?.Item is ItemWearable)
                                    {
                                        knockbackBonus += ItemAttributeUtils.GetAttributeFloatScaled(slot.Itemstack, ItemAttributeUtils.AttrKnockbackMult);
                                    }
                                }

                                if (knockbackBonus != 0f && damageSource.KnockbackStrength > 0f)
                                {
                                    float mult = GameMath.Clamp(1f + knockbackBonus, 0f, 5f);
                                    damageSource.KnockbackStrength *= mult;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (damage <= 0f) return;

                if (damageSource != null)
                {
                    try
                    {
                        var sourceEntity = damageSource.SourceEntity;
                        var causeEntity = damageSource.GetCauseEntity() ?? sourceEntity;
                        var sourceAttrs = sourceEntity?.WatchedAttributes ?? causeEntity?.WatchedAttributes;
                        if (sourceAttrs != null && sourceAttrs.GetBool("alegacyvsquest:bossclone", false))
                        {
                            long ownerId = sourceAttrs.GetLong("alegacyvsquest:bossclone:ownerid", 0);
                            if (ownerId > 0 && __instance != null && __instance.EntityId == ownerId)
                            {
                                damage = 0f;
                                if (damageSource != null)
                                {
                                    damageSource.KnockbackStrength = 0f;
                                }
                                return;
                            }
                        }

                        if (sourceEntity?.WatchedAttributes != null)
                        {
                            long firedById = sourceEntity.WatchedAttributes.GetLong("firedBy", 0);
                            if (firedById > 0 && __instance?.World != null)
                            {
                                var firedByEntity = __instance.World.GetEntityById(firedById);
                                var firedByAttrs = firedByEntity?.WatchedAttributes;
                                if (firedByAttrs != null && firedByAttrs.GetBool("alegacyvsquest:bossclone", false))
                                {
                                    long ownerId = firedByAttrs.GetLong("alegacyvsquest:bossclone:ownerid", 0);
                                    if (ownerId > 0 && __instance.EntityId == ownerId)
                                    {
                                        damage = 0f;
                                        if (damageSource != null)
                                        {
                                            damageSource.KnockbackStrength = 0f;
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        var sourceEntity = damageSource.SourceEntity;
                        var causeEntity = damageSource.GetCauseEntity() ?? sourceEntity;
                        var sourceAttrs = sourceEntity?.WatchedAttributes ?? causeEntity?.WatchedAttributes;
                        if (sourceAttrs == null) return;

                        float mult = sourceAttrs.GetFloat("alegacyvsquest:bossclone:damagemult", 0f);
                        if (mult > 0f && mult < 0.999f)
                        {
                            damage *= mult;
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        float growthMult = damageSource.SourceEntity.WatchedAttributes.GetFloat("alegacyvsquest:bossgrowthritual:damagemult", 0f);
                        if (growthMult > 0f && Math.Abs(growthMult - 1f) > 0.001f)
                        {
                            damage *= growthMult;
                        }
                    }
                    catch
                    {
                    }
                }

                if (damageSource?.SourceEntity is not EntityPlayer byEntity) return;
                if (byEntity.WatchedAttributes == null) return;

                float bonus = byEntity.WatchedAttributes.GetFloat("vsquestadmin:attr:attackpower", 0f);
                if (bonus != 0f)
                {
                    damage += bonus;
                }
            }
        }

        [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "OnGameTick")]
        public class EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth_Patch
        {
            public static void Prefix(EntityBehaviorBodyTemperature __instance)
            {
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
                catch
                {
                }
            }
        }
    }
}
