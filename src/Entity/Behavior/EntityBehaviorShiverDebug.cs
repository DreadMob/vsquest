using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorShiverDebug : EntityBehavior
    {
        private const string AnimIndexKey = "alegacyvsquest:shiverdebug:index";

        private List<string> cachedAnimations;

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

        private long keepAliveListenerId;
        private string currentAnim;

        public EntityBehaviorShiverDebug(Entity entity) : base(entity)
        {
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode != EnumInteractMode.Interact) return;
            if (entity?.Api is not ICoreServerAPI sapi) return;
            if (byEntity is not EntityPlayer player) return;

            string nextAnim = NextAnimation();
            StartForcedAnimation(nextAnim);

            sapi.SendMessage(player.Player as IServerPlayer, GlobalConstants.GeneralChatGroup, Lang.Get("alegacyvsquest:shiverdebug-current", nextAnim), EnumChatType.Notification);
            handled = EnumHandling.Handled;
        }

        public override void OnEntityDespawn(EntityDespawnData despawnData)
        {
            StopKeepAlive();
            base.OnEntityDespawn(despawnData);
        }

        public override string PropertyName() => "shiverdebug";

        private string NextAnimation()
        {
            var animations = GetAnimations();

            int index = entity.WatchedAttributes.GetInt(AnimIndexKey, 0);
            if (index < 0) index = 0;
            string anim = animations[index % animations.Count];
            entity.WatchedAttributes.SetInt(AnimIndexKey, index + 1);
            entity.WatchedAttributes.MarkPathDirty(AnimIndexKey);
            return anim;
        }

        private List<string> GetAnimations()
        {
            if (cachedAnimations != null && cachedAnimations.Count > 0)
            {
                return cachedAnimations;
            }

            var list = new List<string>();
            var animations = entity?.Properties?.Client?.AnimationsByMetaCode;
            if (animations != null)
            {
                foreach (var kvp in animations)
                {
                    var code = kvp.Key;
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    list.Add(code);
                }
            }

            if (list.Count == 0)
            {
                cachedAnimations = ShiverAnimations;
                return cachedAnimations;
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
            cachedAnimations = list;
            return cachedAnimations;
        }

        private void StartForcedAnimation(string animCode)
        {
            if (entity?.AnimManager == null) return;

            StopKeepAlive();

            currentAnim = animCode;
            entity.AnimManager.StopAllAnimations();
            var meta = ResolveAnimationMeta(animCode);
            if (meta != null)
            {
                entity.AnimManager.StartAnimation(meta);
            }
            else
            {
                entity.AnimManager.StartAnimation(animCode);
            }

            keepAliveListenerId = entity.Api.Event.RegisterGameTickListener(_ =>
            {
                if (entity?.AnimManager == null)
                {
                    StopKeepAlive();
                    return;
                }

                if (!string.Equals(currentAnim, animCode, StringComparison.OrdinalIgnoreCase)) return;

                if (!entity.AnimManager.IsAnimationActive(animCode))
                {
                    var keepMeta = ResolveAnimationMeta(animCode);
                    if (keepMeta != null)
                    {
                        entity.AnimManager.StartAnimation(keepMeta);
                    }
                    else
                    {
                        entity.AnimManager.StartAnimation(animCode);
                    }
                }
            }, 500);
        }

        private AnimationMetaData ResolveAnimationMeta(string animCode)
        {
            var animations = entity?.Properties?.Client?.AnimationsByMetaCode;
            if (animations == null) return null;

            if (animations.TryGetValue(animCode, out var meta) && meta != null)
            {
                return meta.Clone();
            }

            return null;
        }

        private void StopKeepAlive()
        {
            if (keepAliveListenerId != 0)
            {
                entity.Api.Event.UnregisterGameTickListener(keepAliveListenerId);
                keepAliveListenerId = 0;
            }
        }
    }
}
