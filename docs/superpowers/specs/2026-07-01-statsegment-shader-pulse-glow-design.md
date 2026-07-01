# StatSegment Shader Pulse-Glow — Design

> **Status:** Experiment. This adds a *second, toggleable* glow path alongside the
> existing sprite glow. Nothing in the current glow path is removed, so the change
> is fully reversible by flipping an Inspector enum back to its default.

## Goal

Replace the flat, static look of the selected-segment glow with a soft **breathing
pulse** driven by a hand-authored UI shader (per-pixel / per-frame, like Godot's
`ShaderMaterial` + `TIME`), while keeping the existing sprite glow intact so the two
can be compared in the Editor and the loser deleted later.

## Context

- Each Choice/Improvise option button (e.g. `AttackButton` in
  `Assets/Prefabs/ImproviseToggle.prefab`) carries a `StatSegment`
  (`Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs`).
- `StatSegment.SetState(Selected)` today calls `SetGlow(true)`, which activates a
  child glow `Image` using a static soft-glow **sprite** (guid `efee20b6…`). This is
  the "flat" look being replaced.
- Project is **URP** with **uGUI** (Canvas + Image/Button + TextMeshPro); no UI
  Toolkit. uGUI Canvases (Screen-Space Overlay/Camera) render through Unity's UI
  system rather than an SRP render pass, so a classic transparent UI shader (derived
  from UI-Default) is the correct, compatible tool. Shader Graph is **not** used —
  it is a GUI-only authoring asset and unnecessary here.

## Scope

**In scope**
- A hand-authored UI shader that renders a breathing (alpha-oscillating) halo.
- A material using it.
- An additive change to `StatSegment`: a `GlowMode { Sprite, Shader }` enum (default
  `Sprite`) and a `shaderGlow` child reference, so `Selected` lights whichever path
  the enum selects.
- A new `shaderGlow` child `Image` per segment (Editor wiring, or prefab-YAML).

**Out of scope (YAGNI / deferred)**
- **Hover** triggering (confirmed select-only for this experiment; the existing
  structure is select-triggered and stays that way).
- Removing the existing sprite glow, `glow` field, or `SetGlow`.
- Any change to selection logic, `CardPlaySelection`, `CardInspector`, panels,
  palette, or the center-float/scrim work from Phase 3a.
- A shared/global toggle — the toggle is **per-segment**.

## Components

### 1. `Assets/Shaders/UIPulseGlow.shader` (new)
A ShaderLab UI shader based on the standard **UI-Default** shader (keeps uGUI
vertex-color, `_ClipRect` masking, and stencil support so it behaves under UI masks).
Modifications:
- Samples `_MainTex` (the soft-glow sprite already used today, reused as the halo
  falloff shape) × the Image's **vertex color** (so each segment glows its own
  accent from a *single shared material* — no per-segment material instancing).
- Multiplies output alpha by a breathing term:
  `lerp(_MinAlpha, _MaxAlpha, 0.5 + 0.5 * sin(_Time.y * _Speed))`.
- Exposed properties: `_MainTex`, `_Speed`, `_MinAlpha`, `_MaxAlpha` (plus the
  standard UI clip/stencil properties inherited from UI-Default).

The breathing is entirely GPU-side (`_Time`); no per-frame C# is required.

### 2. `Assets/Shaders/UIPulseGlow.mat` (new)
Material referencing `UIPulseGlow.shader`, assigned the existing soft-glow sprite as
`_MainTex`, with sensible defaults (e.g. `_Speed ≈ 3`, `_MinAlpha ≈ 0.55`,
`_MaxAlpha ≈ 1.0`). One shared material is used by all segments.

### 3. `StatSegment.cs` (additive edit)
- Add `enum GlowMode { Sprite, Shader }` and `[SerializeField] GlowMode glowMode`
  (**default `Sprite`** — preserves today's behavior byte-for-byte until flipped).
- Add `[SerializeField] GameObject shaderGlow` — the new child glow Image (larger
  rect than the button so the halo bleeds past the edge; material = `UIPulseGlow.mat`).
- Add a guarded `SetShaderGlow(bool)` mirroring the existing `SetGlow`'s self-guard.
- In `SetState`:
  - `Selected`: if `glowMode == Shader` → `SetShaderGlow(true)`, set the shaderGlow
    Image's `color` to the accent (drives per-segment glow color via vertex color),
    and `SetGlow(false)`; else (`Sprite`) → `SetGlow(true)` and `SetShaderGlow(false)`
    (exactly today's behavior).
  - `Available` / `Locked`: both `SetGlow(false)` and `SetShaderGlow(false)`.
- Existing `glow` field, `SetGlow`, and the sprite glow object are untouched.

### 4. Prefab wiring (`ImproviseToggle.prefab`, ×4 segments)
Per option button: add a `shaderGlow` child `Image` (rect padded beyond the button
edge, `Raycast Target` off, material = `UIPulseGlow.mat`, sprite = existing glow
sprite), assign it to `StatSegment.shaderGlow`, leave `glowMode = Sprite`. Done in the
Unity Editor, or via careful prefab-YAML edits — decided at implementation time.

## Data Flow

```
CardInspector.Changed → ChoiceBanner/ImprovisePanel.Render → StatSegment.SetState(Selected)
   └─ glowMode == Shader → shaderGlow.SetActive(true); shaderGlow.Image.color = accent
                           (GPU: alpha breathes via _Time × vertex color)
   └─ glowMode == Sprite → glow.SetActive(true)   (existing, unchanged)
```

## Testing

- **EditMode:** none required — this is presentation-only and the shader/anim is
  GPU-side. `StatSegment` has no pure logic worth a unit test beyond what exists.
- **Manual Play-mode:** open a Choice/Improvise card; with `glowMode = Sprite`, the
  selected segment shows today's static glow. Flip a segment's `glowMode` to `Shader`
  in the Inspector → its selected glow now breathes in the stat's accent color,
  halo softly extending past the button edge; other states show no glow. Flip back →
  identical to today. Console clean.

## Reversibility

The experiment is won or lost by comparing modes in the Inspector. To keep the
shader: leave `glowMode = Shader` (optionally later delete the sprite path). To
discard it: leave/flip `glowMode = Sprite`; delete `UIPulseGlow.shader/.mat`, the
`shaderGlow` children, and the additive `StatSegment` members.
