using NUnit.Framework;

public class CombatLayoutRulesTests
{
    const float Spacing = CombatLayoutRules.SlotSpacingDegrees;

    [Test]
    public void SingleCard_SitsDeadCentreTop()
    {
        var s = CombatLayoutRules.SlotFor(0, 1, 300f, 120f);
        Assert.AreEqual(0f, s.X, 1e-3f);   // cos(90) == 0
        Assert.AreEqual(300f, s.Y, 1e-3f); // sin(90) * radius
        Assert.AreEqual(1f, s.Scale, 1e-4f);
    }

    [Test]
    public void TwoCards_AreSymmetricAcrossTop()
    {
        var left = CombatLayoutRules.SlotFor(0, 2, 300f, 120f);
        var right = CombatLayoutRules.SlotFor(1, 2, 300f, 120f);
        Assert.AreEqual(left.X, -right.X, 1e-3f); // mirror across x=0
        Assert.AreEqual(left.Y, right.Y, 1e-3f);  // same height
    }

    [Test]
    public void Index0_IsLeft_XIncreasesRightward()
    {
        // Angle sweeps left (index 0, larger angle) to right; X strictly increases.
        var a = CombatLayoutRules.SlotFor(0, 3, 300f, 120f);
        var b = CombatLayoutRules.SlotFor(1, 3, 300f, 120f);
        var c = CombatLayoutRules.SlotFor(2, 3, 300f, 120f);
        Assert.Less(a.X, b.X);
        Assert.Less(b.X, c.X);
        Assert.AreEqual(0f, b.X, 1e-3f); // middle card centred
    }

    [Test]
    public void Scale_FullUpToThreshold_ThenStepsDown()
    {
        Assert.AreEqual(1f, CombatLayoutRules.ScaleFor(1), 1e-4f);
        Assert.AreEqual(1f, CombatLayoutRules.ScaleFor(CombatLayoutRules.CrowdThreshold), 1e-4f);
        Assert.Less(CombatLayoutRules.ScaleFor(CombatLayoutRules.CrowdThreshold + 1), 1f);
    }

    [Test]
    public void Scale_IsFlooredForLargeRosters()
    {
        Assert.GreaterOrEqual(CombatLayoutRules.ScaleFor(20), 0.6f);
    }

    [Test]
    public void AngleFor_CountOneOrZero_IsTop()
    {
        Assert.AreEqual(90f, CombatLayoutRules.AngleFor(0, 1, 120f), 1e-4f);
        Assert.AreEqual(90f, CombatLayoutRules.AngleFor(0, 0, 120f), 1e-4f);
    }

    // --- Fan width ----------------------------------------------------------

    [Test]
    public void FanArc_WidensWithTheRoster_ThenCaps()
    {
        Assert.AreEqual(0f, CombatLayoutRules.FanArc(1, Spacing), 1e-3f);
        Assert.AreEqual(55f, CombatLayoutRules.FanArc(2, Spacing), 1e-3f);
        Assert.AreEqual(110f, CombatLayoutRules.FanArc(3, Spacing), 1e-3f);
        Assert.AreEqual(165f, CombatLayoutRules.FanArc(4, Spacing), 1e-3f);
        Assert.AreEqual(CombatLayoutRules.MaxArcDegrees, CombatLayoutRules.FanArc(5, Spacing), 1e-3f);
        Assert.AreEqual(CombatLayoutRules.MaxArcDegrees, CombatLayoutRules.FanArc(12, Spacing), 1e-3f);
    }

    [Test]
    public void FourEnemies_LandOnTheDesignedRing()
    {
        // The placement spec's picture: 7.5 / 62.5 / 117.5 / 172.5 degrees.
        float arc = CombatLayoutRules.FanArc(4, Spacing);
        Assert.AreEqual(172.5f, CombatLayoutRules.AngleFor(0, 4, arc), 1e-3f);
        Assert.AreEqual(117.5f, CombatLayoutRules.AngleFor(1, 4, arc), 1e-3f);
        Assert.AreEqual(62.5f, CombatLayoutRules.AngleFor(2, 4, arc), 1e-3f);
        Assert.AreEqual(7.5f, CombatLayoutRules.AngleFor(3, 4, arc), 1e-3f);
    }

    [Test]
    public void SpacedSlots_NeverDropBelowTheCentre()
    {
        for (int n = 1; n <= 8; n++)
            for (int i = 0; i < n; i++)
                Assert.Greater(CombatLayoutRules.SpacedSlotFor(i, n, 200f, Spacing).Y, 0f);
    }

    // --- Safe area ----------------------------------------------------------

    static CombatLayoutRules.Box Safe => new CombatLayoutRules.Box(-500f, -100f, 500f, 300f);

    [Test]
    public void SafeArea_LeavesAContainedClusterAlone()
    {
        var d = CombatLayoutRules.ShiftIntoSafeArea(new CombatLayoutRules.Box(-50f, 0f, 50f, 100f), Safe);
        Assert.AreEqual(0f, d.X, 1e-4f);
        Assert.AreEqual(0f, d.Y, 1e-4f);
    }

    [Test]
    public void SafeArea_LiftsAClusterOutOfTheHandBand()
    {
        // Bottom edge 80px under the safe area's floor -> pushed up exactly 80.
        var d = CombatLayoutRules.ShiftIntoSafeArea(new CombatLayoutRules.Box(-50f, -180f, 50f, -20f), Safe);
        Assert.AreEqual(0f, d.X, 1e-4f);
        Assert.AreEqual(80f, d.Y, 1e-4f);
    }

    [Test]
    public void SafeArea_PullsAClusterBackFromTheRightEdge()
    {
        var d = CombatLayoutRules.ShiftIntoSafeArea(new CombatLayoutRules.Box(400f, 0f, 560f, 100f), Safe);
        Assert.AreEqual(-60f, d.X, 1e-4f);
        Assert.AreEqual(0f, d.Y, 1e-4f);
    }

    [Test]
    public void SafeArea_OversizedCluster_PinsToTheMinEdge()
    {
        // Too tall to fit: pin the bottom rather than oscillate between edges.
        var d = CombatLayoutRules.ShiftIntoSafeArea(new CombatLayoutRules.Box(-50f, -200f, 50f, 400f), Safe);
        Assert.AreEqual(100f, d.Y, 1e-4f);
    }

    // --- Button keep-out ----------------------------------------------------

    static CombatLayoutRules.Box Buttons => new CombatLayoutRules.Box(40f, -120f, 340f, 120f);

    [Test]
    public void KeepOut_IgnoresADisjointCluster()
    {
        var d = CombatLayoutRules.ShiftOutOfKeepOut(new CombatLayoutRules.Box(-400f, 0f, -200f, 200f), Buttons);
        Assert.AreEqual(0f, d.X, 1e-4f);
        Assert.AreEqual(0f, d.Y, 1e-4f);
    }

    [Test]
    public void KeepOut_PushesAnOverlappingClusterStraightUp()
    {
        // Overlap is shallow on the vertical axis and deep on the horizontal.
        var cluster = new CombatLayoutRules.Box(60f, 80f, 320f, 300f);
        var d = CombatLayoutRules.ShiftOutOfKeepOut(cluster, Buttons);
        Assert.AreEqual(0f, d.X, 1e-4f);
        Assert.AreEqual(40f, d.Y, 1e-4f); // keepOut.MaxY - cluster.MinY
    }

    [Test]
    public void KeepOut_PushesSidewaysWhenThatIsTheShorterEscape()
    {
        // Deeply overlapping vertically, barely overlapping on the left edge.
        var cluster = new CombatLayoutRules.Box(-160f, -110f, 60f, 110f);
        var d = CombatLayoutRules.ShiftOutOfKeepOut(cluster, Buttons);
        Assert.AreEqual(-20f, d.X, 1e-4f); // keepOut.MinX - cluster.MaxX
        Assert.AreEqual(0f, d.Y, 1e-4f);
    }

    // --- End to end ---------------------------------------------------------

    [Test]
    public void TokenClusterNearTheBottom_EndsUpSafeAndOffTheButtons()
    {
        // A lone card seated over a token low and right of the player: apply the
        // real pass order (safe area -> keep-out -> safe area) and check both.
        var slot = CombatLayoutRules.SpacedSlotFor(0, 1, 110f, Spacing);
        float cx = 200f + slot.X, cy = -240f + slot.Y;
        var cluster = CombatLayoutRules.Box.FromCentre(cx, cy, 150f, 204f);

        cluster = Translate(cluster, CombatLayoutRules.ShiftIntoSafeArea(cluster, Safe));
        cluster = Translate(cluster, CombatLayoutRules.ShiftOutOfKeepOut(cluster, Buttons));
        cluster = Translate(cluster, CombatLayoutRules.ShiftIntoSafeArea(cluster, Safe));

        Assert.GreaterOrEqual(cluster.MinX, Safe.MinX - 1e-3f);
        Assert.GreaterOrEqual(cluster.MinY, Safe.MinY - 1e-3f);
        Assert.LessOrEqual(cluster.MaxX, Safe.MaxX + 1e-3f);
        Assert.LessOrEqual(cluster.MaxY, Safe.MaxY + 1e-3f);

        bool clearOfButtons = cluster.MinX >= Buttons.MaxX || cluster.MaxX <= Buttons.MinX
                           || cluster.MinY >= Buttons.MaxY || cluster.MaxY <= Buttons.MinY;
        Assert.IsTrue(clearOfButtons, "cluster still overlaps the parked buttons");
    }

    static CombatLayoutRules.Box Translate(CombatLayoutRules.Box b, CombatLayoutRules.Anchor d)
        => new CombatLayoutRules.Box(b.MinX + d.X, b.MinY + d.Y, b.MaxX + d.X, b.MaxY + d.Y);
}
