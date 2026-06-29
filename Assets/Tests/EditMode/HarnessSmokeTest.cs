using NUnit.Framework;

public class HarnessSmokeTest
{
    [Test]
    public void Harness_Compiles_And_Runs()
    {
        Assert.AreEqual(2, 1 + 1);
    }
}
