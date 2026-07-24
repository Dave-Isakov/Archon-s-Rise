using NUnit.Framework;

public class OppositeAnchorTests
{
    [Test]
    public void EnemiesAbove_ButtonGoesStraightDown()
    {
        var a = CombatLayoutRules.OppositeAnchor(0f, 100f, 200f);
        Assert.AreEqual(0f, a.X, 1e-3f);
        Assert.AreEqual(-200f, a.Y, 1e-3f);
    }

    [Test]
    public void EnemiesRight_ButtonGoesLeft_AtDistance()
    {
        var a = CombatLayoutRules.OppositeAnchor(100f, 0f, 50f);
        Assert.AreEqual(-50f, a.X, 1e-3f);
        Assert.AreEqual(0f, a.Y, 1e-3f);
    }

    [Test]
    public void Diagonal_IsMirroredAndNormalisedToDistance()
    {
        // centroid (30,40) has length 50 -> unit (0.6,0.8) -> anchor -unit*100.
        var a = CombatLayoutRules.OppositeAnchor(30f, 40f, 100f);
        Assert.AreEqual(-60f, a.X, 1e-3f);
        Assert.AreEqual(-80f, a.Y, 1e-3f);
    }

    [Test]
    public void DistanceIsIndependentOfCentroidMagnitude()
    {
        // A far centroid still yields an anchor exactly `distance` from origin.
        var a = CombatLayoutRules.OppositeAnchor(0f, 1000f, 200f);
        Assert.AreEqual(0f, a.X, 1e-3f);
        Assert.AreEqual(-200f, a.Y, 1e-3f);
    }

    [Test]
    public void DegenerateCentroid_FallsStraightDown()
    {
        var a = CombatLayoutRules.OppositeAnchor(0f, 0f, 150f);
        Assert.AreEqual(0f, a.X, 1e-3f);
        Assert.AreEqual(-150f, a.Y, 1e-3f);
    }
}
