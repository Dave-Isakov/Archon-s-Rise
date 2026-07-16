using NUnit.Framework;

public class GlowPulseTests
{
    [Test]
    public void Alpha_At_Time_Zero_Is_Midpoint()
    {
        // sin(0) = 0 -> normalized 0.5 -> midpoint of [0.3, 1.0] = 0.65
        Assert.AreEqual(0.65f, GlowPulse.Alpha(0f, 0.3f, 1.0f, 4f), 0.0001f);
    }

    [Test]
    public void Alpha_Peaks_At_Quarter_Period()
    {
        // sin(pi/2) = 1 -> normalized 1 -> max. speed = 1 so time = pi/2.
        Assert.AreEqual(1.0f, GlowPulse.Alpha((float)(System.Math.PI / 2), 0.3f, 1.0f, 1f), 0.0001f);
    }

    [Test]
    public void Alpha_Stays_Within_Bounds()
    {
        for (int i = 0; i < 200; i++)
        {
            float a = GlowPulse.Alpha(i * 0.137f, 0.3f, 1.0f, 4f);
            Assert.GreaterOrEqual(a, 0.3f - 0.0001f);
            Assert.LessOrEqual(a, 1.0f + 0.0001f);
        }
    }
}
