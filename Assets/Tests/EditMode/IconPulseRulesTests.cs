using NUnit.Framework;

public class IconPulseRulesTests
{
    [Test]
    public void Pulses_WhenSourceCarriesTheIconsStat()
    {
        Assert.IsTrue(IconPulseRules.ShouldPulse(StatType.Attack, StatType.Attack, false));
    }

    [Test]
    public void DoesNotPulse_ForAnUnrelatedStat()
    {
        Assert.IsFalse(IconPulseRules.ShouldPulse(StatType.Attack, StatType.Explore, false));
    }

    // A Siege card co-flags Attack, so both those icons legitimately pulse.
    [Test]
    public void CompositeSource_PulsesEveryFlaggedIcon()
    {
        var siegeCard = StatType.Siege | StatType.Attack;
        Assert.IsTrue(IconPulseRules.ShouldPulse(siegeCard, StatType.Siege, false));
        Assert.IsTrue(IconPulseRules.ShouldPulse(siegeCard, StatType.Attack, false));
        Assert.IsFalse(IconPulseRules.ShouldPulse(siegeCard, StatType.Defend, false));
    }

    // The copy-paste landmine: HasFlag(None) is always true, so a duplicated
    // PlayerIcon whose type was never set would pulse on every single play.
    [Test]
    public void IconTypeNone_NeverPulses()
    {
        Assert.IsFalse(IconPulseRules.ShouldPulse(StatType.Attack, StatType.None, false));
        Assert.IsFalse(IconPulseRules.ShouldPulse(StatType.None, StatType.None, false));
    }

    [Test]
    public void SourceNone_NeverPulses()
    {
        Assert.IsFalse(IconPulseRules.ShouldPulse(StatType.None, StatType.Attack, false));
    }

    // PlayCommand.Undo() re-raises the SAME event as Execute(), so without this
    // gate undoing a play replays the increase animation while the stat drops.
    [Test]
    public void Undo_SuppressesAnOtherwiseMatchingPulse()
    {
        Assert.IsFalse(IconPulseRules.ShouldPulse(StatType.Attack, StatType.Attack, true));
        Assert.IsFalse(IconPulseRules.ShouldPulse(
            StatType.Siege | StatType.Attack, StatType.Siege, true));
    }
}
