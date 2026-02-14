# Remove Journal Migration System

Remove the legacy journal migration system that imported entries from vanilla `ModJournal` to the custom Quest Journal system. This is no longer needed as all active players have already been migrated.

## Files to Modify/Delete

### 1. Delete `src/Systems/Journal/QuestJournalMigration.cs`
- Remove the entire migration file containing `MigrateFromVanilla`, `NormalizeQuestId`, `GetNormalizedCompletedQuestIds`

### 2. Modify `src/Systems/Management/QuestEventHandler.cs`
- Remove call to `QuestJournalMigration.MigrateFromVanilla(sapi, byPlayer)` in `OnPlayerJoin`
- Remove associated try-catch block for migration

### 3. Modify `src/Systems/QuestSystem.cs`
- Remove `NormalizeQuestId` and `GetNormalizedCompletedQuestIds` calls to QuestJournalMigration
- Inline the normalization logic directly in `QuestSystem` or simplify to just use quest IDs as-is
- The `NormalizeQuestId` method should handle the `vsquest:` to `alegacyvsquest:` prefix mapping inline

### 4. Modify `docs/journal.md`
- Remove the "Migration from Vanilla Journal" section

### 5. Keep (for now) `AddVanillaJournalEntryQuestAction.cs`
- This action (`addvanillajournalentry`) may still be used by quests to add entries to vanilla journal
- Only remove if explicitly confirmed by user

## Cleanup Steps
1. Delete `QuestJournalMigration.cs`
2. Update `QuestEventHandler.cs` - remove migration call
3. Update `QuestSystem.cs` - inline normalization logic
4. Update `journal.md` - remove migration docs
5. Update `actions.md` - remove or update `addvanillajournalentry` if needed
