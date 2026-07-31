using NUnit.Framework;

// Lives outside Assets/ on purpose: InputContextState sits in the main assembly
// (Assets/Scripts/Input/) with no asmdef of its own, so there is no EditMode tests
// asmdef that could reference it. The CLI harness compiles it by path, and Unity
// never sees this file. Run with:
//   tools/pure-tests/run.sh Assets/Scripts/Input/InputContext.cs tools/pure-tests/cases/InputContextStateTests.cs
public class InputContextStateTests
{
    [SetUp]
    public void Reset()
    {
        InputContextState.ReleaseMap();
        InputContextState.Current = InputContext.Board;
    }

    [Test]
    public void OrdinaryTransitions_StillWork()
    {
        InputContextState.Current = InputContext.Fan;
        Assert.AreEqual(InputContext.Fan, InputContextState.Current);
        InputContextState.Current = InputContext.Inspector;
        Assert.AreEqual(InputContext.Inspector, InputContextState.Current);
        InputContextState.Current = InputContext.Board;
        Assert.AreEqual(InputContext.Board, InputContextState.Current);
    }

    [Test]
    public void EnteringMap_IsAllowed()
    {
        InputContextState.Current = InputContext.Map;
        Assert.AreEqual(InputContext.Map, InputContextState.Current);
    }

    // The bug this latch exists for: clicking a hand card in map mode ran
    // CardInspector.Open(), which set Inspector, and Close() then set Board — so
    // every "== InputContext.Map" gate went dead while the map was still open.
    [Test]
    public void WhileMapIsOpen_InspectorWriteIsIgnored()
    {
        InputContextState.Current = InputContext.Map;
        InputContextState.Current = InputContext.Inspector;
        Assert.AreEqual(InputContext.Map, InputContextState.Current);
    }

    [Test]
    public void WhileMapIsOpen_BoardWriteIsIgnored()
    {
        InputContextState.Current = InputContext.Map;
        InputContextState.Current = InputContext.Board;
        Assert.AreEqual(InputContext.Map, InputContextState.Current);
    }

    [Test]
    public void WhileMapIsOpen_FanWriteIsIgnored()
    {
        InputContextState.Current = InputContext.Map;
        InputContextState.Current = InputContext.Fan;
        Assert.AreEqual(InputContext.Map, InputContextState.Current);
    }

    [Test]
    public void WhileMapIsOpen_ReassertingMapIsHarmless()
    {
        InputContextState.Current = InputContext.Map;
        InputContextState.Current = InputContext.Map;
        Assert.AreEqual(InputContext.Map, InputContextState.Current);
    }

    [Test]
    public void ReleaseMap_IsTheOnlyWayOut()
    {
        InputContextState.Current = InputContext.Map;
        InputContextState.ReleaseMap();
        Assert.AreEqual(InputContext.Board, InputContextState.Current);
    }

    [Test]
    public void ReleaseMap_AfterRelease_ContextIsWritableAgain()
    {
        InputContextState.Current = InputContext.Map;
        InputContextState.ReleaseMap();
        InputContextState.Current = InputContext.Fan;
        Assert.AreEqual(InputContext.Fan, InputContextState.Current);
    }

    // Close() is idempotent and DataManager can route Escape to it from any state,
    // so a stray release must not yank a live Inspector/Fan back to Board.
    [Test]
    public void ReleaseMap_WhenNotInMap_LeavesTheContextAlone()
    {
        InputContextState.Current = InputContext.Inspector;
        InputContextState.ReleaseMap();
        Assert.AreEqual(InputContext.Inspector, InputContextState.Current);
    }

    [Test]
    public void MapOpen_TracksTheContext()
    {
        Assert.IsFalse(InputContextState.MapOpen);
        InputContextState.Current = InputContext.Map;
        Assert.IsTrue(InputContextState.MapOpen);
        InputContextState.ReleaseMap();
        Assert.IsFalse(InputContextState.MapOpen);
    }
}
