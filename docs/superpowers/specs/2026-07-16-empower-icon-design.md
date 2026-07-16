# Empower Icon — Design Spec

**Date:** 2026-07-16
**Status:** Approved
**Builds on:** `docs/superpowers/specs/2026-07-15-m2.11-ui-language-iconography-design.md` (the icon language and `IconMarkup`/`IconRegistrySO` it introduced).

## Goal

Give the concept **Empower** its own canonical glyph so the literal word "Empower" can be
replaced by an icon — in authored card/skill descriptions and on the ConvertBanner label — the
same way every other core concept already reads as an icon.

## Motivation

The M2.11 icon language turned every core concept into a glyph, but "Empower" is still spelled
out. Card descriptions read e.g. `Empower <sprite="Sword" index=0>: 6`, mixing a word into an
otherwise icon-driven line. A dedicated glyph completes the visual language and shortens the
empowered-line header.

## Scope

**In scope**
- New `IconConcept.Empower` + `IconMarkup.TmpName` case (`"empower"`).
- One new single-glyph TMP Sprite Asset `empower` and a 17th `IconRegistry` entry.
- ConvertBanner label `"Empower to unlock"` → `"<empower-glyph> to unlock"`.
- Authored card/skill descriptions: swap the literal `Empower ` header for the glyph.
- Test + docs updates.

**Out of scope**
- The CardInspector validation message (`"…to empower this card."`). "Empower" there is a verb
  mid-sentence; a glyph reads as a verb awkwardly, so it stays prose (user decision 2026-07-16).
- Any `StatType`→concept mapping. Empower is a modifier, not an action stat.

## Design

### Concept model
- Add `Empower` to the `IconConcept` enum (append at the end — the enum is not persisted by index
  in a way that requires a specific slot, but appending is the safe convention).
- `IconMarkup.TmpName(IconConcept.Empower)` returns `"empower"`.
- Empower is **not** added to `IconMarkup.ActionStatOrder` and **not** handled by
  `IconMarkup.TryForStat` — it is a modifier concept with no action-stat semantics. This also means
  the `AuthoredDescriptionsListActionStatsInCanonicalOrder` validation test ignores `empower` tags
  (they are not ranked), which is correct: an `empower` glyph may legitimately precede any stat.

### Rendering / authoring convention
- Empowered-line header in descriptions: `<sprite="empower" index=0> <sprite="X" index=0>: N`
  (glyph + single space + the stat block), replacing the old `Empower <sprite="X" index=0>: N`.
- Because the empowered line still carries an action-stat icon, per-line canonical stat order is
  unaffected (only one action stat appears per empowered line today).

### Code label
- `ConvertBanner.cs`: the locked-reason text `"Empower to unlock"` becomes
  `$"{IconMarkup.Tag(IconConcept.Empower)} to unlock"`.

### Assets (USER, editor)
- Author a single-glyph TMP Sprite Asset named exactly `empower` in
  `Assets/TextMesh Pro/Resources/Sprite Assets/` (suggested art: an upward chevron / spark / "+"
  motif distinct from the crystal, since empower is the *act*, not the crystal).
- Add a 17th `IconRegistry.asset` entry mapping `Empower` → the same sprite (required by the
  `RegistryAssetIsComplete` validation test, which iterates every concept).
- Sweep the authored card/skill descriptions that contain the word "Empower", replacing the header
  word with the glyph. No automated test forces this swap (validation only checks that tags are
  known and action-stat order per line); it is a manual authoring pass.

## Testing

- **`IconMarkupTests` (mcs harness, RED→GREEN):** assert
  `IconMarkup.Tag(IconConcept.Empower) == "<sprite=\"empower\" index=0>"`. The existing
  `TmpName_NonEmptyForEveryConcept` foreach already covers the new member automatically.
- **`IconRegistryValidationTests` (editor Test Runner):** `RegistryAssetIsComplete` and
  `EveryConceptTmpAssetResolves` will require the new sprite asset + registry entry to be authored
  before they go green (expected RED until the USER editor step).

## Docs

- `content-rules.md` UI-language section: bump "16 canonical names" → 17 and add `empower`;
  note the empowered-line header convention (`<empower> <stat>: N`).
- Append a dated decisions-log entry.

## Risks

- **Icon-as-verb awkwardness** — mitigated by keeping the CardInspector prose message as words
  (out of scope).
- **Manual description sweep** is unenforced by tests; a missed card simply keeps the word
  "Empower" (harmless, just inconsistent). The acceptance step is a visual pass over the card pool.
