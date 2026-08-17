# Unity Skills Extraction — Design

**Date:** 2026-08-16
**Status:** Approved

## Purpose

Extract the transferable Unity knowledge built up in Archon's Rise into a family of
user-level skills, so a new isometric-tactics project on a square grid starts with the
working process and the solved problems instead of rediscovering them.

The new project shares the *shape* of several problems — tilemap generation, unit movement
on a grid, cell highlighting, tooltips, saving, undo, multi-device input — but not the
architecture. Nothing here presumes the new game copies Archon's Rise.

## Scope Decisions

Four decisions bound this work. They were settled before design and are not open questions.

**Extracted only.** Every claim traces to shipped code in this repo or to a memory file
written from a real debugging pass. Nothing is written from general Unity knowledge or
inferred from the hex map onto a square one. Areas the new game needs but this project never
built are named as gaps, not filled with guesses.

**Facts separated from decisions.** The sorting axis is *"is this a Unity truth, or an
Archon's Rise choice?"* — not topic. Unconditional truths are stated plainly. Knowledge that
holds only under an architectural choice is written condition-first: "IF you chose X, THEN
this bites you." Game-design taste is excluded entirely.

**User-level install.** Skills go to `C:\Users\Dave's Comp\.claude\skills\`, available in the
new project the moment it exists. Triggers must be Unity-specific enough not to misfire in
non-Unity projects.

**Portable code where the work was real.** The grid skill carries liftable, tested source,
not just prose. The other three carry prose, checklists, and tooling.

## Cross-Cutting Authoring Rules

These apply to every file in every skill.

**Discovery over hardcoded paths.** The new project will use a different Unity version and
different assembly names. Skills teach the discovery — `ls -d .../Editor/*/Editor/Data/
MonoBleedingEdge/bin | tail -1` — never a literal `6000.5.1f1`. Any tooling shipped resolves
its own paths at runtime.

**Archon's Rise appears as evidence, never as instruction.** File paths from this repo are
cited to show a claim was learned somewhere real. They are never presented as paths the new
project should have.

**Structural, not nominal.** Patterns are described by their shape and their seam, so they
survive different namespaces, assembly names, and grid topology.

**Every skill ends with a gap list.** An explicit statement of what the skill does not know,
so it is never mistaken for complete coverage of its topic.

## Skill 1 — `unity-workflow`

The highest-value extraction: how to actually operate Unity as an agent on this machine.

**`SKILL.md`** — The editor holds the project lock, so `-runTests -batchmode` and headless
builds both fail whenever it is open, which is nearly always. Check for a running Unity
process before attempting either. The three-tier verification ladder is the core contract:
pure-class test (seconds) → Roslyn compile-check of the MonoBehaviour assemblies (~40s) →
user acceptance in the editor. Work is not done before tier three. Also carries the editor
handoff protocol (scene and prefab changes are written as numbered in-editor checklists for
the user to execute; `.unity` YAML is never hand-edited and fileIDs are never invented) and
the comment discipline (specs carry the reasoning; code stays sparse).

**`testing.md`** — The Mono `mcs` pure-class harness. `mcs.bat` works only when every path
passed to it is relative to the repo root, which is why `tools/pure-tests/run.sh` works and
absolute-path invocations fail on spaces; the alternative is invoking `mcs.exe` under
`mono.exe` directly. `-langversion:latest` is mandatory — mcs defaults to C# 7.0 and rejects
`readonly struct`. The built exe must run under `mono.exe`, not bare, or it links the wrong
mscorlib. `nunit.framework.dll` must be copied next to the built test DLL for test discovery.

**`compiling.md`** — Verifying MonoBehaviour edits compile while the editor holds the lock.
Drive Unity's own Roslyn `csc.dll` over the generated `.csproj` files. Sources from
`<Compile Include>`, references from `<Reference><HintPath>`, and `<ProjectReference>`
resolved to built output — omitting the last is the usual cause of a CS0246 flood on your own
types. Arguments go through response files, one quoted path per line, with `&apos;` decoded
first. Build the whole graph topologically from source into one scratch dir rather than
resolving to a stale `Library/ScriptAssemblies`, or freshly-added members report false errors
at their call sites.

**`assemblies.md`** — Pure logic classes each get a dedicated folder asmdef, and the test
asmdef must list it in `references` or tests fail with CS0103. MonoBehaviours stay in the main
assembly, because an asmdef cannot reference `Assembly-CSharp` back.

**`testable-design.md`** — The two architecture rules that make the ladder possible, framed as
testability enablers rather than aesthetics. First: decisions live in `UnityEngine`-free
static classes taking primitives and returning a `readonly struct` verdict — the worked
examples are `HexActionRules`, `UndoGate`, `TileDescriptor`, `MapCameraRules`, and the
87-file pure test suite that shape produced. Second: never key game logic off scene-graph
state or FX timing; keep a logical model. The evidence is the `childCount == 1` combat-close
check that broke when a dissolving card lingered half a second, and the fix of banking kills
immediately while treating FX as presentation only.

**Shipped assets** — genericized `run.sh`, `Runner.cs`, and `staged-compile-check.ps1`, with
paths discovered rather than hardcoded.

## Skill 2 — `unity-grid-map`

Knowledge plus liftable, tested source. The generation and placement work does not need
redoing.

**Portable code.** `SpawnRules.cs` (168 pure lines) is the payload: min-spacing scatter
(`SeedZones`), radius flood (`CellsWithin`), spacing, starter quarantine, tier-weighted
picking. Everything above the topology layer is already grid-agnostic. The topology-specific
members — `HexNeighbors`, `Spacing`, `SharesApproachHex` — become an injected seam. Also
ported: the `Cell` struct, `GridActionRules` (genericized from `HexActionRules`), and the
existing pure NUnit tests, so the grid layer lands green and gives the workflow skill's
harness something real to run on day one.

The seam ships with the hex implementation as its worked example. The square/isometric
implementation is left as a named signature to fill in — writing an untested square topology
here would violate the extracted-only rule, and it is a few lines against a seam that is
already proven.

**Prose.** Generation is seeded from a `System.Random` built on the saved run seed, so
derived data is regenerated rather than saved (`ZoneCells` is the example). Terrain is split
across layered tilemaps with fog as its own layer. RuleTile subclasses carry gameplay data,
so a tile asset answers questions about itself. A comfort radius around the spawn remaps
hostile terrain rolls, because the start sits in a corner with two exits and one bad roll
could wall a new player in. Movement is an entry-cost model against a pool. Pointer-to-cell
resolution sits behind an interface. One classification verdict drives cursor, tooltip, and
action together, rather than three code paths answering the same question separately.

**Gaps.** No pathfinding and no move-range flood fill — this project only ever needed
adjacency. No isometric depth sorting. No line of sight. No turn order or initiative.

## Skill 3 — `unity-ui-input`

Facts and traps, with conditionals clearly labeled. Input folds in here rather than shipping
a separate skill that would be mostly disclaimer.

**Unconditional.** `GameEventListener` registers in `OnEnable` and unregisters in
`OnDisable`, so any object that hides itself with `SetActive(false)` unsubscribes its own
listeners and can never receive the event that would re-show it; the fix is revive-before-raise
from a controller, or hosting the listener on an always-active parent. The invisible-UI
diagnostic ladder — inherited alpha, then `cull`/`materialCount`, then screen rect, then the
Frame Debugger — with its two false-reading traps: sampling in `LateUpdate` reads pre-rebuild
noise, and `Canvas.renderOrder` is an emission index, not draw order. TMP tints sprite glyphs
by multiplying source texels, so only white-or-near-white art on alpha is tintable, and no
importer setting converts painted art into tintable art. UnityEvent inspector wiring offers a
Dynamic and a Static group; picking Static serializes a fixed argument, so the listener fires
with 0 forever regardless of payload.

**Conditional, condition stated first.** If canvases are Screen Space - Camera: a UI
element's `transform.position` is world space, root canvas RectTransforms are driven every
frame, and placement math must be done in screen pixels then projected with
`RectTransformUtility.ScreenPointToWorldPointInRectangle`. If input is organized as one
semantic action map plus a context enum: surfaces add context values rather than per-screen
action maps, and any modal context needs a guarded setter, because unconditional writers
elsewhere silently released the map context and left every gate open. If modals are
serialized through a FIFO queue: a modal must close *before* calling its completion callback,
because the queue advances synchronously inside that call and a modal closing afterward tears
down its own successor.

**Gaps.** No touch or mobile input of any kind — this project has none. No on-screen
controls, no device-switching beyond keyboard and gamepad, no isometric-specific UI.

## Skill 4 — `unity-persistence`

**Unconditional.** Schema versioning discipline: a bump touches the model, the migrator, the
capture/restore path, and every existing migrator test, because they all assert the current
version number — a bump breaks assertions in files that have nothing to do with the new
field. `JsonUtility` yields null for any key missing from an older file, so every added field
needs a null-guard in the migrator and an empty default in the model. `persistentDataPath` is
derived from `companyName`, making it load-bearing: changing it orphans every existing save,
so it should be set deliberately before anyone has one. Prefer seed plus delta over
serializing generated content. Content references need stable IDs through a registry, not
asset paths.

**Conditional.** If undo is a snapshot stack: gate it on commit points rather than clearing
eagerly. The evidence is a bug where a cost validated at open and paid later could be driven
negative by undoing the card that funded it, and the over-correction of clearing the stack at
fight-open, which broke the contract that a Defend stays undoable until the counterattack
lands.

**Gaps.** No mid-battle tactical state persistence. No cloud saves. No save-file encryption
or tamper resistance.

## Not Extracted

Game-design taste is excluded: the over-the-head fan UI, toast-versus-modal feedback routing,
icon-only buttons, click-off dismissal, sprite-tag stat iconography. These are real
preferences for a hex deckbuilder and would have the new project inheriting opinions before
it forms its own.

Archon's Rise mechanics — cards, crystals, doom clock, wounds, influence — are out of scope,
as is anything covered by the two existing project skills, which stay in this repo.

## Success Criteria

1. Four skills exist under the user-level skills directory and load in a fresh project.
2. Every factual claim traces to a repo file or memory file; nothing is inferred.
3. Conditional knowledge states its condition before its content.
4. No hardcoded Unity version or absolute install path in any shipped tooling.
5. The grid skill's ported code compiles and its tests pass under the harness the workflow
   skill describes, verified from a scratch directory with no reference to this repo's
   assemblies — the new project does not exist yet, so standalone compilation is the proxy
   for portability.
6. Every skill ends with an explicit gap list.
