using Vintagestory.API.Common;

namespace VsQuest
{
    public static class ModClassRegistry
    {
        public static void RegisterAll(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));
            api.RegisterEntityBehaviorClass("questtarget", typeof(EntityBehaviorQuestTarget));
            api.RegisterEntityBehaviorClass("bossnametag", typeof(EntityBehaviorBossNameTag));
            api.RegisterEntityBehaviorClass("bossrespawn", typeof(EntityBehaviorBossRespawn));
            api.RegisterEntityBehaviorClass("bossdespair", typeof(EntityBehaviorBossDespair));
            api.RegisterEntityBehaviorClass("bosshuntcombatmarker", typeof(EntityBehaviorBossHuntCombatMarker));
            api.RegisterEntityBehaviorClass("bosssummonritual", typeof(EntityBehaviorBossSummonRitual));
            api.RegisterEntityBehaviorClass("bossgrowthritual", typeof(EntityBehaviorBossGrowthRitual));
            api.RegisterEntityBehaviorClass("bossrebirth", typeof(EntityBehaviorBossRebirth));
            api.RegisterEntityBehaviorClass("shiverdebug", typeof(EntityBehaviorShiverDebug));

            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));
            api.RegisterItemClass("ItemEntitySpawner", typeof(ItemEntitySpawner));

            api.RegisterBlockClass("BlockCooldownPlaceholder", typeof(BlockCooldownPlaceholder));
            api.RegisterBlockEntityClass("CooldownPlaceholder", typeof(BlockEntityCooldownPlaceholder));

            api.RegisterBlockClass("BlockQuestSpawner", typeof(BlockQuestSpawner));
            api.RegisterBlockEntityClass("QuestSpawner", typeof(BlockEntityQuestSpawner));

            api.RegisterBlockClass("BlockBossHuntAnchor", typeof(BlockBossHuntAnchor));
            api.RegisterBlockEntityClass("BossHuntAnchor", typeof(BlockEntityBossHuntAnchor));
        }
    }
}
