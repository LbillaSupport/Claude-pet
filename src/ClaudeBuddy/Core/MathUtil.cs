namespace ClaudeBuddy.Core;

/// <summary>
/// Small, allocation-free math helpers shared across systems. Centralising these
/// keeps the rest of the engine free of "magic" arithmetic and clamping.
/// </summary>
public static class MathUtil
{
    public const float Tau = MathF.PI * 2f;
    public const float HalfPi = MathF.PI * 0.5f;

    public static float Clamp(float value, float min, float max) =>
        value < min ? min : (value > max ? max : value);

    public static float Clamp01(float value) => Clamp(value, 0f, 1f);

    public static int Clamp(int value, int min, int max) =>
        value < min ? min : (value > max ? max : value);

    public static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    public static float InverseLerp(float a, float b, float value) =>
        MathF.Abs(b - a) < 1e-6f ? 0f : Clamp01((value - a) / (b - a));

    public static float Remap(float value, float inMin, float inMax, float outMin, float outMax) =>
        Lerp(outMin, outMax, InverseLerp(inMin, inMax, value));

    /// <summary>
    /// Frame-rate independent exponential smoothing. <paramref name="speed"/> is
    /// roughly "how many units per second" the value converges toward the target.
    /// </summary>
    public static float Damp(float current, float target, float speed, float dt) =>
        Lerp(current, target, 1f - MathF.Exp(-speed * dt));

    public static Vector2 Damp(Vector2 current, Vector2 target, float speed, float dt)
    {
        float t = 1f - MathF.Exp(-speed * dt);
        return Vector2.Lerp(current, target, t);
    }

    public static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + (MathF.Sign(target - current) * maxDelta);
    }

    public static float PingPong(float t, float length)
    {
        float l2 = length * 2f;
        float m = t % l2;
        if (m < 0f)
        {
            m += l2;
        }

        return m <= length ? m : (l2 - m);
    }

    public static float DegToRad(float degrees) => degrees * (MathF.PI / 180f);
    public static float RadToDeg(float radians) => radians * (180f / MathF.PI);
}
