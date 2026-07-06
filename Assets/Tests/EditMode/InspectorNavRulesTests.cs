using NUnit.Framework;

public class InspectorNavRulesTests
{
    // Shorthand: full choice card (all sections reachable), 3 choice segments.
    static InspectorNavPosition Move(InspectorNavPosition p, int dx, int dy,
        bool choice = true, bool improvise = true, bool empower = true,
        int choiceOptions = 3, int improviseOptions = 4)
        => InspectorNavRules.Move(p, dx, dy, choice, improvise, empower, choiceOptions, improviseOptions);

    static InspectorNavPosition At(InspectorSection s, int option = 0) => new InspectorNavPosition(s, option);

    [Test]
    public void Open_StartsOnPlayButton()
    {
        var p = InspectorNavRules.Open();
        Assert.AreEqual(InspectorSection.Play, p.Section);
        Assert.AreEqual(0, p.Option);
    }

    [Test]
    public void Play_LeftRight_TogglesPlayAndBack()
    {
        Assert.AreEqual(1, Move(At(InspectorSection.Play, 0), +1, 0).Option);
        Assert.AreEqual(0, Move(At(InspectorSection.Play, 1), -1, 0).Option);
    }

    [Test]
    public void Play_Up_PrefersChoiceThenImproviseThenEmpower()
    {
        Assert.AreEqual(InspectorSection.Choice,    Move(At(InspectorSection.Play), 0, +1).Section);
        Assert.AreEqual(InspectorSection.Improvise, Move(At(InspectorSection.Play), 0, +1, choice: false).Section);
        Assert.AreEqual(InspectorSection.Empower,   Move(At(InspectorSection.Play), 0, +1, choice: false, improvise: false).Section);
        Assert.AreEqual(InspectorSection.Play,      Move(At(InspectorSection.Play), 0, +1, choice: false, improvise: false, empower: false).Section);
    }

    [Test]
    public void Choice_CyclesThenOverflowsToSideSections()
    {
        Assert.AreEqual(1, Move(At(InspectorSection.Choice, 0), +1, 0).Option);
        Assert.AreEqual(InspectorSection.Empower,   Move(At(InspectorSection.Choice, 2), +1, 0).Section);
        Assert.AreEqual(InspectorSection.Improvise, Move(At(InspectorSection.Choice, 0), -1, 0).Section);
        Assert.AreEqual(InspectorSection.Play,      Move(At(InspectorSection.Choice, 1), 0, -1).Section);
    }

    [Test]
    public void Choice_OverflowToUnreachableSection_Stays()
    {
        var stay = Move(At(InspectorSection.Choice, 2), +1, 0, empower: false);
        Assert.AreEqual(InspectorSection.Choice, stay.Section);
        Assert.AreEqual(2, stay.Option);
    }

    [Test]
    public void Improvise_CyclesVertically_TopToChoice_BottomToPlay()
    {
        Assert.AreEqual(1, Move(At(InspectorSection.Improvise, 2), 0, +1).Option);
        Assert.AreEqual(3, Move(At(InspectorSection.Improvise, 2), 0, -1).Option);
        Assert.AreEqual(InspectorSection.Choice, Move(At(InspectorSection.Improvise, 0), 0, +1).Section);
        Assert.AreEqual(InspectorSection.Play,   Move(At(InspectorSection.Improvise, 3), 0, -1).Section);
        Assert.AreEqual(InspectorSection.Empower, Move(At(InspectorSection.Improvise, 1), +1, 0).Section);
    }

    [Test]
    public void Improvise_TopWithNoChoice_Stays()
    {
        var stay = Move(At(InspectorSection.Improvise, 0), 0, +1, choice: false);
        Assert.AreEqual(InspectorSection.Improvise, stay.Section);
        Assert.AreEqual(0, stay.Option);
    }

    [Test]
    public void Empower_JumpsLeftUpDown()
    {
        Assert.AreEqual(InspectorSection.Improvise, Move(At(InspectorSection.Empower), -1, 0).Section);
        Assert.AreEqual(InspectorSection.Choice,    Move(At(InspectorSection.Empower), 0, +1).Section);
        Assert.AreEqual(InspectorSection.Play,      Move(At(InspectorSection.Empower), 0, -1).Section);
    }

    [Test]
    public void ImproviseActive_ChoiceAndEmpowerUnreachable_FromImprovise()
    {
        // Regression for the spec rule: while Improvise is the mode, Choice and
        // Empower lock — navigation must not land on them.
        Assert.AreEqual(InspectorSection.Improvise,
            Move(At(InspectorSection.Improvise, 0), 0, +1, choice: false, empower: false).Section);
        Assert.AreEqual(InspectorSection.Improvise,
            Move(At(InspectorSection.Improvise, 1), +1, 0, choice: false, empower: false).Section);
    }

    [Test]
    public void JumpingIntoASection_LandsOnOptionZero()
    {
        Assert.AreEqual(0, Move(At(InspectorSection.Play), 0, +1).Option);
        Assert.AreEqual(0, Move(At(InspectorSection.Choice, 2), +1, 0).Option);
    }
}
