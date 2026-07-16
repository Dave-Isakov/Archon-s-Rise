# Refresh Icon — Design Spec

**Date:** 2026-07-16
**Status:** Approved
**Builds on:** `docs/superpowers/specs/2026-07-16-empower-icon-design.md` (same pattern: one more concept in the M2.11 icon language). Executed inline without a separate plan file — the delta is four files on the empower template.

## Goal

Give **Refresh** its own canonical glyph so the last icon-less keyword reads as an icon: in the
Mobilize card description and on the unit-picker panel title.

## Scope

**In scope**
- New `IconConcept.Refresh` + `IconMarkup.TmpName` case (`"refresh"`).
- **`TryForStat` now maps `StatType.Refresh` → `IconConcept.Refresh`.** The old `false` return was
  justified by "an effect flag with no icon of its own"; that rationale ends when the icon exists.
  Test assertion flips `IsFalse` → `IsTrue`. No current consumer passes Refresh, so this is
  contract-correctness, not a behavior change.
- Mobilize description: glyph replaces the word (current colon-less style) —
  `Refresh 3` → `<sprite="refresh" index=0> 3`, `Refresh 6` → `<sprite="refresh" index=0> 6`,
  legend `Refresh = Unit <gem>` → `<sprite="refresh" index=0> = Unit <sprite="gem" index=0>`.
- UnitPickerPanel title keeps icon **+ word** (panel-header clarity, matching the town-button
  `[icon] Label` dialect): `$"{IconMarkup.Tag(IconConcept.Refresh)} Refresh — {_remaining} left"`.
- TMP Sprite Asset `refresh` (already authored) + an 18th `IconRegistry` entry (USER editor).
- Docs: content-rules canonical names 17 → 18; decisions-log entry.

**Out of scope**
- `ActionStatOrder` (Refresh is an effect, not an action stat).
- Any other UI: no other player-facing "Refresh" strings exist (verified by grep).

## Testing

- `IconMarkupTests` (mcs, RED→GREEN): tag assertion for `Refresh`; `TryForStat_MapsSingleFlags`
  expectation flipped for `StatType.Refresh`.
- `IconRegistryValidationTests` (editor Test Runner): green once the registry entry is added.
