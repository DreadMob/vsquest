using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace VsQuest
{
    public class EntityBehaviorBossHuntCombatMarker : EntityBehavior
    {
        public const string BossHuntAttackersKey = "alegacyvsquest:bosshunt:attackers";
        public const string BossHuntDamageByPlayerKey = "alegacyvsquest:bosshunt:damageByPlayer";
        public const string BossHuntLastDamageMsKey = "alegacyvsquest:bosshunt:lastDamageMs";

        public EntityBehaviorBossHuntCombatMarker(Entity entity) : base(entity)
        {
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (entity?.Api?.Side != EnumAppSide.Server) return;
            if (damage <= 0) return;

            bool byPlayerDamage = damageSource?.GetCauseEntity() is EntityPlayer;

            try
            {
                var wa = entity.WatchedAttributes;
                if (wa != null)
                {
                    wa.SetLong(BossHuntLastDamageMsKey, entity.World.ElapsedMilliseconds);
                    wa.MarkPathDirty(BossHuntLastDamageMsKey);
                }
            }
            catch
            {
            }

            if (damageSource?.SourceEntity is EntityPlayer byPlayerEntity && !string.IsNullOrWhiteSpace(byPlayerEntity.PlayerUID))
            {
                try
                {
                    var wa = entity.WatchedAttributes;
                    if (wa != null)
                    {
                        var existing = wa.GetStringArray(BossHuntAttackersKey, new string[0]) ?? new string[0];
                        bool found = false;
                        for (int i = 0; i < existing.Length; i++)
                        {
                            if (string.Equals(existing[i], byPlayerEntity.PlayerUID, System.StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            var merged = new string[existing.Length + 1];
                            for (int i = 0; i < existing.Length; i++) merged[i] = existing[i];
                            merged[existing.Length] = byPlayerEntity.PlayerUID;
                            wa.SetStringArray(BossHuntAttackersKey, merged);
                            wa.MarkPathDirty(BossHuntAttackersKey);
                        }

                        try
                        {
                            var tree = wa.GetTreeAttribute(BossHuntDamageByPlayerKey);
                            if (tree == null)
                            {
                                tree = new Vintagestory.API.Datastructures.TreeAttribute();
                                wa.SetAttribute(BossHuntDamageByPlayerKey, tree);
                            }

                            double prev = tree.GetDouble(byPlayerEntity.PlayerUID, 0);
                            tree.SetDouble(byPlayerEntity.PlayerUID, prev + damage);
                            wa.MarkPathDirty(BossHuntDamageByPlayerKey);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }

            if (byPlayerDamage)
            {
                try
                {
                    var calendar = entity.World?.Calendar;
                    if (calendar == null) return;

                    double nowHours = calendar.TotalHours;

                    var wa = entity.WatchedAttributes;
                    if (wa == null) return;

                    wa.SetDouble(BossHuntSystem.LastBossDamageTotalHoursKey, nowHours);
                    wa.MarkPathDirty(BossHuntSystem.LastBossDamageTotalHoursKey);
                }
                catch
                {
                }
            }
        }

        public override string PropertyName() => "bosshuntcombatmarker";
    }
}
