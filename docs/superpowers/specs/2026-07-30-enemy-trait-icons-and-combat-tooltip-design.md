# Enemy trait icons and combat tooltip

2026-07-30. Follows up on `2026-07-29-enemy-traits-and-wound-plumbing-design.md`, which
deliberately shipped trait badges as single letters ("A", "K", "S"...) with an explicit
upgrade seam (`IconMarkup.TraitBadge`) for real icon art later. This spec is that later:
real icons, a fix for the layout bug the half-finished art attempt left behind, and a new
in-combat way to learn what a badge means without leaving the fight screen.

## 1. Problem

Three complaints, all confirmed by reading the current code and the uncommitted prefab
edits already in the working tree:

1. **The icon art is half-wired.** `EnemyPreviewEntry`'s trait legend (`traitLines`) and
   `EnemyCard`'s badge row (`traitBadges`) both got a new TMP GameObject added by hand, but
   each one is a leftover placeholder: a hardcoded `"T"`, a zero-size rect
   (`m_SizeDelta: {0,0}`), anchored at a fixed pixel offset instead of flowing with the
   card. That's why the preview currently renders "way off" — the box was sized for one
   glyph, not a multi-line legend.
2. **No real icon art exists**, so every trait still shows its bare letter badge on the
   live `EnemyCard`, with no visual distinction between traits beyond that letter.
3. **Trait effects are only explained pre-fight.** The badge legend
   (badge + name + rule text) only exists in `EnemyPreviewPanel`, which is hover-driven off
   the map/dungeon icon *before* a fight is joined. Once combat starts, the player sees
   bare badges on `EnemyCard` with no way to recall what any of them do short of retreating
   to check the preview again (which may no longer be reachable mid-fight).

## 2. Scope

**In scope:**
- 13 real icons for the `EnemyTrait` flags, replacing the letter badges everywhere they
  render (`EnemyCard`, `EnemyPreviewEntry`).
- Fixing the RectTransform layout on both trait-legend UI elements so real content (wider
  glyphs, multi-line rule text) displays correctly instead of clipping/overlapping.
- A new hover tooltip on `EnemyCard`'s badge row, live during combat, showing that one
  enemy's badge+name+rule legend — the same content `EnemyPreviewEntry` already shows
  pre-fight, reachable without leaving the fight.

**Out of scope:**
- Any change to trait *mechanics* (`EnemyTraitRules`, `CombatPhaseRules`) — this is
  presentation only.
- Touch/controller-driven tooltip triggers — hover-only, matching every other tooltip in
  the game today (`HexTooltip`, `EnemyPreviewPanel`). Controller support is tracked as a
  separate, later initiative and combat cards are explicitly a later phase in that plan.
- The `IconRegistrySO` / `Image`-based icon system used elsewhere (HUD, M2.12 canvas art).
  Trait badges are TMP text today and stay TMP text — see §3.

## 3. Icon delivery: TMP Sprite Assets, not `IconRegistrySO`

Two icon systems already coexist in this project:
- `IconMarkup` — TMP sprite-tag strings (`<sprite="name">`) for inline use in TMP text.
  Every existing stat glyph (Attack, HP, Wound, ...) and the current letter badges go
  through this.
- `IconRegistrySO` — a `Sprite` lookup for `Image`-based UI (HUD chrome, M2.12 canvas art).

The half-finished attempt that produced the "way off" preview was reaching for the
`Image`/`IconRegistrySO` path for trait badges. That's a bigger change than it looks
(badges would need to become `Image` children in a layout group instead of inline text
glyphs) and it's not what `EnemyCard`/`EnemyPreviewEntry` are built around — both render
badges as part of a TMP string today.

**Decision: stay on the TMP Sprite Asset path**, per the upgrade seam the previous spec
already designed for exactly this moment (§8.1 of the prior spec: "call sites never
change... swaps from a letter to `<sprite=…>`"). Each of the 13 approved PNGs becomes its
own one-glyph TMP Sprite Asset, the same pattern every existing glyph already uses
(`Sword.asset`, `shield.asset`, `wound.asset`, ... are each a single-glyph asset, always
referenced at `index=0`).

## 4. The 13 icons

All sourced from `Assets/Images/500FreeSkillIcons/Icons/` — the pack already backing the
doom/dungeon/empower/guardian/refresh glyphs, so no new art style enters the game. Chosen
and approved against a visual proposal (two icons — Toxic, Leech — were swapped after
review for showing hands with visibly wrong finger counts; Brutal was swapped proactively
for the same reason before it could be flagged).

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

Deliberate choices worth recording:
- **Ironclad** gets its own shield (not Armored's), since both can be visible on the same
  roster at once and need to read as different badges.
- **Outrider** gets an eagle, not a second sneaker, for the same reason — it *grants*
  Swift, it isn't Swift.
- **Leech** deliberately avoids a crystal/gem shape (it steals crystals) because that
  would visually collide with the game's actual Crystal resource glyph in the same badge
  row.

## 5. Code changes

### 5.1 `IconMarkup.cs`
Replace the letter switch inside `TraitBadge`/`TraitBadgeTinted` with the sprite-asset
names from §4. Call sites (`EnemyCard.RefreshTraitBadges`, `EnemyPreviewEntry.Populate`,
the new tooltip in §6) do not change — that's the seam paying off.

`TraitBadgeTinted` currently wraps the letter in a `<color=#hex></color>` rich-text tag
for auras. **That does not tint a TMP sprite glyph** — sprites only honor a `color=`
*attribute on the `<sprite>` tag itself* (and only if the glyph's Tint option is on), the
way `IconMarkup.CrystalTag` already does it for the crystal glyph. `TraitBadgeTinted` must
switch to that same inline-attribute form, e.g. `<sprite="traitWarlord" index=0
color=#F5D90A>`, not a wrapping `<color>` tag.

**Manual step (you, in-editor):** when creating each of the 13 TMP Sprite Assets, the
aura ones (Warlord, Miasma, Ironclad, Outrider) need **Tint** enabled on their glyph entry
in the Sprite Asset's glyph table, or the amber aura recolor will silently do nothing. The
9 self-trait icons don't need it (they never render with a color override).

### 5.2 `EnemyTraitCopy.cs`
Extract the "badge + name + rule" line format — currently written inline once, inside
`EnemyPreviewEntry.Populate` — into a shared pure method:

```
public static string LegendLine(EnemyTrait t, EnemyTraitTuning tuning)
    => IconMarkup.TraitBadgeTinted(t) + " " + IconMarkup.TraitName(t) + " — " + Rule(t, tuning);

public static string Legend(EnemyTrait mask, EnemyTraitTuning tuning)
    // one LegendLine per Split(mask), newline-joined; empty string for EnemyTrait.None
```

`EnemyPreviewEntry.Populate` calls `Legend(...)` instead of its own StringBuilder loop.
The new combat tooltip (§6) calls the same method. One implementation of the legend
format, two callers — pre-fight preview and in-combat tooltip render identically by
construction.

### 5.3 New: `EnemyTraitTooltip.cs`
A small scene-placed singleton, structurally the screen-anchored-with-edge-clamping
pattern `EnemyPreviewPanel` already implements (same `PreviewRules.ClampAxis` helper,
same "project a screen point onto the canvas plane" placement) — but showing only one
enemy's legend text in a single TMP block, no stat rows, no blind-state. Two calls:

```
Show(EnemyTrait traits, EnemyTraitTuning tuning, Vector3 screenPosition)
Hide()
```

`Show` is a no-op when `EnemyTraitCopy.Split(traits)` is empty — an untraited enemy's
badge row is never shown in the first place (`traitBadges.gameObject.SetActive(parts.Length
> 0)` already gates that), so the tooltip trigger only exists where there's something to
show.

### 5.4 New: `EnemyTraitBadgeHover.cs`
```
MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
[SerializeField] EnemyCard card;
```
Attached to the `traitBadges` GameObject itself (already `m_RaycastTarget: 1`, so it
already accepts pointer events — it just has no handler wired yet). Scoped to the badge
row specifically, not the whole card, per your call on where this should live. On enter:
`EnemyTraitTooltip.Instance.Show(card.Traits, CombatController.Instance.Tuning,
RectTransformUtility.WorldToScreenPoint(...))`. On exit: `Hide()`.

Shows the card's own **authored** `Traits`, not roster-effective traits — matching the
existing convention in `EnemyPreviewEntry` (a granting aura narrates its effect through
its own Rule() line rather than duplicating a badge onto every other card).

### 5.5 Tests
`EnemyTraitCopyTests.HulkingIsK_BecauseHarryingTookH` asserts literal letters ("K", "H")
— delete it, the constraint it encoded (avoid letter collisions) no longer applies once
badges are icons. `EveryTraitHasANonEmptyBadge` / `AllBadgesAreUnique` keep working
unchanged (they're already generic over whatever `TraitBadge` returns). Add:
- every trait's `TraitBadge` output contains its assigned sprite-asset name from §4 (catches
  a copy-paste mismatch in the switch).
- `LegendLine`/`Legend` are non-empty for every trait / non-`None` mask, mirroring the
  existing `EveryTraitHasANonEmptyRuleLine` coverage.

## 6. Manual Unity-editor work (you)

Per your usual pattern, you're doing the in-editor asset creation and prefab wiring by
hand from these steps, not me:

1. **13 TMP Sprite Assets** — for each PNG in §4: import as a Sprite (2D and UI), then
   right-click → Create → TextMeshPro → Sprite Asset (same process already used for the
   existing 25 glyphs). Name each asset per the "New TMP Sprite Asset name" column. For
   the 4 aura ones, enable **Tint** on the glyph entry (§5.1).
2. **Fix `EnemyPreviewEntry`'s `traitLines` RectTransform** — currently anchored
   `{0.5,0.5}`→`{1,0}` at a fixed offset with `m_SizeDelta: {0,0}` (sized for the leftover
   "T" placeholder). Needs to flow with the entry: stretch/anchor it so multi-line legend
   text has room to grow downward without clipping the card below it or overlapping the
   stat row above it.
3. **Fix `EnemyCard`'s `traitBadges` RectTransform** — same problem, anchored at a fixed
   `(25, 35)` pixel offset with `m_SizeDelta: {0,0}`. Needs to sit in the card's existing
   HP/Attack/Influence row (or immediately below it) with enough width for up to several
   icon glyphs side by side without overlapping neighboring elements.
4. **Wire `EnemyTraitBadgeHover`** onto the `traitBadges` GameObject on `EnemyCard`,
   pointing its `card` field at the parent `EnemyCard`.
5. **Place `EnemyTraitTooltip`** in the combat scene as a singleton (mirroring how
   `EnemyPreviewPanel` is scene-placed today), with its panel/label refs wired.

## 7. Testing

- EditMode: `EnemyTraitCopyTests` updates from §5.5, run via the existing mcs/nunit CLI
  harness (Unity editor lock permitting) or Unity's own Test Runner.
- Manual: after the editor wiring in §6, verify in Play mode — badge row on a multi-trait
  enemy shows distinct icons with correct aura tint, hovering the badge row during combat
  shows the same legend text the pre-fight preview shows, and the preview panel itself no
  longer clips/overlaps.
