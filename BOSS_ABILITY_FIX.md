# Boss Ability Fix Analysis

## Problem
After the v3.0 refactor (commits f134205 through 54e4beb), most boss abilities stopped working even though they show "ACTIVATING" in the logs.

## Root Cause
The refactor introduced `BossAbilityBase` as a base class for all boss abilities, which changed how abilities execute:

### Old System (Before Refactor - commit b203ba5)
- Each ability was standalone, extending `EntityBehavior` directly
- Abilities used `RegisterGameTickListener` for continuous effects (like dash movement)
- Tick listeners ran at fixed intervals (e.g., every 50ms)
- Direct control over tick registration and unregistration

### New System (After Refactor)
- All abilities extend `BossAbilityBase`
- Base class provides unified lifecycle: `OnGameTick` → `CheckAbility` → `ActivateAbility` → `OnAbilityTick`
- Uses `abilityActive` flag to control when `OnAbilityTick` is called
- **PROBLEM**: Some abilities lost their `RegisterGameTickListener` calls during refactoring

## What Works vs What Doesn't

### ✅ Working Abilities
- **Marks** (BossStillnessMark, BossWoundedMark): Work because they apply effects immediately in `ActivateAbility` and use `OnAbilityTick` for state maintenance
- **Summon Rituals** (BossSummonRitual, BossPeriodicSpawn): Work because they spawn entities immediately in `ActivateAbility`
- **BossRespawn**: Works (separate system)
- **BossGrowthRitual**: Partially works (animation/sound/lightning work, but scaling doesn't)

### ❌ Broken Abilities
- **BossDash**: Movement doesn't work - **FIXED** by restoring `RegisterGameTickListener` in `BeginDash`
- **BossTeleport**: May not work - needs investigation
- **BossPushback**: Should work (immediate effect in `ActivateAbility`)
- **BossLifeDrainNova**: Should work (calls `PerformNova` in `ActivateAbility`)
- **BossAshFloor**: Needs investigation
- **BossSurroundedResponse**: Needs investigation
- **BossRequiemChains**: Needs investigation
- **BossCorpseExplosion**: Needs investigation

## Fix Applied

### EntityBehaviorBossDash.cs
**Issue**: The refactored version relied solely on `OnAbilityTick` for movement updates, but this wasn't providing smooth continuous movement like the old tick listener did.

**Solution**: Restored the `RegisterGameTickListener` call in `BeginDash` method to match the original implementation. This ensures the dash movement is updated every 50ms for smooth motion.

```csharp
private void BeginDash(DashStage stage)
{
    if (entity == null || stage == null) return;

    TryPlayAnimation(stage.dashAnimation);

    // Register game tick listener for continuous movement updates
    dashTickListenerId = Sapi.Event.RegisterGameTickListener(_ =>
    {
        try
        {
            if (!IsAbilityActive)
            {
                StopDash();
                return;
            }

            long now = Sapi.World.ElapsedMilliseconds;
            if (now >= dashEndsAtMs)
            {
                StopDash();
                return;
            }

            // ... movement logic ...
        }
        catch (Exception ex)
        {
            entity?.Api?.Logger?.Error($"[vsquest] Exception in dash tick: {ex}");
        }
    }, 50);
}
```

## Testing Instructions

1. **Deploy the fixed dll** to your server
2. **Test each boss ability**:
   - **Ashen Lurker**: Test dash, teleport, summon ritual, ash floor, pushback, marks, life drain nova
   - **Bone Colossus**: Test growth ritual (check if scaling works), summon ritual, marks, corpse explosion
   - **Ossuary Warden**: Test requiem chains, grab, marks, rebirth
   - **Ossuary Warden Reborn**: Test dash, teleport, marks

3. **Check server logs** for:
   - "ACTIVATING" messages (confirms ability is triggering)
   - Error messages during ability execution
   - Movement updates for dash abilities

4. **Verify in-game**:
   - Does the boss actually dash towards you?
   - Does teleport move the boss?
   - Does pushback knock players back?
   - Does life drain nova damage players and heal boss?

## Next Steps

If abilities are still broken after this fix, we need to:

1. **Add debug logging** to `OnAbilityTick` in each ability to confirm it's being called
2. **Compare old vs new implementations** for each broken ability
3. **Check if abilities need `RegisterGameTickListener`** restored like BossDash
4. **Verify `ActivateAbility` is doing the actual work** (not just setting state)

## Key Pattern for Fixing Abilities

Abilities fall into two categories:

### Category 1: Immediate Effect Abilities
These execute their main effect in `ActivateAbility`:
- Pushback
- Summon Ritual
- Teleport (delayed via callback)
- Life Drain Nova

**Pattern**: Should work if `ActivateAbility` calls the effect method

### Category 2: Continuous Effect Abilities  
These need ongoing updates during the ability duration:
- Dash (movement updates)
- Ash Floor (block placement over time?)
- Requiem Chains (continuous effect?)

**Pattern**: May need `RegisterGameTickListener` restored

## Growth Ritual Scaling Issue

The user reported: "bonecolossus фаза 2 босс гроу ритуал, но босс не увеличивается в 2 раза остальные эффекты гроу ритуала работают"

This is a separate issue - the growth ritual plays animation/sound/lightning but doesn't scale the boss. This needs investigation in `EntityBehaviorBossGrowthRitual.cs` to see if the scaling logic was broken during refactor.
