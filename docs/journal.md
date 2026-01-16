# Alegacy VS Quest Journal

> **Documentation Version:** v1.2.0

---

## Overview

Alegacy VS Quest includes a custom **Quest Journal** UI that displays journal entries created by quests and actions.

The journal is opened via a hotkey:
- **Default:** `N`

---

## Data Model

Journal entries are stored per-player in `WatchedAttributes` as JSON.

### Entry fields

Each journal entry is represented by `QuestJournalEntry`:
- `QuestId` — group id used by the UI (top-level selector)
- `LoreCode` — unique identifier of the entry
- `Title` — UI title for the entry
- `Chapters` — list of chapter strings

### Storage keys

- `alegacyvsquest:journal:entries`
  - A JSON array of all `QuestJournalEntry` values.

- `alegacyvsquest:journal:<groupId>:lorecodes`
  - A string array of `LoreCode` values that have been written for the group.

---

## UI Behavior

The journal UI (`QuestJournalGui`) shows:
- A **quest/group selector** (based on distinct `QuestId` values found in entries)
- A list of entries for the selected group:
  - Uses **tabs** if the group has up to 12 entries
  - Uses a **dropdown** if the group has more than 12 entries

Ordering:
- When an entry is updated via `addjournalentry`, it is moved to the end of the stored list.
- The UI defaults to selecting the most recently updated entry for the group.

---

## Writing Entries (Quest Actions)

Entries are created/updated via the `addjournalentry` action.

Supported formats:

- New: `addjournalentry <groupId> <loreCode> <title> [overwrite] <chapter1> [chapter2...]`
- Legacy: `addjournalentry <loreCode> <title> <chapter1> [chapter2...]`
- Legacy: `addjournalentry <loreCode> <chapter1> [chapter2...]`

See [Actions](actions.md) for argument details.

---

## Migration from Vanilla Journal

Alegacy VS Quest can import entries from the vanilla `ModJournal` system (server-side), using `QuestJournalMigration.MigrateFromVanilla(...)`.

Notes:
- Only entries whose `LoreCode` starts with `alegacyvsquest` (configurable prefix) are imported.
- Imported chapters are deduplicated.
