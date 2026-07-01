# StatSegment Shader Pulse-Glow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a hand-authored breathing-pulse UI shader as a *toggleable, parallel* alternative to the existing static sprite glow on `StatSegment`, so the two glow looks can be compared in the Editor and the loser deleted later.

**Architecture:** A ShaderLab UI shader (derived from Unity's UI-Default) renders the segment's soft-glow sprite with an alpha that breathes via `_Time`, tinted by the Image's vertex color so one shared material serves all four segments. `StatSegment` gains an additive `GlowMode { Sprite, Shader }` enum (default `Sprite`) and a `shaderGlow` child reference; `SetState(Selected)` lights whichever path the enum picks. The existing `glow` field, `SetGlow`, and sprite glow object are left fully intact.

**Tech Stack:** Unity 6 (Assembly-CSharp), URP + uGUI (Canvas/Image/Button/TextMeshPro), ShaderLab/HLSL (CGPROGRAM, UI-Default lineage). No Shader Graph. No new packages.

## Global Constraints

- **Experiment, fully reversible.** Do not remove or alter the existing glow path: the `glow` serialized field, the `SetGlow` method, or the sprite glow child objects all stay. (Spec: "Nothing in the current glow path is removed.")
- **`GlowMode` defaults to `Sprite`** so current behavior is byte-for-byte preserved until the enum is flipped. (Spec: Components §3.)
- **Select-only.** No hover / `IPointerEnter/Exit`. (Spec: Scope — hover deferred.)
- **Per-segment toggle.** The `GlowMode` enum lives on each `StatSegment`; no shared/global config. (Spec: Scope.)
- **Presentation only.** No change to selection logic, `CardPlaySelection`, `CardInspector`, `ChoiceBanner`/`ImprovisePanel` behavior, `StatPalette`, or the Phase 3a float/scrim. (Spec: Scope — out of scope.)
- **One shared material, per-segment color via vertex color** — do not instance a material per segment. (Spec: Components §1.)
- Each commit message ends with the project trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## File Structure

- `Assets/Shaders/UIPulseGlow.shader` — **new**. The breathing UI shader (UI-Default + `_Time` alpha pulse). One responsibility: draw a sprite as a uGUI-compatible transparent quad whose alpha oscillates.
- `Assets/Materials/UIPulseGlow.mat` — **new** (Editor-created). Material bound to the shader, `_MainTex` = the existing soft-glow sprite, tuned defaults. Shared by all segments.
- `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs` — **modify** (additive). Adds `GlowMode`, `glowMode`, `shaderGlow`, `SetShaderGlow`, and the mode branch in `SetState`.
- `Assets/Prefabs/ImproviseToggle.prefab` — **modify** (Editor). Adds one `shaderGlow` child Image per segment, assigns the material and the `StatSegment.shaderGlow` field.

> **Testing note:** This is presentation-only — the pulse is GPU-side and `StatSegment.SetState` only toggles GameObjects and sets `Image` colors on live scene components. There is no pure logic to unit-test (the existing codebase has no EditMode test for `StatSegment`, consistent with the Phase 3a editor/visual tasks). Verification is **manual Play-mode**, as in the Phase 3a plan's editor tasks.

---

## Task 1: UIPulseGlow shader + material

**Files:**
- Create: `Assets/Shaders/UIPulseGlow.shader`
- Create (Editor): `Assets/Materials/UIPulseGlow.mat`
- Test: Shader compiles clean; manual (no unit test).

**Interfaces:**
- Produces:
  - A shader named `"UI/PulseGlow"` with float properties `_Speed`, `_MinAlpha` (Range 0–1), `_MaxAlpha` (Range 0–1), plus the standard UI `_MainTex`/`_Color`/stencil/clip properties. Alpha output = `sprite.a * vertexColor.a * lerp(_MinAlpha, _MaxAlpha, 0.5 + 0.5*sin(_Time.y*_Speed))`.
  - A material `Assets/Materials/UIPulseGlow.mat` using that shader, consumed by Task 3's `shaderGlow` Images.

- [ ] **Step 1: Create the shader file**

Create `Assets/Shaders/UIPulseGlow.shader` with exactly this content (it is Unity's UI-Default shader with a `_Time`-driven alpha pulse added in `frag`, so it keeps uGUI vertex color, rect-mask clipping, and stencil masking):

```shaderlab
Shader "UI/PulseGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // Breathing pulse controls (GPU-side; no per-frame C#).
        _Speed ("Pulse Speed", Float) = 3
        _MinAlpha ("Min Alpha", Range(0,1)) = 0.55
        _MaxAlpha ("Max Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Speed;
            float _MinAlpha;
            float _MaxAlpha;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // Breathing pulse: oscillate the whole quad's alpha over time.
                float pulse = lerp(_MinAlpha, _MaxAlpha, 0.5 + 0.5 * sin(_Time.y * _Speed));
                color.a *= pulse;

                return color;
            }
        ENDCG
        }
    }
}
```

- [ ] **Step 2: Let Unity import and verify the shader compiles**

Switch to the Unity Editor (it auto-imports on focus). Select `Assets/Shaders/UIPulseGlow.shader` in the Project window and confirm the Inspector shows the shader with **no compile errors** (no red error box; the property list shows Pulse Speed / Min Alpha / Max Alpha). Check the Console is clean.

Expected: shader compiles; `UI/PulseGlow` appears under the shader dropdown menu.

- [ ] **Step 3: Create the material (Editor)**

In the Project window:
1. Create the folder `Assets/Materials` if it does not exist (right-click → Create → Folder).
2. Right-click `Assets/Materials` → **Create → Material**, name it `UIPulseGlow`.
3. In its Inspector, set **Shader → UI/PulseGlow**.
4. Leave **_MainTex (Sprite Texture)** empty — it is `[PerRendererData]` (greyed out by design); the sprite is supplied per-renderer by the `ShaderGlow` Image's Source Image in Task 3, not by the material.
5. Leave `Pulse Speed = 3`, `Min Alpha = 0.55`, `Max Alpha = 1`, `Tint = white`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Shaders/UIPulseGlow.shader" "Assets/Shaders/UIPulseGlow.shader.meta" "Assets/Materials/UIPulseGlow.mat" "Assets/Materials/UIPulseGlow.mat.meta" "Assets/Materials.meta" "Assets/Shaders.meta"
git commit -m "feat: UIPulseGlow breathing UI shader + shared material

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

> If some `.meta` paths above don't exist yet (Unity generates them on import), run the `git add` after switching to the Editor once so imports complete; `git add` silently skips paths that aren't present, so re-run it, then commit.

---

## Task 2: StatSegment toggleable glow path (additive)

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs`
- Test: Compile clean; manual Play-mode (in Task 3).

**Interfaces:**
- Consumes: `Assets/Materials/UIPulseGlow.mat` (Task 1) — applied to the `shaderGlow` Image in Task 3, not referenced from code.
- Produces:
  - `enum StatSegment.GlowMode { Sprite, Shader }` and serialized `glowMode` (default `Sprite`).
  - Serialized `GameObject shaderGlow`.
  - `SetState` behavior: `Selected` lights the sprite glow (`SetGlow(true)`) when `glowMode == Sprite`, or the shader glow (`SetShaderGlow(true)` + accent color on the shaderGlow Image) when `glowMode == Shader`; the unused path is turned off. `Available`/`Locked` turn both off. Consumed by `ChoiceBanner`/`ImprovisePanel` (unchanged callers of `SetState`).

- [ ] **Step 1: Rewrite StatSegment.cs with the additive parallel path**

Replace the entire contents of `Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs` with (existing sprite path preserved verbatim; new members marked):

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One Choice/Improvise option button. Renders Selected / Available / Locked from
// StatPalette so selection reads as colour + glow, never as a greyed-out button.
public class StatSegment : MonoBehaviour
{
    public enum State { Selected, Available, Locked }

    // EXPERIMENT: which glow visual lights on Selected. Sprite = the original static
    // glow sprite (unchanged). Shader = the UIPulseGlow breathing halo. Defaults to
    // Sprite so behaviour is unchanged until flipped per-segment in the Inspector.
    public enum GlowMode { Sprite, Shader }

    [SerializeField] StatType stat;
    [SerializeField] Image background;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] Button button;
    [SerializeField] GameObject glow;          // optional outer-glow object, lit only when Selected

    [Header("Shader-glow experiment")]
    [SerializeField] GlowMode glowMode = GlowMode.Sprite; // default preserves current look
    [SerializeField] GameObject shaderGlow;    // optional child Image w/ UIPulseGlow.mat, lit only when Selected & mode == Shader

    public StatType Stat => stat;
    public Button Button => button;

    // glow is an optional separate child object. Guard against it being mis-wired to the
    // segment's own GameObject: SetActive(false) on self would deactivate the whole
    // segment (and silently hide any available-but-unselected choice).
    void SetGlow(bool on) { if (glow != null && glow != gameObject) glow.SetActive(on); }

    // Same self-guard as SetGlow for the parallel shader-glow object.
    void SetShaderGlow(bool on) { if (shaderGlow != null && shaderGlow != gameObject) shaderGlow.SetActive(on); }

    public void SetState(State state)
    {
        Color accent = StatPalette.For(stat);
        switch (state)
        {
            case State.Selected:
                background.color = accent;
                label.color = new Color(0.05f, 0.07f, 0.09f, 1f); // dark text on bright fill
                button.interactable = true;
                if (glowMode == GlowMode.Shader)
                {
                    SetGlow(false);
                    SetShaderGlow(true);
                    // Drive per-segment glow colour through the Image's vertex colour so
                    // one shared UIPulseGlow material tints to each stat's accent.
                    if (shaderGlow != null && shaderGlow != gameObject)
                    {
                        var img = shaderGlow.GetComponent<Image>();
                        if (img != null) img.color = accent;
                    }
                }
                else
                {
                    SetShaderGlow(false);
                    SetGlow(true);
                }
                break;

            case State.Available:
                background.color = new Color(accent.r, accent.g, accent.b, 0.14f);
                label.color = StatPalette.Muted;
                button.interactable = true;
                SetGlow(false);
                SetShaderGlow(false);
                break;

            case State.Locked:
                background.color = new Color(StatPalette.Locked.r, StatPalette.Locked.g, StatPalette.Locked.b, 0.40f);
                label.color = StatPalette.Locked;
                button.interactable = false;
                SetGlow(false);
                SetShaderGlow(false);
                break;
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Switch to the Unity Editor and let it recompile. Confirm the **Console has no compile errors**. Select a `StatSegment` in the prefab/scene and confirm the Inspector now shows a **Glow Mode** dropdown (defaulting to `Sprite`) and a **Shader Glow** object slot under the "Shader-glow experiment" header.

Expected: clean compile; existing `stat`/`background`/`label`/`button`/`glow` fields still populated (additive change doesn't clear them).

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/CardMenuScripts/Sections/StatSegment.cs"
git commit -m "feat: StatSegment toggleable shader-glow path (default Sprite, unchanged)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Prefab wiring + Play-mode comparison

**Files:**
- Modify (Editor): `Assets/Prefabs/ImproviseToggle.prefab` (the 4 segment buttons: Attack / Defend / Influence / Explore)
- Test: Manual Play-mode verification.

**Interfaces:**
- Consumes: `StatSegment.glowMode` / `StatSegment.shaderGlow` (Task 2), `Assets/Materials/UIPulseGlow.mat` (Task 1).
- Produces: each segment has a `ShaderGlow` child Image wired to `StatSegment.shaderGlow`; comparison-ready.

- [ ] **Step 1: Add a ShaderGlow child Image to each segment (Editor)**

Open `Assets/Prefabs/ImproviseToggle.prefab` in Prefab Mode. For **each** of the 4 option buttons (Attack, Defend, Influence, Explore — each carrying a `StatSegment`):
1. Duplicate the existing glow child Image (so the new one inherits the same soft-glow sprite, rect, and off-by-default a good starting point) **or** create a new UI → Image child. Name it `ShaderGlow`.
2. Set its **Source Image** to the existing soft-glow sprite (guid `efee20b64d1e73942a29ba61ce3379c3`) if not already inherited.
3. Set its **Material** to `Assets/Materials/UIPulseGlow.mat`.
4. Make its `RectTransform` **larger than the button** (e.g. stretch with negative offsets / add ~20–40px padding on all sides) so the halo bleeds past the button edge.
5. Turn **Raycast Target off**.
6. Order it **behind** the button fill/label (as the sibling index of the current glow) so it reads as a backing halo.
7. Set the child **inactive** (unchecked) — `SetState` controls activation at runtime.

- [ ] **Step 2: Wire the field and leave the mode on Sprite (Editor)**

On each segment's `StatSegment` component:
1. Assign the new `ShaderGlow` object to the **Shader Glow** slot.
2. Leave **Glow Mode = Sprite** for now.
Confirm the existing **glow** field still points at the original sprite glow object (unchanged).

- [ ] **Step 3: Play-mode verification — Sprite mode unchanged**

Enter Play mode, open a Choice/Improvise card, and select segments:
1. With every segment on `glowMode = Sprite`, the selected segment shows **exactly today's static glow**; hover/other states unchanged; Console clean.
2. Confirm no `ShaderGlow` object is visible (it stays inactive in Sprite mode).

Expected: byte-for-byte the current behavior (regression guard for the additive edit).

- [ ] **Step 4: Play-mode verification — Shader mode breathes**

Exit Play mode. On one segment (e.g. Attack), set **Glow Mode = Shader**. Re-enter Play mode:
1. Select that segment → its glow now **breathes** (alpha oscillates ~0.55→1.0), tinted the stat's accent color (red for Attack), the halo softly extending past the button edge.
2. Deselect / pick another stat → the shader glow turns off; `Available`/`Locked` show no glow.
3. Flip the segment back to `Sprite` → identical to Task 3 Step 3. Console clean throughout.
4. (Optional tuning) adjust `UIPulseGlow.mat`'s `Pulse Speed` / `Min Alpha` / `Max Alpha` live in Play mode to taste; copy the values back after exiting Play mode if changed.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Prefabs/ImproviseToggle.prefab"
git commit -m "feat: wire per-segment ShaderGlow child for glow-mode comparison

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:**
- Hand-authored breathing UI shader, URP/uGUI-compatible, `_Time` alpha, vertex-color tint, one shared material → Task 1 (shader + material). ✅
- Additive `GlowMode { Sprite, Shader }` (default `Sprite`) + `shaderGlow`, `SetState` mode branch, existing sprite path untouched → Task 2. ✅
- New `shaderGlow` child Image per segment, halo bleeds past edge, wiring → Task 3. ✅
- Select-only (no hover / `IPointerEnter/Exit`) → honored; no pointer handlers anywhere. ✅
- Per-segment toggle (no shared/global config) → `glowMode` is a per-`StatSegment` field. ✅
- Reversibility (flip enum back; delete shader/mat/children/members) → default `Sprite` + Task 3 Step 3 regression check. ✅
- Out of scope honored: no change to selection logic, `CardPlaySelection`, `CardInspector`, panels, `StatPalette`, Phase 3a float/scrim. ✅

**Placeholder scan:** No "TBD"/"handle edge cases"/"write tests for the above". Shader and `StatSegment` steps show full code; Editor/material/prefab steps list concrete objects, the exact sprite guid, and exact field assignments. The "no unit test" decision is stated explicitly with rationale (GPU + live-component presentation), matching the Phase 3a plan's editor/visual tasks. ✅

**Type consistency:** `GlowMode { Sprite, Shader }`, `glowMode`, `shaderGlow`, `SetShaderGlow(bool)`, `SetGlow(bool)`, `SetState(State)`, `StatPalette.For(StatType)→Color` — names identical across Tasks 2 and 3 and match the existing file. Shader property names `_Speed`/`_MinAlpha`/`_MaxAlpha` consistent between Task 1 Step 1 (shader) and Step 3 / Task 3 Step 4 (material tuning). Material path `Assets/Materials/UIPulseGlow.mat` consistent across Tasks 1 and 3. ✅

**Ordering / dependency check:** Task 2 is independent of Task 1 (no code reference to the material). Task 3 consumes both. Shader/material first, code second, prefab wiring + verification last — Editor availability flows correctly. ✅
