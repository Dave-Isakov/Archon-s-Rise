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
