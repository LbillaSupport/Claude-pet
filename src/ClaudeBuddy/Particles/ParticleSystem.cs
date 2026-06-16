using ClaudeBuddy.Core;

namespace ClaudeBuddy.Particles;

/// <summary>A single live particle. Mutable struct stored contiguously for cache-friendly updates.</summary>
public struct Particle
{
    public Vector2 Position;      // world (screen) space
    public Vector2 Velocity;
    public float Age;
    public float Life;
    public float Size;
    public float Rotation;
    public float Spin;
    public float GravityScale;
    public float Drag;
    public RgbaColor Color;
    public ParticleKind Kind;

    public readonly float Normalized => Life <= 0f ? 1f : MathUtil.Clamp01(Age / Life);
    public readonly bool IsDead => Age >= Life;
}

/// <summary>
/// A lightweight CPU particle system. Capacity is capped so it can never affect the
/// 1%-CPU idle budget; the oldest particle is recycled when the pool is full. All
/// emission helpers are expressed in world space so effects stay glued to wherever
/// the mascot is, even as it walks across the desktop.
/// </summary>
public sealed class ParticleSystem
{
    private const int MaxParticles = 256;
    private readonly Particle[] _particles = new Particle[MaxParticles];
    private readonly Rng _rng;
    private int _count;

    public ParticleSystem(Rng rng) => _rng = rng;

    public int Count => _count;

    public ReadOnlySpan<Particle> Active => _particles.AsSpan(0, _count);

    public void Update(float dt)
    {
        for (int i = 0; i < _count; i++)
        {
            ref Particle p = ref _particles[i];
            p.Age += dt;
            if (p.IsDead)
            {
                // Swap-remove keeps the array packed without shifting.
                _particles[i] = _particles[--_count];
                i--;
                continue;
            }

            p.Velocity = new Vector2(
                MathUtil.Damp(p.Velocity.X, 0f, p.Drag, dt),
                p.Velocity.Y + (EngineConstants.Gravity * p.GravityScale * dt));
            p.Position += p.Velocity * dt;
            p.Rotation += p.Spin * dt;
        }
    }

    public void Clear() => _count = 0;

    private void Add(in Particle p)
    {
        if (_count < MaxParticles)
        {
            _particles[_count++] = p;
        }
        else
        {
            // Pool full: overwrite the oldest so bursts still look alive.
            int oldest = 0;
            float best = -1f;
            for (int i = 0; i < _count; i++)
            {
                if (_particles[i].Normalized > best)
                {
                    best = _particles[i].Normalized;
                    oldest = i;
                }
            }

            _particles[oldest] = p;
        }
    }

    public void EmitHearts(Vector2 center, int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = _rng.InsideUnitCircle();
            Add(new Particle
            {
                Position = center + (dir * _rng.Range(0f, 18f)),
                Velocity = new Vector2(_rng.Range(-40f, 40f), _rng.Range(-160f, -90f)),
                Life = _rng.Range(0.9f, 1.5f),
                Size = _rng.Range(12f, 20f),
                Spin = _rng.Range(-2f, 2f),
                GravityScale = -0.02f, // hearts drift gently upward
                Drag = 1.4f,
                Color = RgbaColor.Lerp(RgbaColor.HeartPink, RgbaColor.Blush, _rng.NextFloat()),
                Kind = ParticleKind.Heart,
            });
        }
    }

    public void EmitSparkles(Vector2 center, int count = 8)
    {
        for (int i = 0; i < count; i++)
        {
            Add(new Particle
            {
                Position = center + (_rng.InsideUnitCircle() * _rng.Range(0f, 40f)),
                Velocity = _rng.InsideUnitCircle() * _rng.Range(20f, 90f),
                Life = _rng.Range(0.5f, 1.0f),
                Size = _rng.Range(5f, 11f),
                Spin = _rng.Range(-6f, 6f),
                GravityScale = 0.02f,
                Drag = 2.5f,
                Color = RgbaColor.Lerp(RgbaColor.StarGold, RgbaColor.Cream, _rng.NextFloat()),
                Kind = ParticleKind.Sparkle,
            });
        }
    }

    public void EmitStars(Vector2 center, int count = 6)
    {
        for (int i = 0; i < count; i++)
        {
            Add(new Particle
            {
                Position = center + (_rng.InsideUnitCircle() * 14f),
                Velocity = new Vector2(_rng.Range(-70f, 70f), _rng.Range(-150f, -60f)),
                Life = _rng.Range(0.7f, 1.3f),
                Size = _rng.Range(10f, 18f),
                Spin = _rng.Range(-4f, 4f),
                GravityScale = 0.25f,
                Drag = 1.0f,
                Color = RgbaColor.StarGold,
                Kind = ParticleKind.Star,
            });
        }
    }

    public void EmitConfetti(Vector2 center, int count = 40)
    {
        ReadOnlySpan<RgbaColor> palette =
        [
            new(0xFF, 0x6B, 0x8A), new(0xFF, 0xD1, 0x66), new(0x6B, 0xCB, 0xFF),
            new(0x9B, 0xE5, 0x6B), new(0xC9, 0x8B, 0xFF), RgbaColor.ClaudeClay,
        ];

        for (int i = 0; i < count; i++)
        {
            Add(new Particle
            {
                Position = center + new Vector2(_rng.Range(-20f, 20f), _rng.Range(-10f, 10f)),
                Velocity = new Vector2(_rng.Range(-260f, 260f), _rng.Range(-520f, -200f)),
                Life = _rng.Range(1.4f, 2.6f),
                Size = _rng.Range(7f, 13f),
                Spin = _rng.Range(-12f, 12f),
                GravityScale = 0.5f,
                Drag = 0.7f,
                Color = palette[_rng.Range(0, palette.Length)],
                Kind = ParticleKind.Confetti,
            });
        }
    }

    public void EmitZzz(Vector2 head)
    {
        Add(new Particle
        {
            Position = head,
            Velocity = new Vector2(_rng.Range(6f, 18f), _rng.Range(-34f, -22f)),
            Life = _rng.Range(1.6f, 2.4f),
            Size = _rng.Range(14f, 22f),
            Spin = 0f,
            GravityScale = -0.01f,
            Drag = 0.8f,
            Color = RgbaColor.Ink.WithAlpha((byte)170),
            Kind = ParticleKind.ZzZ,
        });
    }

    public void EmitDust(Vector2 feet, int count = 4)
    {
        for (int i = 0; i < count; i++)
        {
            float dir = _rng.Sign();
            Add(new Particle
            {
                Position = feet + new Vector2(_rng.Range(-6f, 6f), -2f),
                Velocity = new Vector2(dir * _rng.Range(40f, 110f), _rng.Range(-40f, -10f)),
                Life = _rng.Range(0.3f, 0.6f),
                Size = _rng.Range(8f, 15f),
                Spin = _rng.Range(-2f, 2f),
                GravityScale = 0.1f,
                Drag = 3.5f,
                Color = RgbaColor.Cream.WithAlpha((byte)150),
                Kind = ParticleKind.Dust,
            });
        }
    }

    public void EmitMagic(Vector2 center, int count = 10)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = _rng.Range(0f, MathUtil.Tau);
            Add(new Particle
            {
                Position = center,
                Velocity = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * _rng.Range(40f, 120f),
                Life = _rng.Range(0.8f, 1.4f),
                Size = _rng.Range(6f, 12f),
                Spin = _rng.Range(-8f, 8f),
                GravityScale = -0.05f,
                Drag = 1.8f,
                Color = RgbaColor.Lerp(new RgbaColor(0xC9, 0x8B, 0xFF), RgbaColor.StarGold, _rng.NextFloat()),
                Kind = ParticleKind.Magic,
            });
        }
    }

    /// <summary>Spawns one drifting weather mote somewhere across the given band.</summary>
    public void EmitWeatherMote(WeatherKind kind, float left, float right, float top)
    {
        Particle p = new()
        {
            Position = new Vector2(_rng.Range(left, right), top),
            Rotation = _rng.Range(0f, MathUtil.Tau),
        };

        switch (kind)
        {
            case WeatherKind.Snow:
                p.Velocity = new Vector2(_rng.Range(-20f, 20f), _rng.Range(40f, 90f));
                p.Life = _rng.Range(3f, 6f);
                p.Size = _rng.Range(6f, 12f);
                p.GravityScale = 0f;
                p.Drag = 0.4f;
                p.Spin = _rng.Range(-1f, 1f);
                p.Color = RgbaColor.Cream;
                p.Kind = ParticleKind.Snow;
                break;
            case WeatherKind.Leaves:
            case WeatherKind.Petals:
                p.Velocity = new Vector2(_rng.Range(-40f, 40f), _rng.Range(30f, 70f));
                p.Life = _rng.Range(3f, 6f);
                p.Size = _rng.Range(10f, 16f);
                p.GravityScale = 0.02f;
                p.Drag = 0.3f;
                p.Spin = _rng.Range(-3f, 3f);
                p.Color = kind == WeatherKind.Leaves
                    ? new RgbaColor(0xE2, 0x9A, 0x4B)
                    : new RgbaColor(0xFF, 0xC2, 0xD6);
                p.Kind = ParticleKind.Leaf;
                break;
            default:
                p.Velocity = new Vector2(_rng.Range(-6f, 6f), _rng.Range(260f, 360f));
                p.Life = _rng.Range(1.2f, 2.2f);
                p.Size = _rng.Range(2f, 4f);
                p.GravityScale = 0.1f;
                p.Drag = 0.2f;
                p.Color = new RgbaColor(0x9A, 0xC4, 0xE8, 0xC0);
                p.Kind = ParticleKind.Dust;
                break;
        }

        Add(p);
    }
}
