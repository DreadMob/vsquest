using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class QuestEventHandler
    {
        private double bossKillCreditMinShareCeil = 0.5;
        private double bossKillCreditMinShareFloor = 0.08;
        private float bossKillHealFraction = 0.17f;

        private const string SummonedByEntityIdKey = "alegacyvsquest:bosssummonritual:summonedByEntityId";

        private readonly Dictionary<string, Quest> questRegistry;
        private readonly QuestPersistenceManager persistenceManager;
        private readonly ICoreServerAPI sapi;
        private QuestSystem questSystem;

        private void ApplyCoreConfig()
        {
            AlegacyVsQuestConfig cfg = null;
            try
            {
                cfg = questSystem?.CoreConfig;
            }
            catch
            {
                cfg = null;
            }

            var combat = cfg?.BossCombat;
            if (combat != null)
            {
                bossKillCreditMinShareCeil = combat.BossKillCreditMinShareCeil;
                bossKillCreditMinShareFloor = combat.BossKillCreditMinShareFloor;
                bossKillHealFraction = combat.BossKillHealFraction;

                if (bossKillCreditMinShareCeil < 0) bossKillCreditMinShareCeil = 0;
                if (bossKillCreditMinShareFloor < 0) bossKillCreditMinShareFloor = 0;
                if (bossKillCreditMinShareFloor > bossKillCreditMinShareCeil) bossKillCreditMinShareFloor = bossKillCreditMinShareCeil;

                if (bossKillHealFraction < 0f) bossKillHealFraction = 0f;
            }
        }

        public QuestEventHandler(Dictionary<string, Quest> questRegistry, QuestPersistenceManager persistenceManager, ICoreServerAPI sapi)
        {
            this.questRegistry = questRegistry;
            this.persistenceManager = persistenceManager;
            this.sapi = sapi;
        }

        public void RegisterEventHandlers()
        {
            questSystem = QuestSystemCache.Get(sapi);

            ApplyCoreConfig();

            sapi.Event.GameWorldSave += OnGameWorldSave;
            sapi.Event.PlayerJoin += OnPlayerJoin;
            sapi.Event.PlayerDisconnect += OnPlayerDisconnect;
            sapi.Event.OnEntityDeath += OnEntityDeath;
            sapi.Event.DidBreakBlock += OnBlockBroken;
            sapi.Event.DidPlaceBlock += OnBlockPlaced;
            sapi.Event.RegisterGameTickListener(OnQuestTick, 5000);
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (byPlayer == null) return;

            // Delay to give vanilla ModJournal time to load the player's journal.
            sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    var epl = byPlayer.Entity;
                    if (epl?.Stats != null)
                    {
                        epl.Stats.Remove("walkspeed", "alegacyvsquest");
                        epl.Stats.Remove("walkspeed", "alegacyvsquest:bossgrab");
                        epl.Stats.Remove("walkspeed", "alegacyvsquest:bosshook");
                        epl.walkSpeed = epl.Stats.GetBlended("walkspeed");
                    }
                }
                catch
                {
                }

                var questSystem = QuestSystemCache.Get(sapi);
                QuestSystemAdminUtils.ForgetOutdatedQuestsForPlayer(questSystem, byPlayer, sapi);
            }, 1000);
        }

        private void OnGameWorldSave()
        {
            persistenceManager.SaveAllPlayerQuests();
        }

        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            persistenceManager.UnloadPlayerQuests(byPlayer.PlayerUID);
            QuestTickUtil.ClearPlayerCache(byPlayer.PlayerUID);
            WalkDistanceObjective.ClearPlayerCache(byPlayer.PlayerUID);
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            var credited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                long summonedBy = 0;
                try
                {
                    summonedBy = entity?.WatchedAttributes?.GetLong(SummonedByEntityIdKey, 0) ?? 0;
                }
                catch
                {
                    summonedBy = 0;
                }

                if (summonedBy != 0)
                {
                    sapi.Event.RegisterCallback(_ =>
                    {
                        try
                        {
                            if (entity == null) return;
                            if (entity.Alive) return;

                            sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        }
                        catch
                        {
                        }
                    }, 2000);
                }
            }
            catch
            {
            }

            try
            {
                var killer = damageSource?.SourceEntity ?? damageSource?.CauseEntity;
                if (killer != null && killer.Alive && killer != entity && IsBossEntity(killer))
                {
                    TryHealBossOnKill(killer);
                }
            }
            catch
            {
            }

            if (damageSource?.SourceEntity is EntityPlayer player && !string.IsNullOrWhiteSpace(player.PlayerUID))
            {
                credited.Add(player.PlayerUID);
            }

            if (IsBossEntity(entity) && IsFinalBossStage(entity))
            {
                try
                {
                    var wa = entity?.WatchedAttributes;
                    if (wa != null)
                    {
                        try
                        {
                            float maxHp = 0;
                            var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
                            if (healthBh != null)
                            {
                                maxHp = healthBh.MaxHealth;
                            }

                            var dmgTree = wa.GetTreeAttribute(EntityBehaviorBossCombatMarker.BossCombatDamageByPlayerKey);
                            if (dmgTree != null && maxHp > 0)
                            {
                                var attackers = wa.GetStringArray(EntityBehaviorBossCombatMarker.BossCombatAttackersKey, new string[0]) ?? new string[0];
                                int attackersWithDamage = 0;
                                for (int i = 0; i < attackers.Length; i++)
                                {
                                    var uid = attackers[i];
                                    if (string.IsNullOrWhiteSpace(uid)) continue;

                                    double dmg = dmgTree.GetDouble(uid, 0);
                                    if (dmg > 0)
                                    {
                                        attackersWithDamage++;
                                    }
                                }

                                double minShare = GetBossKillCreditMinShare(attackersWithDamage);
                                for (int i = 0; i < attackers.Length; i++)
                                {
                                    var uid = attackers[i];
                                    if (string.IsNullOrWhiteSpace(uid)) continue;

                                    double dmg = dmgTree.GetDouble(uid, 0);
                                    if (dmg > 0 && dmg / maxHp >= minShare)
                                    {
                                        credited.Add(uid);
                                    }
                                }
                            }
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

            var creditedPlayers = new List<IServerPlayer>();

            foreach (var uid in credited)
            {
                if (string.IsNullOrWhiteSpace(uid)) continue;

                IPlayer iPlayer = null;
                try
                {
                    iPlayer = sapi?.World?.PlayerByUid(uid);
                }
                catch
                {
                    iPlayer = null;
                }

                var epl = iPlayer?.Entity as EntityPlayer;
                if (epl == null) continue;

                var serverPlayer = iPlayer as IServerPlayer;
                if (serverPlayer != null)
                {
                    creditedPlayers.Add(serverPlayer);
                }

                var quests = persistenceManager.GetPlayerQuests(uid);
                QuestDeathUtil.HandleEntityDeath(sapi, quests, epl, entity);
            }

            try
            {
                if (IsBossEntity(entity) && IsFinalBossStage(entity))
                {
                    if (creditedPlayers.Count > 0)
                    {
                        BossKillAnnouncementUtil.AnnounceBossDefeated(sapi, creditedPlayers, entity);
                    }
                    else if (damageSource?.SourceEntity is EntityPlayer announcePlayer)
                    {
                        var serverPlayer = announcePlayer.Player as IServerPlayer;
                        if (serverPlayer != null)
                        {
                            BossKillAnnouncementUtil.AnnounceBossDefeated(sapi, serverPlayer, entity);
                        }
                    }
                }
            }
            catch
            {
            }

            var victimPlayer = entity as EntityPlayer;
            if (victimPlayer != null)
            {
                var killer = damageSource?.SourceEntity ?? damageSource?.CauseEntity;
                if (killer != null && (killer.GetBehavior<EntityBehaviorQuestBoss>() != null || killer.GetBehavior<EntityBehaviorQuestTarget>() != null || killer.GetBehavior<EntityBehaviorBoss>() != null))
                {
                    var serverVictim = victimPlayer.Player as IServerPlayer;
                    if (serverVictim != null)
                    {
                        var qs = QuestSystemCache.Get(sapi);
                        if (qs?.Config == null || qs.Config.ShowCustomBossDeathMessage)
                        {
                            BossKillAnnouncementUtil.AnnouncePlayerKilledByBoss(sapi, serverVictim, killer);
                        }
                    }
                }
            }
        }

        private void TryHealBossOnKill(Entity boss)
        {
            if (boss == null) return;

            try
            {
                if (!BossBehaviorUtils.TryGetHealth(boss, out ITreeAttribute healthTree, out float currentHealth, out float maxHealth)) return;
                if (maxHealth <= 0f) return;

                float add = maxHealth * bossKillHealFraction;
                if (add <= 0f) return;

                float next = currentHealth + add;
                if (next > maxHealth) next = maxHealth;

                healthTree.SetFloat("currenthealth", next);
                boss.WatchedAttributes.MarkPathDirty("health");
            }
            catch
            {
            }
        }

        private double GetBossKillCreditMinShare(int attackersWithDamage)
        {
            if (attackersWithDamage <= 1)
            {
                return bossKillCreditMinShareCeil;
            }

            double ceil = bossKillCreditMinShareCeil;
            double floor = bossKillCreditMinShareFloor;

            if (ceil < 0) ceil = 0;
            if (floor < 0) floor = 0;
            if (floor > ceil) floor = ceil;

            double share = ceil / Math.Sqrt(Math.Max(1, attackersWithDamage));
            if (share < floor) share = floor;
            if (share > ceil) share = ceil;

            return share;
        }

        private static bool IsBossEntity(Entity entity)
        {
            if (entity == null) return false;

            return entity.GetBehavior<EntityBehaviorBossHuntCombatMarker>() != null
                || entity.GetBehavior<EntityBehaviorBossCombatMarker>() != null
                || entity.GetBehavior<EntityBehaviorBossRespawn>() != null
                || entity.GetBehavior<EntityBehaviorBossDespair>() != null
                || entity.GetBehavior<EntityBehaviorQuestBoss>() != null;
        }

        private static bool IsFinalBossStage(Entity entity)
        {
            if (entity == null) return false;

            var rebirth2 = entity.GetBehavior<EntityBehaviorBossRebirth2>();
            return rebirth2 == null || rebirth2.IsFinalStage;
        }

        private void OnBlockBroken(IServerPlayer byPlayer, int blockId, BlockSelection blockSel)
        {
            if (byPlayer == null || blockSel == null)
            {
                return;
            }

            var blockCode = sapi.World.GetBlock(blockId)?.Code.ToString();
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            var playerQuests = persistenceManager.GetPlayerQuests(byPlayer.PlayerUID);
            if (playerQuests == null || playerQuests.Count == 0) return;

            QuestSystem qs = null;
            try
            {
                qs = QuestSystemCache.Get(sapi);
            }
            catch
            {
                qs = null;
            }

            int count = playerQuests.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= playerQuests.Count) break;
                var quest = playerQuests[i];
                if (quest == null || string.IsNullOrWhiteSpace(quest.questId)) continue;

                try
                {
                    if (qs?.QuestRegistry == null) continue;
                    if (!qs.QuestRegistry.TryGetValue(quest.questId, out var questDef) || questDef == null) continue;
                    if (questDef.blockBreakObjectives == null || questDef.blockBreakObjectives.Count == 0) continue;
                }
                catch
                {
                }

                quest.OnBlockBroken(blockCode, position, byPlayer);
            }
        }

        private void OnBlockPlaced(IServerPlayer byPlayer, int oldBlockId, BlockSelection blockSel, ItemStack itemstack)
        {
            if (byPlayer == null || blockSel == null)
            {
                return;
            }

            var blockCode = sapi.World.BlockAccessor.GetBlock(blockSel.Position)?.Code.ToString();
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            var playerQuests = persistenceManager.GetPlayerQuests(byPlayer.PlayerUID);
            if (playerQuests == null || playerQuests.Count == 0) return;

            QuestSystem qs = null;
            try
            {
                qs = QuestSystemCache.Get(sapi);
            }
            catch
            {
                qs = null;
            }

            int count = playerQuests.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= playerQuests.Count) break;
                var quest = playerQuests[i];
                if (quest == null || string.IsNullOrWhiteSpace(quest.questId)) continue;

                try
                {
                    if (qs?.QuestRegistry == null) continue;
                    if (!qs.QuestRegistry.TryGetValue(quest.questId, out var questDef) || questDef == null) continue;
                    if (questDef.blockPlaceObjectives == null || questDef.blockPlaceObjectives.Count == 0) continue;
                }
                catch
                {
                }

                quest.OnBlockPlaced(blockCode, position, byPlayer);
            }
        }

        private void OnQuestTick(float dt)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                OnQuestTickInternal(dt);
            }
            finally
            {
                sw.Stop();
                if (sw.ElapsedMilliseconds > 10)
                {
                    sapi.Logger.Warning("[QuestLagDebug] OnQuestTick took {0}ms for {1} players", sw.ElapsedMilliseconds, sapi.World.AllOnlinePlayers.Length);
                }
            }
        }

        private void OnQuestTickInternal(float dt)
        {
            questSystem ??= QuestSystemCache.Get(sapi);
            if (questSystem == null) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double missingLogThrottle = 1.0 / 60.0;
            double passiveThrottle = 1.0 / 3600.0;
            try
            {
                var cfg = questSystem.CoreConfig?.QuestTick;
                if (cfg != null)
                {
                    if (cfg.MissingQuestLogThrottleHours > 0) missingLogThrottle = cfg.MissingQuestLogThrottleHours;
                    if (cfg.PassiveCompletionThrottleHours > 0) passiveThrottle = cfg.PassiveCompletionThrottleHours;
                }
            }
            catch
            {
                missingLogThrottle = 1.0 / 60.0;
                passiveThrottle = 1.0 / 3600.0;
            }

            QuestTickUtil.HandleQuestTick(dt, questRegistry, questSystem.ActionObjectiveRegistry, players, persistenceManager.GetPlayerQuests, uid => persistenceManager.MarkDirty(uid), sapi, missingLogThrottle, passiveThrottle);
        }

        public void HandleVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message)
        {
            if (player == null || message == null)
            {
                return;
            }

            if (message.BlockCode == "alegacyvsquest:cooldownplaceholder")
            {
                return;
            }

            var playerQuests = persistenceManager.GetPlayerQuests(player.PlayerUID);
            if (playerQuests == null || playerQuests.Count == 0) return;

            QuestSystem qs = questSystem ??= QuestSystemCache.Get(sapi);
            int[] position = new int[] { message.Position.X, message.Position.Y, message.Position.Z };

            for (int i = 0; i < playerQuests.Count; i++)
            {
                var activeQuest = playerQuests[i];
                if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;

                if (qs?.QuestRegistry == null || !qs.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef) || questDef == null) continue;

                // Проверка: нужен ли этому квесту ивент взаимодействия
                bool needsInteract = (questDef.interactObjectives != null && questDef.interactObjectives.Count > 0);
                if (!needsInteract && questDef.actionObjectives != null)
                {
                    for (int ao = 0; ao < questDef.actionObjectives.Count; ao++)
                    {
                        var a = questDef.actionObjectives[ao];
                        if (a != null && (a.id == "interactat" || a.id == "interactcount"))
                        {
                            needsInteract = true;
                            break;
                        }
                    }
                }

                if (needsInteract)
                {
                    activeQuest.OnBlockUsed(message.BlockCode, position, player, sapi);
                }
            }
        }
    }
}
