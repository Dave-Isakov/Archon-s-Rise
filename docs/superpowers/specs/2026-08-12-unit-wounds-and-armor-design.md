# Unit Wounds and Armor — design

**Date:** 2026-08-12
**Status:** design agreed, not yet planned
**Supersedes parts of:** `2026-07-29-enemy-traits-and-wound-plumbing-design.md` §6.4

---

## 1. The principle

**Unit armor is Defend, bought with a unit wound instead of a card.**

Everything in this spec falls out of that one sentence. A unit's `armorClass` is added to the Defend
pool before the existing counterattack comparison runs, so it composes with Swift's cap, Brutal's
surcharge, Warlord's aura and per-enemy blocking for free — none of that machinery learns that units
exist. The price is not a card from hand but a **wound on the unit**, which disables it until healed.

Two traits are authored exceptions, and they are exceptions precisely because they care *who is
holding the shield*:

- **Leech** reads the **pre-soak** number. The shield does not stop it draining *you*.
- **Toxic** transfers: its discard wounds become **0** and every committed unit takes **2** wounds
  instead of 1. The venom goes into the body that took the hit.

Everything else inherits Defend's behaviour unchanged.

### 1.1 Compatibility guarantee

**With zero units committed, every number this spec produces is identical to today's shipped
behaviour.** `soak` is 0, so pre-soak and post-soak are the same value; the Toxic transfer is gated
on at least one committed unit. This is the same guarantee per-enemy blocking made in the traits
spec, and it means no enemy needs re-tuning to ship this.

---

## 2. Why now, and what this reverses

The 2026-07-29 traits spec deliberately built the seam for this feature and recorded a forward
plan in §6.4. Two of its recorded assumptions are reversed here, and one of its reservations is
retired. Recorded explicitly so nobody re-derives the old plan from the old spec:

| §6.4 said | This spec does | Why |
|---|---|---|
| "Unit armor should be a divisor, not a pool" | Flat soak added to Defend | A divisor makes a Scout's armor unreadable at a glance. Soak means armor and Defend are the same thing, which is the whole design. |
| "Toxic gains a natural second meaning: bypasses unit armor" | Toxic **transfers** — discard wounds → 0, committed units take 2 | "Venom hurts whoever stepped in" is more legible than "venom ignores the unit", and it makes Toxic a heal-*size* threshold rather than just more of the same. |
| `WoundDestination.Unit = 2` reserved | Reservation deleted | Units never receive a wound *card*. The soak happens before the wound math, so no new destination is needed. |

What §6.4 got right and this spec keeps: **unit wounds do not count toward wound-out**
(`PlayerHand.TotalWoundCount` is untouched), which is what makes units a wound sink that trades
run-loss pressure for army capability. And **`UnitPickerPanel` is the right host**, including its
warning about click-off (§4.2).

---

## 3. Unit armor and the soak math

### 3.1 The field

`UnitsSO.armorClass` — `int`, `OnValidate` clamps to ≥ 0. `0` means the unit cannot shield at all
(support units, casters). No new enum, no new SO, no new icon: armor renders with the existing
`shield` glyph, because armor *is* Defend.

### 3.2 Where soak enters

Today's pipeline (`EnemyTraitRules`, spec 2026-07-29 §4.3) is unchanged. `soak` is simply added to
`defendLeft` for the hand-wound path:

```
soak = Σ armorClass over committed units      // 0 when nothing is committed

handWounds     = bite(Effective(p, defendLeft + soak), toughness)     // soaked
discardWounds  = (committed > 0 && anyToxicUnblocked)
                   ? 0                                                // TRANSFERRED
                   : bite(share(p, defendLeft, ToxicContribution), toughness) * toxicCopies
crystalsStolen = bite(share(p, defendLeft, LeechContribution), toughness) * leechCrystals   // PRE-soak
woundsPerUnit  = anyToxicUnblocked ? 2 : 1
```

`anyToxicUnblocked` needs no new field — it is `preview.ToxicContribution > 0`, which is already
computed over unblocked survivors only.

New pure API in `Assets/Scripts/CardPlay/` (existing `ArchonsRise.CardPlay` asmdef, so no asmdef
work and the mcs harness picks it up):

```csharp
public struct CounterattackOutcome
{
    public int HandWounds;
    public int DiscardWounds;
    public int CrystalsStolen;
    public int WoundsPerCommittedUnit;   // applied per committed unit by the caller
}

public static CounterattackOutcome Resolve(
    CounterattackPreview p, int defendLeft, int soak, int toughness,
    EnemyTraitTuning t, int committedUnits);
```

`CombatController` makes one call. `CombatRules.WoundCount`'s bite loop remains the single
implementation of "divide a shortfall into Toughness-sized bites".

### 3.3 Worked examples

Tuning defaults: `toxicCopies = 1`, `leechCrystals = 1`, `brutalSurchargeMult = 1`.

**A — the plain case.** Enemy Attack 5, no traits. Defend 0, Toughness 2, Scout AC 2.

```
no shelter    shortfall 5   capped 5   effective 5   -> 3 wounds
Scout (2)     shortfall 3   capped 3   effective 3   -> 2 wounds   + Scout: 1 wound
```

**B — both exceptions live.** Enemy A Attack 4 Toxic, Enemy B Attack 2 Leech, both unblocked.
Defend 1, Toughness 2, Knight AC 3.

```
preview: threat 6, basis 6, contribution 6 (toxic 4, leech 2)

no shelter    effective 5   hand 3   discard 2   crystals 1
Knight (3)    effective 2   hand 1   discard 0   crystals 1   + Knight: 2 wounds
                                     ^transferred  ^pre-soak, unchanged
```

Hand wounds fall 3 → 1, the discard wounds vanish into the Knight, and Leech takes its crystal
regardless. The Knight is now a heal-2 problem, which a Field Medic (heal 1) cannot solve alone.

**C — soaking to zero kills the surcharge.** Enemy Attack 3, Brutal. Defend 2, Toughness 2,
Knight AC 3.

```
no shelter    shortfall 1   capped 1   + surcharge 3   effective 4   -> 2 wounds
Knight (3)    shortfall -2  -> Effective() short-circuits at <= 0     -> 0 wounds  + Knight: 1 wound
```

Correct by construction: `Effective` returns 0 before the surcharge is added. Brutal punishes
*going unblocked*, and the player did not.

---

## 4. The wound picker

### 4.1 A second mode on `UnitPickerPanel`

```csharp
public void OpenForWounds(CounterattackPreview preview, int defendLeft, int toughness,
                          EnemyTraitTuning tuning, System.Action<IReadOnlyList<Unit>> onConfirm);
```

Entries: every unit where `!IsWounded`. Exhausted units are eligible — taking a hit is not *using*
the unit, so spending its option earlier in the round does not stop it stepping in front of you. One
filter, one sentence to explain, and a unit is never dead weight.

**Selection is non-destructive and toggleable.** Nothing commits until the take-hit row is clicked,
so clicking a picked unit un-picks it. Free stacking with no take-backs would be punishing; the
shrine payment fan already established this pattern.

Unit rows lock (`UiLock`, alpha 0.4) once the shortfall reaches 0 — no pointless picks. The pick that
*crosses* zero still wounds its unit: over-soak is not refunded, exactly as over-spent Defend is not.
Locking the rows after that point is what keeps the waste bounded to a single unit.

```
        <Sword>5  ->  <Sword>3

  [ Scout    <shield>2 ]  <- picked
  [ Knight   <shield>3 ]
  [ Ranger   <shield>0 ]  locked
  [ Take 2 <wound>     ]  <- commit row, always last, always live
```

### 4.2 No click-off — structurally, not by convention

The traits spec flagged this precisely: `UnitPickerPanel.Close()` is public **specifically** so
`ClickOffCatcher` can bind to it, and forfeiting on dismiss is right for Mobilize but catastrophic
here — the player would dismiss their way out of the counterattack.

`Close()` becomes a **dismiss request**. In refresh and heal modes it closes exactly as today; in
wound mode it does nothing but post *"Choose who takes the hit, then confirm."* to `GameLog`. A new
private `CloseInternal()` performs the real teardown and is called by the take-hit row. **No scene
rewiring**, and the hazard is closed by structure rather than by remembering.

Wound mode is the only picker mode with no click-off, because it is the only one the player is not
allowed to escape.

### 4.3 The take-hit row

Not a separate Confirm button — **another row in the list**, rendered by the same entry prefab. The
panel then reads as one homogeneous question ("who takes this hit?") with the player as one of the
answers.

- Label is live: `Take 3 <wound>` → `Take 2 <wound>` → `Take 0 <wound>` as picks land.
- Tinted with `CombatButtons.takeHitColor` (`0.90, 0.20, 0.20`), so the row visually continues the
  red `3 <wound> - use <shield>!!` button the player just pressed.
- **Always last, always live.** Last so a reflexive top-click cannot eat wounds — wounds are the loss
  axis. Always live because it is both the commit and the only exit.
- Clicking it with nothing selected is the no-shelter path, identical to today (§1.1).

Once soak zeroes the shortfall every unit row locks and this is the only live entry, so the panel
funnels to it on its own.

### 4.4 When the picker is skipped

The panel does not open when either is true:

- **No wounds are coming.** `HandWounds == 0 ⟺ Effective == 0`, and the pipeline short-circuits at
  `shortfall <= 0` before Toxic and Leech, so nothing lands at all. Nothing to shelter from.
- **No eligible unit exists** (empty army, or every unit already wounded).

Both fall straight through to today's path, which satisfies wound-plumbing contract rule 7 ("a
placement producer that cannot place must fall back to Hand") by never opening.

### 4.5 `ResolveDefend` becomes two-stage

`CombatController.ResolveDefend` (`CombatController.cs:546`) currently computes and applies in one
synchronous pass. It splits: build the preview, open the picker, apply in the callback. The applying
half keeps everything it does today — `PlayerAvatar.Play(AvatarState.Hurt)`, the Defend-pool
drawdown, `commands.ClearStack()`, the `GameLog` lines, crystal theft, `ClearBlocks()`, and
`SetPhase(CombatPhase.Attack)` — in the same order.

`WoundPlacementRules.Place(handWounds, discardWounds)` is **unchanged** and still produces the
`IReadOnlyList<WoundDestination>` that `PlayerHand.AddWound` consumes. Unit wounds are applied
separately via the unit itself. The stale `Unit = 2` reservation comment in `WoundDestination.cs`
is deleted.

---

## 5. Unit wound state

### 5.1 Model

`Unit.WoundCount` (`int`, 0–2) and `IsWounded => WoundCount > 0`. An int, not a bool, because Toxic
makes 2 a real and mechanically distinct state (§6.2).

### 5.2 Visuals

`ApplyExhaustTint` generalises to `ApplyStateTint`, with wounded taking precedence over exhausted:

```
IsWounded  -> woundedRed           (new serialized field, beside exhaustedGrey)
IsPlayed   -> exhaustedGrey        (today's behaviour)
otherwise  -> unitSO.color
```

A wound decal object on the Unit prefab toggles with `IsWounded`, plus a count label rendered only
when `WoundCount > 1`. `Selectable` gains `&& !IsWounded`. Clicking a wounded unit posts to `GameLog`
("<name> is wounded and cannot act until healed.") rather than opening the inspector — consistent
with the existing already-played message and with the tooltip-and-log direction over popups.

### 5.3 Exhaustion interactions

Three existing readers of `IsPlayed` need the wounded filter, or they misbehave quietly:

- `UnitPickerPanel`'s refresh filter — a wounded unit must not list as refreshable.
- `Player.AnyRefreshable` (`Player.cs:475`) — otherwise Mobilize's fizzle check is wrong and the card
  reports work it cannot do.
- `BarFocusController.RelayoutUnits()` — must fire on wound change exactly as it does on exhaust
  change; its own comment says the selectable mask is driven by `IsPlayed`.

`Player.ReadyUnit` / `ExhaustUnit` (`Player.cs:469-470`) gain a `WoundUnit` / `HealUnit` pair beside
them, so wound state has the same single write path exhaustion does, relayout included.

**Round refresh still clears `IsPlayed` on a wounded unit.** The unit stays unusable via `IsWounded`,
so healing returns it *ready* rather than ready-but-spent.

---

## 6. Healing

### 6.1 The heal picker

```csharp
public void OpenForHeal(int budget, System.Action<HealTarget> onPick);
```

One list, hand wounds and wounded units competing for the same budget. Unspent budget is lost on
close and click-off dismisses — Mobilize semantics exactly, because unlike wound mode there is
nothing here the player must not escape.

```
        <Heal> Heal - 3 left

  [ Wounds in hand      x4   <Heal>1 ]
  [ Scout                    <Heal>1 ]
  [ Knight   venom           <Heal>2 ]
  [ Ranger   -- healthy, not listed  ]
```

The `Wounds in hand` row appears only when the hand actually holds one; a wounded unit's row shows
its `WoundCount` as the cost. Picks apply incrementally and the panel rebuilds, matching
`OpenForRefresh`. (Wound mode batches on
confirm; heal mode applies per pick. The difference tracks escapability: wound mode has no dismiss
and a commit row, heal mode has a dismiss and none.)

### 6.2 Healing a unit is atomic

A unit's row costs its full `WoundCount` and locks when the budget cannot cover it, rather than
accepting a point and leaving the unit down at 1 wound. Two reasons:

1. It is the existing picker idiom — `RefreshRules.CanPick` already means "cost ≤ remaining", and
   entries over budget already show disabled.
2. It stops the player spending a heal point for no state change.

The consequence is the point of Toxic: 2 wounds is a heal **size** threshold, not merely a longer
grind. A per-round Field Medic (heal 1) can never restore a venom-struck unit alone.

### 6.3 Undo by snapshot, not by sign flip

Four call sites route through the picker:

| Call site | Undoable |
|---|---|
| `PlayerHand.Heal(Card)` (`PlayerHand.cs:229`) | yes |
| `Player.ApplyUnitOption` → `UnitEffect.Heal` (`Player.cs:519`) | yes |
| `Player.ApplySkill` → `SkillEffect.HealWound` (`Player.cs:618`) | yes |
| `PlayerHand.TownHeal` (`PlayerHand.cs:242`) | no — a town service commits the action |

The first three currently undo by sign flip: `RestoreHealedWound()` N times. That breaks the moment
the player chooses *where* the healing went. This is the identical problem conversion hit, and
`SkillToken.cs:15-16` already records the fix: "the sign-flip undo pattern can't reverse a
conversion, so the applied amounts live here."

Same shape. Each activation snapshots `{ handWoundsHealed, (unit, woundsRestored)[] }`; undo replays
it exactly — `RestoreHealedWound()` per hand wound, `WoundCount += restored` per unit.

**Fizzle rule carries over from Refresh:** nothing healable anywhere and the heal fizzles. This is no
regression — a Heal card played with a clean hand is already a dead play today.

### 6.4 The town heal guard

**This is a live bug, independent of this feature, and it burns two resources.**

`TownToken.cs:70-76` handles `PlaceActionId.Heal` by raising `healTownEvent`, then
`healInfluenceCostEvent(healLevel)`, then `CommitVisitAction()` — with no precondition.
`PlayerHand.TownHeal` then loops `HealWound()`, which is internally guarded by
`cardsInPlay.Exists(... Wound)` and silently no-ops. `HealButton.cs:10` gates only on
`currentPlayerInfluence < healLevel` — affordability, never necessity.

So with a clean hand the player pays the Influence **and** the turn's action and receives nothing,
with no undo (correctly — a town visit is not undoable).

The fix is two layers over one pure predicate:

```csharp
// Assets/Scripts/CardPlay/HealRules.cs — alongside the existing HealCount
public static int HealableCount(int handWounds, IReadOnlyList<int> unitWoundCounts);
public static bool CanHeal(int handWounds, IReadOnlyList<int> unitWoundCounts);
```

**Layer 1 — the button never lights.** `TownActionSnapshot` already carries `anyUnitAffordable` to
gate Recruit for exactly this reason; Heal gets the parallel `anyWoundToHeal`.
`PlaceActionRules.ForTown` returns the disabled state and the control renders `UiLock`-dimmed at
alpha 0.4 like every other unaffordable control.

**Layer 2 — the commit refuses anyway.** `TownToken`'s `PlaceActionId.Heal` case checks *before
raising anything*, so no route — controller nav, a rebind, a stale click — can spend. Layer 2 makes
the guarantee; layer 1 only lets the player see it coming.

Feedback goes through `GameLog.Instance.Post`, the channel every other refusal uses ("You've already
taken your action this turn."). Copy: **"You don't require healing — nobody is wounded."**

This feature makes the guard *correct* rather than merely safe: `anyWoundToHeal` counts hand wounds
**and** wounded units, so a player with a clean hand and a downed Knight still gets the service.
Castle heal is the same code path and is covered free.

Implementation note: `HealButton.cs` and `TownToken`'s fan path wire the same three effects
(`TownToken.cs:71` says so). Confirm which is live; guard both, or delete the dead one.

### 6.5 The Influence channel comes free

§6.4 of the traits spec predicted it: disband a wounded unit, recruit a fresh one, and Influence has
been converted directly into survival without a Heal card. That makes Influence a survival stat and
serves pillar 1. No new mechanic is needed — but `DisbandPanel` must **show wound state**, or the
player is choosing blind.

Note this channel is priced by the army cap, not by the disband: you can only rehire into a slot you
freed.

---

## 7. Army HUD readout

Everything needed already exists: `Player.Units.Count`, `Player.ArmyCap` (`Player.cs:56` →
`LevelRules.DerivedArmyCap`), and the registered `army` glyph (`IconMarkup.cs:28`).

One small `MonoBehaviour`, **Update-polled**, one serialized TMP reference. Deliberately not
event-driven: an `IntEvent` + listener means more hand-wiring and re-exposes the Static-vs-Dynamic
footgun that silently pins listeners to 0. `HealButton`, `CombatButtons` and `CombatController` all
already refresh per frame; polling two ints is in-idiom.

Renders `IconMarkup.Tag(IconConcept.Army)` + `{count}/{cap}` — `army 0/1`, `army 1/1`, `army 1/2`.
Readout dialect (icon, space, value), not the no-space cost dialect, since this is not a price.

**At-cap state reads `ArmyRules.NeedsDisband(count, cap)`** — the same predicate `RecruitPanel.cs:58`
and `Player.cs:390` use to decide whether recruiting opens the disband picker, so the HUD and the
recruit flow cannot drift apart. The component exposes the boolean; presentation is authored.

**Optional wounded suffix:** a second, optional TMP field rendering a wounded count only when above
zero — `2/2` is ambiguous between "full army" and "full army, both down". Left unassigned by default.

---

## 8. Persistence — schema v13 → v14

`RunState.unitWounds` — `int[]`, parallel to `unitIds`, the same shape `unitExhausted` uses
(`SaveModels.cs:38`). Int, not bool, because Toxic makes 2 a real state.

- `SaveModels.cs:20` — `schemaVersion = 14`, plus the version comment block.
- `SaveMigrator` — v13 → v14: absent `unitWounds` → `Array.Empty<int>()` (all units healthy).
- `DataManager.CaptureRun` (~`:401`) — `unitWounds` joins the **same** `ConvertAll`, honouring the
  existing comment that promises `unitIds[i]` and `unitExhausted[i]` always pair.
- `DataManager.ApplyRun` (~`:337`) and `Player.RebuildUnits` — one extra argument.
- New `SaveMigratorV14Tests.cs`.

**The tail is the trap.** 22 assertions of `13` across 12 files assert the current version and must
all move to 14: `SaveMigratorTests`, `SaveMigratorV3Tests` … `SaveMigratorV13Tests`. This is the most
likely thing in the whole spec to be forgotten.

Unit wounds are **not** added to `PlayerHand.TotalWoundCount` — see §2.

---

## 9. Change surface

**Combat**
- `CombatController.ResolveDefend` — two-stage (§4.5)
- `EnemyTraitRules` — `Resolve` + `CounterattackOutcome` (§3.2)
- `WoundDestination.cs` — delete the obsolete `Unit = 2` reservation comment

**Units**
- `UnitsSO` — `armorClass` + `OnValidate` clamp
- `Unit.cs` — `WoundCount`, `IsWounded`, `ApplyStateTint`, `Selectable`, `OnPointerClick`
- `Player.cs` — `WoundUnit`/`HealUnit`; `AnyRefreshable` excludes wounded; round refresh unchanged
- `BarFocusController` — relayout on wound change
- `UnitInspector` — refuse a wounded unit

**Picker** (`UnitPickerPanel`)
- `OpenForWounds`, `OpenForHeal`; `Close()` → dismiss request; new `CloseInternal()`
- Take-hit row (§4.3)

**Healing**
- Four call sites route through the picker (§6.3) + undo snapshot
- `HealRules.HealableCount` / `CanHeal`

**Town heal guard**
- `TownActionSnapshot` + `PlaceActionRules.ForTown` — `anyWoundToHeal`
- `TownToken` `PlaceActionId.Heal` — refuse before raising
- `HealButton.cs` — guard, or delete if dead
- `DisbandPanel` — show wound state

**HUD**
- One new `MonoBehaviour` (§7)

**Persistence** — §8.

### 9.1 Phasing

This is one mechanism but a wide surface, so the plan should stage it. The dependency order is forced:

1. **Unit wound state + persistence** (§5, §8) — nothing else can be built or tested without it.
2. **Soak math** (§3) — pure, fully testable before any UI exists.
3. **Wound picker** (§4) — needs 1 and 2.
4. **Healing rework** (§6.1–6.3) — needs 1; independent of 3.
5. **Town heal guard** (§6.4) — a standalone bug fix, shippable on its own once `HealRules` gains
   the predicate. Could go first if it wants to ship early.
6. **Army HUD readout** (§7) — fully independent; no dependency in either direction.

---

## 10. Balance

**Armor scales with recruit price**, so armor is bought rather than free:

| Recruit band | `armorClass` |
|---|---|
| cheap (2–3 Influence) | 1 |
| standard (3–4) | 2 |
| premium (5+) | 3 |
| support / caster | 0 |

*Starting values — tune in playtest*, per `balance.md` convention.

**The army cap is already the stacking limit.** It starts at 1 and rises only on milestone
level-ups, so unlimited stacking can never exceed a level-gated resource. No separate soak cap is
needed — the cap exists, it just lives somewhere else. This is also why the Army HUD readout (§7)
belongs in this spec rather than being cosmetic: the cap is now a defensive stat.

---

## 11. Tests

**Pure (mcs CLI harness):**
- soak arithmetic, including soak > threat
- unsheltered output is identical to today across all four values (§1.1)
- Toxic transfer at 0 committed units (no transfer) and at 1+ (discard → 0, 2 wounds each)
- Leech invariance under soak
- soak-to-zero kills the Brutal surcharge (§3.3 example C)
- `HealableCount` counting hand wounds and unit wounds
- atomic-heal affordability (a 2-wound unit locks at budget 1)

**EditMode:** `SaveMigratorV14Tests` + the 22 assertion bumps (§8).

**Validation:** none new — no new icons, no new authored copy.

---

## 12. Manual Unity edits

Per standing practice, these are authored in the editor from step-by-step instructions, never by
hand-editing scene or prefab YAML:

- Unit prefab: wound decal object + count label
- `UnitPickerPanel` prefab: take-hit row styling
- `UnitPickerPanel` canvas sort order above the combat canvas
- Army HUD label placement and look
- `armorClass` authored onto every existing `UnitsSO` asset

---

## 13. Documentation updates

Same-pass, not deferred:

- `mechanics.md` — Units section (armor, wounds, the cap readout); Lose—Wounds section (units are a
  non-counting sink); **correct the stale Toxic line**: the shipped `EnemyTraitRules.HandWounds` does
  not subtract the toxic share, so Toxic *adds* discard copies rather than "diverting" them.
- `content-rules.md` — `UnitsSO` table + rules; note that `TownsSO.healLevel` carries two meanings
  (Influence price *and* wounds healed).
- `balance.md` — new Unit Armor section (§10).
- `decisions-log.md` — the §6.4 reversals (§2).

---

## 14. Out of scope

- Units absorbing **Withdraw/flee** wounds (1 field/dungeon, 3 guardian) — flee is meant to hurt.
- Units absorbing **Vengeful** on-death wounds — Vengeful exists to make Siege matter in a solo
  fight, and the picker would interrupt the kill FX mid-swing.
- Changing `healLevel`'s double duty as price and amount.
- Any change to wound-out or wound-hand loss conditions.

---

## 15. Decisions recorded

1. Unit armor is a **flat soak added to Defend**, not a Toughness-style divisor (reverses §6.4).
2. Units **stack freely**; the army cap is the implicit limit.
3. Eligibility is **`!IsWounded` only** — exhausted units may shield.
4. **Leech** reads pre-soak; **Toxic** transfers to committed units at 2 wounds each. These are the
   only two exceptions to "armor is Defend".
5. Shelter applies to the **Defend-phase counterattack only**.
6. The heal picker is **one list**, hand wounds and units competing for one budget.
7. Healing a unit is **atomic** — full `WoundCount` or nothing.
8. Wound mode is the **only picker with no click-off**; its commit is a **row**, not a button.
9. `WoundDestination.Unit = 2` is **retired unused**.
10. Town heal is guarded at both the button and the commit; the commit guard is the real one.
