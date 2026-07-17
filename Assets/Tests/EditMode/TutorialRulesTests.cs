using NUnit.Framework;

public class TutorialRulesTests
{
    // Mirrors the launch rail's shape: "" = informational Next-button step.
    static readonly string[] Rail =
    {
        "", "card-played", "", "player-moved",
        "combat-started", "enemy-resolved", "turn-ended", "",
    };

    static TutorialRules Fresh()
    {
        return new TutorialRules(Rail, true, 0, null, null);
    }

    [Test]
    public void NextAdvancesInformationalStep()
    {
        var t = Fresh();
        Assert.AreEqual(RailChange.StepChanged, t.NextPressed());
        Assert.AreEqual(1, t.RailStep);
    }

    [Test]
    public void EventStepIgnoresNextButton()
    {
        var t = Fresh();
        t.NextPressed(); // -> step 1 (card-played)
        Assert.AreEqual(RailChange.None, t.NextPressed());
        Assert.AreEqual(1, t.RailStep);
    }

    [Test]
    public void MatchingEventAdvances()
    {
        var t = Fresh();
        t.NextPressed();
        Assert.AreEqual(RailChange.StepChanged, t.NotifyEvent("card-played"));
        Assert.AreEqual(2, t.RailStep);
    }

    [Test]
    public void EarlyEventIsRecordedAndAutoCompletesItsStepLater()
    {
        var t = Fresh();
        t.NextPressed();               // step 1: card-played
        t.NotifyEvent("player-moved"); // explored ahead of the script
        Assert.AreEqual(1, t.RailStep);
        t.NotifyEvent("card-played");  // completes 1 -> lands on 2 (informational)
        Assert.AreEqual(2, t.RailStep);
        t.NextPressed();               // step 3 (player-moved) already fired -> chains to 4
        Assert.AreEqual(4, t.RailStep);
    }

    [Test]
    public void RailCompletionReported()
    {
        var t = Fresh();
        t.NextPressed();
        t.NotifyEvent("card-played");
        t.NextPressed();
        t.NotifyEvent("player-moved");
        t.NotifyEvent("combat-started");
        t.NotifyEvent("enemy-resolved");
        Assert.AreEqual(RailChange.StepChanged, t.NotifyEvent("turn-ended")); // -> send-off
        Assert.AreEqual(RailChange.RailCompleted, t.NextPressed());
        Assert.AreEqual(TutorialRules.Done, t.RailStep);
        Assert.IsFalse(t.RailActive);
    }

    [Test]
    public void SkipEndsRailAndSuppressesOneShots()
    {
        var t = Fresh();
        Assert.AreEqual(RailChange.RailCompleted, t.Skip(new[] { "tip.wound", "tip.crystal" }));
        Assert.AreEqual(TutorialRules.Done, t.RailStep);
        Assert.IsFalse(t.NotifyOneShot("tip.wound"));
        Assert.IsFalse(t.NotifyOneShot("tip.crystal"));
    }

    [Test]
    public void OneShotFiresExactlyOnceAfterRail()
    {
        var t = new TutorialRules(Rail, true, TutorialRules.Done, null, null);
        Assert.IsTrue(t.NotifyOneShot("tip.wound"));
        Assert.IsFalse(t.NotifyOneShot("tip.wound"));
    }

    [Test]
    public void OneShotDuringRailIsDeferredOldestFirst()
    {
        var t = new TutorialRules(new[] { "" }, true, 0, null, null);
        Assert.IsFalse(t.NotifyOneShot("tip.wound"));
        Assert.IsFalse(t.NotifyOneShot("tip.crystal"));
        Assert.IsFalse(t.NotifyOneShot("tip.wound")); // duplicate defers once
        string id;
        Assert.IsFalse(t.TryDequeuePendingOneShot(out id)); // rail still running
        Assert.AreEqual(RailChange.RailCompleted, t.NextPressed());
        Assert.IsTrue(t.TryDequeuePendingOneShot(out id));
        Assert.AreEqual("tip.wound", id);
        Assert.IsTrue(t.TryDequeuePendingOneShot(out id));
        Assert.AreEqual("tip.crystal", id);
        Assert.IsFalse(t.TryDequeuePendingOneShot(out id));
        Assert.IsFalse(t.NotifyOneShot("tip.wound")); // now seen
    }

    [Test]
    public void DisabledMutesRailAndOneShots()
    {
        var t = new TutorialRules(Rail, false, 0, null, null);
        Assert.IsFalse(t.RailActive);
        Assert.AreEqual(RailChange.None, t.NextPressed());
        Assert.AreEqual(RailChange.None, t.NotifyEvent("card-played"));
        Assert.IsFalse(t.NotifyOneShot("tip.wound"));
        t.SetEnabled(true);
        Assert.AreEqual(0, t.RailStep); // resumes at the same step
        Assert.IsTrue(t.RailActive);
        // The card played while muted still counts for auto-completion.
        Assert.AreEqual(RailChange.StepChanged, t.NextPressed());
        Assert.AreEqual(2, t.RailStep);
    }

    [Test]
    public void ResumesAtSavedStep()
    {
        var t = new TutorialRules(Rail, true, 3, null, null);
        Assert.AreEqual(3, t.RailStep);
        Assert.AreEqual(RailChange.StepChanged, t.NotifyEvent("player-moved"));
        Assert.AreEqual(4, t.RailStep);
    }

    [Test]
    public void SavedStepBeyondRailIsDone()
    {
        var t = new TutorialRules(Rail, true, 99, null, null);
        Assert.AreEqual(TutorialRules.Done, t.RailStep);
    }

    [Test]
    public void SeenOneShotsFromSaveAreRespected()
    {
        var t = new TutorialRules(Rail, true, TutorialRules.Done, new[] { "tip.wound" }, null);
        Assert.IsFalse(t.NotifyOneShot("tip.wound"));
        Assert.IsTrue(t.NotifyOneShot("tip.crystal"));
    }

    [Test]
    public void HelpPulsesUntilSeenAndOnlyWhileEnabled()
    {
        var t = Fresh();
        Assert.IsTrue(t.ShouldPulseHelp("hud"));
        t.MarkHelpSeen("hud");
        Assert.IsFalse(t.ShouldPulseHelp("hud"));
        Assert.IsTrue(t.ShouldPulseHelp("recruit"));
        t.SetEnabled(false);
        Assert.IsFalse(t.ShouldPulseHelp("recruit"));
    }
}
