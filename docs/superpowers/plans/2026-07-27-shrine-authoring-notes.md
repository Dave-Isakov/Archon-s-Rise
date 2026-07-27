# Shrine — Editor Authoring Notes

Everything the shrine feature needs from the Unity editor. All C# is committed and the pure logic is
TDD-green (51/51 via the mcs harness); nothing here has been compiled against Assembly-CSharp or run
in Play mode yet — that's this document.

Work top to bottom. Step 1 gates everything else.

---

## 0. Answering the prefab question up front

**One slot prefab, not five.** `ShrineSlotButton` swaps its `Image.sprite` at runtime from the
panel's 5-entry `bucketSprites` array. A new crystal color later is a sprite, not a prefab.

You author **5 sprites** (Red, Yellow, Green, Purple, Wild) and **1 prefab**.

---

## 1. Recompile

Return to Unity and let it compile + generate `.meta` files for the new scripts:

| File | Purpose |
|------|---------|
| `Assets/Scripts/Shrines/ShrinePaymentRules.cs` | slot-cycle math (pure) |
| `Assets/Scripts/Shrines/ShrineRules.cs` | roll math (pure) |
| `Assets/Scripts/Shrines/ShrineLedger.cs` | per-run shrine state (pure) |
| `Assets/Scripts/Shrines/ArchonsRise.Shrines.asmdef` | their assembly |
| `Assets/Scripts/GameScriptableObjectTypes/ShrineSO.cs` | content type |
| `Assets/Scripts/Managers/ShrineTracker.cs` | scene registry + guardian spawn |
| `Assets/Scripts/GameObjectScripts/GameBoardObjects/ShrineToken.cs` | map token |
| `Assets/Scripts/TilemapScripts/ShrineRuleTile.cs` | ground tile |
| `Assets/Scripts/GameObjectScripts/ShrineMenuScripts/ShrinePanel.cs` | the fan UI |
| `Assets/Scripts/GameObjectScripts/ShrineMenuScripts/ShrineSlotButton.cs` | one slot |

**Expect zero errors.** If anything fails, stop here and send me the console text — everything below
assumes a clean compile.

---

## 2. Rule tile asset

`Assets ▸ Create ▸ ScriptableObjects ▸ Tiles ▸ Shrine Rule Tiles`

- Name it `Shrine Rule Tile`, put it next to `Dungeon Rule Tile.asset`.
- Assign the shrine ground sprite as the **Default Sprite** (one color-agnostic marker; no tiling
  rules needed, same as the hotspot tile).
- Set **`exploreCost`**. Dungeons/hotspots use a flat static cost — hotspot is `3`. Placement
  overwrites the underlying terrain, so this *is* the cell's entry cost. Suggest `3`.
- Set **`terrain`** to whatever the shrine should count as for terrain checks (match the dungeon tile).

---

## 3. Shrine token prefab

Duplicate `Assets/Prefabs/GameTokens/` → the dungeon token prefab, name it `Shrine Token.prefab`.

- Remove `DungeonToken`, add **`ShrineToken`**.
- Leave `shrineSO` and `gridPos` alone — `GridGeneration` assigns both at spawn.
- Three optional marker children, wired to the matching fields:

| Field | Active when | Suggested look |
|-------|-------------|----------------|
| `liveMarker` | shrine unused | the glowing/active shrine art |
| `dormantMarker` | shrine spent | greyed out, no glow |
| `guardingMarker` | guardian alive | a hostile tint or chain/lock motif |

All three are null-safe — wire only what you build. But **wire at least `dormantMarker` or
`guardingMarker`**, or a used shrine looks identical to a fresh one on the board.

- Keep the collider from the dungeon token; `ShrineToken` uses `IPointerClickHandler`.

---

## 4. `ShrineSO` content assets

`Assets ▸ Create ▸ ScriptableObjects ▸ Shrine` — make 2–3 to start.

| Field | Value | Notes |
|-------|-------|-------|
| `id` (inherited) | unique slug, e.g. `shrine_fortune` | **Never rename** — save identity |
| `cardName` | display name | used in the "must be standing at…" message |
| `crystalCost` | `4` | drives the slot count |
| `goodRollChance` | `0.5` | strict less-than; `0.5` is a true coin flip |
| `rewardTypes` | all three | CardPick, Unit, LargeExp |
| `unitPool` | some `UnitsSO` | **required if `Unit` is in `rewardTypes`** — an empty pool silently pays nothing |
| `largeExp` | `15` | the 1× value; the guardian path pays 2× |
| `cardTier` | `3` | reuses the `Rewards` card pools |
| `summonedEnemy` | a **tier-3** `EnemiesSO` | see the warning below |

> **`summonedEnemy` must be in the `EnemyDeck` pool.** `ShrineTracker.SpawnGuardian` looks it up by
> `deck.enemies.IndexOf(...)`; if it's absent it logs a warning and **no guardian spawns** — the
> player pays 4 crystals and gets nothing. Check the `EnemyDeck` component's `enemies` list.

---

## 5. The shrine panel

This is the bulk of the work. Build it in `GameBoard.unity` next to `DungeonPanel`.

### 5a. Hierarchy

```
ShrinePanelRoot            <- GameObject, INACTIVE by default; goes in `root`
├── ClickOffCatcher        <- full-screen transparent Image, Button -> ShrinePanel.Dismiss()
├── FanContainer           <- RectTransform; goes in `fanContainer`
│   ├── (slots spawn here at runtime)
│   └── ConfirmButton      <- the checkmark; MUST be a child of FanContainer
└── HelpIcon               <- see step 6
```

The `ShrinePanel` component itself can live on `ShrinePanelRoot` or a parent — `ShrineToken` finds it
with `FindObjectsInactive.Include`, so an inactive root is fine.

### 5b. Positioning `FanContainer` — simpler than it looks

**You do not need any per-hex math.** The player must be *standing on* the shrine to open it, and the
camera rides `PlayerPosition` — so the shrine is always **dead centre of screen** when the panel is up.

On a Screen Space – Camera canvas: anchor `FanContainer` to centre (0.5, 0.5), then set
`anchoredPosition` to a fixed upward offset so the arc floats over the shrine's head. Something like
`(0, 140)` — tune by eye against your screenshot.

### 5c. `ClickOffCatcher`

- Stretch to fill the canvas, `Image` with **alpha 0**, **Raycast Target ON**.
- Order it as the **first** child so it sits behind the fan.
- `Button.onClick` → `ShrinePanel.Dismiss()`.

> **Watch this one.** Your commit `7c3e0` made the hex pointer ignore world token colliders and
> suppress only on "real UI". The catcher must register as real UI for that check, or a dismissing
> click falls through and dispatches a **move** onto the hex underneath. Test this explicitly:
> open a shrine, click empty space, confirm the player doesn't walk.

### 5d. Slot prefab

New prefab, e.g. `Assets/Prefabs/UI/Shrine Crystal Slot.prefab`:

```
ShrineCrystalSlot          <- RectTransform + Image (the white circle) + Button + ShrineSlotButton
└── CrystalIcon            <- Image; goes in ShrineSlotButton.crystalImage
```

- `ShrineSlotButton.button` → the Button on the root (its `Reset()` auto-fills this if the Button is
  on the same object).
- `crystalImage` → the child `CrystalIcon`. The panel **disables** this Image for an empty slot, so
  the white circle shows alone. Make sure the circle reads as an empty socket on its own.
- Don't add an `onClick` in the prefab — `ShrinePanel` binds it per slot at runtime.
- Size it for the small buttons in your mock (~64px).

### 5e. `ConfirmButton`

- A child of `FanContainer` (its position is set in the same local space as the slots).
- Checkmark sprite, `Button.onClick` → `ShrinePanel.Confirm()`.
- Add a **`CanvasGroup`** and wire it to `confirmGroup` — that's what dims to 0.4 alpha when the
  turn's action is already spent.
- Leave it inactive; the panel activates it when all slots are filled.

### 5f. `ShrinePanel` inspector

| Field | Assign |
|-------|--------|
| `root` | `ShrinePanelRoot` |
| `crystals` | the scene `CrystalInventory` |
| `fanContainer` | `FanContainer` |
| `slotPrefab` | `Shrine Crystal Slot.prefab` |
| `bucketSprites` | **size 5, in this exact order** — see below |
| `confirmButton` | `ConfirmButton` |
| `confirmGroup` | its `CanvasGroup` |
| `fan` | `SpreadDegrees` / `CardSpacing` / `ArcDrop` |

**`bucketSprites` order is load-bearing** — it matches
`CrystalInventory.ShrinePaymentColors` + the trailing wild slot:

```
[0] Red
[1] Yellow
[2] Green
[3] Purple
[4] Wild
```

Get this wrong and slots show the wrong crystal while spending the right one.

**`fan` starting values** (defaults in code; tune to taste):

| Setting | Default | Effect |
|---------|---------|--------|
| `SpreadDegrees` | `0` | `0` keeps crystal icons upright; raise it to fan them like cards |
| `CardSpacing` | `70` | horizontal px between slot centres — cards use 120, buttons are smaller |
| `ArcDrop` | `22` | how far the **edge** slots sit below the centre ones (this is the arc in your mock) |

---

## 6. Help icon

Pure reuse — **no new code**.

1. `Assets ▸ Create ▸ ArchonsRise ▸ Tutorial ▸ Help Entry`
   - `panelId`: `shrine` (**never rename** — it's the `tut.help.shrine` PlayerPrefs key)
   - `title` / `body`: explain the gamble. Embed icons with the canonical
     `<sprite="crystal" index=0>` tags — `TutorialCopyValidationTests` fails on unknown tag names.
2. Drop a `HelpIcon` prefab into the panel, assign the entry to its `entry` field and the `?` glyph to
   `pulseTarget`. Its Button's OnClick → `HelpIcon.OpenEntry`.
3. **Add the entry to `TutorialManager.helpEntries`** in the scene — otherwise Skip/Reset won't clear
   its seen state.

---

## 7. Wire `GridGeneration`

On the `GridGeneration` component:

| Field | Assign | Notes |
|-------|--------|-------|
| `shrineTile` | `Shrine Rule Tile` | |
| `shrineTokenPrefab` | `Shrine Token.prefab` | |
| `shrinePool` | your `ShrineSO` assets | placement is skipped entirely if empty |
| `shrineCount` | `3` | tuning |
| `shrineMinSpacing` | `4` | tuning |

All three of tile/prefab/pool must be set or the whole shrine block no-ops silently.

---

## 8. Acceptance play-through

Work down this list; each line is a distinct failure mode.

**Placement**
1. New run → shrines appear, spaced, never on towns/dungeons/hotspots or the start ring.
2. Hover one → tooltip reads `Shrine <crystal>4 — gamble`.
3. No enemy ever spawns on a shrine tile.

**The fan**
4. Walk adjacent, click the shrine → the player **moves** onto it (doesn't open).
5. Standing on it, click → the fan arcs overhead, 4 empty circles.
6. Click a slot repeatedly → it cycles only through colors you actually hold, then to empty, then round again.
7. Hold 2 Red only, set two slots to Red → a third slot **will not offer Red**.
8. Hold nothing → clicking a slot does nothing (no error).
9. Wild crystals appear as their own cycle entry, distinct from the colors.
10. Fill all 4 → the checkmark appears on an appended fan seat and the arc re-centres.
11. Cycle one slot back to empty → the checkmark disappears and the arc re-centres back.
12. **Click empty space → the fan closes, no crystals spent, no action spent, and the player does not move.** (the 5c risk)
13. Reopen → all slots are empty again.

**Resolution**
14. Confirm → exactly the crystals you picked fly away. Pick a wild deliberately and confirm a *wild* is consumed, not a color.
15. Confirm spends the turn's action.
16. Already acted this turn → slots still fill, checkmark appears **dimmed and unclickable**.
17. Force both outcomes (temporarily set `goodRollChance` to `1` then `0`):
    - **good** → 1× reward immediately; shrine → spent; tooltip reads "spent"; clicking it says so.
    - **bad** → a tier-3 guardian appears on a neighbouring cell; shrine → guarding; tooltip updates.
18. Guardian spawns on a *free* cell — not on the player, not on another enemy, not off-map.

**Guardian**
19. Defeat it → **2×** the reward + its defeat exp, **no crystal/card rolls of its own**.
20. Reward ordering: the defeat message resolves *before* any shrine card pick.
21. Shrine → spent after the guardian dies.
22. Flee it → 1 wound, guardian stays, shrine stays guarding; return later and it still pays.

**Persistence**
23. Save while a shrine is guarding (guardian alive) → reload → guardian restores **and still pays 2×** on defeat.
24. A consumed shrine stays consumed; an untouched shrine stays live.
25. Load an older save → it migrates to v10 without error and existing enemies are unaffected.

---

## Known gaps (deliberate, not bugs)

- **A shrine unit reward bypasses the army cap.** `GrantShrineReward` calls `AddUnit` directly, so a
  bonus unit can exceed the cap rather than routing through the disband-to-hire flow. Flagged as a
  tuning follow-up in the plan; say the word and I'll route it through the recruit path.
- **`crystalCost` other than 4 is untested visually.** The fan solves for any count, but the arc was
  tuned against 4.
- **No animation on the arc re-centre.** Slots snap when the checkmark appears. Tweening `Place()` is
  a small follow-up if it reads as jumpy.
