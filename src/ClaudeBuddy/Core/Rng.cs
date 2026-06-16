namespace ClaudeBuddy.Core;

/// <summary>
/// A thin, convenience wrapper over <see cref="Random"/>. A single shared instance
/// keeps behaviour deterministic-friendly and avoids the time-seed collisions you
/// get from <c>new Random()</c> in tight loops.
/// </summary>
public sealed class Rng
{
    private readonly Random _random;

    public Rng()
        : this(Environment.TickCount)
    {
    }

    public Rng(int seed) => _random = new Random(seed);

    /// <summary>A process-wide instance for systems that do not inject their own.</summary>
    public static Rng Shared { get; } = new();

    public float NextFloat() => (float)_random.NextDouble();

    public float Range(float min, float max) => min + ((max - min) * NextFloat());

    public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public bool Chance(float probability) => NextFloat() < probability;

    public float Sign() => NextFloat() < 0.5f ? -1f : 1f;

    public Vector2 InsideUnitCircle()
    {
        // Rejection sampling keeps the distribution uniform inside the disc.
        while (true)
        {
            float x = Range(-1f, 1f);
            float y = Range(-1f, 1f);
            if ((x * x) + (y * y) <= 1f)
            {
                return new Vector2(x, y);
            }
        }
    }

    public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];
}
