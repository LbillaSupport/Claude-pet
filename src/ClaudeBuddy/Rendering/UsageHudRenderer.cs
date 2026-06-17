using ClaudeBuddy.Core;
using ClaudeBuddy.Services;
using SkiaSharp;

namespace ClaudeBuddy.Rendering;

/// <summary>
/// Draws the little session "battery" that floats by the mascot and the occasional
/// speech bubble. Both are screen-upright (never rotated with the body) so they stay
/// readable while the crab clings to a wall or hangs from the ceiling. All sizes scale
/// with DPI so it stays crisp.
/// </summary>
public sealed class UsageHudRenderer
{
    private static readonly SKColor Green = new(0x5B, 0xC8, 0x5B);
    private static readonly SKColor Amber = new(0xF2, 0xB2, 0x3A);
    private static readonly SKColor Red = new(0xE5, 0x54, 0x4A);
    private static readonly SKColor Shell = new(0xF2, 0xF2, 0xF2);
    private static readonly SKColor BubbleFill = new(0xFC, 0xFA, 0xF6);
    private static readonly SKColor BubbleInk = new(0x3A, 0x32, 0x2C);

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly SKPaint _text = new()
    {
        IsAntialias = true,
        Color = BubbleInk,
        Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
    };

    /// <summary>
    /// Draws the battery centred at <paramref name="center"/> (canvas px).
    /// <paramref name="charge"/> is the remaining charge 0..1; <paramref name="pulse"/>
    /// is a 0..1 emphasis used to make a low/charging battery breathe.
    /// </summary>
    public void DrawBattery(SKCanvas canvas, SKPoint center, float dpi, float charge, bool charging, float pulse)
    {
        float w = 46f * dpi;
        float h = 22f * dpi;
        float scale = 1f + (0.06f * pulse);

        canvas.Save();
        canvas.Translate(center.X, center.Y);
        canvas.Scale(scale);

        var body = new SKRect(-w / 2f, -h / 2f, w / 2f, h / 2f);
        float radius = 5f * dpi;

        // Backplate so it reads on any wallpaper.
        _fill.Color = new SKColor(0, 0, 0, 80);
        float bp = 2f * dpi;
        canvas.DrawRoundRect(new SKRect(body.Left - bp, body.Top - bp, body.Right + bp, body.Bottom + bp), radius + bp, radius + bp, _fill);

        // Charge bar (remaining), coloured by how much is left.
        float pad = 3.2f * dpi;
        var inner = new SKRect(body.Left + pad, body.Top + pad, body.Right - pad, body.Bottom - pad);
        float fillW = inner.Width * MathUtil.Clamp01(charge);
        _fill.Color = charge > 0.5f ? Green : (charge > 0.22f ? Amber : Red);
        if (fillW > 0.5f)
        {
            canvas.DrawRoundRect(new SKRect(inner.Left, inner.Top, inner.Left + fillW, inner.Bottom), 2f * dpi, 2f * dpi, _fill);
        }

        // Shell outline + positive terminal nub.
        _stroke.Color = Shell;
        _stroke.StrokeWidth = 2.4f * dpi;
        canvas.DrawRoundRect(body, radius, radius, _stroke);
        _fill.Color = Shell;
        canvas.DrawRoundRect(new SKRect(w / 2f, -h * 0.2f, (w / 2f) + (4.5f * dpi), h * 0.2f), 1.5f * dpi, 1.5f * dpi, _fill);

        // A lightning bolt while charging / just-renewed.
        if (charging)
        {
            _fill.Color = new SKColor(0xFF, 0xF4, 0xD0);
            using var bolt = new SKPath();
            float bw = 7f * dpi;
            float bh = 12f * dpi;
            bolt.MoveTo(bw * 0.2f, -bh / 2f);
            bolt.LineTo(-bw * 0.5f, bh * 0.12f);
            bolt.LineTo(-bw * 0.05f, bh * 0.12f);
            bolt.LineTo(-bw * 0.2f, bh / 2f);
            bolt.LineTo(bw * 0.5f, -bh * 0.12f);
            bolt.LineTo(bw * 0.05f, -bh * 0.12f);
            bolt.Close();
            canvas.DrawPath(bolt, _fill);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Draws a speech bubble whose tail points down at <paramref name="tailTip"/> (canvas
    /// px). <paramref name="alpha"/> fades the whole thing in/out. Returns silently for
    /// empty text. <paramref name="canvasSize"/> keeps it from spilling off the edges.
    /// </summary>
    public void DrawBubble(SKCanvas canvas, SKPoint tailTip, float dpi, string text, float alpha, int canvasSize)
    {
        if (string.IsNullOrEmpty(text) || alpha <= 0.01f)
        {
            return;
        }

        byte a = (byte)(255 * MathUtil.Clamp01(alpha));
        _text.TextSize = 15f * dpi;
        float textW = _text.MeasureText(text);
        SKFontMetrics fm = _text.FontMetrics;
        float textH = fm.Descent - fm.Ascent;

        float padX = 11f * dpi;
        float padY = 7f * dpi;
        float bw = textW + (2f * padX);
        float bh = textH + (2f * padY);
        float tail = 9f * dpi;
        float margin = 6f * dpi;

        float cx = MathUtil.Clamp(tailTip.X, (bw / 2f) + margin, canvasSize - (bw / 2f) - margin);
        float bottom = tailTip.Y - tail;
        var rect = new SKRect(cx - (bw / 2f), bottom - bh, cx + (bw / 2f), bottom);
        float r = 9f * dpi;

        // Drop shadow.
        _fill.Color = new SKColor(0, 0, 0, (byte)(70 * MathUtil.Clamp01(alpha)));
        canvas.DrawRoundRect(new SKRect(rect.Left, rect.Top + (1.5f * dpi), rect.Right, rect.Bottom + (2.5f * dpi)), r, r, _fill);

        // Bubble + tail.
        _fill.Color = BubbleFill.WithAlpha(a);
        canvas.DrawRoundRect(rect, r, r, _fill);
        using (var tailPath = new SKPath())
        {
            tailPath.MoveTo(cx - (6f * dpi), bottom - (1f * dpi));
            tailPath.LineTo(MathUtil.Clamp(tailTip.X, rect.Left + (6f * dpi), rect.Right - (6f * dpi)), tailTip.Y);
            tailPath.LineTo(cx + (6f * dpi), bottom - (1f * dpi));
            tailPath.Close();
            canvas.DrawPath(tailPath, _fill);
        }

        // Text, vertically centred.
        _text.Color = BubbleInk.WithAlpha(a);
        float baseline = rect.MidY - ((fm.Ascent + fm.Descent) / 2f);
        canvas.DrawText(text, cx - (textW / 2f), baseline, _text);
    }
}
