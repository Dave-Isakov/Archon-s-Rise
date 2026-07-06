using NUnit.Framework;

public class InspectorNavRulesTests
{
    // Within-section cycling only needs option counts (default: 3 choice segments, 4 improvise stats).
    static InspectorNavPosition Move(InspectorNavPosition p, int dx, int dy,
        int choiceOptions = 3, int improviseOptions = 4)
        => InspectorNavRules.Move(p, dx, dy, choiceOptions, improviseOptions);

    static InspectorNavPosition At(InspectorSection s, int option = 0) => new InspectorNavPosition(s, option);

    [Test]
    public void Open_StartsOnPlay()
    {
        var p = InspectorNavRules.Open();
        Assert.AreEqual(InspectorSection.Play, p.Section);
        Assert.AreEqual(0, p.Option);
    }

    // --- Section entry (shoulder buttons) ---

    [Test]
    public void EnterChoice_Reachable_JumpsToOptionZero()
    {
        Assert.AreEqual(InspectorSection.Choice, InspectorNavRules.EnterChoice(At(InspectorSection.Play), true).Section);
        Assert.AreEqual(0, InspectorNavRules.EnterChoice(At(InspectorSection.Improvise, 2), true).Option);
    }

    [Test]
    public void EnterChoice_Unreachable_Stays()
    {
        var p = At(InspectorSection.Improvise, 2);
        var r = InspectorNavRules.EnterChoice(p, false);
        Assert.AreEqual(InspectorSection.Improvise, r.Section);
        Assert.AreEqual(2, r.Option);
    }

    [Test]
    public void EnterImprovise_Reachable_JumpsToOptionZero()
    {
        Assert.AreEqual(InspectorSection.Improvise, InspectorNavRules.EnterImprovise(At(InspectorSection.Play), true).Section);
        Assert.AreEqual(0, InspectorNavRules.EnterImprovise(At(InspectorSection.Choice, 1), true).Option);
    }

    [Test]
    public void EnterImprovise_Unreachable_Stays()
    {
        var r = InspectorNavRules.EnterImprovise(At(InspectorSection.Play), false);
        Assert.AreEqual(InspectorSection.Play, r.Section);
    }

    // --- Within-section directional cycling ---

    [Test]
    public void Choice_LeftRight_CyclesAndWraps()
    {
        Assert.AreEqual(1, Move(At(InspectorSection.Choice, 0), +1, 0).Option);
        Assert.AreEqual(0, Move(At(InspectorSection.Choice, 2), +1, 0).Option); // wrap right
        Assert.AreEqual(2, Move(At(InspectorSection.Choice, 0), -1, 0).Option); // wrap left
    }

    [Test]
    public void Choice_Down_GoesToPlay_Up_Stays()
    {
        Assert.AreEqual(InspectorSection.Play, Move(At(InspectorSection.Choice, 1), 0, -1).Section);
        var up = Move(At(InspectorSection.Choice, 1), 0, +1);
        Assert.AreEqual(InspectorSection.Choice, up.Section);
        Assert.AreEqual(1, up.Option);
    }

    [Test]
    public void Improvise_UpDown_Cycles_BottomToPlay_TopStays()
    {
        Assert.AreEqual(1, Move(At(InspectorSection.Improvise, 0), 0, -1).Option); // down
        Assert.AreEqual(1, Move(At(InspectorSection.Improvise, 2), 0, +1).Option); // up
        Assert.AreEqual(InspectorSection.Play, Move(At(InspectorSection.Improvise, 3), 0, -1).Section); // down past last
        var top = Move(At(InspectorSection.Improvise, 0), 0, +1); // up at top stays
        Assert.AreEqual(InspectorSection.Improvise, top.Section);
        Assert.AreEqual(0, top.Option);
    }

    [Test]
    public void Improvise_LeftRight_Stays()
    {
        Assert.AreEqual(InspectorSection.Improvise, Move(At(InspectorSection.Improvise, 1), +1, 0).Section);
        Assert.AreEqual(InspectorSection.Improvise, Move(At(InspectorSection.Improvise, 1), -1, 0).Section);
    }

    [Test]
    public void Play_IsInert()
    {
        Assert.AreEqual(InspectorSection.Play, Move(At(InspectorSection.Play), +1, 0).Section);
        Assert.AreEqual(InspectorSection.Play, Move(At(InspectorSection.Play), 0, +1).Section);
        Assert.AreEqual(InspectorSection.Play, Move(At(InspectorSection.Play), 0, -1).Section);
    }

    // --- Auto-default to Play when the focused section becomes unreachable ---

    [Test]
    public void ClampReachable_UnreachableSection_SnapsToPlay()
    {
        Assert.AreEqual(InspectorSection.Play, InspectorNavRules.ClampReachable(At(InspectorSection.Choice, 1), false, true).Section);
        Assert.AreEqual(InspectorSection.Play, InspectorNavRules.ClampReachable(At(InspectorSection.Improvise, 2), true, false).Section);
        Assert.AreEqual(InspectorSection.Play, InspectorNavRules.ClampReachable(At(InspectorSection.Empower), true, true).Section);
    }

    [Test]
    public void ClampReachable_ReachableSection_Unchanged()
    {
        var r = InspectorNavRules.ClampReachable(At(InspectorSection.Choice, 1), true, true);
        Assert.AreEqual(InspectorSection.Choice, r.Section);
        Assert.AreEqual(1, r.Option);
        Assert.AreEqual(InspectorSection.Play, InspectorNavRules.ClampReachable(At(InspectorSection.Play), false, false).Section);
    }
}
