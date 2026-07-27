using NUnit.Framework;
using ArchonsRise.HexTooltipInfo;
using ArchonsRise.Shrines;

public class TileDescriptorShrineTests
{
    [Test]
    public void Shrine_Live_ShowsCostAndGamble()
    {
        string s = TileDescriptor.Shrine(ShrineVisualState.Live, 4);
        StringAssert.Contains("4", s);
        StringAssert.Contains("amble", s); // "gamble"
    }

    [Test]
    public void Shrine_Guarding_ShowsGuardedState()
    {
        string s = TileDescriptor.Shrine(ShrineVisualState.Guarding, 4);
        StringAssert.Contains("uard", s); // "guarded"/"Guardian"
    }

    [Test]
    public void Shrine_Consumed_ShowsSpent()
    {
        string s = TileDescriptor.Shrine(ShrineVisualState.ConsumedDormant, 4);
        StringAssert.Contains("pent", s); // "Spent"
    }
}
