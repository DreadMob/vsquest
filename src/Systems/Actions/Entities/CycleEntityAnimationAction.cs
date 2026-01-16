using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class CycleEntityAnimationAction : IQuestAction
    {
        private const string DefaultForcedAnimCode = "alegacyvsquest:forcedanim";
        private const string AnimIndexKey = "alegacyvsquest:cycleanim:index";

        private static readonly Dictionary<long, long> ForcedAnimListeners = new Dictionary<long, long>();
        private static readonly Dictionary<long, string> ForcedAnimCodes = new Dictionary<long, string>();

        private static readonly List<string> ShiverAnimations = new List<string>
        {
            "hurt",
            "run",
            "walk",
            "swipe",
            "bite",
            "die",
            "idle",
            "despair",
            "headbang",
            "mouth-open1",
            "mouth-open2",
            "mouth-open3",
            "mouth-idle1",
            "mouth-idle2",
            "mouth-idle3",
            "mouth-attack1",
            "mouth-attack2",
            "mouth-attack3",
            "mouth-close1",
            "mouth-close2",
            "mouth-close3",
            "stroke-start",
            "stroke-idle",
            "stroke-end"
        };

        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;

            var playerEntity = byPlayer.Entity as EntityPlayer;
            var selectedEntity = playerEntity?.EntitySelection?.Entity;
            if (selectedEntity == null)
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "Не выбрана энтити.", EnumChatType.Notification);
                return;
            }

            if (!IsAllowedEntity(selectedEntity, args))
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "Эта энтити не подходит для переключения анимаций.", EnumChatType.Notification);
                return;
            }

            var animations = GetAnimations(args);
            if (animations.Count == 0)
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, "Список анимаций пуст.", EnumChatType.Notification);
                return;
            }

            int index = selectedEntity.WatchedAttributes.GetInt(AnimIndexKey, 0);
            if (index < 0) index = 0;
            string nextAnim = animations[index % animations.Count];
            selectedEntity.WatchedAttributes.SetInt(AnimIndexKey, index + 1);
            selectedEntity.WatchedAttributes.MarkPathDirty(AnimIndexKey);

            StartForcedAnimation(selectedEntity, nextAnim);

            sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, $"Анимация: {nextAnim}", EnumChatType.Notification);
        }

        private static bool IsAllowedEntity(Entity entity, string[] args)
        {
            if (entity?.Code == null) return false;

            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0])) return true;

            string desired = args[0].Trim().ToLowerInvariant();
            string fullCode = entity.Code.ToString().Trim().ToLowerInvariant();
            string shortCode = entity.Code.ToShortString().Trim().ToLowerInvariant();

            return desired == fullCode || desired == shortCode;
        }

        private static List<string> GetAnimations(string[] args)
        {
            if (args == null || args.Length <= 1)
            {
                return ShiverAnimations;
            }

            var list = new List<string>();
            for (int i = 1; i < args.Length; i++)
            {
                var anim = args[i]?.Trim();
                if (string.IsNullOrWhiteSpace(anim)) continue;
                list.Add(anim);
            }

            return list.Count > 0 ? list : ShiverAnimations;
        }

        private static void StartForcedAnimation(Entity entity, string animCode)
        {
            if (entity?.Api == null || entity.AnimManager == null) return;

            StopForcedListener(entity);

            var animMeta = BuildForcedMeta(animCode);
            entity.AnimManager.StopAllAnimations();
            entity.AnimManager.StartAnimation(animMeta);

            long listenerId = entity.Api.Event.RegisterGameTickListener(_ =>
            {
                if (entity?.Api == null || entity.AnimManager == null)
                {
                    StopForcedListener(entity);
                    return;
                }

                if (!ForcedAnimCodes.TryGetValue(entity.EntityId, out var forced) || !string.Equals(forced, animCode, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!entity.AnimManager.IsAnimationActive(animCode))
                {
                    entity.AnimManager.StartAnimation(BuildForcedMeta(animCode));
                }
            }, 500);

            ForcedAnimListeners[entity.EntityId] = listenerId;
            ForcedAnimCodes[entity.EntityId] = animCode;
        }

        private static AnimationMetaData BuildForcedMeta(string animCode)
        {
            return new AnimationMetaData
            {
                Code = animCode,
                Animation = animCode,
                BlendMode = EnumAnimationBlendMode.Average,
                Weight = 1f,
                EaseInSpeed = 20f,
                EaseOutSpeed = 20f,
                SupressDefaultAnimation = true
            };
        }

        private static void StopForcedListener(Entity entity)
        {
            if (entity?.Api == null) return;

            if (ForcedAnimListeners.TryGetValue(entity.EntityId, out var listenerId))
            {
                entity.Api.Event.UnregisterGameTickListener(listenerId);
                ForcedAnimListeners.Remove(entity.EntityId);
            }

            ForcedAnimCodes.Remove(entity.EntityId);
        }
    }
}
