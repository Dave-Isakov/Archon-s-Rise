using NUnit.Framework;

public class UnitNavRulesTests
{
    [Test] public void Open_Focuses_First_Option() => Assert.AreEqual(0, UnitNavRules.Open(3));
    [Test] public void Open_With_No_Options_Focuses_Use() => Assert.AreEqual(UnitNavRules.UseSlot(0), UnitNavRules.Open(0));
    [Test] public void Down_Moves_To_Next_Option() => Assert.AreEqual(1, UnitNavRules.Move(0, -1, 3));
    [Test] public void Down_Past_Last_Option_Lands_On_Use() => Assert.AreEqual(3, UnitNavRules.Move(2, -1, 3));
    [Test] public void Down_On_Use_Stays() => Assert.AreEqual(3, UnitNavRules.Move(3, -1, 3));
    [Test] public void Up_From_Use_Returns_To_Last_Option() => Assert.AreEqual(2, UnitNavRules.Move(3, +1, 3));
    [Test] public void Up_At_Top_Stays() => Assert.AreEqual(0, UnitNavRules.Move(0, +1, 3));
    [Test] public void Zero_Delta_Stays() => Assert.AreEqual(1, UnitNavRules.Move(1, 0, 3));
}
