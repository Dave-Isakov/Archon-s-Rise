using NUnit.Framework;

public class DefeatFxMathTests
{
    [Test]
    public void ShakeEnvelope_Full_At_Start_Zero_At_End()
    {
        Assert.AreEqual(10f, DefeatFxMath.ShakeEnvelope(0f, 0.2f, 10f), 1e-4f);
        Assert.AreEqual(0f, DefeatFxMath.ShakeEnvelope(0.2f, 0.2f, 10f), 1e-4f);
        Assert.AreEqual(0f, DefeatFxMath.ShakeEnvelope(0.5f, 0.2f, 10f), 1e-4f); // past end
    }

    [Test]
    public void ShakeEnvelope_Decays_Linearly()
    {
        Assert.AreEqual(5f, DefeatFxMath.ShakeEnvelope(0.1f, 0.2f, 10f), 1e-4f); // halfway -> half
    }

    [Test]
    public void ShakeEnvelope_Zero_Duration_Is_Zero()
    {
        Assert.AreEqual(0f, DefeatFxMath.ShakeEnvelope(0f, 0f, 10f), 1e-4f);
    }

    [Test]
    public void DissolveProgress_Clamps_Zero_To_One()
    {
        Assert.AreEqual(0f, DefeatFxMath.DissolveProgress(0f, 0.4f), 1e-4f);
        Assert.AreEqual(0.5f, DefeatFxMath.DissolveProgress(0.2f, 0.4f), 1e-4f);
        Assert.AreEqual(1f, DefeatFxMath.DissolveProgress(0.4f, 0.4f), 1e-4f);
        Assert.AreEqual(1f, DefeatFxMath.DissolveProgress(1f, 0.4f), 1e-4f); // past end clamps
        Assert.AreEqual(1f, DefeatFxMath.DissolveProgress(0f, 0f), 1e-4f);   // zero duration
    }
}
