using ClaudeBuddy.Core;
using ClaudeBuddy.Particles;
using SkiaSharp;

namespace ClaudeBuddy.Rendering;

/// <summary>
/// Draws the particle pool. Particles live in world (screen) space, so we subtract the
/// window's top-left to map them into the canvas. Each kind has a tiny bespoke shape;
/// all fade out smoothly over their lifetime.
/// </summary>
public sealed class ParticleRenderer
{
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    public void Draw(SKCanvas canvas, ReadOnlySpan<Particle> particles, Vector2 windowTopLeft, float dpiScale)
    {
        foreach (ref readonly Particle p in particles)
        {
            float t = p.Normalized;
            float fade = 1f - Easing.InQuad(t);
            float cx = (p.Position.X - windowTopLeft.X);
            float cy = (p.Position.Y - windowTopLeft.Y);
            float size = p.Size * dpiScale * (0.6f + (0.4f * Easing.OutBack(MathF.Min(1f, t * 4f))));

            _fill.Color = p.Color.ToSk(fade);

            switch (p.Kind)
            {
                case ParticleKind.Heart:
                    DrawHeart(canvas, cx, cy, size, p.Rotation);
                    break;
                case ParticleKind.Star:
                case ParticleKind.Sparkle:
                case ParticleKind.Magic:
                    DrawSparkle(canvas, cx, cy, size, p.Rotation, p.Kind == ParticleKind.Star ? 5 : 4);
                    break;
                case ParticleKind.Confetti:
                    DrawConfetti(canvas, cx, cy, size, p.Rotation);
                    break;
                case ParticleKind.ZzZ:
                    DrawZ(canvas, cx, cy, size, fade, p.Color);
                    break;
                case ParticleKind.Note:
                    DrawNote(canvas, cx, cy, size);
                    break;
                case ParticleKind.Snow:
                case ParticleKind.Dust:
                    canvas.DrawCircle(cx, cy, size * 0.5f, _fill);
                    break;
                case ParticleKind.Leaf:
                    DrawLeaf(canvas, cx, cy, size, p.Rotation);
                    break;
                default:
                    canvas.DrawCircle(cx, cy, size * 0.5f, _fill);
                    break;
            }
        }
    }

    private void DrawHeart(SKCanvas canvas, float x, float y, float s, float rot)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateRadians(rot * 0.2f);
        float r = s * 0.5f;
        using var path = new SKPath();
        path.MoveTo(0, r * 0.7f);
        path.CubicTo(-r * 1.3f, -r * 0.4f, -r * 0.5f, -r * 1.2f, 0, -r * 0.4f);
        path.CubicTo(r * 0.5f, -r * 1.2f, r * 1.3f, -r * 0.4f, 0, r * 0.7f);
        path.Close();
        canvas.DrawPath(path, _fill);
        canvas.Restore();
    }

    private void DrawSparkle(SKCanvas canvas, float x, float y, float s, float rot, int points)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateRadians(rot);
        using var path = new SKPath();
        float outer = s * 0.5f;
        float inner = s * 0.18f;
        for (int i = 0; i < points * 2; i++)
        {
            float ang = (MathF.PI * i) / points;
            float radius = (i % 2 == 0) ? outer : inner;
            float px = MathF.Cos(ang) * radius;
            float py = MathF.Sin(ang) * radius;
            if (i == 0)
            {
                path.MoveTo(px, py);
            }
            else
            {
                path.LineTo(px, py);
            }
        }

        path.Close();
        canvas.DrawPath(path, _fill);
        canvas.Restore();
    }

    private void DrawConfetti(SKCanvas canvas, float x, float y, float s, float rot)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateRadians(rot);
        // A thin rectangle that flutters as it spins.
        float w = s * 0.7f;
        float h = s * 0.4f * (0.4f + (0.6f * MathF.Abs(MathF.Cos(rot))));
        canvas.DrawRect(-w * 0.5f, -h * 0.5f, w, h, _fill);
        canvas.Restore();
    }

    private void DrawLeaf(SKCanvas canvas, float x, float y, float s, float rot)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateRadians(rot);
        using var path = new SKPath();
        float r = s * 0.5f;
        path.MoveTo(0, -r);
        path.QuadTo(r, 0, 0, r);
        path.QuadTo(-r, 0, 0, -r);
        path.Close();
        canvas.DrawPath(path, _fill);
        canvas.Restore();
    }

    private void DrawNote(SKCanvas canvas, float x, float y, float s)
    {
        float r = s * 0.22f;
        canvas.DrawCircle(x - (r * 0.4f), y + (s * 0.35f), r, _fill);
        using var stem = new SKPaint
        {
            IsAntialias = true,
            Color = _fill.Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1.5f, s * 0.12f),
            StrokeCap = SKStrokeCap.Round,
        };
        canvas.DrawLine(x + (r * 0.6f), y + (s * 0.35f), x + (r * 0.6f), y - (s * 0.4f), stem);
    }

    private void DrawZ(SKCanvas canvas, float x, float y, float s, float fade, RgbaColor color)
    {
        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Color = color.ToSk(fade),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1.5f, s * 0.14f),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
        float h = s * 0.7f;
        using var path = new SKPath();
        path.MoveTo(x - (h * 0.4f), y - (h * 0.5f));
        path.LineTo(x + (h * 0.4f), y - (h * 0.5f));
        path.LineTo(x - (h * 0.4f), y + (h * 0.5f));
        path.LineTo(x + (h * 0.4f), y + (h * 0.5f));
        canvas.DrawPath(path, stroke);
    }
}
