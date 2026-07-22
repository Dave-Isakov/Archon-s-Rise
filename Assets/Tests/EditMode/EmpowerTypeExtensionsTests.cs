using NUnit.Framework;

public class EmpowerTypeExtensionsTests
{
    [Test]
    public void SkillIconTintHex_ReturnsHexForRedYellowGreen()
    {
        Assert.AreEqual("FF0000", EmpowerType.Red.SkillIconTintHex());
        Assert.AreEqual("F5D90A", EmpowerType.Yellow.SkillIconTintHex());
        Assert.AreEqual("46A758", EmpowerType.Green.SkillIconTintHex());
    }

    [Test]
    public void SkillIconTintHex_PurpleAndNoneAreUntinted()
    {
        Assert.AreEqual("", EmpowerType.Purple.SkillIconTintHex());
        Assert.AreEqual("", EmpowerType.None.SkillIconTintHex());
    }
}
