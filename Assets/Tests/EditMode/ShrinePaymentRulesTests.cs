using NUnit.Framework;
using ArchonsRise.Shrines;

// Buckets throughout: 0=Red 1=Yellow 2=Green 3=Purple 4=Wild, matching
// CrystalInventory.ShrinePaymentColors + its trailing wild slot.
public class ShrinePaymentRulesTests
{
    const int E = ShrinePaymentRules.Empty;

    static int[] Picks(params int[] p) => p;

    [Test]
    public void NextPick_FromEmpty_TakesFirstAffordableBucket()
    {
        var holdings = new[] { 0, 0, 2, 0, 0 };      // Green only
        var picks = Picks(E, E);
        Assert.AreEqual(2, ShrinePaymentRules.NextPick(holdings, picks, 0));
    }

    [Test]
    public void NextPick_SkipsBucketsFullyClaimedByOtherSlots()
    {
        // Two Reds exist and both other slots already claim one each, so the
        // third slot must move past Red.
        var holdings = new[] { 2, 0, 1, 0, 0 };
        var picks = Picks(0, 0, E);
        Assert.AreEqual(2, ShrinePaymentRules.NextPick(holdings, picks, 2));
    }

    [Test]
    public void NextPick_WrapsToEmptyAfterTheLastBucket()
    {
        var holdings = new[] { 0, 0, 0, 0, 1 };      // Wild only
        var picks = Picks(4);                         // already on the last bucket
        Assert.AreEqual(E, ShrinePaymentRules.NextPick(holdings, picks, 0));
    }

    [Test]
    public void NextPick_ReturnsEmptyWhenNothingIsSpare()
    {
        var holdings = new[] { 0, 0, 0, 0, 0 };
        var picks = Picks(E);
        Assert.AreEqual(E, ShrinePaymentRules.NextPick(holdings, picks, 0));
    }

    // A slot re-cycling must not count its OWN current claim against itself,
    // or the last crystal of a color could never be re-selected.
    [Test]
    public void NextPick_DoesNotCountTheSlotsOwnClaim()
    {
        var holdings = new[] { 1, 0, 0, 0, 1 };      // one Red, one Wild
        var picks = Picks(4, E);                      // slot 0 holds the Wild
        // Slot 1 sees Red spare (slot 0 isn't claiming it) and takes it.
        Assert.AreEqual(0, ShrinePaymentRules.NextPick(holdings, picks, 1));
    }

    // The full walk from the design example: Red x2, Green x1, Wild x1.
    [Test]
    public void NextPick_WalksTheAuthoredExample()
    {
        var holdings = new[] { 2, 0, 1, 0, 1 };
        var picks = Picks(E, E, E, E);

        picks[0] = ShrinePaymentRules.NextPick(holdings, picks, 0);
        Assert.AreEqual(0, picks[0]);                 // Red

        picks[1] = ShrinePaymentRules.NextPick(holdings, picks, 1);
        Assert.AreEqual(0, picks[1]);                 // the second Red

        picks[2] = ShrinePaymentRules.NextPick(holdings, picks, 2);
        Assert.AreEqual(2, picks[2]);                 // Red exhausted -> Green

        picks[2] = ShrinePaymentRules.NextPick(holdings, picks, 2);
        Assert.AreEqual(4, picks[2]);                 // -> Wild

        picks[2] = ShrinePaymentRules.NextPick(holdings, picks, 2);
        Assert.AreEqual(E, picks[2]);                 // -> empty, slot un-set
    }

    [Test]
    public void Spare_SubtractsOtherSlotClaimsOnly()
    {
        var holdings = new[] { 3, 0, 0, 0, 0 };
        var picks = Picks(0, 0, E);
        Assert.AreEqual(1, ShrinePaymentRules.Spare(holdings, picks, 2, 0));
        // From slot 0's own perspective its claim doesn't count against it.
        Assert.AreEqual(2, ShrinePaymentRules.Spare(holdings, picks, 0, 0));
    }

    [Test]
    public void IsComplete_FalseWhileAnySlotIsEmpty()
    {
        Assert.IsFalse(ShrinePaymentRules.IsComplete(Picks(0, 1, E, 3)));
    }

    [Test]
    public void IsComplete_TrueWhenEverySlotHoldsACrystal()
    {
        Assert.IsTrue(ShrinePaymentRules.IsComplete(Picks(0, 0, 2, 4)));
    }

    [Test]
    public void IsComplete_FalseForNoSlots()
    {
        Assert.IsFalse(ShrinePaymentRules.IsComplete(Picks()));
    }
}
