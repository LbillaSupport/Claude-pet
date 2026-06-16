namespace ClaudeBuddy.Core;

/// <summary>
/// A library of easing functions. The golden rule of this project: nothing moves
/// linearly. Every transition, pose blend and particle uses one of these curves so
/// motion always feels organic rather than robotic.
/// </summary>
public static class Easing
{
    public static float Linear(float t) => t;

    public static float InQuad(float t) => t * t;
    public static float OutQuad(float t) => 1f - ((1f - t) * (1f - t));
    public static float InOutQuad(float t) =>
        t < 0.5f ? 2f * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 2f) / 2f);

    public static float InCubic(float t) => t * t * t;
    public static float OutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
    public static float InOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 3f) / 2f);

    public static float OutQuart(float t) => 1f - MathF.Pow(1f - t, 4f);

    public static float InOutSine(float t) => -(MathF.Cos(MathF.PI * t) - 1f) / 2f;
    public static float OutSine(float t) => MathF.Sin((t * MathF.PI) / 2f);
    public static float InSine(float t) => 1f - MathF.Cos((t * MathF.PI) / 2f);

    public static float OutExpo(float t) =>
        MathF.Abs(t - 1f) < 1e-6f ? 1f : 1f - MathF.Pow(2f, -10f * t);

    /// <summary>Overshoots slightly then settles — perfect for pops and landings.</summary>
    public static float OutBack(float t, float overshoot = 1.70158f)
    {
        float c3 = overshoot + 1f;
        float p = t - 1f;
        return 1f + (c3 * (p * p * p)) + (overshoot * (p * p));
    }

    public static float InBack(float t, float overshoot = 1.70158f)
    {
        float c3 = overshoot + 1f;
        return (c3 * t * t * t) - (overshoot * t * t);
    }

    /// <summary>Springy wobble used for excited reactions and celebrations.</summary>
    public static float OutElastic(float t)
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        const float c4 = (2f * MathF.PI) / 3f;
        return (MathF.Pow(2f, -10f * t) * MathF.Sin(((t * 10f) - 0.75f) * c4)) + 1f;
    }

    /// <summary>Bouncy fall used when the mascot drops onto the ground.</summary>
    public static float OutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
        {
            return n1 * t * t;
        }

        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return (n1 * t * t) + 0.75f;
        }

        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return (n1 * t * t) + 0.9375f;
        }

        t -= 2.625f / d1;
        return (n1 * t * t) + 0.984375f;
    }
}
