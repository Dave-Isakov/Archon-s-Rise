using System.Collections.Generic;
using NUnit.Framework;

public class UnitPlaySelectionTests
{
    static UnitOption Opt(UnitEffect e, int amount, EmpowerType cost = EmpowerType.None,
        EmpowerType grant = EmpowerType.None)
        => new UnitOption { effect = e, amount = amount, crystalCost = cost, grantColor = grant };

    [Test]
    public void Preselects_First_Affordable_Option()
    {
        var sel = new UnitPlaySelection(
            new List<UnitOption> { Opt(UnitEffect.Defend, 6, EmpowerType.Red), Opt(UnitEffect.Defend, 3) },
            new List<bool> { false, true });
        Assert.AreEqual(1, sel.SelectedIndex);
        Assert.IsTrue(sel.CanUse);
    }

    [Test]
    public void No_Affordable_Option_Selects_First_And_Blocks_Use()
    {
        var sel = new UnitPlaySelection(
            new List<UnitOption> { Opt(UnitEffect.Defend, 6, EmpowerType.Red) },
            new List<bool> { false });
        Assert.AreEqual(0, sel.SelectedIndex);
        Assert.IsFalse(sel.CanUse);
    }

    [Test]
    public void Select_Lands_On_Locked_Rows_But_CanUse_Stays_False()
    {
        var sel = new UnitPlaySelection(
            new List<UnitOption> { Opt(UnitEffect.Attack, 2), Opt(UnitEffect.Siege, 2, EmpowerType.Red) },
            new List<bool> { true, false });
        sel.Select(1);
        Assert.AreEqual(1, sel.SelectedIndex);
        Assert.IsFalse(sel.CanUse);
        sel.Select(0);
        Assert.IsTrue(sel.CanUse);
    }

    [Test]
    public void Select_Out_Of_Range_Is_Ignored()
    {
        var sel = new UnitPlaySelection(
            new List<UnitOption> { Opt(UnitEffect.Attack, 2) }, new List<bool> { true });
        sel.Select(5);
        Assert.AreEqual(0, sel.SelectedIndex);
        sel.Select(-1);
        Assert.AreEqual(0, sel.SelectedIndex);
    }

    [Test]
    public void Describe_Free_Stat_Option()
    {
        Assert.AreEqual("<sprite=\"Sword\" index=0> Attack 2",
            UnitOptionText.Describe(Opt(UnitEffect.Attack, 2)));
    }

    [Test]
    public void Describe_Costed_Option_Appends_Cost()
    {
        Assert.AreEqual("<sprite=\"shield\" index=0> Defend 6 — <sprite=\"crystal\" index=0 color=#E5484D>1",
            UnitOptionText.Describe(Opt(UnitEffect.Defend, 6, EmpowerType.Red)));
    }

    [Test]
    public void Describe_AnyColor_Cost()
    {
        var anyColor = EmpowerType.Red | EmpowerType.Yellow | EmpowerType.Green | EmpowerType.Purple;
        Assert.AreEqual("<sprite=\"Heal\" index=0> Heal 1 — <sprite=\"crystal\" index=0>1 (any color)",
            UnitOptionText.Describe(Opt(UnitEffect.Heal, 1, anyColor)));
    }

    [Test]
    public void Describe_Crystallize_Shows_Grant_Color()
    {
        Assert.AreEqual("<sprite=\"crystal\" index=0 color=#F5D90A> Crystallize 1",
            UnitOptionText.Describe(Opt(UnitEffect.Crystallize, 1, EmpowerType.None, EmpowerType.Yellow)));
    }

    [Test]
    public void Empty_Options_Cannot_Use()
    {
        var sel = new UnitPlaySelection(new List<UnitOption>(), new List<bool>());
        Assert.AreEqual(-1, sel.SelectedIndex);
        Assert.IsFalse(sel.CanUse);
    }
}
