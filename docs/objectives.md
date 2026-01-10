# VSQuest Objectives

> **Documentation Version:** v1.1.0

---

## What are Objectives?

**Objectives** are conditions that the player must complete to finish a quest. They track player progress and determine when a quest can be turned in.

> [!IMPORTANT]
> **Objectives** are different from **Actions**:
> - **Objectives** = Conditions the player must *complete* (kill X enemies, walk Y distance)
> - **Actions** = Things that *happen* (give item, play sound, spawn entity)

---

## When Objectives Are Checked

Objectives are defined in the `actionObjectives` array within a quest JSON. They are continuously checked while the quest is active.

```json
{
  "actionObjectives": [
    {
      "id": "objectiveId",
      "args": ["arg1", "arg2"]
    }
  ]
}
```

---

## Objective Format

```json
{
  "id": "objectiveId",
  "args": ["arg1", "arg2", "arg3"]
}
```

- `id` — The objective identifier (see list below)
- `args` — Array of string arguments passed to the objective

---

## All Available Objectives

### `walkdistance`

Requires the player to walk a certain distance in meters.

**Arguments:**
- `<questId>` — Quest ID for tracking (required)
- `<meters>` — Distance in meters to walk (required)
- `[slot]` — Objective slot for multiple walk objectives (optional)

> [!NOTE]
> Use `resetwalkdistance` action in `onAcceptedActions` to reset distance tracking when quest starts.

---

### `randomkill`

Requires the player to kill randomly generated targets. Used with the `randomkill` action which sets up the kill objectives.

**Arguments:**
- `<questId>` — Quest ID for tracking (required)
- `<slot>` — Which random kill slot to check (required)

The `randomkill` action in `onAcceptedActions` generates the kill targets. Each slot corresponds to a randomly selected mob to hunt.

---

### `checkvariable`

Checks if a player attribute meets a condition. Can trigger actions when the condition is met.

**Arguments:**
- `<varName>` — Player attribute key to check (required)
- `<operator>` — Comparison operator: `=`, `==`, `>`, `>=`, `<`, `<=`, `!=` (required)
- `<value>` — Value to compare against (required)
- `[actionsOnComplete]` — Action string to execute when condition is met (optional)

---

### `timeofday`

Requires a specific time of day to complete.

**Arguments:**
- `<mode>` — Time mode (required):
  - `day` — 06:00 to 18:00
  - `night` — 18:00 to 06:00
  - `startHour,endHour` — Custom range (e.g., `8,16`)

---

### `interactat`

Requires the player to interact with blocks at specific coordinates.

**Arguments:**
- `<coord1>` — First coordinate string (required)
- `[coord2...]` — Additional coordinates, all must be interacted with

Use `markinteraction` action to mark a coordinate as completed.

---

### `interactcount`

Counts interactions at multiple coordinates. Similar to `interactat` but shows progress.

**Arguments:**
- `<coord1>` — First coordinate string (required)
- `[coord2...]` — Additional coordinates
- `<displayKey>` — Language key for display text (last argument)

---

### `nearbyflowers`

Requires a minimum number of flowers near the player.

**Arguments:**
- `<count>` — Minimum flowers required within 15 blocks (required)

---

### `playerhasattribute`

Checks if the player has a specific attribute with a specific value.

**Arguments:**
- `<key>` — Attribute key (required)
- `<value>` — Expected value (required)
