using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class DamageSelectedEntityAction : IQuestAction
    {
        public void Execute(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null || byPlayer == null) return;

            var playerEntity = byPlayer.Entity as EntityPlayer;
            var selectedEntity = playerEntity?.EntitySelection?.Entity;
            if (selectedEntity == null)
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, Lang.Get("alegacyvsquest:entity-not-selected"), EnumChatType.Notification);
                return;
            }

            float damage = 50f;
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                if (!float.TryParse(args[0], out damage))
                {
                    damage = 50f;
                }
            }

            // Deal direct damage.
            selectedEntity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Player,
                SourceEntity = byPlayer.Entity,
                Type = EnumDamageType.PiercingAttack,
                DamageTier = 99,
                KnockbackStrength = 0f
            }, damage);

            try
            {
                sapi.SendMessage(byPlayer, GlobalConstants.GeneralChatGroup, Lang.Get("alegacyvsquest:damage-selected-done", damage, selectedEntity.Code), EnumChatType.Notification);
            }
            catch
            {
            }
        }
    }
}
