# Combat preview and trait icons

2026-07-30. Supersedes `2026-07-30-enemy-trait-icons-and-combat-tooltip-design.md`. Same
starting point (real icon art for the 13 `EnemyTrait` flags, a way to read what a badge
means), but a bigger fix underneath: the reason trait info felt hard to get to wasn't a
missing tooltip, it was that the game already has a preview screen (`EnemyPreviewPanel`)
that's *separate* from the real fight screen (`EnemyCard` in the combat canvas) — and every
combat entry point pays its cost (turn action, visit action, explore) the instant you
click, before you've seen anything. Stacking a tooltip on top of the preview screen would
have been a second hover layer on a first one. The actual fix: retire the separate preview
screen, and let opening the real combat canvas be the free look — nothing is spent until
you commit.

## 1. Problem

1. **Two screens show the same enemy, at different fidelity, for no reason.**
   `EnemyPreviewPanel`/`EnemyPreviewEntry` (hover-driven, off `PreviewTrigger` and its three
   subclasses) render a *simplified* stat block + trait legend before a fight opens.
   `EnemyCard` (in the combat canvas) renders the *real* card once the fight opens. A player
   trying to learn a trait's meaning while already inside the real screen has no way to get
   back to the simplified one — and adding a badge tooltip *inside* the real screen, while
   the simplified screen still exists elsewhere, means the game now explains traits in two
   different UI layers that never talk to each other.
2. **Every combat entry point spends its cost before the player sees anything.**
   Traced all three: `EnemyToken.StartCombat` calls `TurnPhaseController.BeginAction()`,
   `GuardianAssault.Begin` calls `CommitVisitAction()`, `DungeonPanel.PerformDelve` spends
   `PlayerExplore` and calls `CommitVisitAction()` — all *before* `CombatController.OpenFight`
   is ever called. Once the canvas is open, the only way out is fighting through to the
   Attack phase and paying `Withdraw()`'s flee penalty (a wound, plus Harrying's hand-size
   cost if any enemy present has it). There is no free look, anywhere, once you've clicked.
3. (Unchanged from the prior spec) **The 13 trait badges are still letters**, and the
   half-finished icon-art attempt left `EnemyPreviewEntry`'s legend box visibly broken.
   Moot once `EnemyPreviewEntry` is deleted rather than fixed — see §3.

## 2. The fix: opening combat is free; committing to it is not

`CombatController` gains a `Committed` flag, false from the moment `OpenFight` runs.
While `!Committed`:
- The canvas is open, cards are real `EnemyCard`s (not a simplified stand-in) with live
  Siege/Influence buttons and — carried forward from the prior spec — a hover tooltip on
  each card's trait badges.
- Nothing has been spent: not the turn's action, not a town visit's action, not Explore,
  not Siege, not Influence.
- The player can close the canvas for free. Nothing happened; nothing is recorded.

The **only** way `Committed` becomes true is pressing one of the three buttons that were
already the "spend something, this fight is now happening" actions:  **Engage**, or
spending **Siege** on a card, or spending **Influence** on a card. Fight/Attack isn't
reachable pre-commit anyway (`CombatPhaseRules.CanNormalAttack` is Attack-phase-only, and
Attack phase is only reached via Engage -> Defend -> ResolveDefend, all downstream of
committing). That gives exactly three player-facing triggers, matching your "engage, siege,
or influence" framing directly.

### 2.1 Why not a new `CombatPhase`

Considered adding a `Scouting` phase ahead of `Siege` instead of a bool. Rejected: a
`Scouting` phase would need *identical* button legality to `Siege` (Engage/Siege/Influence
are already exactly what's legal in Siege phase today) — the only actual difference between
"viewing" and "Siege phase, for real" is one bit: has the cost been paid. Growing the
`CombatPhase` enum for a distinction that changes no button-legality rule would mean
touching every `CombatPhaseRules.Can*` switch and the phase-label HUD mapping for zero
behavioral gain. A bool next to `Phase`, gated through one funnel, is the whole change.

### 2.2 One funnel, not three call sites

The three commit triggers don't actually need three separate `Commit()` calls scattered
through `Player.cs`/`EnemyCard.cs`. All three already pass through exactly two existing
`CombatController` methods before doing anything else:
- **Engage** presses call `CombatController.Engage()` directly.
- **Siege** spend (`Player.SiegeEnemy` -> `ResolveAttack`) and **Influence** spend
  (`Player.InfluenceEnemy` -> `CompleteInfluence`) both end by calling
  `CombatController.NotifyDefeated(...)` — the single existing "an enemy left the fight"
  entry point, already shared by Siege, Influence, and Attack-phase Fight kills.

So the funnel is: an idempotent `Commit()` —

```
void Commit()
{
    if (Committed) return;
    Committed = true;
    pendingOnCommit?.Invoke();   // the context-specific cost, see §2.3
    pendingOnCommit = null;
}
```

— called as the first line of `Engage()` and the first line of `NotifyDefeated()`. Calling
it from `NotifyDefeated` too is harmless post-Engage (it's a no-op by then) and means Fight
kills, which only happen after Engage already committed, need no separate handling. No
change to `Player.cs` or `EnemyCard.cs` is needed for the gate itself — the invariant "no
path starts the fight except through Engage/Siege/Influence" holds structurally, not by
convention.

### 2.3 Who pays what, and when

`OpenFight` gains one new parameter: `System.Action onCommit`. Each entry point now hands
`OpenFight` a callback containing exactly the cost-payment logic it used to run eagerly,
instead of running it before the call:

| Context | Entry point | `onCommit` callback contains |
|---|---|---|
| Field | `EnemyToken.StartCombat` | `TurnPhaseController.Instance.BeginAction()` |
| Guardian | `GuardianAssault.Begin` | `TurnPhaseController.Instance.CommitVisitAction()` |
| Dungeon | `DungeonPanel.PerformDelve` | `player.PlayerExplore -= cost; player.GetCurrentExplore(); TurnPhaseController.Instance.CommitVisitAction();` |

Everything else those methods currently do (destroying the town's fan cards, disabling the
originating canvas, the `GameLog` need-more-resource message when the player can't afford
entry) stays exactly where it is and exactly as eager as it is today — none of that is a
game-state commitment, it's just screen transition and affordability gating. Dungeon keeps
its pre-open affordability check (`PlayerExplore < cost` -> log message, don't open)
unchanged: you still can't open a delve you can't afford, same as you still can't open one
without an unflagged turn action (`UpdateDelveInteractable`'s existing `VisitCanAct` gate).
Opening was already understood as a "free peek" for the dungeon panel specifically — see
its own comment at `UpdateDelveInteractable`: *"opening the panel is a free peek... locked
[only once] the action is spent."* This spec generalizes that existing convention to the
fight screen itself, and to all three contexts uniformly.

### 2.4 Declining

A new `CombatController.Decline()`, callable only while `!Committed`: tears down the spawned
cards (Field: morphs the card back to its token, mirroring the existing flee morph-back,
then `SetBoardVisible(true)`; Guardian/Dungeon: cards just clear), closes the canvas via the
existing `GameManager.Instance.CloseCombatCanvas()`, resets `Phase`/`Committed`/`live` for
the next fight. Explicitly **does not** touch `TurnPhaseController`, `ConquestTracker`,
`DungeonTracker`, rewards, or shrine state — none of that ran, because nothing was
committed. This is not `Withdraw()` with the penalty stripped out; it's a different, much
smaller method, because `Withdraw()`'s job (pay a flee cost, bank partial progress) doesn't
apply to a fight that was never entered.

Trigger: click-off, the same idiom already used elsewhere in this game's menus/fans (no
dedicated "close" button, per the existing "click-off everywhere, no exit buttons"
convention). Once `Committed` is true, click-off does nothing — from that point on, backing
out is `Withdraw()`, Attack-phase only, exactly as today. This spec does not add any new
way to back out of a committed fight; it only makes the pre-commit look free.

## 3. What gets retired

Once `EnemyCard` is the only screen that ever shows enemy info, these are deleted, not
fixed:
- `EnemyPreviewPanel.cs`, `EnemyPreviewEntry.cs` + its prefab (`EnemyPreviewEntry .prefab`)
- `PreviewTrigger.cs` and its three subclasses: `EnemyTokenPreviewTrigger.cs`,
  `FanPreviewTrigger.cs`, `PlacePreviewTrigger.cs`
- `DungeonPanel`'s separate `previewText`/`UpdatePreview` (next-enemy name + Attack/HP as
  plain text inside the dungeon menu) — same duplication complaint at smaller scale: once
  pressing Delve opens the real card for free, a second, simplified description of the same
  enemy one panel back has no job left.
- `PreviewRules.CanPreview()`/`EncounterVisible()` are evaluated once more, at
  `CombatController.OpenFight` time, to decide whether to show real cards or a "you cannot
  see..." blind state in the canvas itself (the one caller of this logic that survives —
  see §3.1). `PreviewRules.ClampAxis` does not survive: it was `EnemyPreviewPanel`-only
  screen-edge-clamping math with no other caller.

Everything these deleted classes fed — `EnemyPreviewData`, roster-aware Attack/HP display
via `EnemyTraitRules.Threat` — is already computed by `CombatController.Roster()`/`Tuning`
for the live cards; nothing new needs to reproduce it.

### 3.1 The blind-source case

`EnemyPreviewPanel` handled "you can't see this encounter" (a blind source hiding it) by
showing a text message instead of stat entries. That check needs a new home now that
there's no separate preview screen to fall back to. `OpenFight` runs
`PreviewRules.EncounterVisible(...)` before spawning cards; if hidden, it opens the canvas
in a blind state (existing `blindText`-style message, now inside the combat canvas rather
than a hover panel) instead of spawning `EnemyCard`s. `Committed` never becomes reachable
from a blind state — there's nothing to Engage/Siege/Influence against — so the only exit is
`Decline()`.

## 4. The 13 icons (carried forward, unchanged)

Same 13 picks approved during the prior spec's review, from
`Assets/Images/500FreeSkillIcons/Icons/` — the pack already backing the
doom/dungeon/empower/guardian/refresh glyphs:

| Trait | Kind | Source PNG | New TMP Sprite Asset name | Read |
|---|---|---|---|---|
| Armored | self | `skill_133.png` | `traitArmored` | ornate blue shield |
| Elusive | self | `skill_067.png` | `traitElusive` | hooded wraith figure |
| Hulking | self | `skill_336.png` | `traitHulking` | glowing muscular figure |
| Swift | self | `skill_349.png` | `traitSwift` | winged sneaker |
| Brutal | self | `skill_233.png` | `traitBrutal` | snarling fanged mouth |
| Toxic | self | `skill_204.png` | `traitToxic` | bubbling green orbs |
| Leech | self | `skill_174.png` | `traitLeech` | red draining spiral |
| Harrying | self | `skill_512.png` | `traitHarrying` | two snarling wolf heads |
| Vengeful | self | `skill_218.png` | `traitVengeful` | skull, glowing red eyes |
| Warlord | aura | `skill_490.png` | `traitWarlord` | blue crystal crown |
| Miasma | aura | `skill_162.png` | `traitMiasma` | green-rimmed void portal |
| Ironclad | aura | `skill_379.png` | `traitIronclad` | gold cross-shield emblem |
| Outrider | aura | `skill_374.png` | `traitOutrider` | eagle head |

Toxic and Leech were swapped once (both originally hand-shaped icons with visibly wrong
finger counts); Brutal was swapped proactively for the same reason. Leech deliberately
avoids a crystal/gem shape despite stealing crystals mechanically, to avoid visually
colliding with the game's actual Crystal resource glyph in the same badge row. Ironclad and
Outrider get their own icon rather than reusing Armored's/Swift's, since a granting aura and
the trait it grants can both be visible on the same roster and need to read as different
badges.

## 5. Code changes

### 5.1 `IconMarkup.cs`
Replace the letter switch inside `TraitBadge` with the sprite-asset names from §4. Call
sites don't change — that's what the seam (`2026-07-29` spec §8.1) was for.

**AMENDED 2026-07-30 — the amber aura tint is dropped.** `TraitBadgeTinted`, `AuraTint`, and
`IconMarkup.IsAuraTrait` are deleted; every call site uses plain `TraitBadge`. Two findings
killed it:

1. **There is no per-glyph "Tint" option.** Verified against this project's TMP
   (`com.unity.ugui`): a sprite asset's character/glyph tables carry no tint field — the
   shipped `crystal.asset`, which tints correctly today, has none. `TMP_Text.cs` maps the
   `color` attribute straight to `m_spriteColor` and applies it either way; the separate
   `tint=1` *tag attribute* only means "also multiply by the surrounding text colour."
   So a `color=` attribute would have worked with no manual step at all.
2. **But the art can't carry a tint anyway.** TMP tints by *multiplying* the glyph, so a
   colour only reads on white/near-white source art. The 13 trait icons are full-colour
   painted art — Warlord blue, Miasma green, Outrider purple all turn muddy under amber,
   and Ironclad is already gold, so the tint is a no-op on it. The distinction fails in
   all four directions.

Auras are now distinguished by their own icon plus the hover legend (§6). Restoring the
tint requires monochrome badge art first; it is then one method emitting
`<sprite="name" index=0 color=#F5D90A>` — colour as an attribute ON the sprite tag, never a
wrapping `<color>` tag, matching `IconMarkup.CrystalTag`.

The aura **combat** rules are untouched and live in `EnemyTraitRules`
(`GrantedByAuras`/`WarlordAura`); the deleted display helpers never fed them.

### 5.2 `EnemyTraitCopy.cs`
Extract the "badge + name + rule" line format into a shared pure method, used by the one
surviving legend renderer (§6):
```
public static string LegendLine(EnemyTrait t, EnemyTraitTuning tuning)
    => IconMarkup.TraitBadge(t) + " " + IconMarkup.TraitName(t) + " — " + Rule(t, tuning);

public static string Legend(EnemyTrait mask, EnemyTraitTuning tuning)
    // one LegendLine per Split(mask), newline-joined; "" for EnemyTrait.None
```

### 5.3 `CombatController.cs`
- `Committed` (bool, public get), reset false in `OpenFight`.
- `System.Action pendingOnCommit`, captured from `OpenFight`'s new `onCommit` parameter.
- `Commit()` (private, idempotent) per §2.2, called first-line in `Engage()` and
  `NotifyDefeated(...)`.
- `Decline()` per §2.4.
- `OpenFight` gains `System.Action onCommit = null` and, per §3.1, checks
  `PreviewRules.EncounterVisible(...)` before spawning cards, opening a blind state instead
  when hidden.

### 5.4 Entry points
`EnemyToken.StartCombat`, `GuardianAssault.Begin`, `DungeonPanel.PerformDelve` each stop
paying their cost inline and instead pass it as `OpenFight`'s `onCommit` argument, per the
table in §2.3. `DungeonPanel.PerformDelve` keeps its pre-open affordability check (log +
don't open if `PlayerExplore < cost`) — that's a gate on *opening*, not a commitment, and
stays eager.

### 5.5 New: `EnemyTraitBadgeHover.cs`
Unchanged in shape from the prior spec, now with one job instead of two: it's the *only*
place trait info renders, not an extra layer next to a preview panel.
```
MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
[SerializeField] EnemyCard card;
```
Attached to `EnemyCard`'s `traitBadges` GameObject (already `m_RaycastTarget: 1`). On enter:
show `EnemyTraitCopy.Legend(card.Traits, CombatController.Instance.Tuning)` in a small
screen-anchored tooltip (structurally the same clamped-positioning pattern
`EnemyPreviewPanel` used, since that math itself was sound — see §6). On exit: hide. Live in
every phase, committed or not, since it's reading the card's own already-visible badges,
not doing anything that needs gating.

### 5.6 Deletions
Per §3: `EnemyPreviewPanel.cs`, `EnemyPreviewEntry.cs` + prefab, `PreviewTrigger.cs`,
`EnemyTokenPreviewTrigger.cs`, `FanPreviewTrigger.cs`, `PlacePreviewTrigger.cs`,
`DungeonPanel.previewText`/`UpdatePreview`, `PreviewRules.ClampAxis`.

### 5.7 Tests
- `EnemyTraitCopyTests.HulkingIsK_BecauseHarryingTookH` — delete (letter-collision avoidance
  no longer applies once badges are icons).
- Add: every trait's `TraitBadge` output contains its assigned sprite-asset name from §4.
- Add: `LegendLine`/`Legend` are non-empty for every trait / non-`None` mask.
- New `CombatController` coverage (EditMode, exercising the controller directly since it's a
  MonoBehaviour singleton the existing combat tests presumably already instantiate — follow
  whatever pattern `Assets/Tests` already uses for it):
  - `OpenFight` leaves `Committed == false` and spends nothing (no `TurnPhaseController`
    call recorded).
  - `Engage()` sets `Committed == true` and invokes `onCommit` exactly once.
  - A Siege kill (`NotifyDefeated`) sets `Committed == true` even if `Engage()` was never
    called.
  - Calling `Commit()`'s effects twice (e.g. `Engage()` then a later `NotifyDefeated`) only
    invokes `onCommit` once.
  - `Decline()` while `!Committed` clears `live`, closes the canvas, and does not invoke
    `onCommit`.

## 6. The badge hover tooltip

A small scene-placed singleton, `EnemyTraitTooltip`, reusing the screen-anchored,
edge-clamped placement `EnemyPreviewPanel` implemented (project a screen point onto the
canvas plane, then slide the whole box back on-screen if it would clip an edge) — that
positioning logic wasn't the problem, having two competing content sources was. One TMP
text block, no stat rows, no blind-state (the canvas itself now owns blind-state, §3.1).
Two calls:
```
Show(EnemyTrait traits, EnemyTraitTuning tuning, Vector3 screenPosition)
Hide()
```
No-op when `EnemyTraitCopy.Split(traits)` is empty — an untraited enemy's badge row is
never shown in the first place (`traitBadges.gameObject.SetActive(parts.Length > 0)`
already gates that), so the hover target only exists where there's something to show.

Shows the card's own **authored** `Traits`, not roster-effective traits — matching the
convention the deleted `EnemyPreviewEntry` used (a granting aura narrates its effect through
its own `Rule()` line rather than duplicating a badge onto every other card).

## 7. Manual Unity-editor work (you)

1. **13 TMP Sprite Assets** — for each PNG in §4: import as a Sprite, then right-click ->
   Create -> TextMeshPro -> Sprite Asset (same process as the existing 25 glyphs). Name each
   per the table. All 13 are treated identically — there is no per-glyph Tint step (§5.1).

   **BLOCKED 2026-07-30 on art.** The current `500FreeSkillIcons` PNGs are unsuitable for
   inline badges and this step is parked until replacements land: all 13 are RGB with **no
   alpha channel**, so each renders as an opaque square block in a text run (every working
   glyph in the game — e.g. `crystal.png` — is an RGBA cutout); they are 512x512 painted
   scene art rather than icons, so at badge size (~24px) `Toxic`/`Miasma` collapse to one
   green mass and `Leech`/`Brutal` to one red mass, while `Elusive`/`Hulking`/`Harrying`
   (full figures) lose any readable silhouette; and `Swift` is a modern running shoe. What
   is needed: flat, single-subject, high-contrast glyphs on transparency — monochrome if
   the aura tint is ever to return. The rest of §7 does not depend on this and proceeds;
   badges keep rendering as the previous letters until real art arrives.
2. **Fix `EnemyCard`'s `traitBadges` RectTransform** — currently anchored at a fixed
   `(25, 35)` pixel offset with `m_SizeDelta: {0,0}` (a leftover from the half-finished
   attempt). Needs to sit in the card's existing HP/Attack/Influence row (or immediately
   below it) with room for several icon glyphs side by side.
3. **Wire `EnemyTraitBadgeHover`** onto the `traitBadges` GameObject on `EnemyCard`, `card`
   field pointing at the parent.
4. **Place `EnemyTraitTooltip`** in the combat scene as a singleton (mirroring how
   `EnemyPreviewPanel` was scene-placed), panel/label refs wired.
5. **Delete** the retired prefab (`EnemyPreviewEntry .prefab`) and any scene references to
   the retired components (§3) once the code side compiles clean without them.
6. **Wire the click-off decline** — hook the canvas's existing click-off catch (the same
   pattern other menus/fans in this game already use) to `CombatController.Decline()`,
   active only while `!Committed`.
7. **Blind-state UI** in the combat canvas (§3.1) — the "you cannot see..." message,
   previously `EnemyPreviewPanel.blindState`/`blindText`, needs an equivalent element in the
   combat canvas itself.

## 8. Testing

- EditMode: `EnemyTraitCopyTests` updates (§5.7) and the new `CombatController` commit-gate
  coverage, run via the existing mcs/nunit CLI harness (editor-lock permitting) or Unity's
  Test Runner.
- Manual, after the editor wiring in §7: open a field encounter, confirm nothing is spent
  and the turn action is still available, click off, confirm the token is back and nothing
  changed; reopen, press Siege on a killable enemy, confirm the turn action is now spent and
  click-off no longer closes the canvas; repeat for a guardian assault (visit action) and a
  dungeon delve (Explore + visit action); hover the badge row on both a single-trait and a
  multi-trait enemy and confirm the tooltip shows the same legend text in both the pre-commit
  and post-Engage states. The tooltip is not gated on trait count — any enemy with at least
  one trait gets one. A trait-less enemy has no badge row to hover (§5.5), so no tooltip.
