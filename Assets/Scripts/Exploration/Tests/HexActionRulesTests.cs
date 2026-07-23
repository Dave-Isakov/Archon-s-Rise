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
