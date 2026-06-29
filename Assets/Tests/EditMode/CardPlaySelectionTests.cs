using NUnit.Framework;

public class CardPlaySelectionTests
{
    static CardSnapshot Rally() =>
        new CardSnapshot(StatType.Attack | StatType.Influence, EmpowerType.Yellow, true,
            2, 0, 2, 0, 4, 0, 4, 0);

    static CardSnapshot Strike() => // single-stat normal, red-empowerable
        new CardSnapshot(StatType.Attack, EmpowerType.Red, false,
            2, 0, 0, 0, 3, 0, 0, 0);

    static CardSnapshot Wound() =>
        new CardSnapshot(StatType.Wound, EmpowerType.None, false,
            0, 0, 0, 0, 0, 0, 0, 0);

    [Test]
    public void Normal_SingleStat_ResolvesPrintedValue()
    {
        var s = new CardPlaySelection(Strike());
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.ResolveStats());
        Assert.AreEqual("+2 Attack", s.Describe());
    }

    [Test]
    public void Empowered_Normal_UsesEmpowerValue()
    {
        var s = new CardPlaySelection(Strike());
        s.SetEmpowered(true);
        Assert.IsTrue(s.EffectiveEmpowered());
        Assert.AreEqual(new[] { 3, 0, 0, 0 }, s.ResolveStats());
    }

    [Test]
    public void Choice_AppliesOnlyChosenStat()
    {
        var s = new CardPlaySelection(Rally());
        s.SetChoiceStat(StatType.Attack);
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.ResolveStats());
        s.SetChoiceStat(StatType.Influence);
        Assert.AreEqual(new[] { 0, 0, 2, 0 }, s.ResolveStats());
    }

    [Test]
    public void Improvise_GivesFlatOneToChosenStat()
    {
        var s = new CardPlaySelection(Rally());
        s.SetImproviseStat(StatType.Defend);
        Assert.AreEqual(PlayMode.Improvise, s.Mode);
        Assert.AreEqual(new[] { 0, 1, 0, 0 }, s.ResolveStats());
        Assert.AreEqual("+1 Defend (improvised)", s.Describe());
    }

    [Test]
    public void CanEmpower_FalseForImproviseAndForWound()
    {
        var imp = new CardPlaySelection(Rally());
        imp.SetImproviseStat(StatType.Attack);
        Assert.IsFalse(imp.CanEmpower());

        var wound = new CardPlaySelection(Wound());
        Assert.IsFalse(wound.CanEmpower());
        Assert.IsFalse(wound.IsPlayable());
    }

    // The bug: choosing Improvise must not destroy the choice selection.
    [Test]
    public void SwitchingToImproviseAndBack_PreservesChoice()
    {
        var s = new CardPlaySelection(Rally());
        s.SetChoiceStat(StatType.Influence);          // choice = Influence
        s.SetImproviseStat(StatType.Attack);          // mode -> Improvise
        s.SetMode(PlayMode.Choice);                   // back to choice
        Assert.AreEqual(StatType.Influence, s.ChoiceStat);
        Assert.AreEqual(new[] { 0, 0, 2, 0 }, s.ResolveStats());
    }

    // Empower flag survives a trip through Improvise (future-proofing the rule).
    [Test]
    public void EmpowerFlag_SurvivesImproviseRoundTrip()
    {
        var s = new CardPlaySelection(Strike());
        s.SetEmpowered(true);
        s.SetImproviseStat(StatType.Attack);          // CanEmpower now false
        Assert.IsFalse(s.EffectiveEmpowered());
        s.SetMode(PlayMode.Normal);                   // empower applies again
        Assert.IsTrue(s.EffectiveEmpowered());
    }
}
