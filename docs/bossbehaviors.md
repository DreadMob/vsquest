# Boss Behaviors (Entity Behaviors)

> **Documentation Version:** v3.0.0

---

## Overview

Boss behaviors are `EntityBehavior` components attached to boss entities via their entity JSON definition. They provide combat abilities, defensive mechanics, phase transitions, and utility functions.

**All behaviors follow a common pattern:**
- Configured via `attributes` in the entity JSON
- Use **stages** triggered at health thresholds (`whenHealthRelBelow`)
- Support cooldowns, animations, and sounds

---

## Common Properties

Most boss behaviors share these configuration patterns:

| Property | Type | Description |
|----------|------|-------------|
| `whenHealthRelBelow` | float | Health fraction threshold (0.0-1.0) to activate this stage |
| `cooldownSeconds` | float | Cooldown between ability uses |
| `windupMs` | int | Windup delay in milliseconds before ability executes |
| `animation` | string | Animation to play during ability |
| `sound` | string | Sound asset path (without `sounds/` prefix) |
| `soundRange` | float | Sound audible range (default: 24) |
| `soundStartMs` | int | Delay before sound plays |
| `soundVolume` | float | Sound volume multiplier (default: 1.0) |
| `loopSound` | string | Looping sound during ability |
| `loopSoundIntervalMs` | int | Interval for loop sound (default: 900ms) |

---

## Combat Abilities

### `bossdash` — EntityBehaviorBossDash

Boss performs a quick dash towards or away from the target.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `minTargetRange` | float | 0 | Minimum distance to target |
| `maxTargetRange` | float | 30 | Maximum distance to target |
| `windupMs` | int | 350 | Windup before dash |
| `dashMs` | int | 650 | Dash duration |
| `dashSpeed` | float | 0.18 | Speed multiplier |
| `dashDirection` | string | "towards" | Direction: `towards`, `away`, `left`, `right`, `side` |
| `windupAnimation` | string | null | Animation during windup |
| `dashAnimation` | string | null | Animation during dash |

**Example:**
```json
{
  "code": "myboss",
  "behaviors": [
    {
      "code": "bossdash",
      "attributes": {
        "stages": [
          {
            "whenHealthRelBelow": 1.0,
            "cooldownSeconds": 8,
            "minTargetRange": 5,
            "maxTargetRange": 25,
            "windupMs": 400,
            "dashMs": 500,
            "dashSpeed": 0.22,
            "dashDirection": "towards",
            "sound": "game:entity/drifter-idle"
          }
        ]
      }
    }
  ]
}
```

---

### `bossteleport` — EntityBehaviorBossTeleport

Boss teleports to a random position around the target.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `minTargetRange` | float | 0 | Minimum distance to target |
| `maxTargetRange` | float | 40 | Maximum distance to target |
| `minRadius` | float | 3 | Minimum teleport radius from target |
| `maxRadius` | float | 7 | Maximum teleport radius from target |
| `tries` | int | 10 | Number of attempts to find valid position |
| `windupMs` | int | 250 | Windup before teleport |
| `windupAnimation` | string | null | Animation before teleport |
| `arriveAnimation` | string | null | Animation after teleport |
| `requireSolidGround` | bool | true | Require solid ground at destination |
| `teleportClones` | bool | false | Also teleport clones |
| `swapWithClones` | bool | false | Swap position with a clone |

---

### `bossgrab` — EntityBehaviorBossGrab

Boss grabs a player and holds them, dealing periodic damage.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `minTargetRange` | float | 0 | Minimum distance to target |
| `maxTargetRange` | float | 30 | Maximum distance to target |
| `windupMs` | int | 500 | Windup before grab |
| `grabMs` | int | 3000 | Duration of grab |
| `victimWalkSpeedMult` | float | 0.1 | Victim's movement speed during grab |
| `damageIntervalMs` | int | 500 | Interval between damage ticks |
| `damage` | float | 2 | Damage per tick |
| `damageTier` | int | 0 | Damage tier |
| `damageType` | string | "generic" | Damage type |

---

### `bosshook` — EntityBehaviorBossHook

Boss pulls a target towards itself (like a hook/whip).

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `minTargetRange` | float | 8 | Minimum distance to target |
| `maxTargetRange` | float | 30 | Maximum distance to target |
| `windupMs` | int | 400 | Windup before hook |
| `pullSpeed` | float | 0.8 | Speed of pulling |
| `hookAnimation` | string | null | Animation during hook |

---

### `bosslifedrainnova` — EntityBehaviorBossLifeDrainNova

Boss creates a nova that drains health from nearby players and heals itself.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `radius` | float | 8 | Nova radius |
| `durationMs` | int | 5000 | Duration |
| `drainIntervalMs` | int | 500 | Interval between drains |
| `drainDamage` | float | 3 | Damage per tick |
| `healMultiplier` | float | 0.5 | Heal = damage × this |

---

### `bossparasiteleech` — EntityBehaviorBossParasiteLeech

Boss attaches parasites to players that drain health over time.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `maxParasites` | int | 3 | Maximum parasites per player |
| `parasiteDurationMs` | int | 8000 | Duration per parasite |
| `damageIntervalMs` | int | 1000 | Damage interval |
| `damagePerParasite` | float | 1 | Damage per parasite per tick |

---

### `bosspushback` — EntityBehaviorBossPushback

Boss creates a shockwave that pushes players away.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `radius` | float | 10 | Pushback radius |
| `force` | float | 8 | Push force |
| `windupMs` | int | 300 | Windup |

---

### `bossrandomlightning` — EntityBehaviorBossRandomLightning

Boss summons lightning strikes on random nearby positions.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `radius` | float | 15 | Strike radius |
| `strikeCount` | int | 3 | Number of strikes |
| `strikeIntervalMs` | int | 500 | Interval between strikes |
| `damage` | float | 8 | Damage per strike |

---

### `bossrepulsestun` — EntityBehaviorBossRepulseStun

Boss stuns and repulses nearby players.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `radius` | float | 12 | Effect radius |
| `stunDurationMs` | int | 2000 | Stun duration |
| `pushForce` | float | 10 | Push force |
| `windupMs` | int | 600 | Windup |

---

### `bossstillnessmark` — EntityBehaviorBossStillnessMark

Boss marks a player; if they move, they take damage.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `markDurationMs` | int | 4000 | Mark duration |
| `damageIfMoved` | float | 15 | Damage if player moves |
| `radius` | float | 20 | Target search range |

---

### `bosssurroundedresponse` — EntityBehaviorBossSurroundedResponse

Boss triggers an ability when surrounded by multiple players.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `surroundRadius` | float | 10 | Detection radius |
| `minPlayers` | int | 3 | Minimum players to trigger |
| `responseType` | string | "pushback" | Response type |

---

### `bosstrapclone` — EntityBehaviorBossTrapClone

Boss creates fake clones that explode when attacked.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `cloneCount` | int | 3 | Number of clones |
| `cloneDurationMs` | int | 10000 | Clone lifetime |
| `explosionDamage` | float | 10 | Damage when clone is destroyed |
| `explosionRadius` | float | 5 | Explosion radius |

---

### `bosswoundedmark` — EntityBehaviorBossWoundedMark

Boss marks players who deal high damage, making them take extra damage.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `damageThreshold` | float | 50 | Damage threshold to mark player |
| `markDurationMs` | int | 8000 | Mark duration |
| `extraDamagePercent` | float | 30 | Extra damage percent |

---

### `bossfaketeleport` — EntityBehaviorBossFakeTeleport

Boss creates a fake teleport illusion while actually staying in place.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `illusionDurationMs` | int | 3000 | Illusion duration |
| `windupMs` | int | 400 | Windup |

---

### `explodeondeath` — EntityBehaviorExplodeOnDeath

Boss explodes upon death after a fuse delay. Plays ticking sounds during the fuse.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `fuseMs` | int | 2000 | Fuse time before explosion |
| `explosionRadius` | float | 3 | Explosion radius |
| `explosionDamage` | float | 10 | Explosion damage |
| `damageTier` | int | 1 | Damage tier |
| `damageType` | int | 2 | Damage type (EnumDamageType, default PiercingAttack) |
| `explodeSound` | string | "effect/smallexplosion" | Explosion sound |
| `explodeSoundVolume` | float | 0.5 | Explosion sound volume |

---

### `bossashfloor` — EntityBehaviorBossAshFloor

Boss creates a damaging ash floor area.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `radius` | float | 10 | Floor radius |
| `durationMs` | int | 6000 | Duration |
| `damageIntervalMs` | int | 500 | Damage interval |
| `damage` | float | 2 | Damage per tick |

---

## Defense Abilities

### `bossdamageshield` — EntityBehaviorBossDamageShield

Boss activates a shield reducing incoming damage.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `shieldMs` | int | 2500 | Shield duration |
| `windupMs` | int | 0 | Windup |
| `cooldownSeconds` | float | 0 | Cooldown |
| `repeatable` | bool | false | Can repeat while conditions met |
| `immobileDuringShield` | bool | false | Boss cannot move during shield |
| `lockYawDuringShield` | bool | false | Lock rotation during shield |
| `incomingDamageMultiplier` | float | 0.5 | Damage multiplier during shield |

---

### `bossdespair` — EntityBehaviorBossDespair

Boss applies a despair debuff reducing player effectiveness.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `radius` | float | 15 | Effect radius |
| `durationMs` | int | 5000 | Debuff duration |
| `walkSpeedMult` | float | 0.7 | Walk speed multiplier |

---

### `bossintoxicationaura` — EntityBehaviorBossIntoxicationAura

Boss creates an aura that intoxicates players.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `radius` | float | 10 | Aura radius |
| `damagePerSecond` | float | 1 | Damage per second |

---

### `bossoxygendrainaura` — EntityBehaviorBossOxygenDrainAura

Boss drains oxygen from nearby players.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `radius` | float | 12 | Aura radius |
| `drainPerSecond` | float | 5 | Oxygen drain per second |

---

### `bossdamageinvulnerability` — EntityBehaviorBossDamageInvulnerability

Boss becomes invulnerable to damage for a duration.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `invulnerabilityMs` | int | 2000 | Duration |
| `cooldownSeconds` | float | 0 | Cooldown |

---

### `bossdynamicsscaling` — EntityBehaviorBossDynamicScaling

**DISABLED** - Boss HP scales with nearby player count.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `baseHealthMult` | float | 1.6 | Base health multiplier |
| `hpPerPlayer` | float | 0.30 | HP added per player |
| `maxMultiplier` | float | 4.0 | Maximum multiplier |
| `regenPerPlayer` | float | 0.2 | Regen per player per second |
| `playerDetectionRange` | int | 60 | Detection range |

---

### `bossdamagesourceimmunity` — EntityBehaviorBossDamageSourceImmunity

Boss is immune to specific damage sources.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `immuneDamageTypes` | array | [] | List of immune damage types |
| `immuneDamageSources` | array | [] | List of immune damage sources |

---

## Phase Transitions

### `bossrebirth2` — EntityBehaviorBossRebirth2

Boss transforms into another entity upon death. Prevents despawn during transition and notifies BossHuntSystem.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `nextEntityCode` | string | null | Entity code to transform into |
| `isFinalStage` | bool | false | Is this the final phase? |
| `spawnDelayMs` | int | 2000 | Delay before spawning next form |
| `spawnLightning` | bool | true | Spawn lightning effect |
| `sound` | string | null | Sound on death |
| `soundRange` | float | 24 | Sound range |
| `soundStartMs` | int | 0 | Sound delay |
| `spawnSound` | string | null | Sound on spawn |
| `spawnSoundRange` | float | 24 | Spawn sound range |
| `spawnSoundStartMs` | int | 0 | Spawn sound delay |
| `loopSound` | string | null | Looping sound during transition |
| `loopSoundRange` | float | 24 | Loop sound range |
| `loopSoundIntervalMs` | int | 900 | Loop sound interval |

**Example (multi-phase boss):**
```json
{
  "code": "boss_phase1",
  "behaviors": [
    {
      "code": "bossrebirth2",
      "attributes": {
        "nextEntityCode": "boss_phase2",
        "isFinalStage": false,
        "spawnDelayMs": 3000,
        "spawnLightning": true,
        "sound": "game:entity/bell-idle"
      }
    }
  ]
}
```

---

### `bossformswap` — EntityBehaviorBossFormSwap

Boss swaps into a different entity form. Copies target and anchor to the new entity.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `alternateEntityCode` | string | null | Entity code to swap to |
| `swapChance` | float | 1.0 | Chance to swap (0-1) |
| `keepHealthFraction` | bool | true | Preserve health percentage |

---

### `bossformswaplist` — EntityBehaviorBossFormSwapList

Boss swaps into one of several predefined forms from stages. Rebinds player clones to new entity.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `entityCode` | string | null | Entity code for this stage |
| `keepHealthFraction` | bool | true | Preserve health percentage |

---

### `bossintermissiondispel` — EntityBehaviorBossIntermissionDispel

Boss enters an intermission phase: takes reduced damage, freezes, and spawns adds and dispel objects. Phase ends when all dispel objects are destroyed or max duration expires.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `intermissionMaxMs` | int | 10000 | Max intermission duration |
| `freezeBoss` | bool | true | Boss is immobile |
| `lockYaw` | bool | false | Lock boss rotation |
| `incomingDamageMultiplier` | float | 0 | Incoming damage multiplier (0 = full invulnerability) |
| `adds` | array | [] | Spawned adds configurations |
| `dispelEntityCode` | string | null | Entity code for dispel objects |
| `dispelCount` | int | 1 | Number of dispel objects to spawn |

---

### `bossplayerclone` — EntityBehaviorBossPlayerClone

Boss creates clones of nearby players. Clones mimic player appearance, inventory and skin. Damage dealt to clones is transferred to the boss.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cloneEntityCode` | string | null | Clone entity code (player copy) |
| `cloneRange` | float | 20 | Range to detect players for cloning |

---

### `bossrespawn` — EntityBehaviorBossRespawn

Boss corpse remains and respawns into a new entity after a set time. Prevents duplicate spawns if another boss already exists nearby.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `respawnInGameHours` | double | 24 | Time before respawn (in-game hours) |
| `spawnNewBoss` | bool | false | Spawn new boss or just despawn corpse |
| `respawnEntityCode` | string | null | Entity code for respawn (defaults to same) |

---

### `bosscastphase` — EntityBehaviorBossCastPhase

Boss enters a casting phase: heals over time and takes reduced damage.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `castMs` | int | 8000 | Cast phase duration |
| `healPerSecond` | float | 0 | Heal per second |
| `healRelPerSecond` | float | 0 | Relative heal per second (fraction) |
| `incomingDamageMultiplier` | float | 0.5 | Incoming damage multiplier |
| `animation` | string | null | Casting animation |
| `loopSound` | string | null | Looping sound during cast |
| `loopSoundRange` | float | 24 | Loop sound range |
| `loopSoundIntervalMs` | int | 900 | Loop sound interval |
| `windupSound` | string | null | Windup sound |
| `windupSoundRange` | float | 24 | Windup sound range |
| `windupSoundStartMs` | int | 0 | Windup sound delay |

---

### `bossmodelswap` — EntityBehaviorBossModelSwap

Boss temporarily changes its visual model (shape and texture) without changing abilities or behaviors. Reverts after duration or on death/despawn.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `swapSound` | string | "albase:dark-magic-charge-up" | Sound on swap |
| `soundRange` | float | 32 | Sound range |
| `forms` | array | [] | List of possible forms |

**Forms array item:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `shape` | string | null | Shape asset path |
| `texture` | string | null | Texture asset path |
| `durationMs` | int | 15000 | Form duration |

---

## Ritual Abilities

### `bosssummonritual` — EntityBehaviorBossSummonRitual

Boss performs a ritual to summon minions.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `entityCode` | string | null | Entity to summon |
| `minCount` | int | 1 | Minimum summons |
| `maxCount` | int | 1 | Maximum summons |
| `maxNearby` | int | 0 | Max nearby before stopping |
| `nearbyRange` | float | 0 | Range to check for nearby |
| `ritualMs` | int | 4000 | Ritual duration |
| `spawnRange` | float | 6 | Spawn range from boss |
| `cooldownSeconds` | float | 0 | Cooldown |
| `healPerSecond` | float | 0 | Heal during ritual |
| `healRelPerSecond` | float | 0 | Relative heal per second |
| `circleRadius` | float | 0 | Circle movement radius |
| `circleMoveSpeed` | float | 0 | Circle movement speed |
| `spawns` | array | [] | Multiple entity types to spawn |

**Spawns array item:**
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `entityCode` | string | null | Entity code |
| `minCount` | int | 1 | Minimum count |
| `maxCount` | int | 1 | Maximum count |
| `chance` | float | 1.0 | Spawn chance |
| `spawnDelayMs` | int | 0 | Delay before spawn |

**Example:**
```json
{
  "code": "necromancer",
  "behaviors": [
    {
      "code": "bosssummonritual",
      "attributes": {
        "stages": [
          {
            "whenHealthRelBelow": 0.7,
            "cooldownSeconds": 30,
            "ritualMs": 5000,
            "spawnRange": 8,
            "maxNearby": 6,
            "nearbyRange": 20,
            "spawns": [
              { "entityCode": "game:drifter-normal", "minCount": 2, "maxCount": 4 },
              { "entityCode": "game:drifter-bow", "minCount": 1, "maxCount": 2 }
            ],
            "sound": "game:effect/intromusic"
          }
        ]
      }
    }
  ]
}
```

---

### `bosscloning` — EntityBehaviorBossCloning

Boss creates permanent clones of itself. Clones scale with boss health stages and respawn if killed while boss lives.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cloneCount` | int | 2 | Number of clones |
| `spawnRange` | float | 8 | Spawn radius |
| `cloneDamageMult` | float | 1.0 | Clone damage multiplier |
| `cloneWalkSpeedMult` | float | 1.0 | Clone walk speed multiplier |
| `cloneInvulnerable` | bool | false | Clones are invulnerable |
| `cloneFollowOwner` | bool | false | Clones follow owner |

---

### `bossgrowthritual` — EntityBehaviorBossGrowthRitual

Boss grows in size, speed and damage. Heals to full health. Visual and audio feedback included.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `sizeMultiplier` | float | 1.5 | Size multiplier |
| `speedMultiplier` | float | 1.0 | Walk speed multiplier |
| `damageMultiplier` | float | 1.3 | Damage multiplier |
| `animation` | string | null | Growth animation |
| `lightningFlash` | bool | false | Lightning flash effect |

---

### `bossperiodicspawn` — EntityBehaviorBossPeriodicSpawn

Boss periodically spawns entities at intervals. Checks for nearby entity caps.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `entityCode` | string | null | Entity code to spawn |
| `minCount` | int | 1 | Min per spawn |
| `maxCount` | int | 1 | Max per spawn |
| `chance` | float | 1.0 | Spawn chance (0-1) |
| `maxNearby` | int | 5 | Max nearby entities |
| `spawnRange` | float | 8 | Spawn radius |
| `requireHasTarget` | bool | false | Only spawn if boss has a target |

---

## Unique Abilities

### `bosscorpseexplosion` — EntityBehaviorBossCorpseExplosion

Boss's corpse explodes after death, damaging and poisoning nearby players. Can also trigger on ability use with a separate cooldown.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `fuseMs` | int | 2000 | Fuse time |
| `explosionRadius` | float | 3 | Explosion radius |
| `explosionDamage` | float | 10 | Explosion damage |
| `damageTier` | int | 1 | Damage tier |
| `damageType` | string | "BluntAttack" | Damage type |
| `explodeSound` | string | "effect/smallexplosion" | Explosion sound |
| `explodeSoundVolume` | float | 0.5 | Sound volume |
| `damage` | float | 15 | Direct damage to players |
| `radius` | float | 4 | Direct damage radius |
| `animation` | string | null | Animation on trigger |
| `sound` | string | null | Trigger sound |
| `soundRange` | float | 24 | Sound range |
| `soundStartMs` | int | 0 | Sound delay |
| `soundVolume` | float | 1.0 | Sound volume |
| `poisonPerSecond` | float | 8 | Poison applied per second |
| `poisonDurationSeconds` | int | 10 | Poison duration |
| `cooldownBetweenExplosionsMs` | int | 2500 | Cooldown between explosions |

---

### `bossrequiemchains` — EntityBehaviorBossRequiemChains

Boss binds random nearby players with chains, pulling them towards the boss and disabling movement abilities. Damages over time.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | int | 10 | Cooldown |
| `range` | float | 8 | Target selection range |
| `maxTargets` | int | 2 | Max chained targets |
| `duration` | int | 5 | Chain duration in seconds |
| `pullSpeed` | float | 0.08 | Pull speed towards boss |
| `damagePerSecond` | float | 5 | Damage per second |

---

### `bossmindcontrol` — EntityBehaviorBossMindControl

Boss takes control of a random nearby player. The controlled player attacks other players with reduced movement speed.

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `cooldownSeconds` | float | 0 | Cooldown |
| `controlDurationSeconds` | float | 5 | Control duration |
| `controlRange` | float | 30 | Target selection range |
| `victimMoveSpeed` | float | 0.04 | Controlled player move speed |
| `attackDamage` | float | 10 | Attack damage per hit |
| `attackDamageTier` | int | 3 | Attack damage tier |
| `particleEffect` | string | null | Particle effect path |
| `controlSound` | string | "albase:dark-magic-charge-up" | Sound on control |

---

## Utility Behaviors

### `bossanticheese` — EntityBehaviorBossAntiCheese

Prevents players from cheesing the boss (exploiting safe spots).

**Stages array:** `stages`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `whenHealthRelBelow` | float | 1.0 | Health threshold |
| `checkIntervalMs` | int | 250 | Check interval |
| `cooldownSeconds` | float | 12 | Cooldown between teleports |
| `searchRange` | float | 40 | Search range for players |
| `farRange` | float | 18 | Distance considered "far" |
| `farSeconds` | float | 2.5 | Seconds before teleport when far |
| `noLosSeconds` | float | 2.0 | Seconds before teleport when no line of sight |
| `minRadius` | float | 3 | Min teleport radius |
| `maxRadius` | float | 7 | Max teleport radius |
| `tries` | int | 10 | Tries to find valid position |
| `requireSolidGround` | bool | true | Require solid ground |

---

### `bosshastargetsync` — EntityBehaviorBossHasTargetSync

Syncs the "has target" state to clients for visual effects.

---

### `bosshealthbaroverride` — EntityBehaviorBossHealthbarOverride

Overrides the boss healthbar display.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `showHealthbar` | bool | true | Show healthbar |
| `displayName` | string | null | Custom display name |

---

### `bosshuntcombatmarker` — EntityBehaviorBossHuntCombatMarker

Marks boss as part of the Boss Hunt system. Tracks attackers and damage.

---

### `bossmusiccontroller` — EntityBehaviorBossMusicController

Plays custom music during boss fight.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `musicTrack` | string | null | Music track path |
| `fadeOutMs` | int | 2000 | Fade out duration |

---

### `bossmusicurlcontroller` — EntityBehaviorBossMusicUrlController

Plays music from URL during boss fight (requires BossMusicServer mod).

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `musicUrl` | string | null | Music URL |
| `volume` | float | 1.0 | Volume |

---

### `bossnametag` — EntityBehaviorBossNameTag

Customizes boss nametag display.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `displayName` | string | null | Custom name |
| `showHealth` | bool | true | Show health in nametag |
| `color` | string | "#FF0000" | Nametag color |

---

### `explosivelocust` — EntityBehaviorExplosiveLocust

Entity acts as an explosive locust that seeks targets.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `seekRange` | float | 20 | Seek range |
| `explosionRadius` | float | 3 | Explosion radius |
| `damage` | float | 10 | Damage |

---

## BossBehaviorUtils Helper Class

Static utility class at `VsQuest.BossBehaviorUtils` with common functions:

| Method | Description |
|--------|-------------|
| `ShouldPlaySoundLimited(key, cooldownMs)` | Rate-limits sound playback |
| `SetWatchedBoolDirty(entity, key, value)` | Sets watched attribute and marks dirty |
| `TryGetHealthFraction(entity, out fraction)` | Gets health as fraction (0-1) |
| `TryGetHealth(entity, out tree, out current, out max)` | Gets detailed health info |
| `StopAiAndFreeze(entity)` | Stops AI and freezes movement |
| `ApplyRotationLock(entity, ref yawLocked, ref lockedYaw)` | Locks boss rotation |
| `IsCooldownReady(sapi, entity, key, cooldownSeconds)` | Checks if cooldown is ready |
| `MarkCooldownStart(sapi, entity, key)` | Marks cooldown start time |
| `UpdatePlayerWalkSpeed(player, epsilon)` | Updates walk speed efficiently |

---

## Creating Custom Boss Behaviors

To create a custom boss behavior:

1. Inherit from `EntityBehavior`
2. Override `PropertyName()` to return your behavior code
3. Override `Initialize()` to read attributes
4. Override `OnGameTick(float dt)` for active behaviors
5. Register in entity JSON under `behaviors` array

**Template:**
```csharp
public class EntityBehaviorBossCustom : EntityBehavior
{
    private ICoreServerAPI sapi;
    private float triggerHealthBelow;
    private float cooldownSeconds;
    
    public EntityBehaviorBossCustom(Entity entity) : base(entity) { }
    
    public override string PropertyName() => "bosscustom";
    
    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        sapi = entity?.Api as ICoreServerAPI;
        triggerHealthBelow = attributes["triggerHealthBelow"].AsFloat(0.5f);
        cooldownSeconds = attributes["cooldownSeconds"].AsFloat(10f);
    }
    
    public override void OnGameTick(float dt)
    {
        if (sapi == null || entity == null || !entity.Alive) return;
        // Your logic here
    }
}
```
