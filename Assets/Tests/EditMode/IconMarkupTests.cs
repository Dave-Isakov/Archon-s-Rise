using NUnit.Framework;

public class IconMarkupTests
{
    [Test]
    public void Tag_UsesExistingAssetNames()
    {
        Assert.AreEqual("<sprite=\"Sword\" index=0>", IconMarkup.Tag(IconConcept.Attack));
        Assert.AreEqual("<sprite=\"shield\" index=0>", IconMarkup.Tag(IconConcept.Defend));
        Assert.AreEqual("<sprite=\"scroll\" index=0>", IconMarkup.Tag(IconConcept.Explore));
        Assert.AreEqual("<sprite=\"gem\" index=0>", IconMarkup.Tag(IconConcept.Influence));
        Assert.AreEqual("<sprite=\"Heal\" index=0>", IconMarkup.Tag(IconConcept.Heal));
        Assert.AreEqual("<sprite=\"crystal\" index=0>", IconMarkup.Tag(IconConcept.Crystal));
        Assert.AreEqual("<sprite=\"siege\" index=0>", IconMarkup.Tag(IconConcept.Siege));
    }

    [Test]
    public void Tag_NewConceptsGetNewNames()
    {
        Assert.AreEqual("<sprite=\"wound\" index=0>", IconMarkup.Tag(IconConcept.Wound));
        Assert.AreEqual("<sprite=\"hp\" index=0>", IconMarkup.Tag(IconConcept.Hp));
        Assert.AreEqual("<sprite=\"doom\" index=0>", IconMarkup.Tag(IconConcept.Doom));
        Assert.AreEqual("<sprite=\"xp\" index=0>", IconMarkup.Tag(IconConcept.Experience));
        Assert.AreEqual("<sprite=\"army\" index=0>", IconMarkup.Tag(IconConcept.Army));
        Assert.AreEqual("<sprite=\"town\" index=0>", IconMarkup.Tag(IconConcept.Town));
        Assert.AreEqual("<sprite=\"keep\" index=0>", IconMarkup.Tag(IconConcept.Keep));
        Assert.AreEqual("<sprite=\"castle\" index=0>", IconMarkup.Tag(IconConcept.Castle));
        Assert.AreEqual("<sprite=\"dungeon\" index=0>", IconMarkup.Tag(IconConcept.Dungeon));
        Assert.AreEqual("<sprite=\"empower\" index=0>", IconMarkup.Tag(IconConcept.Empower));
    }

    [Test]
    public void TmpName_NonEmptyForEveryConcept()
    {
        foreach (IconConcept c in System.Enum.GetValues(typeof(IconConcept)))
            Assert.IsFalse(string.IsNullOrEmpty(IconMarkup.TmpName(c)), c + " has no tmp name");
    }

    [Test]
    public void Cost_IsIconThenNumber()
    {
        Assert.AreEqual("<sprite=\"gem\" index=0>3", IconMarkup.Cost(IconConcept.Influence, 3));
        Assert.AreEqual("<sprite=\"scroll\" index=0>2", IconMarkup.Cost(IconConcept.Explore, 2));
    }

    [Test]
    public void CrystalTag_TintsByColor()
    {
        Assert.AreEqual("<sprite=\"crystal\" index=0 color=#E5484D>", IconMarkup.CrystalTag(EmpowerType.Red));
        Assert.AreEqual("<sprite=\"crystal\" index=0 color=#F5D90A>", IconMarkup.CrystalTag(EmpowerType.Yellow));
        Assert.AreEqual("<sprite=\"crystal\" index=0 color=#46A758>", IconMarkup.CrystalTag(EmpowerType.Green));
        Assert.AreEqual("<sprite=\"crystal\" index=0 color=#8E4EC6>", IconMarkup.CrystalTag(EmpowerType.Purple));
    }

    [Test]
    public void CrystalTag_NoneAndAllColorsAreUntinted()
    {
        Assert.AreEqual("<sprite=\"crystal\" index=0>", IconMarkup.CrystalTag(EmpowerType.None));
        var all = EmpowerType.Red | EmpowerType.Yellow | EmpowerType.Green | EmpowerType.Purple;
        Assert.AreEqual("<sprite=\"crystal\" index=0>", IconMarkup.CrystalTag(all));
    }

    [Test]
    public void ActionStatOrder_IsCanonical()
    {
        var o = IconMarkup.ActionStatOrder;
        Assert.AreEqual(4, o.Length);
        Assert.AreEqual(IconConcept.Attack, o[0]);
        Assert.AreEqual(IconConcept.Defend, o[1]);
        Assert.AreEqual(IconConcept.Explore, o[2]);
        Assert.AreEqual(IconConcept.Influence, o[3]);
    }

    [Test]
    public void TryForStat_MapsSingleFlags()
    {
        IconConcept c;
        Assert.IsTrue(IconMarkup.TryForStat(StatType.Attack, out c));
        Assert.AreEqual(IconConcept.Attack, c);
        Assert.IsTrue(IconMarkup.TryForStat(StatType.Siege, out c));
        Assert.AreEqual(IconConcept.Siege, c);
        Assert.IsTrue(IconMarkup.TryForStat(StatType.Wound, out c));
        Assert.AreEqual(IconConcept.Wound, c);
        Assert.IsFalse(IconMarkup.TryForStat(StatType.Refresh, out c));
        Assert.IsFalse(IconMarkup.TryForStat(StatType.None, out c));
        Assert.IsFalse(IconMarkup.TryForStat(StatType.Attack | StatType.Defend, out c));
    }
}
