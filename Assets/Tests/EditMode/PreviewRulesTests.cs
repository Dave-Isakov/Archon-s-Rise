using System.Collections.Generic;
using NUnit.Framework;

public class PreviewRulesTests
{
    [Test]
    public void CanPreview_TrueByDefault_FalseWhenHidden()
    {
        Assert.IsTrue(PreviewRules.CanPreview());       // no blind source today
        Assert.IsTrue(PreviewRules.CanPreview(false));
        Assert.IsFalse(PreviewRules.CanPreview(true));  // future blindness passes true
    }

    [Test]
    public void EncounterVisible_TrueOnlyWhenAllVisible()
    {
        Assert.IsTrue(PreviewRules.EncounterVisible(new List<bool>()));                // empty → nothing blind
        Assert.IsTrue(PreviewRules.EncounterVisible(new List<bool> { true, true }));
        Assert.IsFalse(PreviewRules.EncounterVisible(new List<bool> { true, false })); // one blind blinds all
        Assert.IsFalse(PreviewRules.EncounterVisible(new List<bool> { false }));
    }

    [Test]
    public void RemainingGuardians_ReturnsTailAfterDefeats()
    {
        var roster = new List<string> { "a", "b", "c" };
        Assert.AreEqual(new[] { "a", "b", "c" }, PreviewRules.RemainingGuardians(roster, 0));
        Assert.AreEqual(new[] { "c" }, PreviewRules.RemainingGuardians(roster, 2));
        Assert.AreEqual(new string[0], PreviewRules.RemainingGuardians(roster, 3)); // conquered → empty
    }
}
