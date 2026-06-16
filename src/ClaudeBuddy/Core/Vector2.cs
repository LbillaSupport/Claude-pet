namespace ClaudeBuddy.Core;

/// <summary>
/// A lightweight 2D vector used throughout the engine for positions, velocities
/// and offsets. Values are in device (physical) pixels unless stated otherwise.
/// </summary>
public readonly struct Vector2 : IEquatable<Vector2>
{
    public readonly float X;
    public readonly float Y;

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 Zero => new(0f, 0f);
    public static Vector2 One => new(1f, 1f);
    public static Vector2 UnitX => new(1f, 0f);
    public static Vector2 UnitY => new(0f, 1f);

    public float Length => MathF.Sqrt((X * X) + (Y * Y));
    public float LengthSquared => (X * X) + (Y * Y);

    public Vector2 Normalized()
    {
        float len = Length;
        return len > 1e-5f ? new Vector2(X / len, Y / len) : Zero;
    }

    public Vector2 WithX(float x) => new(x, Y);
    public Vector2 WithY(float y) => new(X, y);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);
    public static Vector2 operator *(Vector2 a, float s) => new(a.X * s, a.Y * s);
    public static Vector2 operator *(float s, Vector2 a) => new(a.X * s, a.Y * s);
    public static Vector2 operator /(Vector2 a, float s) => new(a.X / s, a.Y / s);

    public static float Distance(Vector2 a, Vector2 b) => (a - b).Length;
    public static float Dot(Vector2 a, Vector2 b) => (a.X * b.X) + (a.Y * b.Y);

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) =>
        new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));

    public bool Equals(Vector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X:0.##}, {Y:0.##})";
}
