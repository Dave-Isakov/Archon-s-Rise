using NUnit.Framework;

public class MapCameraRulesTests
{
    const float Tol = 0.0001f;

    [Test]
    public void ClampAxis_InsideRange_ReturnsValueUnchanged()
    {
        Assert.AreEqual(5f, MapCameraRules.ClampAxis(5f, 0f, 10f), Tol);
    }

    [Test]
    public void ClampAxis_BelowMin_ReturnsMin()
    {
        Assert.AreEqual(0f, MapCameraRules.ClampAxis(-3f, 0f, 10f), Tol);
    }

    [Test]
    public void ClampAxis_AboveMax_ReturnsMax()
    {
        Assert.AreEqual(10f, MapCameraRules.ClampAxis(99f, 0f, 10f), Tol);
    }

    [Test]
    public void ClampAxis_ExactlyAtLimits_ReturnsThoseLimits()
    {
        Assert.AreEqual(0f, MapCameraRules.ClampAxis(0f, 0f, 10f), Tol);
        Assert.AreEqual(10f, MapCameraRules.ClampAxis(10f, 0f, 10f), Tol);
    }

    // A degenerate map (a single row or column of terrain, or no terrain at all)
    // can produce min > max. That axis locks to the midpoint instead of returning
    // NaN or snapping to an arbitrary end.
    [Test]
    public void ClampAxis_MinGreaterThanMax_ReturnsMidpoint()
    {
        Assert.AreEqual(7.5f, MapCameraRules.ClampAxis(100f, 10f, 5f), Tol);
        Assert.AreEqual(7.5f, MapCameraRules.ClampAxis(-100f, 10f, 5f), Tol);
    }

    [Test]
    public void NormalizeInput_SubUnitInput_IsUnchanged()
    {
        MapCameraRules.NormalizeInput(0.5f, 0f, out float nx, out float ny);
        Assert.AreEqual(0.5f, nx, Tol);
        Assert.AreEqual(0f, ny, Tol);
    }

    [Test]
    public void NormalizeInput_ZeroInput_StaysZero()
    {
        MapCameraRules.NormalizeInput(0f, 0f, out float nx, out float ny);
        Assert.AreEqual(0f, nx, Tol);
        Assert.AreEqual(0f, ny, Tol);
    }

    // Two keys held at once must not pan faster than one.
    [Test]
    public void NormalizeInput_FullDiagonal_HasMagnitudeOne()
    {
        MapCameraRules.NormalizeInput(1f, 1f, out float nx, out float ny);
        float magnitude = (float)System.Math.Sqrt(nx * nx + ny * ny);
        Assert.AreEqual(1f, magnitude, Tol);
        Assert.AreEqual(nx, ny, Tol);
    }

    [Test]
    public void NormalizeInput_CardinalFullPress_StaysOne()
    {
        MapCameraRules.NormalizeInput(1f, 0f, out float nx, out float ny);
        Assert.AreEqual(1f, nx, Tol);
        Assert.AreEqual(0f, ny, Tol);
    }

    [Test]
    public void StepAxis_IntegratesInputTimesSpeedTimesDt()
    {
        // 2 + (1 * 12 * 0.5) = 8, inside [0, 20]
        Assert.AreEqual(8f, MapCameraRules.StepAxis(2f, 1f, 12f, 0.5f, 0f, 20f), Tol);
    }

    [Test]
    public void StepAxis_NegativeInputMovesBackward()
    {
        // 10 + (-1 * 12 * 0.5) = 4
        Assert.AreEqual(4f, MapCameraRules.StepAxis(10f, -1f, 12f, 0.5f, 0f, 20f), Tol);
    }

    [Test]
    public void StepAxis_ClampsTheIntegratedResult()
    {
        // 18 + (1 * 12 * 0.5) = 24, clamped to 20
        Assert.AreEqual(20f, MapCameraRules.StepAxis(18f, 1f, 12f, 0.5f, 0f, 20f), Tol);
    }

    [Test]
    public void StepAxis_ZeroInput_StillClampsAnOutOfRangeCurrent()
    {
        // Guards the case where the limits shrink while the camera sits outside them.
        Assert.AreEqual(20f, MapCameraRules.StepAxis(50f, 0f, 12f, 0.5f, 0f, 20f), Tol);
    }
}
