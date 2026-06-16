using ClaudeBuddy.Animation;
using ClaudeBuddy.Core;
using ClaudeBuddy.Skins;
using SkiaSharp;

namespace ClaudeBuddy.Rendering;

/// <summary>
/// Paints Claude Buddy entirely from vector primitives — no sprite sheets. Given a
/// <see cref="Pose"/> and a <see cref="SkinPalette"/>, it draws a soft, rounded, very
/// expressive little creature: squashy body, swinging limbs, blinking eyes that track
/// the cursor, blush, props, and a tiny Anthropic-style sunburst badge. Because it is
/// all maths, it is crisp at any DPI and a new skin is just seven colours.
/// </summary>
public sealed class CharacterArtist
{
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly SKPaint _shadow = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = new SKColor(0, 0, 0, 70),
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f),
    };

    /// <summary>
    /// Draws the full character into the canvas. <paramref name="feet"/> is the canvas
    /// point where the feet rest; <paramref name="groundY"/> is the canvas Y of the
    /// floor (so the shadow stays put while the body jumps).
    /// </summary>
    public void Draw(
        SKCanvas canvas, SKPoint feet, float groundY, float height,
        Facing facing, float squashX, float squashY, Pose pose, SkinPalette palette, float dpiScale)
    {
        float h = height;
        float airHeight = MathF.Max(0f, groundY - feet.Y);

        DrawGroundShadow(canvas, feet.X, groundY, h, airHeight);

        canvas.Save();
        canvas.Translate(feet.X, feet.Y);
        canvas.Translate(pose.BodyOffset.X * dpiScale, pose.BodyOffset.Y * dpiScale);

        // Whole-body rotation pivots about the body centre (spin/roll/trip).
        float bodyCenterY = -0.46f * h;
        if (MathF.Abs(pose.WholeBodyRotation) > 1e-4f)
        {
            canvas.Translate(0, bodyCenterY);
            canvas.RotateRadians(pose.WholeBodyRotation);
            canvas.Translate(0, -bodyCenterY);
        }

        int sign = (int)facing;
        float sx = sign * squashX * pose.BodyScaleX;
        float sy = squashY * pose.BodyScaleY;
        canvas.Scale(sx, sy); // pivots at the feet, so landings squash from the floor up

        // Eye look is corrected for the horizontal mirror so the gaze always points
        // toward the real cursor regardless of which way the body faces.
        float lookX = pose.EyeLookX * sign;

        DrawLegs(canvas, h, pose, palette);
        DrawArms(canvas, h, pose, palette, behind: true);
        DrawBody(canvas, h, palette);
        DrawBelly(canvas, h, palette);
        DrawSunburst(canvas, h, palette, pose);
        DrawFace(canvas, h, pose, palette, lookX);
        DrawArms(canvas, h, pose, palette, behind: false);
        DrawProps(canvas, h, pose, palette);

        canvas.Restore();
    }

    private void DrawGroundShadow(SKCanvas canvas, float x, float groundY, float h, float airHeight)
    {
        // The higher the mascot, the smaller and fainter its shadow.
        float lift = MathUtil.Clamp01(airHeight / (h * 1.1f));
        float scale = MathUtil.Lerp(1f, 0.45f, lift);
        float rx = 0.30f * h * scale;
        float ry = 0.07f * h * scale;
        _shadow.Color = new SKColor(0, 0, 0, (byte)(70 * (1f - (0.5f * lift))));
        canvas.DrawOval(new SKRect(x - rx, groundY - ry, x + rx, groundY + ry), _shadow);
    }

    private void DrawBody(SKCanvas canvas, float h, SkinPalette palette)
    {
        float bodyW = 0.62f * h;
        float bodyH = 0.80f * h;
        float cx = 0f;
        float cy = -0.46f * h;
        var rect = new SKRect(cx - (bodyW / 2f), cy - (bodyH / 2f), cx + (bodyW / 2f), cy + (bodyH / 2f));

        _fill.Color = palette.Body.ToSk();
        canvas.DrawOval(rect, _fill);

        // Bottom shading: a slightly darker overlay ellipse for soft volume.
        _fill.Color = palette.BodyShadow.ToSk(0.55f);
        var shade = new SKRect(rect.Left + (bodyW * 0.08f), cy + (bodyH * 0.06f), rect.Right - (bodyW * 0.08f), rect.Bottom);
        canvas.Save();
        canvas.ClipRect(rect);
        canvas.DrawOval(shade, _fill);
        canvas.Restore();

        // Top highlight for a gentle sheen.
        _fill.Color = new SKColor(255, 255, 255, 38);
        var hi = new SKRect(cx - (bodyW * 0.28f), cy - (bodyH * 0.42f), cx + (bodyW * 0.18f), cy - (bodyH * 0.06f));
        canvas.DrawOval(hi, _fill);
    }

    private void DrawBelly(SKCanvas canvas, float h, SkinPalette palette)
    {
        float w = 0.34f * h;
        float bh = 0.42f * h;
        float cy = -0.34f * h;
        _fill.Color = palette.Belly.ToSk();
        canvas.DrawOval(new SKRect(-w / 2f, cy - (bh / 2f), w / 2f, cy + (bh / 2f)), _fill);
    }

    private void DrawSunburst(SKCanvas canvas, float h, SkinPalette palette, Pose pose)
    {
        // A small Anthropic-style radial badge on the chest. Twinkles a touch when proud/excited.
        float cy = -0.30f * h;
        float r = 0.058f * h * (1f + (0.12f * pose.StarEyes));
        _fill.Color = palette.Accent.ToSk(0.9f);

        const int rays = 8;
        using var path = new SKPath();
        for (int i = 0; i < rays; i++)
        {
            float a = (MathUtil.Tau * i) / rays;
            float bx = MathF.Cos(a) * r;
            float by = MathF.Sin(a) * r;
            float wob = r * 0.34f;
            float px = MathF.Cos(a + 0.32f) * wob;
            float py = MathF.Sin(a + 0.32f) * wob;
            path.MoveTo(0, cy);
            path.LineTo(px, cy + py);
            path.LineTo(bx, cy + by);
            float nx = MathF.Cos(a - 0.32f) * wob;
            float ny = MathF.Sin(a - 0.32f) * wob;
            path.LineTo(nx, cy + ny);
            path.Close();
        }

        canvas.DrawPath(path, _fill);
        _fill.Color = palette.Belly.ToSk();
        canvas.DrawCircle(0, cy, r * 0.30f, _fill);
    }

    private void DrawLegs(SKCanvas canvas, float h, Pose pose, SkinPalette palette)
    {
        float footY = -0.02f * h;
        float spread = 0.13f * h;
        float footR = 0.075f * h;
        float swing = MathF.Sin(pose.LegPhase) * pose.StrideAmount * 0.12f * h;
        float lift = MathF.Max(0f, MathF.Sin(pose.LegPhase)) * pose.StrideAmount * 0.05f * h;

        // Feet share the body's shadow colour for a grounded look.
        _fill.Color = palette.BodyShadow.ToSk();

        DrawFoot(canvas, -spread + swing, footY - lift, footR);
        DrawFoot(canvas, spread - swing, footY - (MathF.Max(0f, -MathF.Sin(pose.LegPhase)) * pose.StrideAmount * 0.05f * h), footR);
    }

    private void DrawFoot(SKCanvas canvas, float x, float y, float r)
    {
        canvas.DrawOval(new SKRect(x - r, y - (r * 0.7f), x + r, y + (r * 0.7f)), _fill);
    }

    private void DrawArms(SKCanvas canvas, float h, Pose pose, SkinPalette palette, bool behind)
    {
        // Behind pass draws the limbs that sit under the body; front pass draws a raised
        // arm over the body so waves/celebrations read clearly.
        float shoulderY = -0.54f * h;
        float shoulderX = 0.27f * h;
        float armLen = 0.30f * h;
        float thickness = 0.11f * h;

        bool leftFront = pose.ArmLeft > 1.6f;
        bool rightFront = pose.ArmRight > 1.6f;

        if (behind ? !leftFront : leftFront)
        {
            DrawArm(canvas, -shoulderX, shoulderY, pose.ArmLeft, -1, armLen, thickness, palette);
        }

        if (behind ? !rightFront : rightFront)
        {
            DrawArm(canvas, shoulderX, shoulderY, pose.ArmRight, 1, armLen, thickness, palette);
        }
    }

    private void DrawArm(SKCanvas canvas, float sxp, float syp, float angle, int sideOut, float len, float thickness, SkinPalette palette)
    {
        float hx = sxp + (MathF.Sin(angle) * len * sideOut);
        float hy = syp + (MathF.Cos(angle) * len);

        _stroke.Color = palette.Body.ToSk();
        _stroke.StrokeWidth = thickness;
        canvas.DrawLine(sxp, syp, hx, hy, _stroke);

        // Little rounded hand.
        _fill.Color = palette.Body.ToSk();
        canvas.DrawCircle(hx, hy, thickness * 0.62f, _fill);
        _fill.Color = palette.BodyShadow.ToSk(0.5f);
        canvas.DrawCircle(hx, hy + (thickness * 0.12f), thickness * 0.5f, _fill);
    }

    private void DrawFace(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX)
    {
        float eyeY = -0.60f * h;
        float eyeX = 0.155f * h;
        float eyeR = 0.085f * h;

        // Blush.
        if (pose.Blush > 0.02f)
        {
            _fill.Color = palette.Blush.ToSk(pose.Blush * 0.8f);
            float by = -0.50f * h;
            float bx = 0.24f * h;
            canvas.DrawOval(new SKRect(-bx - (eyeR * 0.9f), by - (eyeR * 0.5f), -bx + (eyeR * 0.9f), by + (eyeR * 0.5f)), _fill);
            canvas.DrawOval(new SKRect(bx - (eyeR * 0.9f), by - (eyeR * 0.5f), bx + (eyeR * 0.9f), by + (eyeR * 0.5f)), _fill);
        }

        DrawEye(canvas, -eyeX, eyeY, eyeR, pose, palette, lookX);
        DrawEye(canvas, eyeX, eyeY, eyeR, pose, palette, lookX);

        DrawBrows(canvas, h, pose, palette, eyeX, eyeY, eyeR);
        DrawMouth(canvas, h, pose, palette);
    }

    private void DrawEye(SKCanvas canvas, float ex, float ey, float r, Pose pose, SkinPalette palette, float lookX)
    {
        if (pose.HappyEyes > 0.5f)
        {
            // ^_^ upward arc.
            _stroke.Color = palette.Pupil.ToSk();
            _stroke.StrokeWidth = r * 0.55f;
            using var path = new SKPath();
            path.MoveTo(ex - (r * 0.8f), ey + (r * 0.2f));
            path.QuadTo(ex, ey - (r * 0.9f), ex + (r * 0.8f), ey + (r * 0.2f));
            canvas.DrawPath(path, _stroke);
            return;
        }

        if (pose.StarEyes > 0.5f)
        {
            DrawStarEye(canvas, ex, ey, r, palette);
            return;
        }

        float open = MathUtil.Clamp(pose.EyeOpen, 0f, 1.4f);
        if (open < 0.12f)
        {
            // Closed: a gentle curved line.
            _stroke.Color = palette.Pupil.ToSk();
            _stroke.StrokeWidth = r * 0.4f;
            using var line = new SKPath();
            line.MoveTo(ex - (r * 0.7f), ey);
            line.QuadTo(ex, ey + (r * 0.35f), ex + (r * 0.7f), ey);
            canvas.DrawPath(line, _stroke);
            return;
        }

        float ry = r * open;
        _fill.Color = palette.Pupil.ToSk();
        float px = ex + (lookX * r * 0.4f);
        float py = ey + (pose.EyeLookY * r * 0.35f);
        canvas.DrawOval(new SKRect(px - r, py - ry, px + r, py + ry), _fill);

        // Sparkle highlight.
        _fill.Color = new SKColor(255, 255, 255, 235);
        canvas.DrawCircle(px - (r * 0.3f), py - (ry * 0.4f), r * 0.28f, _fill);
        canvas.DrawCircle(px + (r * 0.25f), py + (ry * 0.25f), r * 0.12f, _fill);
    }

    private void DrawStarEye(SKCanvas canvas, float ex, float ey, float r, SkinPalette palette)
    {
        _fill.Color = palette.Accent.ToSk();
        using var path = new SKPath();
        for (int i = 0; i < 10; i++)
        {
            float ang = (MathF.PI * i) / 5f - MathUtil.HalfPi;
            float radius = (i % 2 == 0) ? r : r * 0.42f;
            float x = ex + (MathF.Cos(ang) * radius);
            float y = ey + (MathF.Sin(ang) * radius);
            if (i == 0)
            {
                path.MoveTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        path.Close();
        canvas.DrawPath(path, _fill);
    }

    private void DrawBrows(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float eyeX, float eyeY, float eyeR)
    {
        if (MathF.Abs(pose.BrowAngle) < 0.05f || pose.HappyEyes > 0.5f)
        {
            return;
        }

        _stroke.Color = palette.Pupil.ToSk(0.85f);
        _stroke.StrokeWidth = eyeR * 0.32f;
        float browY = eyeY - (eyeR * 1.5f);
        float tilt = pose.BrowAngle * eyeR * 0.6f;
        float len = eyeR * 0.9f;

        // Worried/angry (negative) tilts inner ends down; surprised (positive) raises both.
        canvas.DrawLine(-eyeX - len, browY + tilt, -eyeX + len, browY - tilt, _stroke);
        canvas.DrawLine(eyeX - len, browY - tilt, eyeX + len, browY + tilt, _stroke);
    }

    private void DrawMouth(SKCanvas canvas, float h, Pose pose, SkinPalette palette)
    {
        float my = -0.42f * h;
        float mw = 0.10f * h;

        if (pose.MouthOpen > 0.14f)
        {
            float openH = pose.MouthOpen * 0.16f * h;
            _fill.Color = palette.Mouth.ToSk();
            using var mouth = new SKPath();
            var rect = new SKRect(-mw, my - (openH * 0.3f), mw, my + openH);
            mouth.AddRoundRect(rect, mw * 0.8f, openH * 0.6f);
            canvas.DrawPath(mouth, _fill);

            // Tongue.
            _fill.Color = palette.Blush.ToSk();
            canvas.DrawOval(new SKRect(-mw * 0.6f, my + (openH * 0.2f), mw * 0.6f, my + openH), _fill);
            return;
        }

        _stroke.Color = palette.Mouth.ToSk();
        _stroke.StrokeWidth = 0.022f * h;
        float curve = pose.MouthCurve * 0.07f * h;
        using var path = new SKPath();
        path.MoveTo(-mw, my);
        path.QuadTo(0, my + curve, mw, my);
        canvas.DrawPath(path, _stroke);
    }

    private void DrawProps(SKCanvas canvas, float h, Pose pose, SkinPalette palette)
    {
        if (pose.CoffeeProp > 0.3f)
        {
            DrawCoffee(canvas, h, pose.CoffeeProp);
        }

        if (pose.BookProp > 0.3f)
        {
            DrawBook(canvas, h, pose.BookProp);
        }

        if (pose.ThinkBubble > 0.3f)
        {
            DrawThinkBubble(canvas, h, pose.ThinkBubble);
        }

        if (pose.UmbrellaProp > 0.3f)
        {
            DrawUmbrella(canvas, h, pose.UmbrellaProp, palette);
        }
    }

    private void DrawCoffee(SKCanvas canvas, float h, float a)
    {
        float x = 0.34f * h;
        float y = -0.40f * h;
        float w = 0.10f * h;
        float ch = 0.11f * h;
        _fill.Color = new SKColor(0xF7, 0xF3, 0xEC, (byte)(255 * a));
        canvas.DrawRoundRect(new SKRect(x - (w / 2f), y - (ch / 2f), x + (w / 2f), y + (ch / 2f)), w * 0.2f, w * 0.2f, _fill);
        _fill.Color = new SKColor(0x6F, 0x3F, 0x2A, (byte)(255 * a));
        canvas.DrawOval(new SKRect(x - (w * 0.36f), y - (ch * 0.36f), x + (w * 0.36f), y - (ch * 0.12f)), _fill);
        _stroke.Color = new SKColor(0xF7, 0xF3, 0xEC, (byte)(255 * a));
        _stroke.StrokeWidth = h * 0.014f;
        canvas.DrawArc(new SKRect(x + (w * 0.3f), y - (ch * 0.3f), x + (w * 0.8f), y + (ch * 0.3f)), -90, 180, false, _stroke);
    }

    private void DrawBook(SKCanvas canvas, float h, float a)
    {
        float y = -0.30f * h;
        float w = 0.34f * h;
        float bh = 0.16f * h;
        _fill.Color = new SKColor(0xF7, 0xF3, 0xEC, (byte)(255 * a));
        canvas.DrawRoundRect(new SKRect(-w / 2f, y - (bh / 2f), w / 2f, y + (bh / 2f)), bh * 0.15f, bh * 0.15f, _fill);
        _stroke.Color = new SKColor(0xC2, 0x62, 0x43, (byte)(255 * a));
        _stroke.StrokeWidth = h * 0.012f;
        canvas.DrawLine(0, y - (bh / 2f), 0, y + (bh / 2f), _stroke);
        canvas.DrawLine(-w * 0.32f, y - (bh * 0.22f), -w * 0.08f, y - (bh * 0.22f), _stroke);
        canvas.DrawLine(w * 0.08f, y - (bh * 0.22f), w * 0.32f, y - (bh * 0.22f), _stroke);
    }

    private void DrawThinkBubble(SKCanvas canvas, float h, float a)
    {
        float x = 0.30f * h;
        float y = -0.92f * h;
        byte alpha = (byte)(235 * a);
        _fill.Color = new SKColor(255, 255, 255, alpha);
        canvas.DrawCircle(x, y, 0.10f * h, _fill);
        canvas.DrawCircle(x - (0.13f * h), y + (0.10f * h), 0.045f * h, _fill);
        canvas.DrawCircle(x - (0.20f * h), y + (0.17f * h), 0.028f * h, _fill);
        _fill.Color = new SKColor(0x6B, 0x5B, 0x52, alpha);
        for (int i = -1; i <= 1; i++)
        {
            canvas.DrawCircle(x + (i * 0.04f * h), y, 0.014f * h, _fill);
        }
    }

    private void DrawUmbrella(SKCanvas canvas, float h, float a, SkinPalette palette)
    {
        float topY = -1.02f * h;
        float r = 0.30f * h;
        byte alpha = (byte)(255 * a);
        _stroke.Color = new SKColor(0x6B, 0x5B, 0x52, alpha);
        _stroke.StrokeWidth = h * 0.02f;
        canvas.DrawLine(0, topY, 0, -0.7f * h, _stroke);
        _fill.Color = palette.Accent.ToSk(a);
        using var dome = new SKPath();
        dome.MoveTo(-r, topY);
        dome.QuadTo(0, topY - (0.28f * h), r, topY);
        dome.Close();
        canvas.DrawPath(dome, _fill);
    }
}
