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

    static CardSnapshot Crystallization() => // crystal card: no action stats, makes crystals
        new CardSnapshot(StatType.Crystal, EmpowerType.Purple, false,
            0, 0, 0, 0, 0, 0, 0, 0);

    static CardSnapshot HealCard() => // heal card: no action stats, heals wounds
        new CardSnapshot(StatType.Heal, EmpowerType.Green, false,
            0, 0, 0, 0, 0, 0, 0, 0);

    [Test]
    public void Normal_SingleStat_ResolvesPrintedValue()
    {
        var s = new CardPlaySelection(Strike());
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.ResolveStats());
        Assert.AreEqual("<sprite=\"Sword\" index=0>+2", s.Describe());
    }

    [Test]
    public void Empowered_Normal_UsesEmpowerValue()
    {
        var s = new CardPlaySelection(Strike());
        s.SetEmpowered(true);
        Assert.IsTrue(s.EffectiveEmpowered());
        Assert.AreEqual(new[] { 3, 0, 0, 0 }, s.ResolveStats());
    }

    // A choice card must never resolve as "both stats". Fresh out of the constructor it
    // starts in Choice mode on its first flag, so playing it untouched gives one stat —
    // not the Normal-mode sum of every flag.
    [Test]
    public void Choice_DefaultsToChoiceModeOnFirstStat()
    {
        var s = new CardPlaySelection(Rally());          // Attack|Influence, isChoice
        Assert.AreEqual(PlayMode.Choice, s.Mode);
        Assert.AreEqual(StatType.Attack, s.ChoiceStat);  // first flag
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.ResolveStats());
    }

    // A non-choice multi-stat card still sums every flag in Normal mode.
    [Test]
    public void NonChoice_MultiStat_StartsNormalAndSums()
    {
        var both = new CardSnapshot(StatType.Attack | StatType.Influence, EmpowerType.None, false,
            2, 0, 2, 0, 0, 0, 0, 0);
        var s = new CardPlaySelection(both);
        Assert.AreEqual(PlayMode.Normal, s.Mode);
        Assert.AreEqual(new[] { 2, 0, 2, 0 }, s.ResolveStats());
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
        Assert.AreEqual("<sprite=\"shield\" index=0>+1 (improvised)", s.Describe());
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

    // Crystal and Heal cards have no action-stat flags but are still playable
    // (they resolve Normal and let Player create crystals / heal). Only Wounds are unplayable.
    [Test]
    public void NonActionCards_ArePlayable()
    {
        Assert.IsTrue(new CardPlaySelection(Crystallization()).IsPlayable());
        Assert.IsTrue(new CardPlaySelection(HealCard()).IsPlayable());
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

    // PreviewStats reports the would-be totals for a hypothetical empower flag,
    // independent of the live Empowered flag, and never mutates the selection.
    [Test]
    public void PreviewStats_ReportsBaseAndEmpoweredWithoutMutating()
    {
        var s = new CardPlaySelection(Strike());          // single Attack, base 2 / empower 3
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.PreviewStats(false));
        Assert.AreEqual(new[] { 3, 0, 0, 0 }, s.PreviewStats(true));
        // unchanged: still not empowered, ResolveStats still base
        Assert.IsFalse(s.EffectiveEmpowered());
        Assert.AreEqual(new[] { 2, 0, 0, 0 }, s.ResolveStats());
    }

    [Test]
    public void PreviewStats_Choice_UsesChosenStatOnly()
    {
        var s = new CardPlaySelection(Rally());           // Attack|Influence choice, base 2/2 emp 4/4
        s.SetChoiceStat(StatType.Influence);
        Assert.AreEqual(new[] { 0, 0, 2, 0 }, s.PreviewStats(false));
        Assert.AreEqual(new[] { 0, 0, 4, 0 }, s.PreviewStats(true));
    }

    [Test]
    public void PreviewStats_Improvise_IgnoresEmpower()
    {
        var s = new CardPlaySelection(Rally());
        s.SetImproviseStat(StatType.Defend);
        Assert.AreEqual(new[] { 0, 1, 0, 0 }, s.PreviewStats(false));
        Assert.AreEqual(new[] { 0, 1, 0, 0 }, s.PreviewStats(true)); // flat +1 regardless
    }

    static CardSnapshot Converter(bool requiresEmpower)
    {
        // Defend 3 / empower Defend 5, Convert all Defend -> Attack (Shield Bash shape)
        return new CardSnapshot(StatType.Defend, EmpowerType.Red, false,
            0, 3, 0, 0,
            0, 5, 0, 0,
            StatType.Attack, StatType.Defend, requiresEmpower);
    }

    [Test]
    public void Convert_DefaultsOff()
    {
        var sel = new CardPlaySelection(Converter(false));
        Assert.IsFalse(sel.ConvertOn);
        Assert.IsTrue(sel.HasConversion);
        Assert.IsTrue(sel.CanConvert());
        Assert.IsFalse(sel.EffectiveConvert());
    }

    [Test]
    public void Convert_OptInArms()
    {
        var sel = new CardPlaySelection(Converter(false));
        sel.SetConvert(true);
        Assert.IsTrue(sel.EffectiveConvert());
    }

    [Test]
    public void Convert_NoConversionCardCannotConvert()
    {
        var sel = new CardPlaySelection(new CardSnapshot(
            StatType.Defend, EmpowerType.None, false, 0, 3, 0, 0, 0, 5, 0, 0));
        Assert.IsFalse(sel.HasConversion);
        sel.SetConvert(true);
        Assert.IsFalse(sel.EffectiveConvert());
    }

    [Test]
    public void Convert_LockedWhileImprovising()
    {
        var sel = new CardPlaySelection(Converter(false));
        sel.SetConvert(true);
        sel.SetImproviseStat(StatType.Attack);
        Assert.IsFalse(sel.CanConvert());
        Assert.IsFalse(sel.EffectiveConvert());
    }

    [Test]
    public void Convert_RequiresEmpowerGatesUntilEmpowered()
    {
        var sel = new CardPlaySelection(Converter(true));
        sel.SetConvert(true);
        Assert.IsFalse(sel.CanConvert());
        sel.SetEmpowered(true);
        Assert.IsTrue(sel.CanConvert());
        Assert.IsTrue(sel.EffectiveConvert());
    }

    [Test]
    public void IsPlayable_RefreshOnlyCardIsPlayable()
    {
        var sel = new CardPlaySelection(new CardSnapshot(
            StatType.Refresh, EmpowerType.Green, false, 0, 0, 0, 0, 0, 0, 0, 0));
        Assert.IsTrue(sel.IsPlayable());
    }
}
