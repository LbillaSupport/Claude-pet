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

    /// <summary>Wraps an angle (radians) into the range (-π, π].</summary>
    public static float WrapPi(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= Tau;
        }

        while (angle <= -MathF.PI)
        {
            angle += Tau;
        }

        return angle;
    }

    /// <summary>Exponential smoothing for angles, always easing the short way round.</summary>
    public static float DampAngle(float current, float target, float speed, float dt)
    {
        float delta = WrapPi(target - current);
        return current + (delta * (1f - MathF.Exp(-speed * dt)));
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

/// <summary>
/// A tiny stateful spring. Unlike <see cref="MathUtil.Damp"/> (which only ever eases
/// in), a spring carries velocity, so it can overshoot a touch and settle — that
/// little bit of follow-through is what makes arrivals read as "alive" rather than
/// mechanical. Pick a <c>damping</c> below the critical value (≈2·√stiffness) for a
/// visible bounce, or near it for a smooth-but-springy settle.
/// </summary>
public struct Spring
{
    public float Value;
    public float Velocity;

    public Spring(float value)
    {
        Value = value;
        Velocity = 0f;
    }

    public void Step(float target, float stiffness, float damping, float dt)
    {
        // Sub-step on long frames so a stutter can never make the spring explode.
        float remaining = MathF.Min(dt, 0.05f);
        const float maxStep = 1f / 120f;
        while (remaining > 0f)
        {
            float step = MathF.Min(remaining, maxStep);
            float accel = (-stiffness * (Value - target)) - (damping * Velocity);
            Velocity += accel * step;
            Value += Velocity * step;
            remaining -= step;
        }
    }
}
