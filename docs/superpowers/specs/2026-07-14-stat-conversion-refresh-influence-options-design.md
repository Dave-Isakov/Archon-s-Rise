# Stat Conversion, Unit Refresh & Influence-Costed Unit Options — Design

**Date:** 2026-07-14
**Status:** Approved (brainstorm 2026-07-14)
**Goal:** Expand tactical strategy around cards, skills, and units — the groundwork for
per-character identity as more player characters are added. Three mechanics, one spec,
implemented in sequence along existing seams (Approach A: additive extensions, no refactor).

## Decisions (locked during brainstorm)

| Question | Decision |
|----------|----------|
| Scope | One spec covering all three mechanics; shared infrastructure (SkillEffect additions, card fields, unit option costs). |
| Conversion rate | Always **1:1** — every point of source stat becomes one point of target stat. |
| Conversion opt-in | **Opt-in at play time** for cards (inspector toggle); skills are inherently opt-in (clicking is the choice). |
| Conversion pools | The **four action stats only** (Attack/Defend/Explore/Influence). Siege is never a source or target (scarcity pillar); Heal/Crystal/Wound never participate. |
| Refresh budget | **Budget across multiple units**: Refresh N deducts each picked unit's `influenceCost` from N until nothing affordable remains. |
| Unit option costs | **One cost type per option** — crystal OR influence OR free; stronger variants are authored as separate option rows. |

---

## 1. Stat Conversion

A card or skill moves the player's banked stat pool(s) into another stat at 1:1.

Example card: **Shield Bash** — Defend 3 / Empower: Defend 5, Convert all Defend → Attack.
Example card: **Rally to the Banner** — convert ALL action stats → Influence.

### Data model
`CardsSO` gains:
- `convertTo` (`StatType`, single flag; `None` = card has no conversion)
- `convertFrom` (`StatType` flags; one stat, or all four action flags for "convert everything")
- `convertRequiresEmpower` (bool; true = conversion only offered on the empowered play,
  matching the Shield Bash authoring)

`SkillsSO` gains `convertFrom` / `convertTo` (same types); `SkillEffect` appends
**`ConvertStat`**.

**Validation (OnValidate):**
- `convertFrom`/`convertTo` may only contain Attack/Defend/Explore/Influence.
- A card cannot set both `isChoice` and a conversion (keeps the inspector readable).
- `convertTo` must not be flagged in `convertFrom` (self-conversion is a no-op). A
  "convert everything" card therefore flags the three action stats *other than* its target
  (e.g. Rally to the Banner: `convertFrom = Attack | Defend | Explore`, `convertTo = Influence`).

### Play flow
- New **ConvertBanner** section in the card inspector — a sibling of `ChoiceBanner`, same
  visual language. Shows e.g. "Convert all Defend → Attack" with an on/off toggle.
- The banner **locks** (dim + reason, ChoiceBanner's improvise-lock pattern) while Improvise
  is active, and when `convertRequiresEmpower` is set but the card is not empowered.
- **Order of operations:** the card's own stats land first, then conversion moves the
  **entire current pool** of each source stat into the target at 1:1. (4 Defend banked +
  empowered Shield Bash → Defend 9 → all 9 become Attack, Defend 0.)
- Skill variant: activating a `ConvertStat` skill converts immediately, then exhausts on its
  cadence like any skill.

### Undo
- The play command snapshots the exact per-stat amounts moved and reverses them on undo.
  Safe because the command stack is strictly LIFO — no other command can touch the pools
  between execute and undo without itself being undone first.
- The sign-flip pattern in `Player.ApplySkillEffect` cannot reverse a conversion; the
  `ConvertStat` effect stores its per-activation moved-amount snapshot (e.g. on the
  token/command) and restores from it on undo.

### Feedback
On convert, pulse both the source and target HUD stat icons via `PlayerIcon.AnimateStat`.

---

## 2. Unit Refresh

A card or skill readies spent (exhausted) units mid-round, budgeted by recruit value.

Example card: **Mobilize** — Refresh 3 / Empower: Refresh 6.

### Data model
- `CardsSO` gains `refresh` / `empowerRefresh` (ints).
- `StatType` appends **`Refresh = 256`** so the card self-describes its effect (an immediate
  effect like Heal/Crystal, not a per-turn pool).
- `SkillEffect` appends **`RefreshUnits`**; `magnitude` is the budget.

### UnitPickerPanel (new, reusable)
Generalized from `DisbandPanel`'s proven shape: own Canvas (toggled, not SetActive'd),
buttons-per-unit, continuation callback, Done/Cancel.
- Opens with a budget; lists **spent units only**, each button showing name + `influenceCost`.
- A unit whose cost exceeds the remaining budget shows **disabled**.
- Picking a unit readies it immediately (stands up) and deducts its cost from the budget.
- Stays open until **Done**, or auto-closes when no affordable spent unit remains.
- Unspent budget is **lost** — no banking across the turn.
- `DisbandPanel` stays as-is; folding disband into the new panel is optional later cleanup.

### Play flow
- Playing a refresh card applies its other stats normally, then opens the picker with
  budget = refresh value (base or empowered).
- If no spent unit is affordable at play time, the picker does not open and the refresh
  **fizzles**. Authoring guidance: pair refresh with a small secondary stat so the card is
  never a fully dead play.
- Skill activation: same picker, budget = `magnitude`.

### Undo
- The picker is **modal** while open (like DisbandPanel) — board and undo unreachable.
- When the picker closes (Done or auto-close), the set of refreshed units is recorded in
  the play/skill command's snapshot; one undo re-exhausts all of them and refunds the
  empower crystal if one was spent.

### Round interaction
Round-end `RefreshUnits()` is unchanged. A unit refreshed mid-round exhausts again as normal
when used.

---

## 3. Influence-Costed Unit Options

A unit option may cost Influence (from the current turn's pool) instead of a crystal.

Example: mercenary authored rows — *Attack 2 (free)*, *Attack 4 (1 red crystal)*,
*Attack 5 (3 Influence)*.

### Data model
- `UnitOption` gains `influenceCost` (int, 0 = free).
- `UnitsSO.OnValidate` warns if an option sets both `crystalCost` and `influenceCost`
  (one cost type per option).

### Pop-out
- The option row renders the influence price alongside where crystal costs already show.
- Affordability mirrors the crystal rule: `playerInfluence < influenceCost` → row is
  **locked** (dimmed, still focusable to read the cost); Use refuses it.

### Spend + undo
- This is an in-turn tactical spend. Unlike town recruiting (`Player.Influence()`, which
  deliberately clears the undo stack), the deduction happens inside
  `ApplyUnitOption` / `RevertUnitOption` symmetrically — fully undoable via the existing
  `UnitCommand`, like crystal reserve/refund.
- Both paths raise the influence broadcast (`GetCurrentInfluence`) so the HUD updates.

### Composition
Refresh + influence options compose: refreshing an influence-costed unit lets it act again
that round at its influence price — Influence becomes the throughput limiter.

---

## 4. Authoring, Balance & Persistence

### Content shipped with the feature (proves each seam)
| Content | Type | Effect |
|---------|------|--------|
| Shield Bash | Card | Defend 3 / emp. Defend 5 + Convert all Defend → Attack (`convertRequiresEmpower`) |
| Rally to the Banner | Card | Convert Attack + Defend + Explore → Influence |
| Mobilize | Card | Refresh 3 / emp. Refresh 6 |
| Tactician | Skill (per-round) | ConvertStat: Defend → Attack |
| Mercenary option row | UnitOption | Stronger attack at an influence cost on an existing unit |

### Balance guidance (add to balance.md)
- Converter cards price their stats slightly under vanilla cards of the same tier —
  flexibility is worth ~1 point.
- Refresh values sit at typical unit recruit costs: base ≈ one cheap unit, empowered ≈ two
  cheap or one elite.
- An influence-costed option's price ≈ the recruit-value of that stat burst.

### Persistence
No save schema bump:
- Stat pools are turn-scoped and never saved.
- Refresh only flips the already-persisted `unitExhausted` state (schema v5).
- New SO fields serialize automatically; new cards/skills register in the existing content
  pools like any asset.

### Testing
- Pure logic in pure classes per the established pattern (e.g. `ConvertRules`,
  `RefreshRules`): conversion eligibility + moved-amount math, refresh budget/affordability
  math. Each gets its own folder asmdef + tests-asmdef reference for EditMode coverage.
- Scene/prefab wiring (ConvertBanner, UnitPickerPanel, option rows) is done manually in the
  editor from step-by-step instructions.

### Design-bible follow-up
When implemented, update `mechanics.md` (Stats, Units, Skills sections) and `balance.md`,
and append the decisions above to `decisions-log.md`.
