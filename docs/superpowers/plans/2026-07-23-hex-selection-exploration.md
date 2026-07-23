# Hex-Selection Exploration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the six-arrow `DirectionButton` movement placeholder with a model where hexes themselves are hover/click targets — adjacent move (undoable), fog scout (confirm-click), distant entry-cost info, and a fully-built teleport card targeting any visible hex — all gated only by Explore cost.

**Architecture:** A pure `HexActionRules` decision layer (EditMode-testable) classifies the pointed cell relative to the player. A `HexInteractor` MonoBehaviour resolves the pointed cell from a swappable `IHexPointerSource` (mouse now, controller later), gathers cell facts, calls the rules, drives a tooltip + highlight, and dispatches actions to an `ExplorationController` that absorbs the movement/scouting/teleport execution formerly in `DirectionButton`.

**Tech Stack:** Unity 6000.5.1f1, C# (Assembly-CSharp + per-feature asmdefs), Unity Tilemaps (hex, offset coords), the existing `PlayManager` undo/command stack, ScriptableObject `GameEvent` assets, NUnit EditMode tests verified via the MonoBleedingEdge `mcs` harness.

## Global Constraints

- **No impassable terrain.** Every in-bounds cell is enterable; movement is gated only by Explore cost. The only non-target is a cell off the generated 20×20 map (no tile on any terrain tilemap).
- **Terrain entry costs:** plains/forest/desert per their tile asset; **Water = 5, Mountain = 4**. Fog scout = flat **2**.
- **Move onto revealed terrain is undoable** (`MoveCommand`). **Fog scouting is irreversible** (reveals knowledge → `ClearStack` commit) and takes a **confirming second click**. **Teleport is undoable** (targets are visible-only).
- **Teleport is Explore-phase movement**, never the turn's Action. Landing adjacent to a visible enemy must arm combat identically to a walked step (via the `sendNewPositionOfPlayer` event → `EnemyToken.CheckAggro`); the subsequent fight is the Action.
- **Phase gate stays at dispatch:** `ExplorationController` refuses move/scout/teleport unless `TurnPhaseController.Instance.CanMove`, reusing the message `"You can only move during the Explore phase."`.
- **Mouse-first, controller-ready:** all pointer input flows through `IHexPointerSource`; no controller implementation this pass.
- **Pure-class test convention:** `HexActionRules` lives in its own asmdef; MonoBehaviours stay in Assembly-CSharp (which auto-references all asmdefs). EditMode tests can't run in batch mode while the editor is open — verify via the `mcs` harness (see Task 1).
- **Scene/prefab/asset wiring is user work.** The assistant never hand-edits scene or prefab YAML. Manual steps are collected in Task 7.

## Reference paths (this machine)

- `mcs`: `C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mcs.bat`
- `mono`: `C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mono.exe`
- nunit dll: `Library/PackageCache/com.unity.ext.nunit@44f7d31723bd/net472/unity-custom/nunit.framework.dll`
- Scratchpad: `C:/Users/DAVE'S~1/AppData/Local/Temp/claude/c--Users-Dave-s-Comp-source-repos-Archon-s-Rise/aaa188ed-71ec-4e0e-a135-b8f518b3835d/scratchpad`

---

## Task 1: `HexActionRules` pure decision layer

**Files:**
- Create: `Assets/Scripts/Exploration/ArchonsRise.Exploration.asmdef`
- Create: `Assets/Scripts/Exploration/HexActionRules.cs`
- Create: `Assets/Scripts/Exploration/Tests/ArchonsRise.Exploration.Tests.asmdef`
- Create: `Assets/Scripts/Exploration/Tests/HexActionRulesTests.cs`

**Interfaces:**
- Produces:
  - `enum HexActionKind { None, OffMap, DistantInfo, DistantFog, Move, ScoutFog, EnemyFight, TeleportTarget }`
  - `readonly struct HexAction { HexActionKind Kind; int Cost; bool Affordable; bool RequiresConfirm; }` with ctor `HexAction(HexActionKind kind, int cost, bool affordable, bool requiresConfirm)`
  - `static HexAction HexActionRules.Resolve(bool isSameCell, bool hasTerrain, int entryCost, bool isAdjacent, bool isFog, bool enemyOnCell, int explorePool, int fogCost, bool teleportMode)`

- [ ] **Step 1: Create the feature asmdef**

Create `Assets/Scripts/Exploration/ArchonsRise.Exploration.asmdef`:

```json
{
    "name": "ArchonsRise.Exploration",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

(`noEngineReferences: true` — the rules use only primitives, so no `UnityEngine` dependency; this keeps the module trivially compilable by the `mcs` harness.)

- [ ] **Step 2: Write the failing test**

Create `Assets/Scripts/Exploration/Tests/ArchonsRise.Exploration.Tests.asmdef`:

```json
{
    "name": "ArchonsRise.Exploration.Tests",
    "rootNamespace": "ArchonsRise.Exploration.Tests",
    "references": [
        "ArchonsRise.Exploration",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [ "Editor" ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [ "nunit.framework.dll" ],
    "autoReferenced": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Create `Assets/Scripts/Exploration/Tests/HexActionRulesTests.cs`:

```csharp
using NUnit.Framework;

public class HexActionRulesTests
{
    // Convenience: normal-mode Resolve with fogCost 2 unless overridden.
    static HexAction Normal(bool isSameCell, bool hasTerrain, int entryCost, bool isAdjacent,
        bool isFog, bool enemyOnCell, int explorePool)
        => HexActionRules.Resolve(isSameCell, hasTerrain, entryCost, isAdjacent, isFog,
            enemyOnCell, explorePool, fogCost: 2, teleportMode: false);

    [Test]
    public void SameCell_ReturnsNone()
    {
        var a = Normal(isSameCell: true, hasTerrain: true, entryCost: 1, isAdjacent: false,
            isFog: false, enemyOnCell: false, explorePool: 9);
        Assert.AreEqual(HexActionKind.None, a.Kind);
    }

    [Test]
    public void NoTerrain_ReturnsOffMap()
    {
        var a = Normal(false, hasTerrain: false, 0, false, false, false, 9);
        Assert.AreEqual(HexActionKind.OffMap, a.Kind);
    }

    [Test]
    public void DistantRevealedTerrain_ReturnsDistantInfoWithCost()
    {
        var a = Normal(false, true, entryCost: 4, isAdjacent: false, isFog: false, false, 9);
        Assert.AreEqual(HexActionKind.DistantInfo, a.Kind);
        Assert.AreEqual(4, a.Cost);
    }

    [Test]
    public void DistantFog_ReturnsDistantFog()
    {
        var a = Normal(false, true, 4, isAdjacent: false, isFog: true, false, 9);
        Assert.AreEqual(HexActionKind.DistantFog, a.Kind);
    }

    [Test]
    public void AdjacentEnemy_ReturnsEnemyFight()
    {
        var a = Normal(false, true, 1, isAdjacent: true, isFog: false, enemyOnCell: true, 9);
        Assert.AreEqual(HexActionKind.EnemyFight, a.Kind);
    }

    [Test]
    public void AdjacentFog_Affordable_ReturnsScoutFogNeedsConfirm()
    {
        var a = Normal(false, true, 4, isAdjacent: true, isFog: true, false, explorePool: 2);
        Assert.AreEqual(HexActionKind.ScoutFog, a.Kind);
        Assert.AreEqual(2, a.Cost);
        Assert.IsTrue(a.Affordable);
        Assert.IsTrue(a.RequiresConfirm);
    }

    [Test]
    public void AdjacentFog_Unaffordable_ReturnsScoutFogNotAffordable()
    {
        var a = Normal(false, true, 4, isAdjacent: true, isFog: true, false, explorePool: 1);
        Assert.AreEqual(HexActionKind.ScoutFog, a.Kind);
        Assert.IsFalse(a.Affordable);
    }

    [Test]
    public void AdjacentTerrain_Affordable_ReturnsMoveNoConfirm()
    {
        var a = Normal(false, true, entryCost: 3, isAdjacent: true, isFog: false, false, explorePool: 3);
        Assert.AreEqual(HexActionKind.Move, a.Kind);
        Assert.AreEqual(3, a.Cost);
        Assert.IsTrue(a.Affordable);
        Assert.IsFalse(a.RequiresConfirm);
    }

    [Test]
    public void AdjacentTerrain_Unaffordable_ReturnsMoveNotAffordable()
    {
        var a = Normal(false, true, entryCost: 5, isAdjacent: true, isFog: false, false, explorePool: 4);
        Assert.AreEqual(HexActionKind.Move, a.Kind);
        Assert.IsFalse(a.Affordable);
    }

    [Test]
    public void TeleportMode_VisibleTerrainNoEnemy_ReturnsTeleportTarget()
    {
        var a = HexActionRules.Resolve(isSameCell: false, hasTerrain: true, entryCost: 5,
            isAdjacent: false, isFog: false, enemyOnCell: false, explorePool: 0, fogCost: 2,
            teleportMode: true);
        Assert.AreEqual(HexActionKind.TeleportTarget, a.Kind);
    }

    [Test]
    public void TeleportMode_FogOrEnemyOrSelf_ReturnsNone()
    {
        var fog = HexActionRules.Resolve(false, true, 5, false, isFog: true, false, 0, 2, true);
        var enemy = HexActionRules.Resolve(false, true, 5, false, false, enemyOnCell: true, 0, 2, true);
        var self = HexActionRules.Resolve(isSameCell: true, true, 5, false, false, false, 0, 2, true);
        Assert.AreEqual(HexActionKind.None, fog.Kind);
        Assert.AreEqual(HexActionKind.None, enemy.Kind);
        Assert.AreEqual(HexActionKind.None, self.Kind);
    }
}
```

- [ ] **Step 3: Write the failing harness and confirm it does not compile yet**

Write the harness to the scratchpad (single-quoted heredoc so nothing expands). Create `scratchpad/HexHarness.cs`:

```csharp
using System;

static class HexHarness
{
    static int failures = 0;
    static void Check(bool cond, string name)
    {
        if (!cond) { failures++; Console.WriteLine("FAIL: " + name); }
    }

    static void Main()
    {
        // Same cell -> None
        Check(HexActionRules.Resolve(true, true, 1, false, false, false, 9, 2, false).Kind
              == HexActionKind.None, "same-cell None");
        // No terrain -> OffMap
        Check(HexActionRules.Resolve(false, false, 0, false, false, false, 9, 2, false).Kind
              == HexActionKind.OffMap, "off-map");
        // Distant revealed -> DistantInfo + cost
        var di = HexActionRules.Resolve(false, true, 4, false, false, false, 9, 2, false);
        Check(di.Kind == HexActionKind.DistantInfo && di.Cost == 4, "distant info");
        // Distant fog -> DistantFog
        Check(HexActionRules.Resolve(false, true, 4, false, true, false, 9, 2, false).Kind
              == HexActionKind.DistantFog, "distant fog");
        // Adjacent enemy -> EnemyFight
        Check(HexActionRules.Resolve(false, true, 1, true, false, true, 9, 2, false).Kind
              == HexActionKind.EnemyFight, "enemy fight");
        // Adjacent fog affordable -> ScoutFog, confirm, cost 2
        var sf = HexActionRules.Resolve(false, true, 4, true, true, false, 2, 2, false);
        Check(sf.Kind == HexActionKind.ScoutFog && sf.Cost == 2 && sf.Affordable
              && sf.RequiresConfirm, "scout fog affordable");
        // Adjacent fog unaffordable
        Check(!HexActionRules.Resolve(false, true, 4, true, true, false, 1, 2, false).Affordable,
              "scout fog unaffordable");
        // Adjacent terrain affordable -> Move, no confirm
        var mv = HexActionRules.Resolve(false, true, 3, true, false, false, 3, 2, false);
        Check(mv.Kind == HexActionKind.Move && mv.Cost == 3 && mv.Affordable
              && !mv.RequiresConfirm, "move affordable");
        // Adjacent terrain unaffordable
        Check(!HexActionRules.Resolve(false, true, 5, true, false, false, 4, 2, false).Affordable,
              "move unaffordable");
        // Teleport valid
        Check(HexActionRules.Resolve(false, true, 5, false, false, false, 0, 2, true).Kind
              == HexActionKind.TeleportTarget, "teleport target");
        // Teleport invalid (fog / enemy / self)
        Check(HexActionRules.Resolve(false, true, 5, false, true, false, 0, 2, true).Kind
              == HexActionKind.None, "teleport fog none");
        Check(HexActionRules.Resolve(false, true, 5, false, false, true, 0, 2, true).Kind
              == HexActionKind.None, "teleport enemy none");
        Check(HexActionRules.Resolve(true, true, 5, false, false, false, 0, 2, true).Kind
              == HexActionKind.None, "teleport self none");

        Console.WriteLine(failures == 0 ? "ALL PASS" : (failures + " FAILURES"));
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
```

Run (expect a compile error — `HexActionRules.cs` does not exist yet):

```bash
SCR="C:/Users/DAVE'S~1/AppData/Local/Temp/claude/c--Users-Dave-s-Comp-source-repos-Archon-s-Rise/aaa188ed-71ec-4e0e-a135-b8f518b3835d/scratchpad"
"C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mcs.bat" \
  -out:"$SCR/hex.exe" \
  "Assets/Scripts/Exploration/HexActionRules.cs" "$SCR/HexHarness.cs"
```

Expected: FAIL — `error CS2001: Source file '...HexActionRules.cs' could not be found`.

- [ ] **Step 4: Implement `HexActionRules`**

Create `Assets/Scripts/Exploration/HexActionRules.cs`:

```csharp
// Pure decision layer for hex-selection exploration (spec 2026-07-23). Given only
// primitive facts about the pointed cell relative to the player, classifies what a
// click there means. No UnityEngine dependency, so it is trivially unit-testable.
public enum HexActionKind
{
    None,           // your own cell, or an invalid target in the current mode
    OffMap,         // no terrain tile anywhere — off the generated map
    DistantInfo,    // non-adjacent revealed terrain: show its entry cost, not actionable
    DistantFog,     // non-adjacent fog: "Unexplored"
    Move,           // adjacent revealed terrain: step onto it (undoable)
    ScoutFog,       // adjacent fog: reveal it (irreversible, confirm click)
    EnemyFight,     // adjacent visible enemy: the enemy token owns this click
    TeleportTarget  // teleport mode: any visible terrain hex without an enemy
}

public readonly struct HexAction
{
    public readonly HexActionKind Kind;
    public readonly int Cost;             // explore cost relevant to this action
    public readonly bool Affordable;      // explorePool >= Cost (Move / ScoutFog only)
    public readonly bool RequiresConfirm; // true for irreversible actions (fog scout)

    public HexAction(HexActionKind kind, int cost, bool affordable, bool requiresConfirm)
    {
        Kind = kind;
        Cost = cost;
        Affordable = affordable;
        RequiresConfirm = requiresConfirm;
    }
}

public static class HexActionRules
{
    public static HexAction Resolve(
        bool isSameCell, bool hasTerrain, int entryCost, bool isAdjacent,
        bool isFog, bool enemyOnCell, int explorePool, int fogCost, bool teleportMode)
    {
        if (teleportMode)
        {
            bool valid = hasTerrain && !isFog && !enemyOnCell && !isSameCell;
            return new HexAction(valid ? HexActionKind.TeleportTarget : HexActionKind.None,
                0, true, false);
        }

        if (isSameCell) return new HexAction(HexActionKind.None, 0, true, false);
        if (!hasTerrain) return new HexAction(HexActionKind.OffMap, 0, true, false);

        if (!isAdjacent)
            return isFog
                ? new HexAction(HexActionKind.DistantFog, 0, true, false)
                : new HexAction(HexActionKind.DistantInfo, entryCost, explorePool >= entryCost, false);

        if (enemyOnCell) return new HexAction(HexActionKind.EnemyFight, 0, true, false);

        if (isFog)
            return new HexAction(HexActionKind.ScoutFog, fogCost, explorePool >= fogCost, true);

        return new HexAction(HexActionKind.Move, entryCost, explorePool >= entryCost, false);
    }
}
```

- [ ] **Step 5: Run the harness — expect ALL PASS**

```bash
SCR="C:/Users/DAVE'S~1/AppData/Local/Temp/claude/c--Users-Dave-s-Comp-source-repos-Archon-s-Rise/aaa188ed-71ec-4e0e-a135-b8f518b3835d/scratchpad"
"C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mcs.bat" \
  -out:"$SCR/hex.exe" \
  "Assets/Scripts/Exploration/HexActionRules.cs" "$SCR/HexHarness.cs" \
&& "C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mono.exe" "$SCR/hex.exe"
```

Expected: prints `ALL PASS` and exits 0. (The NUnit file `HexActionRulesTests.cs` is the durable artifact for the Unity Test Runner / CI; the harness is the editor-open gate per the pure-test workflow.)

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Exploration/ArchonsRise.Exploration.asmdef" \
        "Assets/Scripts/Exploration/HexActionRules.cs" \
        "Assets/Scripts/Exploration/Tests/ArchonsRise.Exploration.Tests.asmdef" \
        "Assets/Scripts/Exploration/Tests/HexActionRulesTests.cs"
git commit -m "feat: HexActionRules pure hex-selection decision layer + tests"
```

---

## Task 2: `ExplorationController` (absorb `DirectionButton`, retire the arrows)

`ExplorationController` becomes the board-facts + movement hub. It ports every non-arrow responsibility of `DirectionButton`: the explore pool, `Map`/`Fog` exposure, `ApplyMove` (used by `MoveCommand`), undoable moves, and the fog-scout reveal+commit. It also gains terrain lookup across all three terrain tilemaps and adjacency/enemy helpers that Task 3 consumes.

**Files:**
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/ExplorationController.cs`
- Modify: `Assets/Scripts/Managers/Commands/MoveCommand.cs` (retype the ctor arg)
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/MapFog.cs:17` (find `ExplorationController`)
- Modify: `Assets/Scripts/Managers/DataManager.cs:211` and `:301` (find `ExplorationController`)
- Modify: `Assets/Scripts/Editor/LateGameSaveTool.cs:44` (find `ExplorationController`)
- Delete: `Assets/Scripts/GameObjectScripts/PlayerScripts/DirectionButton.cs`

**Interfaces:**
- Consumes: `MoveCommand`, `PlayerPosition`, `Grid`, `Tilemap`, the events `sendNewPositionOfPlayer` (`PlayerPositionEvent`) and `onSuccessfulExplore_AdjustPlayersExplore` (`IntEvent`), `TurnPhaseController`, `GameManager`, `EnemyToken`, `MapFog`.
- Produces (public API relied on by Tasks 3–5):
  - `static ExplorationController Instance { get; }`
  - `Tilemap Map { get; }` (ground), `Tilemap Fog { get; }`
  - `int FogCost { get; }` (default 2)
  - `int PlayerExplore { get; }`
  - `Vector3Int PlayerCell { get; }`
  - `void SetExplore(int explore)`
  - `bool TryTerrain(Vector3Int cell, out int cost)` — true if any terrain tilemap has a tile
  - `bool IsAdjacent(Vector3Int cell)`
  - `Vector3Int[] PlayerNeighbors()` — the six neighbour cells (affordable-ring highlight)
  - `bool EnemyOccupies(Vector3Int cell)`
  - `void Move(Vector3Int targetCell)` — undoable step onto revealed terrain
  - `void ScoutFog(Vector3Int targetCell)` — spend + reveal + commit
  - `void ApplyMove(Vector3 worldPos, int exploreDelta, bool refund = false)` — used by `MoveCommand`
  - `void ApplyTeleport(Vector3 worldPos)` — reposition + raise position event (used in Task 5)

- [ ] **Step 1: Create `ExplorationController`**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/ExplorationController.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Board movement + facts hub (spec 2026-07-23). Absorbs the non-arrow duties of the
// retired DirectionButton: the explore pool, Map/Fog exposure, ApplyMove (MoveCommand),
// undoable moves, and the fog-scout reveal+commit. HexInteractor (input/feedback) calls
// the query + action methods here; MoveCommand calls ApplyMove; MapFog/DataManager reach
// the fog tilemap through this component.
public class ExplorationController : MonoBehaviour
{
    public static ExplorationController Instance { get; private set; }

    [SerializeField] Grid gameboard;
    [SerializeField] Tilemap ground;
    [SerializeField] Tilemap water;
    [SerializeField] Tilemap mountains;
    [SerializeField] Tilemap fog;
    [SerializeField] PlayerPosition player;
    [SerializeField] int fogCost = 2;
    [SerializeField] PlayerPositionEvent sendNewPositionOfPlayer;
    [SerializeField] IntEvent onSuccessfulExplore_AdjustPlayersExplore;

    int playerExplore;

    // Same map/fog tilemaps the save system reads (formerly via DirectionButton).
    public Tilemap Map => ground;
    public Tilemap Fog => fog;
    public int FogCost => fogCost;
    public int PlayerExplore => playerExplore;
    public Vector3Int PlayerCell => gameboard.LocalToCell(player.transform.position);

    // Parity-correct hex neighbour offsets for a given cell (reuses PlayerPosition's compass).
    readonly Dictionary<Directions, Vector3Int> compass = new()
    {
        {Directions.Northwest, new Vector3Int(-1,1)},
        {Directions.Northeast, new Vector3Int(0,1)},
        {Directions.East, new Vector3Int(1,0)},
        {Directions.Southeast, new Vector3Int(0,-1)},
        {Directions.Southwest, new Vector3Int(-1,-1)},
        {Directions.West, new Vector3Int(-1,0)}
    };

    void Awake() { Instance = this; }

    // Explore pool sync (listener target, formerly DirectionButton.SetExplore).
    public void SetExplore(int explore) => playerExplore = explore;

    // True if any terrain tilemap holds a tile here; out cost is its HexRuleTile.exploreCost.
    // Ground (plains/forest/desert/town/dungeon), then water (5), then mountain (4).
    public bool TryTerrain(Vector3Int cell, out int cost)
    {
        if (ground.HasTile(cell)) { cost = Cost(ground, cell); return true; }
        if (water.HasTile(cell))  { cost = Cost(water, cell);  return true; }
        if (mountains.HasTile(cell)) { cost = Cost(mountains, cell); return true; }
        cost = 0;
        return false;
    }

    static int Cost(Tilemap map, Vector3Int cell)
    {
        var t = map.GetTile<HexRuleTile>(cell);
        return t != null ? t.exploreCost : 0;
    }

    public bool IsAdjacent(Vector3Int cell)
    {
        var origin = PlayerCell;
        player.UpdateCompass(origin, compass);
        foreach (Directions d in Enum.GetValues(typeof(Directions)))
            if (origin + compass[d] == cell) return true;
        return false;
    }

    // The six parity-correct neighbour cells of the player, for the affordable-ring
    // highlight. Directions has exactly six values, so the array is always full.
    public Vector3Int[] PlayerNeighbors()
    {
        var origin = PlayerCell;
        player.UpdateCompass(origin, compass);
        var result = new Vector3Int[6];
        int i = 0;
        foreach (Directions d in Enum.GetValues(typeof(Directions)))
            result[i++] = origin + compass[d];
        return result;
    }

    // A visible enemy token stands on this cell. Enemies hidden under fog don't count.
    public bool EnemyOccupies(Vector3Int cell)
    {
        if (MapFog.IsHidden(cell)) return false;
        foreach (var token in FindObjectsByType<EnemyToken>())
            if (token.gridPos == cell) return true;
        return false;
    }

    bool CanMovePhase()
    {
        if (TurnPhaseController.Instance != null && !TurnPhaseController.Instance.CanMove)
        {
            GameManager.Instance.ValidationMessage("You can only move during the Explore phase.");
            return false;
        }
        return true;
    }

    // Undoable step onto adjacent revealed terrain. Re-validates phase / enemy / cost
    // as defence in depth (HexInteractor only dispatches Move for affordable cells).
    public void Move(Vector3Int targetCell)
    {
        if (!CanMovePhase()) return;
        if (EnemyOccupies(targetCell))
        {
            GameManager.Instance.ValidationMessage("An enemy blocks the way — attack it instead!");
            return;
        }
        if (!TryTerrain(targetCell, out int cost)) return;
        if (playerExplore < cost)
        {
            GameManager.Instance.ValidationMessage($"Need {cost} to explore!");
            return;
        }
        var from = player.transform.position;
        var to = gameboard.CellToWorld(targetCell);
        GameManager.Instance.commands.AddCommand(new MoveCommand(this, from, to, cost));
    }

    // Reveal an adjacent fog hex in place (does NOT relocate the player). Irreversible:
    // spends fogCost, uncovers the scouted cell + its neighbours, commits the stack.
    public void ScoutFog(Vector3Int targetCell)
    {
        if (!CanMovePhase()) return;
        if (playerExplore < fogCost)
        {
            GameManager.Instance.ValidationMessage($"Need {fogCost} to scout this fog!");
            return;
        }
        playerExplore -= fogCost;
        onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);

        player.UpdateCompass(targetCell, compass);
        fog.SetTile(targetCell, null);
        foreach (Directions d in Enum.GetValues(typeof(Directions)))
            fog.SetTile(targetCell + compass[d], null);

        GameManager.Instance.commands.ClearStack(); // revealed knowledge can't be undone
    }

    // Reposition + adjust explore. Used by MoveCommand for execute (spend) and undo
    // (refund). Raises position + explore events so map + HUD stay in sync.
    public void ApplyMove(Vector3 worldPos, int exploreDelta, bool refund = false)
    {
        player.transform.position = worldPos;
        playerExplore += refund ? exploreDelta : -exploreDelta;
        onSuccessfulExplore_AdjustPlayersExplore.Raise(playerExplore);
        sendNewPositionOfPlayer.Raise(player);
    }

    // Free reposition (teleport, Task 5). Raises the position event so every enemy
    // re-runs CheckAggro — landing adjacent arms combat exactly like a walked step.
    public void ApplyTeleport(Vector3 worldPos)
    {
        player.transform.position = worldPos;
        sendNewPositionOfPlayer.Raise(player);
    }
}
```

- [ ] **Step 2: Retype `MoveCommand` to take `ExplorationController`**

In `Assets/Scripts/Managers/Commands/MoveCommand.cs`, replace the field/ctor type and the doc comment. Full new file:

```csharp
using UnityEngine;

// Undoable board move (spec 2026-07-21, re-homed 2026-07-23). Execute repositions the
// player and spends explore; Undo restores both. Only the no-new-fog branch of
// ExplorationController.Move builds one of these — a fog-revealing scout commits the
// stack instead (irreversible knowledge), so a MoveCommand never re-hides fog.
public class MoveCommand : ICommands
{
    readonly ExplorationController controller;
    readonly Vector3 from;
    readonly Vector3 to;
    readonly int exploreCost;

    public MoveCommand(ExplorationController controller, Vector3 from, Vector3 to, int exploreCost)
    {
        this.controller = controller;
        this.from = from;
        this.to = to;
        this.exploreCost = exploreCost;
    }

    public void Execute() => controller.ApplyMove(to, exploreCost);
    public void Undo()    => controller.ApplyMove(from, exploreCost, refund: true);
}
```

- [ ] **Step 3: Re-point `MapFog` to `ExplorationController`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/MapFog.cs`, update the lookup (lines 6–8 comment and line 17). Replace the `Fog()` method body's finder:

```csharp
    static Tilemap Fog()
    {
        if (fog == null)
        {
            var ctrl = Object.FindAnyObjectByType<ExplorationController>();
            if (ctrl != null) fog = ctrl.Fog;
        }
        return fog;
    }
```

And update the header comment reference from "every DirectionButton" to "the ExplorationController".

- [ ] **Step 4: Re-point `DataManager` (×2) and `LateGameSaveTool`**

In `Assets/Scripts/Managers/DataManager.cs`, replace both occurrences:

Line ~211 (load / fog restore):
```csharp
        var dir = FindAnyObjectByType<ExplorationController>();
        if (dir != null && dir.Fog != null)
            foreach (var c in run.map.revealedCells)
                dir.Fog.SetTile(new Vector3Int(c.x, c.y, 0), null);
```

Line ~301 (capture / revealed cells):
```csharp
        var dir = FindAnyObjectByType<ExplorationController>();
        if (dir != null && dir.Fog != null)
            run.map.revealedCells = CaptureRevealedCells(dir.Fog);
```

In `Assets/Scripts/Editor/LateGameSaveTool.cs:44`, replace:
```csharp
        var dir     = Object.FindAnyObjectByType<ExplorationController>();
```
(Only the type in the `FindAnyObjectByType` call changes; the `.Map`/`.Fog` usages below it are unchanged — `ExplorationController` exposes the same `Map`/`Fog` properties.)

- [ ] **Step 5: Delete `DirectionButton`**

```bash
git rm "Assets/Scripts/GameObjectScripts/PlayerScripts/DirectionButton.cs" \
       "Assets/Scripts/GameObjectScripts/PlayerScripts/DirectionButton.cs.meta"
```

- [ ] **Step 6: Verify compilation in Unity**

Switch focus to the Unity Editor and let it recompile. Open the Console. Expected: **no compile errors**. (The six arrow GameObjects will now show a "missing script" warning — that is expected; they are removed in Task 7. `LateGameSaveTool` reads `.Map`/`.Fog`, `MapFog`/`DataManager` find `ExplorationController` — all resolve.)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: retire DirectionButton, add ExplorationController movement hub"
```

---

## Task 3: `HexInteractor` + mouse pointer source + tooltip/highlight

The input/feedback layer. Polls the active `IHexPointerSource` for the pointed cell + confirm, gathers facts from `ExplorationController` + `MapFog`, calls `HexActionRules`, drives a tooltip and a highlight tile, and dispatches Move/ScoutFog (bare cells only — token cells are handled by the tokens).

**Files:**
- Create: `Assets/Scripts/Exploration/IHexPointerSource.cs`
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/MouseHexPointerSource.cs`
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/HexTooltip.cs`
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs`

**Interfaces:**
- Consumes: `ExplorationController` (Task 2), `HexActionRules`/`HexAction`/`HexActionKind` (Task 1), `MapFog`, `IconMarkup`/`IconConcept` (UiLanguage asmdef), `Grid`, `Tilemap`, Unity Input System `Mouse`, `EventSystem`.
- Produces (relied on by Tasks 4–5):
  - `interface IHexPointerSource { bool TryGetCell(out Vector3Int cell); bool ConfirmPressed { get; } }`
  - `static HexInteractor Instance { get; }`
  - `bool IsTeleporting { get; }`
  - `void BeginTeleport(PlayCommand pendingPlay, Card card)` (implemented in Task 5)

- [ ] **Step 1: Add the Input System reference to the Exploration asmdef**

`MouseHexPointerSource` uses `UnityEngine.InputSystem`. It lives in Assembly-CSharp, which already references the Input System (the project uses generated `Controls`), so no asmdef edit is required for it. `IHexPointerSource` has no engine dependency but is referenced by MonoBehaviours in Assembly-CSharp; place it in the Exploration asmdef. Because the Exploration asmdef set `noEngineReferences: true` in Task 1 and this interface uses `UnityEngine.Vector3Int`, change that flag to `false`.

Edit `Assets/Scripts/Exploration/ArchonsRise.Exploration.asmdef`: set `"noEngineReferences": false`.

Re-run the Task 1 harness to confirm the pure rules still compile standalone (mcs supplies its own mscorlib; the flag only affects Unity's compile):

```bash
SCR="C:/Users/DAVE'S~1/AppData/Local/Temp/claude/c--Users-Dave-s-Comp-source-repos-Archon-s-Rise/aaa188ed-71ec-4e0e-a135-b8f518b3835d/scratchpad"
"C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mcs.bat" -out:"$SCR/hex.exe" "Assets/Scripts/Exploration/HexActionRules.cs" "$SCR/HexHarness.cs" && "C:/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Data/MonoBleedingEdge/bin/mono.exe" "$SCR/hex.exe"
```
Expected: `ALL PASS`.

- [ ] **Step 2: Create the pointer-source interface**

Create `Assets/Scripts/Exploration/IHexPointerSource.cs`:

```csharp
using UnityEngine;

// The seam between input devices and the board interactor. The mouse implementation
// ships now; a controller-cursor implementation drops in later without touching
// HexInteractor. TryGetCell returns false when the pointer is over nothing usable
// (e.g. over UI, or off-screen).
public interface IHexPointerSource
{
    bool TryGetCell(out Vector3Int cell);
    bool ConfirmPressed { get; }
}
```

- [ ] **Step 3: Create the mouse pointer source**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/MouseHexPointerSource.cs`:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Mouse implementation of IHexPointerSource. Converts the cursor to a grid cell each
// frame and reports left-click as confirm. Suppresses everything while the pointer is
// over UI (so clicking the hand never moves the board).
public class MouseHexPointerSource : IHexPointerSource
{
    readonly Grid grid;
    readonly Camera cam;

    public MouseHexPointerSource(Grid grid, Camera cam)
    {
        this.grid = grid;
        this.cam = cam;
    }

    bool OverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    public bool TryGetCell(out Vector3Int cell)
    {
        cell = default;
        if (Mouse.current == null || cam == null || OverUI) return false;
        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        cell = grid.WorldToCell(world);
        return true;
    }

    public bool ConfirmPressed =>
        !OverUI && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
}
```

- [ ] **Step 4: Create the tooltip component**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/HexTooltip.cs`:

```csharp
using UnityEngine;
using TMPro;

// Small screen-space label that follows the pointed cell. HexInteractor sets its text
// and world anchor each frame; empty text hides it. Uses TMP so IconMarkup sprite tags
// (the explore/scroll glyph) render inline, matching card text.
public class HexTooltip : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] RectTransform panel;      // the panel to move; anchored in screen space
    [SerializeField] Vector2 screenOffset = new Vector2(0f, 40f);

    public void Hide()
    {
        if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
    }

    public void Show(string text, Vector3 worldAnchor)
    {
        if (panel == null || label == null) return;
        if (string.IsNullOrEmpty(text)) { Hide(); return; }
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        label.text = text;
        var c = cam != null ? cam : Camera.main;
        if (c != null)
            panel.position = c.WorldToScreenPoint(worldAnchor) + (Vector3)screenOffset;
    }
}
```

- [ ] **Step 5: Create `HexInteractor`**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs`:

```csharp
using UnityEngine;
using UnityEngine.Tilemaps;

// Input + feedback layer for hex-selection exploration (spec 2026-07-23). Each frame:
// resolve the pointed cell from the active IHexPointerSource, gather facts from
// ExplorationController + MapFog, classify via HexActionRules, drive the tooltip +
// highlight, and on confirm dispatch Move/ScoutFog. Token cells (enemy/place) are left
// to the tokens; teleport targeting is added in Task 5.
public class HexInteractor : MonoBehaviour
{
    public static HexInteractor Instance { get; private set; }

    [SerializeField] Grid gameboard;
    [SerializeField] Camera boardCamera;
    [SerializeField] ExplorationController exploration;
    [SerializeField] HexTooltip tooltip;
    [SerializeField] Tilemap highlight;          // overlay tilemap, above terrain / below tokens
    [SerializeField] TileBase highlightTile;      // single hex tile stamped on the pointed cell
    [SerializeField] Color moveColor    = new Color(0.3f, 1f, 0.3f, 0.5f);
    [SerializeField] Color blockedColor = new Color(1f, 0.3f, 0.3f, 0.5f);
    [SerializeField] Color scoutColor   = new Color(0.3f, 0.6f, 1f, 0.5f);
    [SerializeField] Color teleportColor = new Color(0.7f, 0.3f, 1f, 0.5f);
    [SerializeField] Color infoColor    = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] Color ringColor    = new Color(0.3f, 1f, 0.3f, 0.18f); // faint affordable-ring

    IHexPointerSource pointer;
    readonly System.Collections.Generic.List<Vector3Int> painted = new(); // cells tinted this frame
    Vector3Int? armedFogCell;   // fog scout requires a confirming second click on the same cell

    // Teleport state (wired up in Task 5).
    protected bool teleportMode;
    public bool IsTeleporting => teleportMode;

    void Awake() { Instance = this; }

    void Start()
    {
        pointer = new MouseHexPointerSource(gameboard, boardCamera != null ? boardCamera : Camera.main);
    }

    void Update()
    {
        if (!pointer.TryGetCell(out var cell))
        {
            ClearPainted();
            tooltip.Hide();
            armedFogCell = null;
            return;
        }

        var verdict = Classify(cell, out bool placeOnCell);
        Render(cell, verdict);

        if (pointer.ConfirmPressed)
            Dispatch(cell, verdict, placeOnCell);
    }

    HexAction Classify(Vector3Int cell, out bool placeOnCell)
    {
        bool isSameCell = cell == exploration.PlayerCell;
        bool hasTerrain = exploration.TryTerrain(cell, out int entryCost);
        bool isAdjacent = exploration.IsAdjacent(cell);
        bool isFog = MapFog.IsHidden(cell);
        bool enemyOnCell = exploration.EnemyOccupies(cell);
        placeOnCell = PlaceOccupies(cell);
        return HexActionRules.Resolve(isSameCell, hasTerrain, entryCost, isAdjacent,
            isFog, enemyOnCell, exploration.PlayerExplore, exploration.FogCost, teleportMode);
    }

    void Dispatch(Vector3Int cell, HexAction verdict, bool placeOnCell)
    {
        switch (verdict.Kind)
        {
            case HexActionKind.Move:
                // Place cells are moved onto via the place token (Task 4); defer.
                if (!placeOnCell) { exploration.Move(cell); armedFogCell = null; }
                break;
            case HexActionKind.ScoutFog:
                if (armedFogCell == cell) { exploration.ScoutFog(cell); armedFogCell = null; }
                else armedFogCell = cell;   // first click arms; tooltip prompts to confirm
                break;
            // EnemyFight / DistantInfo / DistantFog / None / OffMap: no board dispatch.
            // TeleportTarget is handled by the Task 5 override.
            default:
                break;
        }
    }

    void Render(Vector3Int cell, HexAction verdict)
    {
        ClearPainted();
        PaintRing(cell);            // faint tint on affordable adjacent cells
        PaintHover(cell, verdict);  // stronger tint on the pointed cell (drawn on top)
        tooltip.Show(TooltipText(cell, verdict), gameboard.GetCellCenterWorld(cell));
    }

    // Persistent affordable-ring affordance (replaces the arrows' always-visible costs):
    // faintly tint each adjacent cell the player could currently step onto. Skips the
    // hovered cell so PaintHover's stronger tint wins there. Suppressed in teleport mode.
    void PaintRing(Vector3Int hoverCell)
    {
        if (teleportMode) return;
        foreach (var n in exploration.PlayerNeighbors())
        {
            if (n == hoverCell) continue;
            if (!exploration.TryTerrain(n, out int cost)) continue;
            if (MapFog.IsHidden(n)) continue;
            if (exploration.EnemyOccupies(n)) continue;
            if (exploration.PlayerExplore < cost) continue;
            Paint(n, ringColor);
        }
    }

    void PaintHover(Vector3Int cell, HexAction verdict)
    {
        Color? tint = verdict.Kind switch
        {
            HexActionKind.Move           => verdict.Affordable ? moveColor : blockedColor,
            HexActionKind.ScoutFog       => verdict.Affordable ? scoutColor : blockedColor,
            HexActionKind.TeleportTarget => teleportColor,
            HexActionKind.DistantInfo    => infoColor,
            _ => (Color?)null
        };
        if (tint.HasValue) Paint(cell, tint.Value);
    }

    string TooltipText(Vector3Int cell, HexAction verdict)
    {
        string exp = IconMarkup.Tag(IconConcept.Explore);
        switch (verdict.Kind)
        {
            case HexActionKind.Move:
                return verdict.Affordable
                    ? $"Move here — {exp} {verdict.Cost}"
                    : $"Need {exp} {verdict.Cost} to move here";
            case HexActionKind.ScoutFog:
                if (!verdict.Affordable) return $"Need {exp} {verdict.Cost} to scout this fog";
                return armedFogCell == cell
                    ? "Click again to scout"
                    : $"Scout this fog — {exp} {verdict.Cost}";
            case HexActionKind.DistantInfo:
                return $"{exp} {verdict.Cost}";
            case HexActionKind.DistantFog:
                return "Unexplored";
            case HexActionKind.TeleportTarget:
                return "Teleport here";
            default:
                return null; // None / OffMap / EnemyFight (enemy token shows its own preview)
        }
    }

    void Paint(Vector3Int cell, Color tint)
    {
        if (highlight == null || highlightTile == null) return;
        highlight.SetTile(cell, highlightTile);
        highlight.SetTileFlags(cell, TileFlags.None);
        highlight.SetColor(cell, tint);
        painted.Add(cell);
    }

    void ClearPainted()
    {
        if (highlight != null)
            foreach (var c in painted) highlight.SetTile(c, null);
        painted.Clear();
    }

    // A town or dungeon token stands on this cell (visible). These handle their own
    // clicks (Task 4), so HexInteractor never dispatches Move onto them.
    protected bool PlaceOccupies(Vector3Int cell)
    {
        if (MapFog.IsHidden(cell)) return false;
        foreach (var t in FindObjectsByType<TownToken>())
            if (t.gridPos == cell) return true;
        foreach (var d in FindObjectsByType<DungeonToken>())
            if (d.gridPos == cell) return true;
        return false;
    }
}
```

- [ ] **Step 6: Verify compilation in Unity**

Focus the Unity Editor, let it recompile, open the Console. Expected: **no compile errors**. (Runtime wiring — assigning the serialized fields on a scene `HexInteractor` — happens in Task 7; this task only needs to compile.)

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/Exploration/IHexPointerSource.cs" \
        "Assets/Scripts/Exploration/ArchonsRise.Exploration.asmdef" \
        "Assets/Scripts/GameObjectScripts/PlayerScripts/MouseHexPointerSource.cs" \
        "Assets/Scripts/GameObjectScripts/PlayerScripts/HexTooltip.cs" \
        "Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs"
git commit -m "feat: HexInteractor + mouse pointer source + hex tooltip/highlight"
```

---

## Task 4: Place tokens — click adjacent to move onto them

Today clicking an adjacent town/dungeon shows "You must be standing in X to enter it." With hexes as targets, an adjacent place click should **move onto the place** (via `ExplorationController`); a click while standing opens the menu (unchanged). Tokens also yield during teleport targeting (Task 5 sets `HexInteractor.IsTeleporting`).

**Files:**
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs:24-53`
- Modify: `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonToken.cs` (its `OnPointerClick`)

**Interfaces:**
- Consumes: `ExplorationController.Instance.IsAdjacent`, `ExplorationController.Instance.Move`, `HexInteractor.Instance.IsTeleporting`.

- [ ] **Step 1: Update `TownToken.OnPointerClick`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs`, replace the not-standing branch (lines ~28-35) so an adjacent click moves onto the town, and add the teleport-yield guard at the top:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable

        // During teleport targeting the interactor owns all clicks (you can teleport
        // onto a place cell); let it handle this one.
        if (HexInteractor.Instance != null && HexInteractor.Instance.IsTeleporting) return;

        // Places are entered by standing on the cell. If the player is adjacent instead,
        // treat the click as a move request onto this cell (Explore-phase movement); the
        // menu opens on the next click, once standing here.
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            if (ExplorationController.Instance != null && ExplorationController.Instance.IsAdjacent(gridPos))
                ExplorationController.Instance.Move(gridPos);
            else
                GameManager.Instance.ValidationMessage(
                    $"You must be standing in {townSO.cardName} to enter it.");
            return;
        }

        if (TurnPhaseController.Instance != null)
            TurnPhaseController.Instance.BeginVisit();

        GameManager.Instance.townCanvas.enabled = true;
        deck.CreateTown(this);
        TownMenu.Instance.PrepareButtons();
        onClick_GetTownData.Raise(this);
        onClick_OpenTownMenu.Raise(this);
    }
```

- [ ] **Step 2: Update `DungeonToken.OnPointerClick`**

In `Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonToken.cs`, replace the not-standing branch (lines ~29-34) and add the teleport-yield guard:

```csharp
    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapFog.IsHidden(gridPos)) return; // hidden by fog → not interactable

        // During teleport targeting the interactor owns all clicks; let it handle this.
        if (HexInteractor.Instance != null && HexInteractor.Instance.IsTeleporting) return;

        // Dungeons are entered by standing on the cell. If adjacent instead, treat the
        // click as a move request onto this cell (Explore-phase movement).
        if (gameboard.LocalToCell(player.transform.position) != gridPos)
        {
            if (ExplorationController.Instance != null && ExplorationController.Instance.IsAdjacent(gridPos))
                ExplorationController.Instance.Move(gridPos);
            else
                GameManager.Instance.ValidationMessage(
                    $"You must be standing at {dungeonSO.cardName} to enter it.");
            return;
        }

        if (TurnPhaseController.Instance != null)
            TurnPhaseController.Instance.BeginVisit();

        FindAnyObjectByType<DungeonPanel>(FindObjectsInactive.Include).Open(this);
    }
```

- [ ] **Step 3: Verify compilation in Unity**

Focus Unity, recompile, check Console. Expected: **no compile errors**.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/GameObjectScripts/GameBoardObjects/TownToken.cs" \
        "Assets/Scripts/GameObjectScripts/GameBoardObjects/DungeonToken.cs"
git commit -m "feat: click adjacent place to move onto it; yield during teleport"
```

---

## Task 5: Teleport card + targeting mode

A card with `grantsTeleport` defers its play until a hex is picked: playing it enters teleport targeting; picking a visible hex commits one undoable `TeleportCommand` (plays the card + repositions); cancelling returns the card to hand with nothing on the stack.

**Files:**
- Modify: `Assets/Scripts/GameScriptableObjectTypes/CardsSO.cs` (add `grantsTeleport`)
- Modify: `Assets/Scripts/CardPlay/CardSnapshot.cs` (add `GrantsTeleport`)
- Modify: `Assets/Scripts/CardPlay/CardPlaySelection.cs:66` (`IsPlayable` recognises teleport)
- Modify: `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs` (`Snapshot` + `Play`)
- Create: `Assets/Scripts/Managers/Commands/TeleportCommand.cs`
- Modify: `Assets/Scripts/Managers/Commands/PlayManager.cs:44-55` (`ClearStack` commits `TeleportCommand`)
- Create: `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractorTeleport.cs` (partial-class teleport methods)
- Modify: `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs` (make class `partial`, dispatch TeleportTarget, cancel input)

**Interfaces:**
- Consumes: `PlayCommand`, `Card`, `ExplorationController.ApplyTeleport`, `Grid.GetCellCenterWorld`, `TurnPhaseController.CanMove`.
- Produces: `void HexInteractor.BeginTeleport(PlayCommand pendingPlay, Card card)`; `class TeleportCommand : ICommands { void Commit(); }`.

- [ ] **Step 1: Add `grantsTeleport` to `CardsSO`**

In `Assets/Scripts/GameScriptableObjectTypes/CardsSO.cs`, add after the `refresh` header block (after line 33):

```csharp
    [Header("Teleport (spec 2026-07-23)")]
    // Playing this card enters hex teleport-targeting instead of applying immediately;
    // picking a visible hex repositions the player (Explore-phase movement, free).
    public bool grantsTeleport;
```

- [ ] **Step 2: Add `GrantsTeleport` to `CardSnapshot`**

In `Assets/Scripts/CardPlay/CardSnapshot.cs`, add the field and a defaulted ctor param so existing callers keep working. Add field after `ConvertRequiresEmpower` (line 10):

```csharp
    public readonly bool GrantsTeleport;
```

Change the ctor signature (line 12-16) to add a trailing optional param, and assign it:

```csharp
    public CardSnapshot(StatType cardType, EmpowerType empowerType, bool isChoice,
        int attack, int defend, int influence, int explore,
        int empowerAttack, int empowerDefend, int empowerInfluence, int empowerExplore,
        StatType convertTo = StatType.None, StatType convertFrom = StatType.None,
        bool convertRequiresEmpower = false, bool grantsTeleport = false)
    {
        CardType = cardType;
        EmpowerType = empowerType;
        IsChoice = isChoice;
        Attack = attack; Defend = defend; Influence = influence; Explore = explore;
        EmpowerAttack = empowerAttack; EmpowerDefend = empowerDefend;
        EmpowerInfluence = empowerInfluence; EmpowerExplore = empowerExplore;
        ConvertTo = convertTo;
        ConvertFrom = convertFrom;
        ConvertRequiresEmpower = convertRequiresEmpower;
        GrantsTeleport = grantsTeleport;
    }
```

- [ ] **Step 3: `IsPlayable` recognises a pure-teleport card**

In `Assets/Scripts/CardPlay/CardPlaySelection.cs`, add a teleport check to `IsPlayable` (after the Wound guard, line 68):

```csharp
    public bool IsPlayable()
    {
        if (_card.CardType.HasFlag(StatType.Wound)) return false;
        if (_card.GrantsTeleport) return true;
        if (_card.CardType.HasFlag(StatType.Crystal)) return true;
        if (_card.CardType.HasFlag(StatType.Heal)) return true;
        if (_card.CardType.HasFlag(StatType.Refresh)) return true;
        foreach (var s in ActionStats)
            if (_card.CardType.HasFlag(s)) return true;
        return false;
    }
```

- [ ] **Step 4: `CardInspector.Snapshot` carries teleport; `Play` defers teleport cards**

In `Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs`, update `Snapshot` (line 191-195) to pass `so.grantsTeleport`:

```csharp
    static CardSnapshot Snapshot(CardsSO so) =>
        new CardSnapshot(so.cardType, so.empowerType, so.isChoice,
            so.attack, so.defend, so.influence, so.explore,
            so.empowerAttack, so.empowerDefend, so.empowerInfluence, so.empowerExplore,
            so.convertTo, so.convertFrom, so.convertRequiresEmpower, so.grantsTeleport);
```

Then update `Play()` (lines 136-161) to branch for teleport cards before the normal `AddCommand`:

```csharp
    public void Play()
    {
        if (Selection == null || !Selection.IsPlayable()) return;
        Card.IsEmpowered = Selection.EffectiveEmpowered();
        Card.ConvertOn = Selection.EffectiveConvert();
        var evt = EventFor(Selection);
        if (evt == null) return;

        Vector3 origin = Card.transform.position;
        var applied = Selection.PreviewStats(Selection.EffectiveEmpowered());

        // Teleport card: defer the play until a hex is picked (spec 2026-07-23). The
        // card is not committed here — HexInteractor holds a pending PlayCommand and
        // completes it (or discards it on cancel) from teleport targeting.
        if (Card.cardSO.grantsTeleport)
        {
            if (TurnPhaseController.Instance != null && !TurnPhaseController.Instance.CanMove)
            {
                GameManager.Instance.ValidationMessage("You can only teleport during the Explore phase.");
                return; // leave the inspector open so the player can cancel/back out
            }
            _reserved = null;
            HexInteractor.Instance.BeginTeleport(new PlayCommand(evt, Card), Card);
            Close();
            return;
        }

        GameManager.Instance.commands.AddCommand(new PlayCommand(evt, Card));
        _reserved = null;

        if (echoes != null)
            foreach (var e in StatEchoPlan.NonZero(applied))
                echoes.Emit(origin, e.Stat, e.Amount);

        Close();
    }
```

- [ ] **Step 5: Create `TeleportCommand`**

Create `Assets/Scripts/Managers/Commands/TeleportCommand.cs`:

```csharp
using UnityEngine;

// One undoable unit that plays a teleport card AND repositions the player (spec
// 2026-07-23). Built only when the player picks a hex in teleport targeting; cancelling
// never creates one (the card was never played). Teleport targets are visible-only, so
// nothing irreversible is revealed — this stays on the undo stack until a normal commit
// point, where Commit() discards the card exactly like a PlayCommand.
public class TeleportCommand : ICommands
{
    readonly PlayCommand play;
    readonly ExplorationController controller;
    readonly Vector3 from;
    readonly Vector3 to;

    public TeleportCommand(PlayCommand play, ExplorationController controller, Vector3 from, Vector3 to)
    {
        this.play = play;
        this.controller = controller;
        this.from = from;
        this.to = to;
    }

    public void Execute()
    {
        play.Execute();               // apply the card play (marks played, applies any stats)
        controller.ApplyTeleport(to); // reposition + raise position event (arms aggro)
    }

    public void Undo()
    {
        controller.ApplyTeleport(from);
        play.Undo();                  // un-play the card (returns it to hand)
    }

    // At an irreversible commit point the card can no longer be undone → discard it,
    // mirroring PlayCommand.Commit.
    public void Commit() => play.Commit();
}
```

- [ ] **Step 6: `ClearStack` commits `TeleportCommand`**

In `Assets/Scripts/Managers/Commands/PlayManager.cs`, extend the `ClearStack` commit loop (lines 47-49):

```csharp
        foreach (var command in commandManager)
        {
            if (command is PlayCommand playCommand) playCommand.Commit();
            else if (command is TeleportCommand teleportCommand) teleportCommand.Commit();
        }
```

- [ ] **Step 7: Make `HexInteractor` partial and dispatch/cancel teleport**

In `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs`, change the class declaration to `partial`:

```csharp
public partial class HexInteractor : MonoBehaviour
```

Add a `TeleportTarget` case to the `Dispatch` switch (before `default`):

```csharp
            case HexActionKind.TeleportTarget:
                CompleteTeleport(cell);
                break;
```

And add a cancel check at the **very top** of `Update()` (before `pointer.TryGetCell`), so Esc/right-click cancels even when the pointer is over UI or off a cell:

```csharp
    void Update()
    {
        if (teleportMode && CancelPressed()) { CancelTeleport(); return; }

        if (!pointer.TryGetCell(out var cell))
        // ... existing body unchanged ...
```

- [ ] **Step 8: Create the teleport partial**

Create `Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractorTeleport.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

// Teleport targeting for HexInteractor (spec 2026-07-23). BeginTeleport holds the
// pending card play; picking a visible hex commits one undoable TeleportCommand;
// cancelling (right-click / Esc) discards the pending play so the card returns to hand.
public partial class HexInteractor
{
    PlayCommand pendingTeleportPlay;
    Card pendingTeleportCard;

    // Called by CardInspector.Play when a grantsTeleport card is played. The play is
    // NOT yet on the stack — it commits only when a hex is picked.
    public void BeginTeleport(PlayCommand pendingPlay, Card card)
    {
        pendingTeleportPlay = pendingPlay;
        pendingTeleportCard = card;
        teleportMode = true;
    }

    void CompleteTeleport(Vector3Int cell)
    {
        if (pendingTeleportPlay == null) { teleportMode = false; return; }
        // The player always stands at CellToWorld(PlayerCell) (moves/teleports snap
        // there), so this round-trips exactly on undo. Use CellToWorld — the same
        // convention ExplorationController.Move uses for the destination.
        var from = gameboard.CellToWorld(exploration.PlayerCell);
        var to = gameboard.CellToWorld(cell);
        GameManager.Instance.commands.AddCommand(
            new TeleportCommand(pendingTeleportPlay, exploration, from, to));
        EndTeleport();
    }

    void CancelTeleport()
    {
        // Nothing was ever added to the stack; the card was never played, so it simply
        // stays in hand. Drop the pending play and leave targeting.
        EndTeleport();
    }

    void EndTeleport()
    {
        pendingTeleportPlay = null;
        pendingTeleportCard = null;
        teleportMode = false;
        armedFogCell = null;
    }

    static bool CancelPressed()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        return (kb != null && kb.escapeKey.wasPressedThisFrame)
            || (mouse != null && mouse.rightButton.wasPressedThisFrame);
    }
}
```

Note: `armedFogCell` and `exploration`/`gameboard` are private fields on the main `HexInteractor` partial (Task 3) — accessible here because both files declare the same `partial class`. `pendingTeleportCard` is retained for clarity/possible future UI even though `CancelTeleport` currently needs no explicit hand-return (the card was never played).

- [ ] **Step 9: Verify compilation in Unity**

Focus Unity, recompile, check Console. Expected: **no compile errors**.

- [ ] **Step 10: Commit**

```bash
git add "Assets/Scripts/GameScriptableObjectTypes/CardsSO.cs" \
        "Assets/Scripts/CardPlay/CardSnapshot.cs" \
        "Assets/Scripts/CardPlay/CardPlaySelection.cs" \
        "Assets/Scripts/GameObjectScripts/CardMenuScripts/CardInspector.cs" \
        "Assets/Scripts/Managers/Commands/TeleportCommand.cs" \
        "Assets/Scripts/Managers/Commands/PlayManager.cs" \
        "Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractor.cs" \
        "Assets/Scripts/GameObjectScripts/PlayerScripts/HexInteractorTeleport.cs"
git commit -m "feat: teleport card + hex targeting mode (deferred undoable play)"
```

---

## Task 6: Terrain cost data — Mountain 4

**Files:**
- Modify: `Assets/Scripts/TilemapScripts/MountainRuleTile.asset:20`

**Interfaces:** none (data asset).

- [ ] **Step 1: Set Mountain exploreCost to 4**

Edit `Assets/Scripts/TilemapScripts/MountainRuleTile.asset`, change line 20 `exploreCost: 5` to:

```
  exploreCost: 4
```

(Water is already `exploreCost: 5` in `WaterRuleTile.asset` — no change. Alternatively set this field via the tile asset's Inspector in Unity; the value must end at 4.)

- [ ] **Step 2: Commit**

```bash
git add "Assets/Scripts/TilemapScripts/MountainRuleTile.asset"
git commit -m "balance: Mountain explore cost 5 -> 4 (single-axis terrain gating)"
```

---

## Task 7: Manual Unity integration (USER WORK — scene/prefab/asset wiring)

These steps are done by the user in the Unity Editor (the assistant does not edit scene/prefab/asset YAML). Provide this as a checklist; the executor pauses here and hands it over. No code commits from the assistant; the user commits scene/prefab/asset changes.

- [ ] **Step 1: Highlight tilemap**
  - In `GameBoard.unity`, add a new Tilemap under the board Grid named `HexHighlight`, sorted **above** the terrain tilemaps and **below** the token layers.
  - Create a hex-shaped highlight `Tile` asset (a translucent white hex sprite) and note it for the `HexInteractor.highlightTile` field.

- [ ] **Step 2: Tooltip UI**
  - Create a screen-space `HexTooltip` panel (a small `Panel` with a `TextMeshProUGUI` child) on the board Canvas. Assign `cam` (board camera), `label` (the TMP text), and `panel` (its `RectTransform`) on the `HexTooltip` component.

- [ ] **Step 3: `ExplorationController` object**
  - Add an `ExplorationController` component (e.g. on the `Grid`/board manager object). Wire: `gameboard` (Grid), `ground`/`water`/`mountains`/`fog` tilemaps, `player` (PlayerPosition), `fogCost` = 2, and the two events `sendNewPositionOfPlayer` (`PlayerPositionEvent`) and `onSuccessfulExplore_AdjustPlayersExplore` (`IntEvent`) — the exact assets the old `DirectionButton` referenced.
  - **Move the explore-pool sync listener** that used to call `DirectionButton.SetExplore` so it now calls `ExplorationController.SetExplore` (the listener behind `OnToggle_SetExploreListenerCommand` / the explore-sync chain). Verify via the Dynamic-argument dropdown (not a hardcoded 0).

- [ ] **Step 4: `HexInteractor` object**
  - Add a `HexInteractor` component (same object is fine). Wire: `gameboard` (Grid), `boardCamera`, `exploration` (the ExplorationController), `tooltip` (HexTooltip), `highlight` (the HexHighlight tilemap), `highlightTile` (the highlight tile asset). Leave the color fields at defaults or tune.

- [ ] **Step 5: Author a teleport test card**
  - Create a `CardsSO` asset (e.g. `Assets/Scripts/ScriptableObjectData/Player/Cards/Teleport.asset`): `cardType = None`, all stats 0, `grantsTeleport = true`, a name/description (use the explore/scroll sprite tag in the description).
  - Add it to a deck you can draw in testing (starting deck or a reward pool) so it appears in hand.

- [ ] **Step 6: Remove the retired arrows**
  - Delete the six `DirectionButton` arrow GameObjects (they now carry missing-script components) from `GameBoard.unity`.

- [ ] **Step 7: Tutorial rail**
  - Update the movement rail step(s) (`Assets/Scripts/Tutorial/Rail/move.asset`, and the new `pick-card.asset`) that highlighted the arrow buttons via `TutorialTarget` so they teach the hex-click interaction instead (point the target at the player/hex area, adjust copy). This is authoring work in the tutorial assets.

- [ ] **Step 8: User commits the scene/prefab/asset changes**

```bash
git add -A
git commit -m "chore: wire HexInteractor/ExplorationController, teleport card, remove arrows, tutorial rail"
```

---

## Task 8: End-to-end play verification

Manual play-test in the Editor after Task 7 wiring. Confirm each spec flow.

- [ ] **Step 1: Move + affordability + undo**
  - Hover an adjacent revealed hex → green highlight + "Move here — 🗞 X". Click → player steps there, explore decreases by X. Undo (existing Undo button) → player returns, explore refunded.
  - Hover an adjacent hex you can't afford → red highlight + "Need 🗞 X to move here". Click → validation "Need X to explore!", no move.

- [ ] **Step 2: Distant + fog hover**
  - Hover a non-adjacent revealed hex → "🗞 X" info tooltip; click does nothing.
  - Hover a non-adjacent fog hex → "Unexplored".

- [ ] **Step 3: Fog scout (confirm click, reveal in place)**
  - Hover an adjacent fog hex → blue highlight + "Scout this fog — 🗞 2". Click once → "Click again to scout". Click again → fog lifts around that hex, explore −2, the move undo stack is committed (a prior move can no longer be undone). Player did **not** move. The revealed hex now hovers as a normal Move target.

- [ ] **Step 4: Terrain costs**
  - Confirm an adjacent Mountain hex shows cost 4 and Water shows cost 5, and both are enterable (no "impassable").

- [ ] **Step 5: Teleport — core + adjacent-to-enemy**
  - Draw and play the Teleport card during Explore → targeting mode; visible hexes highlight, hovering shows "Teleport here"; fog hexes and enemy hexes are not valid.
  - Teleport onto an interior lake (mid-board water) → allowed (lands there, no cost).
  - Teleport to a hex **adjacent to a visible enemy** → the enemy's aggro halo arms immediately; clicking the enemy starts combat, and combat spends the turn's Action (a second encounter is refused). Confirms teleport = Explore movement, fight = Action.
  - Undo after teleport → player returns and the Teleport card returns to hand.
  - Play Teleport then press Esc / right-click → targeting cancels and the card is back in hand with nothing spent.

- [ ] **Step 6: Place entry via hex click**
  - Stand adjacent to a town → click it → player moves onto the town cell (Explore). Click again (now standing) → the town menu opens (a free peek).

- [ ] **Step 7: Save/load fog**
  - Reveal several fog cells, save, reload → the same cells stay revealed (confirms `DataManager`/`MapFog` re-homing to `ExplorationController`).

- [ ] **Step 8: Record results**
  - If all pass, the feature is complete. Note any failures against the specific flow for follow-up (use `superpowers:systematic-debugging`).

---

## Notes on spec deviations settled here

- **Teleport trigger is a direct call, not a `VoidEvent`.** The design doc floated an `onTeleportRequested` `VoidEvent` for pattern-consistency, but the deferred-commit model must hand the pending `PlayCommand` + `Card` to the interactor, which an argument-less `VoidEvent` cannot carry. `CardInspector.Play()` calls `HexInteractor.Instance.BeginTeleport(...)` directly. No new event asset is needed.
- **Fog reveal is a single symmetric ring** around the scouted cell (the scouted hex + its 6 neighbours), a slight simplification of `DirectionButton`'s old two-ring forward reveal — cleaner and direction-agnostic now that any of the 6 fog neighbours can be scouted.
- **Teleport highlight is per-hover, not a full-map flood.** In teleport mode only the pointed cell is tinted purple (valid) or left untinted (invalid: fog/enemy/self), matching the hover-feedback model used everywhere else. Tinting every visible hex at once would be visually noisy on a large revealed map; hovering plus the "Teleport here" tooltip communicates validity clearly.
