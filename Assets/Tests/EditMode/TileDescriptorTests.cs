using NUnit.Framework;
using ArchonsRise.HexTooltipInfo;

public class TileDescriptorTests
{
    [Test]
    public void Hotspot_ShowsChargeCount()
    {
        string s = TileDescriptor.Hotspot(EmpowerType.Red, 3);
        StringAssert.Contains("3", s);
        StringAssert.Contains("sprite", s); // an IconMarkup crystal tag is present
    }

    [Test]
    public void Hotspot_ShowsInfinityWhenUnlimited()
    {
        string s = TileDescriptor.Hotspot(EmpowerType.Green, -1);
        StringAssert.Contains("∞", s);
    }

    [Test]
    public void Hotspot_ShowsDepletedAtZero()
    {
        string s = TileDescriptor.Hotspot(EmpowerType.Purple, 0);
        StringAssert.Contains("epleted", s); // "Depleted"/"depleted"
    }

    [Test]
    public void Dungeon_ShowsProgress()
    {
        string s = TileDescriptor.Dungeon("Wyrm's Hollow", 1, 3);
        StringAssert.Contains("Wyrm's Hollow", s);
        StringAssert.Contains("1", s);
        StringAssert.Contains("3", s);
    }
}
