using System.Collections.Generic;
using NUnit.Framework;

public class EnemyTraitCopyTests
{
    [Test]
    public void EveryTraitHasANonEmptyBadge()
    {
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsFalse(string.IsNullOrEmpty(IconMarkup.TraitBadge(t)), "no badge for " + t);
    }

    [Test]
    public void AllBadgesAreUnique()
    {
        var seen = new HashSet<string>();
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsTrue(seen.Add(IconMarkup.TraitBadge(t)), "duplicate badge for " + t);
    }

    [Test]
    public void EveryTraitHasANonEmptyName()
    {
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsFalse(string.IsNullOrEmpty(IconMarkup.TraitName(t)), "no name for " + t);
    }

    [Test]
    public void AllThirteenTraitsAreListed()
    {
        Assert.AreEqual(13, IconMarkup.AllTraits.Length);
    }

    [Test]
    public void AurasAreFlaggedAsAuras()
    {
        Assert.IsTrue(IconMarkup.IsAuraTrait(EnemyTrait.Warlord));
        Assert.IsTrue(IconMarkup.IsAuraTrait(EnemyTrait.Miasma));
        Assert.IsTrue(IconMarkup.IsAuraTrait(EnemyTrait.Ironclad));
        Assert.IsTrue(IconMarkup.IsAuraTrait(EnemyTrait.Outrider));
        Assert.IsFalse(IconMarkup.IsAuraTrait(EnemyTrait.Armored));
        Assert.IsFalse(IconMarkup.IsAuraTrait(EnemyTrait.Toxic));
    }

    [Test]
    public void HulkingIsK_BecauseHarryingTookH()
    {
        Assert.AreEqual("K", IconMarkup.TraitBadge(EnemyTrait.Hulking));
        Assert.AreEqual("H", IconMarkup.TraitBadge(EnemyTrait.Harrying));
    }

    [Test]
    public void EveryTraitHasANonEmptyRuleLine()
    {
        var tuning = new EnemyTraitTuning();
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsFalse(string.IsNullOrEmpty(EnemyTraitCopy.Rule(t, tuning)), "no rule for " + t);
    }

    [Test]
    public void RuleTextTracksTuning_NotHardcoded()
    {
        var a = new EnemyTraitTuning { armorSiegeMult = 2 };
        var b = new EnemyTraitTuning { armorSiegeMult = 3 };
        Assert.AreNotEqual(EnemyTraitCopy.Rule(EnemyTrait.Armored, a),
                           EnemyTraitCopy.Rule(EnemyTrait.Armored, b));
    }

    [Test]
    public void Split_ReturnsEachSetTrait()
    {
        var list = EnemyTraitCopy.Split(EnemyTrait.Armored | EnemyTrait.Toxic);
        Assert.AreEqual(2, list.Count);
        Assert.Contains(EnemyTrait.Armored, list);
        Assert.Contains(EnemyTrait.Toxic, list);
    }

    [Test]
    public void Split_NoneIsEmpty()
    {
        Assert.AreEqual(0, EnemyTraitCopy.Split(EnemyTrait.None).Count);
    }
}
