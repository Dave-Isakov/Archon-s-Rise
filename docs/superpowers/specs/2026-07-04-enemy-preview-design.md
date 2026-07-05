# Enemy Preview — information parity before combat

**Date:** 2026-07-04
**Status:** Design approved; ready for implementation plan.

## Goal

Let the player see *what they are up against before they commit to combat*. Today an enemy's
stats (Attack, HP, Influence cost) only render once combat is engaged — `EnemyCard.Start()`
populates them after the card is instantiated. On the map, and at a guarded place's Assault
button, the player commits blind.

This is the original "information parity / plan your approach" intent that was deferred out of M2
(`m2-deferred-followups` item #3, the "hover enemy preview") and explicitly left out of scope by
the Siege spec (`2026-07-04-siege-attack-type-design.md`, "Aggro auto-combat info-parity ... stays
out of scope — that remains the separately-deferred hover-preview item"). Siege *raised* the value
of this: with Normal / Siege / Influence all available, "what am I up against?" is now a richer,
plan-worthy question, and it must be answerable before you engage.

**This is pure information parity, not feasibility judging.** The preview shows the enemy's stats;
it does **not** tell the player whether their current pools can defeat it.

## Design decisions

- **Preview is gated by a single blind hook.** Visibility of the preview is itself a gameable
  property. A single `PreviewRules.CanPreview(enemy)` predicate is the one chokepoint every preview
  passes through. Today it always returns `true`. Any future source of blindness (an enemy trait, a
  player debuff, map fog) adds its condition *there* and nothing else changes.
  _Why:_ the user wants room for future mechanics to withhold the preview without redesigning it;
  building only the hook + the blind visual (no real cause yet) is the cheapest way to leave that
  room. _Rejected for now:_ a per-enemy `previewable` field and a player-run "Blinded" condition —
  either can be added later behind the same hook; neither is needed today.

- **Blindness hides the whole confrontation, not individual stats.** When any enemy in a preview
  request is not previewable, the panel replaces its *entire* contents with one blind message —
  **"You cannot see the enemy you are about to confront"** (singular) / **"...the enemies you are
  about to confront"** (plural for a guarded place). It never blanks stats on a per-stat or
  per-entry basis: the player learns they can't see who they face, not that one number is redacted.
  The per-enemy `CanPreview` hook is preserved; the panel simply aggregates — *any* blind enemy in
  the set makes the whole panel blind. Today all enemies are previewable, so this never fires.
  _Why:_ the user's intent is "you can't see what you're about to fight," an encounter-level fog,
  not a stat-by-stat redaction.

- **Input-agnostic trigger.** The preview request is decoupled from the input event. Mouse hover
  drives it today; a gamepad focus/select will drive the identical panel at the controller
  milestone (a project goal: game playable by controller). The trigger exposes `Focus()`/`Unfocus()`
  that both `IPointerEnter/Exit` (now) and `ISelect/Deselect` (later) call — the panel and rules
  never learn which input fired.
  _Why:_ controller support is a planned milestone; baking `OnPointerEnter` into the panel would
  force a rewrite later.

- **Multi-enemy from the start.** The panel shows a *list* of enemies: one for a map token, all
  remaining guardians for a guarded place. Guardians are moving toward simultaneous multi-enemy
  fights (superseding the current one-at-a-time assault), so the preview is built multi-enemy now.
  _Why:_ matches the intended future combat shape and gives full commitment info at a guarded place;
  a partly-conquered place previews only the guardians still standing.

- **Read-only, zero side effects.** The preview starts no combat, instantiates no `EnemyCard`,
  raises no events. It is strictly a view over `EnemiesSO` data.

- **Dedicated preview components, not `EnemyCard` reuse.** `EnemyCard.Start()` wires combat
  listeners, logs "entered the battlefield", and drives influence buttons; reusing it read-only
  means fighting its side effects. A small dedicated stack is single-purpose and testable.
  _Rejected:_ reviving the commented-out `EnemyCard.OnPointerEnter/Exit` hover-scale — it only acts
  on cards already in combat, so it cannot cover map tokens or the Assault button.

## Mechanics

### Surfaces
- **Map enemy tokens** — hover (later: focus) a free enemy's token to preview its single enemy.
- **Guarded-place Assault button** — hover/focus to preview all remaining guardians of the place.
- **Not** the in-combat enemy card (stats are already visible there).

### Content (per enemy, mirroring the combat card)
- Name, Attack, HP, Influence cost (when `canInfluence`), reusing the exact sprite-tag text format
  from `EnemyCard.Start()` so parity is literal.
- No enemy **art**: `AllCards` has only `id`/`cardName`/`cardDescription`; there is no art field.
  Leave an unbound art slot in the entry for a future field; do not build or depend on it.

### Blind state
- When any enemy in the request fails `PreviewRules.CanPreview`, the panel drops all stat entries
  and shows a single whole-panel message: "You cannot see the enemy you are about to confront"
  (one enemy) / "...the enemies you are about to confront" (a guarded place). Blindness is
  encounter-level, never per-stat or per-entry. Today no enemy is blind (the predicate always
  returns `true`).

## Architecture (components)

**1. `PreviewRules` — a pure decision function (the blind gate).**
- `static bool CanPreview(EnemiesSO enemy)` — returns `true` today; the single place future
  blindness plugs in. Pure in the sense that matters: deterministic, side-effect-free, no scene
  state — but it may take the `EnemiesSO` type directly for simplicity.
- Lives beside `CombatRules` in `Assets/Scripts/CardPlay/`. Because it touches `EnemiesSO`, it is
  verified in the **in-editor EditMode Test Runner** rather than the `mcs` CLI harness — CLI-only
  testability is explicitly *not* a requirement here (user call). The contract ("one predicate,
  true today, single chokepoint, encounter-level aggregation in the panel") is fixed.

**2. `EnemyPreviewPanel` — MonoBehaviour, input-agnostic view.**
- `void Show(IReadOnlyList<EnemiesSO> enemies, RectTransform anchor)`
- `void Hide()`
- Scene-level singleton (reached like `GameManager.Instance`). `Show` clears its container, then
  checks the whole set against `CanPreview`: if **every** enemy is previewable, it spawns one
  `EnemyPreviewEntry` per enemy; if **any** is blind, it drops the entries and shows the single
  whole-panel blind message (singular/plural by count). Positions itself near `anchor`. Knows
  nothing about mouse vs. gamepad.

**3. `EnemyPreviewEntry` — MonoBehaviour, one enemy's stat block.**
- Renders name / Attack / HP / Influence-cost from an `EnemiesSO`, matching `EnemyCard`'s text
  format. Unbound art slot. Prefab, instantiated 1..N into the panel.

**4. `PreviewTrigger` — MonoBehaviour, the only input-aware piece.**
- `void Focus()` — resolve this source's enemies and call `EnemyPreviewPanel.Show(enemies, anchor)`.
- `void Unfocus()` — call `Hide()`.
- Today implements `IPointerEnterHandler`/`IPointerExitHandler` → `Focus`/`Unfocus`. Later
  `ISelectHandler`/`IDeselectHandler` call the same two methods (controller), with no change to the
  panel or rules.
- Source resolution (a serialized "source kind" or two small subclasses):
  - **token** → `{ EnemyToken.enemy }` (single).
  - **place** → remaining guardians = `TownToken.townSO.guardians` minus
    `ConquestTracker.Instance.DefeatedCount(gridPos)`.

## Data flow

```
Map enemy token hover ─┐
                       ├─► PreviewTrigger.Focus()
Assault button hover ──┘        │  resolves enemies (token → one; place → remaining guardians)
                                ▼
                     EnemyPreviewPanel.Show(enemies, anchor)
                                │  all enemies → PreviewRules.CanPreview?
                                ├─ all true → one EnemyPreviewEntry per enemy
                                └─ any blind → whole-panel "You cannot see the
                                               enemy/enemies you are about to confront"

Unhover / deselect ─► PreviewTrigger.Unfocus() ─► EnemyPreviewPanel.Hide()
```

No combat is started, no `EnemyCard` instantiated, no events raised.

## Implementation surface (touch points)

- `Assets/Scripts/CardPlay/PreviewRules.cs` *(create)* — pure `CanPreview` blind gate.
- `Assets/Tests/EditMode/PreviewRulesTests.cs` *(create)* — EditMode tests for the gate and the
  whole-panel aggregation rule.
- `Assets/Scripts/GameObjectScripts/.../EnemyPreviewPanel.cs` *(create)* — `Show`/`Hide` view.
- `Assets/Scripts/GameObjectScripts/.../EnemyPreviewEntry.cs` *(create)* — one enemy's stat block.
- `Assets/Scripts/GameObjectScripts/.../PreviewTrigger.cs` *(create)* — input adapter + source
  resolution.
- Prefabs/scene *(manual Unity, handed off as steps)* — `EnemyPreviewPanel` object + blind-state
  object on the combat/HUD canvas; `EnemyPreviewEntry` prefab; `PreviewTrigger` on the enemy-token
  prefab and on the Assault button in the town menu. Do not hand-edit YAML.

## Scope boundaries (YAGNI)

- **In:** map-token preview, Assault-button preview, multi-enemy panel, blind hook + blind visual,
  input-agnostic trigger API.
- **Out:** no real blindness *source* (hook returns `true`); no enemy **art** (no field exists —
  slot left, not built); no in-combat-card enrichment; no gamepad *implementation* (API shaped only);
  no feasibility / "can I beat it" hinting.
- **Front-runs but does not build:** simultaneous-guardian combat — the panel is multi-enemy by
  design, but the `GuardianAssault` driver is untouched here.

## Testing

`PreviewRules` touches `EnemiesSO`, so it is verified in the in-editor EditMode Test Runner (not the
`mcs` CLI harness). Unity-coupled pieces are verified by compile + in-editor confirmation, since the
open editor holds the project lock.

- **`PreviewRules` (EditMode):** `CanPreview` returns `true` for a normal enemy today; a
  forced-blind fixture returns `false`. Locks the hook contract for future blindness sources.
- **Whole-panel aggregation (EditMode):** an all-previewable set yields one entry per enemy; a set
  containing any blind enemy yields the single blind message.
- **Remaining-guardian resolution:** all guardians for a fresh place; the tail after N defeats;
  empty when conquered.
- **Unity-coupled (compile + play confirmation):** panel `Show`/`Hide`, trigger `Focus`/`Unfocus`,
  multi-entry layout, anchor positioning, and the play-mode checks below.

Play-mode acceptance:
1. Hover a map enemy token → its stats appear; unhover → they disappear; combat does **not** start.
2. Hover Assault on a 2-guardian Castle → **both** remaining guardians preview.
3. Defeat one guardian, reopen → only the survivor previews.
4. Nothing in preview instantiates an `EnemyCard` or raises a combat event.

## Docs to update on implementation

- `.claude/skills/archons-rise-design/mechanics.md` — note the enemy preview and the future-facing
  blind concept.
- `.claude/skills/archons-rise-roadmap/decisions-log.md` — record the preview decision, that it
  closes `m2-deferred-followups` item #3 (hover preview), and that it picks up the info-parity gap
  the Siege spec left open.
