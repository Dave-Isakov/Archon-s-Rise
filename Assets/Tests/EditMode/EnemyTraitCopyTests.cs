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
    public void TraitBadge_ReturnsItsSpriteAssetName()
    {
        Assert.AreEqual("<sprite=\"traitArmored\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Armored));
        Assert.AreEqual("<sprite=\"traitElusive\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Elusive));
        Assert.AreEqual("<sprite=\"traitHulking\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Hulking));
        Assert.AreEqual("<sprite=\"traitSwift\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Swift));
        Assert.AreEqual("<sprite=\"traitBrutal\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Brutal));
        Assert.AreEqual("<sprite=\"traitToxic\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Toxic));
        Assert.AreEqual("<sprite=\"traitLeech\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Leech));
        Assert.AreEqual("<sprite=\"traitHarrying\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Harrying));
        Assert.AreEqual("<sprite=\"traitVengeful\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Vengeful));
        Assert.AreEqual("<sprite=\"traitWarlord\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Warlord));
        Assert.AreEqual("<sprite=\"traitMiasma\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Miasma));
        Assert.AreEqual("<sprite=\"traitIronclad\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Ironclad));
        Assert.AreEqual("<sprite=\"traitOutrider\" index=0>", IconMarkup.TraitBadge(EnemyTrait.Outrider));
    }

    [Test]
    public void TraitBadgeTinted_AurasCarryColorAsSpriteAttribute_NotAWrappingColorTag()
    {
        // TMP sprites only honor color as an attribute ON the <sprite> tag itself
        // (and only with Tint enabled on the glyph) — a wrapping <color> tag does
        // nothing to a sprite glyph. See IconMarkup.CrystalTag for the existing
        // precedent this must match.
        Assert.AreEqual("<sprite=\"traitWarlord\" index=0 color=#F5D90A>",
            IconMarkup.TraitBadgeTinted(EnemyTrait.Warlord));
        Assert.AreEqual("<sprite=\"traitMiasma\" index=0 color=#F5D90A>",
            IconMarkup.TraitBadgeTinted(EnemyTrait.Miasma));
        Assert.AreEqual("<sprite=\"traitIronclad\" index=0 color=#F5D90A>",
            IconMarkup.TraitBadgeTinted(EnemyTrait.Ironclad));
        Assert.AreEqual("<sprite=\"traitOutrider\" index=0 color=#F5D90A>",
            IconMarkup.TraitBadgeTinted(EnemyTrait.Outrider));
    }

    [Test]
    public void TraitBadgeTinted_SelfTraitsAreUntinted()
    {
        Assert.AreEqual("<sprite=\"traitArmored\" index=0>", IconMarkup.TraitBadgeTinted(EnemyTrait.Armored));
        Assert.AreEqual("<sprite=\"traitBrutal\" index=0>", IconMarkup.TraitBadgeTinted(EnemyTrait.Brutal));
    }

    [Test]
    public void EveryTraitHasANonEmptyRuleLine()
    {
        var tuning = new EnemyTraitTuning();
        foreach (var t in IconMarkup.AllTraits)
            Assert.IsFalse(string.IsNullOrEmpty(EnemyTraitCopy.Rule(t, tuning)), "no rule for " + t);
    }

    [Test]
    public void EveryTraitHasANonEmptyLegendLine()
    {
        var tuning = new EnemyTraitTuning();
        foreach (EnemyTrait t in System.Enum.GetValues(typeof(EnemyTrait)))
        {
            if (t == EnemyTrait.None) continue;
            Assert.IsFalse(string.IsNullOrEmpty(EnemyTraitCopy.LegendLine(t, tuning)), "no legend line for " + t);
        }
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

    [Test]
    public void LegendLine_IsBadgeThenNameThenRule()
    {
        var tuning = new EnemyTraitTuning();
        string expected = IconMarkup.TraitBadgeTinted(EnemyTrait.Armored) + " " +
                           IconMarkup.TraitName(EnemyTrait.Armored) + " — " +
                           EnemyTraitCopy.Rule(EnemyTrait.Armored, tuning);
        Assert.AreEqual(expected, EnemyTraitCopy.LegendLine(EnemyTrait.Armored, tuning));
    }

    [Test]
    public void Legend_JoinsOneLegendLinePerSetTrait()
    {
        var tuning = new EnemyTraitTuning();
        var mask = EnemyTrait.Armored | EnemyTrait.Toxic;
        string expected = EnemyTraitCopy.LegendLine(EnemyTrait.Armored, tuning) + "\n" +
                           EnemyTraitCopy.LegendLine(EnemyTrait.Toxic, tuning);
        Assert.AreEqual(expected, EnemyTraitCopy.Legend(mask, tuning));
    }

    [Test]
    public void Legend_NoneIsEmpty()
    {
        Assert.AreEqual("", EnemyTraitCopy.Legend(EnemyTrait.None, new EnemyTraitTuning()));
    }
}
