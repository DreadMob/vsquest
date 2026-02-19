using System.Collections.Generic;



namespace VsQuest

{

    public class AlegacyVsQuestConfig

    {

        public bool Debug { get; set; } = false;

        public BossHuntCoreConfig BossHunt { get; set; } = new BossHuntCoreConfig();



		public BossCombatCoreConfig BossCombat { get; set; } = new BossCombatCoreConfig();



		public QuestTickCoreConfig QuestTick { get; set; } = new QuestTickCoreConfig();



		public ActionItemsCoreConfig ActionItems { get; set; } = new ActionItemsCoreConfig();



		public ClientCoreConfig Client { get; set; } = new ClientCoreConfig();



		public HarmonyPatchesCoreConfig HarmonyPatches { get; set; } = new HarmonyPatchesCoreConfig();

		public PerformanceCoreConfig Performance { get; set; } = new PerformanceCoreConfig();

        public class BossHuntCoreConfig

        {

            public bool Debug { get; set; } = false;



            public double SoftResetIdleHours { get; set; } = 1.0;

            public double SoftResetAntiSpamHours { get; set; } = 0.25;

            public double RelocatePostponeHours { get; set; } = 0.25;

            public double BossEntityScanIntervalHours { get; set; } = 1.0 / 60.0;

            public double DebugLogThrottleHours { get; set; } = 0.02;

            public float DefaultActivationRange { get; set; } = 200f;

            public List<string> SkipBossKeys { get; set; } = new List<string>

            {

                "vsquestdebugging:bosshunt:breathbreaker"

            };

        }



		public class BossCombatCoreConfig

		{

			public double BossKillCreditMinShareCeil { get; set; } = 0.5;

			public double BossKillCreditMinShareFloor { get; set; } = 0.08;

			public float BossKillHealFraction { get; set; } = 0.17f;

		}



		public class QuestTickCoreConfig

		{

			public double MissingQuestLogThrottleHours { get; set; } = 1.0 / 60.0;

			public double PassiveCompletionThrottleHours { get; set; } = 1.0 / 3600.0;

		}



		public class ActionItemsCoreConfig

		{

			public int HotbarEnforcerMaxSlotsPerTick { get; set; } = 64;



			public string BossHuntTrackerActionItemId { get; set; } = "albase:bosshunt-tracker";

			public float BossHuntTrackerCastDurationSec { get; set; } = 3f;

			public float BossHuntTrackerCastSlowdown { get; set; } = -0.5f;

			public string BossHuntTrackerCastSpeedStatKey { get; set; } = "alegacyvsquest:actionitemcast";



			public int InventoryScanIntervalMs { get; set; } = 1000;

			public int HotbarEnforceIntervalMs { get; set; } = 500;

		}



		public class ClientCoreConfig

		{

			public BossMusicCoreConfig BossMusic { get; set; } = new BossMusicCoreConfig();

			public ViewDistanceFogCoreConfig ViewDistanceFog { get; set; } = new ViewDistanceFogCoreConfig();

		}



		public class BossMusicCoreConfig

		{

			public float VolumeMul { get; set; } = 0.3f;

			public float DefaultFadeOutSeconds { get; set; } = 2f;

		}



		public class ViewDistanceFogCoreConfig

		{

			public int TickIntervalMs { get; set; } = 100;

			public float BaseDensity { get; set; } = 0.00125f;

			public float FogMinMul { get; set; } = 0.03f;

			public float NegativeFogDensityAddMul { get; set; } = 0.006f;

			public float PositiveFogDensitySubMul { get; set; } = 0.0009f;

		}



		public class HarmonyPatchesCoreConfig
		{

			public bool ItemAttributePatchesEnabled { get; set; } = true;

			public bool PlayerAttributePatchesEnabled { get; set; } = true;

			public ItemAttributePatchesCoreConfig ItemAttribute { get; set; } = new ItemAttributePatchesCoreConfig();

			public PlayerAttributePatchesCoreConfig PlayerAttribute { get; set; } = new PlayerAttributePatchesCoreConfig();

			public bool ActionItemModePatchesEnabled { get; set; } = true;
			public ActionItemModePatchesCoreConfig ActionItemMode { get; set; } = new ActionItemModePatchesCoreConfig();

			public bool ItemMoveActingPlayerContextPatchesEnabled { get; set; } = true;
			public ItemMoveActingPlayerContextPatchesCoreConfig ItemMoveActingPlayerContext { get; set; } = new ItemMoveActingPlayerContextPatchesCoreConfig();

			public bool ItemTooltipPatchesEnabled { get; set; } = true;
			public ItemTooltipPatchesCoreConfig ItemTooltip { get; set; } = new ItemTooltipPatchesCoreConfig();

			public bool QuestItemHotbarOnlyPatchesEnabled { get; set; } = true;
			public QuestItemHotbarOnlyPatchesCoreConfig QuestItemHotbarOnly { get; set; } = new QuestItemHotbarOnlyPatchesCoreConfig();

			public bool QuestItemNoDropOnDeathPatchesEnabled { get; set; } = true;
			public QuestItemNoDropOnDeathPatchesCoreConfig QuestItemNoDropOnDeath { get; set; } = new QuestItemNoDropOnDeathPatchesCoreConfig();

			public bool EntityInfoTextPatchesEnabled { get; set; } = true;
			public EntityInfoTextPatchesCoreConfig EntityInfoText { get; set; } = new EntityInfoTextPatchesCoreConfig();

			public bool EntityInteractPatchesEnabled { get; set; } = true;
			public EntityInteractPatchesCoreConfig EntityInteract { get; set; } = new EntityInteractPatchesCoreConfig();

			public bool EntityPlayerBotInteractPatchesEnabled { get; set; } = true;
			public EntityPlayerBotInteractPatchesCoreConfig EntityPlayerBotInteract { get; set; } = new EntityPlayerBotInteractPatchesCoreConfig();

			public bool EntityPrefixAndCreatureNamePatchesEnabled { get; set; } = true;
			public EntityPrefixAndCreatureNamePatchesCoreConfig EntityPrefixAndCreatureName { get; set; } = new EntityPrefixAndCreatureNamePatchesCoreConfig();

			public bool EntityShiverStrokePatchesEnabled { get; set; } = true;
			public EntityShiverStrokePatchesCoreConfig EntityShiverStroke { get; set; } = new EntityShiverStrokePatchesCoreConfig();

			public bool EntitySoundPitchPatchesEnabled { get; set; } = true;
			public EntitySoundPitchPatchesCoreConfig EntitySoundPitch { get; set; } = new EntitySoundPitchPatchesCoreConfig();

			public bool BlockInteractPatchesEnabled { get; set; } = true;
			public BlockInteractPatchesCoreConfig BlockInteract { get; set; } = new BlockInteractPatchesCoreConfig();

			public bool ServerBlockInteractPatchesEnabled { get; set; } = true;
			public ServerBlockInteractPatchesCoreConfig ServerBlockInteract { get; set; } = new ServerBlockInteractPatchesCoreConfig();

			public bool QuestItemDropBlockPatchesEnabled { get; set; } = true;
			public QuestItemDropBlockPatchesCoreConfig QuestItemDropBlock { get; set; } = new QuestItemDropBlockPatchesCoreConfig();

			public bool QuestItemEquipBlockPatchesEnabled { get; set; } = true;
			public QuestItemEquipBlockPatchesCoreConfig QuestItemEquipBlock { get; set; } = new QuestItemEquipBlockPatchesCoreConfig();

			public bool QuestItemGroundStorageBlockPatchesEnabled { get; set; } = true;
			public QuestItemGroundStorageBlockPatchesCoreConfig QuestItemGroundStorageBlock { get; set; } = new QuestItemGroundStorageBlockPatchesCoreConfig();

			public bool ConversablePatchesEnabled { get; set; } = true;
			public ConversablePatchesCoreConfig Conversable { get; set; } = new ConversablePatchesCoreConfig();

			// Individual patch settings
			public bool Item_InventoryChangeTracking { get; set; } = true;

		}

		public class ItemAttributePatchesCoreConfig
		{
			public bool CollectibleObject_GetHeldItemName { get; set; } = true;
			public bool ModSystemWearableStats_onFootStep { get; set; } = true;
			public bool EntityBehaviorHealth_OnFallToGround { get; set; } = true;
			public bool EntityBehaviorTemporalStabilityAffected_OnGameTick { get; set; } = true;
			public bool CollectibleObject_GetAttackPower { get; set; } = true;
			public bool CollectibleObject_OnHeldAttackStart_AttackSpeed { get; set; } = true;
			public bool ItemWearable_GetWarmth { get; set; } = true;
			public bool CollectibleObject_GetMiningSpeed_MiningSpeedMult { get; set; } = true;
			public bool ModSystemWearableStats_handleDamaged { get; set; } = true;
			public bool ModSystemWearableStats_updateWearableStats { get; set; } = true;
			public bool CollectibleObject_TryMergeStacks_SecondChanceCharge { get; set; } = true;
			public bool ItemWearable_TryMergeStacks_SecondChanceCharge { get; set; } = true;
			public bool ItemWearable_GetMergableQuantity_SecondChanceCharge { get; set; } = true;
		}

		public class PlayerAttributePatchesCoreConfig
		{
			public bool ModSystemWearableStats_handleDamaged_PlayerAttributes { get; set; } = true;
			public bool EntityAgent_OnGameTick_Unified { get; set; } = true;
			public bool EntityBehaviorHealth_OnEntityReceiveDamage_SecondChance { get; set; } = true;
			public bool EntityBehaviorHealth_OnEntityDeath_SecondChanceReset { get; set; } = true;
			public bool EntityAgent_ReceiveDamage_PlayerAttackPower { get; set; } = true;
			public bool EntityBehaviorBodyTemperature_OnGameTick_PlayerWarmth { get; set; } = true;
		}

		public class ActionItemModePatchesCoreConfig
		{
			public bool CollectibleObject_GetToolModes_ActionItemModes { get; set; } = true;
			public bool CollectibleObject_GetToolMode_ActionItemModes { get; set; } = true;
			public bool CollectibleObject_SetToolMode_ActionItemModes { get; set; } = true;
		}

		public class ItemMoveActingPlayerContextPatchesCoreConfig
		{
			public bool InventoryBase_ActivateSlot { get; set; } = true;
			public bool ItemSlot_ActivateSlot { get; set; } = true;
		}

		public class ItemTooltipPatchesCoreConfig
		{
			public bool CollectibleObject_GetHeldItemInfo { get; set; } = true;
			public bool ItemWearable_GetHeldItemInfo { get; set; } = true;
		}

		public class QuestItemHotbarOnlyPatchesCoreConfig
		{
			public bool ItemSlot_CanTakeFrom { get; set; } = true;
			public bool ItemSlot_CanHold { get; set; } = true;
		}

		public class QuestItemNoDropOnDeathPatchesCoreConfig
		{
			public bool PlayerInventoryManager_OnDeath { get; set; } = true;
		}

		public class EntityInfoTextPatchesCoreConfig
		{
			public bool Entity_GetInfoText { get; set; } = true;
			public bool EntityAgent_GetInfoText { get; set; } = true;
		}

		public class EntityInteractPatchesCoreConfig
		{
			public bool EntityBehavior_OnInteract { get; set; } = true;
		}

		public class EntityPlayerBotInteractPatchesCoreConfig
		{
			public bool EntityPlayerBot_OnInteract { get; set; } = true;
		}

		public class EntityPrefixAndCreatureNamePatchesCoreConfig
		{
			public bool Entity_GetPrefixAndCreatureName { get; set; } = true;
		}

		public class EntityShiverStrokePatchesCoreConfig
		{
			public bool EntityShiver_OnGameTick_StrokeFreq { get; set; } = true;
		}

		public class EntitySoundPitchPatchesCoreConfig
		{
			public bool Entity_PlayEntitySound { get; set; } = true;
		}

		public class BlockInteractPatchesCoreConfig
		{
			public bool Block_OnBlockInteractStart { get; set; } = true;
		}

		public class ServerBlockInteractPatchesCoreConfig
		{
			public bool Block_OnBlockInteractStart { get; set; } = true;
		}

		public class QuestItemDropBlockPatchesCoreConfig
		{
			public bool InventoryManager_DropItem { get; set; } = true;
		}

		public class QuestItemEquipBlockPatchesCoreConfig
		{
			public bool ItemSlotCharacter_CanHold { get; set; } = true;
			public bool ItemSlotCharacter_CanTakeFrom { get; set; } = true;
		}

		public class QuestItemGroundStorageBlockPatchesCoreConfig
		{
			public bool CollectibleBehaviorGroundStorable_Interact { get; set; } = true;
		}

		public class ConversablePatchesCoreConfig
		{
			public bool EntityBehaviorConversable_Controller_DialogTriggers { get; set; } = true;
		}

		public class PerformanceCoreConfig
		{
			// Global enable/disable
			public bool EnablePerformanceOptimizations { get; set; } = true;

			// Individual system toggles
			public bool EnableZeroPollEffects { get; set; } = true;
			public bool EnableInventoryFingerprinting { get; set; } = true;
			public bool EnableStatCoalescing { get; set; } = true;

			// ZeroPollEffectSystem settings
			public int EffectCleanupIntervalSeconds { get; set; } = 30;

			// InventoryFingerprintSystem settings
			public int FingerprintCheckIntervalMs { get; set; } = 250;
			public bool SkipFingerprintOnRapidCalls { get; set; } = true;

			// StatCoalescingEngine settings
			public int StatCoalesceWindowMs { get; set; } = 200;
			public int StatMaxDelayMs { get; set; } = 1000;

			/// <summary>
			/// Validates and applies defaults if needed.
			/// </summary>
			public void Validate()
			{
				if (EffectCleanupIntervalSeconds < 5) EffectCleanupIntervalSeconds = 5;
				if (EffectCleanupIntervalSeconds > 300) EffectCleanupIntervalSeconds = 300;

				if (FingerprintCheckIntervalMs < 50) FingerprintCheckIntervalMs = 50;
				if (FingerprintCheckIntervalMs > 2000) FingerprintCheckIntervalMs = 2000;

				if (StatCoalesceWindowMs < 50) StatCoalesceWindowMs = 50;
				if (StatCoalesceWindowMs > 1000) StatCoalesceWindowMs = 1000;

				if (StatMaxDelayMs < 200) StatMaxDelayMs = 200;
				if (StatMaxDelayMs > 5000) StatMaxDelayMs = 5000;
			}
		}

    }

}
