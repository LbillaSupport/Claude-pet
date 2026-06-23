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

    private static readonly SKColor ComboOutline = new(0x33, 0x26, 0x0E);

    /// <summary>
    /// Draws the juggling combo counter ("x3") centred at <paramref name="center"/>, screen-
    /// upright. <paramref name="fill"/> is the number colour (the engine tiers it by streak /
    /// record); <paramref name="punch"/> (0..1) gives it a brief "pop" scale when it ticks up;
    /// <paramref name="alpha"/> fades it out after the last catch. A chunky number with a dark
    /// outline so it reads like a video-game combo over any wallpaper. The drawn glyphs are
    /// anchored by their visual centre (not the baseline) so a tall number never clips.
    /// </summary>
    public void DrawComboCounter(SKCanvas canvas, SKPoint center, float dpi, int combo, SKColor fill, float alpha, float punch)
    {
        if (combo <= 0 || alpha <= 0.01f)
        {
            return;
        }

        byte a = (byte)(255 * MathUtil.Clamp01(alpha));
        string label = $"x{combo}";
        float size = (28f + (3.5f * combo)) * dpi;    // grows a little as the streak climbs
        size = MathF.Min(size, 58f * dpi);
        float scale = 1f + (0.35f * punch);           // pop on a fresh catch

        _text.TextSize = size * scale;
        _text.TextAlign = SKTextAlign.Center;

        // Vertically centre on the glyph's actual bounds, so the top isn't clipped and it sits
        // visually centred on `center.Y` regardless of font ascent/descent.
        var bounds = new SKRect();
        _text.MeasureText(label, ref bounds);
        float baseline = center.Y - bounds.MidY;

        // Dark outline: a stroked text pass behind the coloured fill (a temporary paint, since
        // the shared _text paint is fill-only and we don't want to mutate its style).
        using (var outline = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.18f,
            StrokeJoin = SKStrokeJoin.Round,
            Color = ComboOutline.WithAlpha(a),
            Typeface = _text.Typeface,
            TextSize = _text.TextSize,
            TextAlign = SKTextAlign.Center,
        })
        {
            canvas.DrawText(label, center.X, baseline, outline);
        }

        _text.Color = fill.WithAlpha(a);
        canvas.DrawText(label, center.X, baseline, _text);

        // restore shared paint defaults for the bubble/other callers
        _text.Color = BubbleInk;
        _text.TextAlign = SKTextAlign.Left;
    }

    /// <summary>
    /// Draws a speech bubble whose tail points down at <paramref name="tailTip"/> (canvas
    /// px). <paramref name="alpha"/> fades the whole thing in/out. Returns silently for
    /// empty text. <paramref name="leftBound"/>/<paramref name="rightBound"/> are the
    /// on-screen slice of the canvas (in canvas px); the bubble word-wraps to that width and
    /// stays inside it, so it never spills off the monitor when the crab hugs a screen edge.
    /// </summary>
    public void DrawBubble(SKCanvas canvas, SKPoint tailTip, float dpi, string text, float alpha,
        float leftBound, float rightBound)
    {
        if (string.IsNullOrEmpty(text) || alpha <= 0.01f)
        {
            return;
        }

        byte a = (byte)(255 * MathUtil.Clamp01(alpha));
        _text.TextSize = 15f * dpi;
        SKFontMetrics fm = _text.FontMetrics;
        float lineH = fm.Descent - fm.Ascent;

        float padX = 11f * dpi;
        float padY = 7f * dpi;
        float tail = 9f * dpi;
        float margin = 6f * dpi;
        float r = 9f * dpi;
        float avail = rightBound - leftBound;

        // Word-wrap so a long line can never spill past the visible region and get clipped at
        // the window/screen edge — the bubble grows taller instead of wider.
        float maxTextW = MathF.Max(40f * dpi, avail - (2f * padX) - (2f * margin));
        List<string> lines = WrapText(text, maxTextW);

        float textW = 0f;
        foreach (string line in lines)
        {
            textW = MathF.Max(textW, _text.MeasureText(line));
        }

        float bw = MathF.Min(textW + (2f * padX), avail - (2f * margin));
        float bh = (lines.Count * lineH) + (2f * padY);

        float cx = MathUtil.Clamp(tailTip.X, leftBound + (bw / 2f) + margin, rightBound - (bw / 2f) - margin);

        // Keep the whole bubble inside the canvas vertically too (it grows upward from the
        // tail, but near the top of the canvas it would clip — so clamp it back in).
        float bottom = tailTip.Y - tail;
        bottom = MathUtil.Clamp(bottom, margin + bh, (canvas.DeviceClipBounds.Height) - margin);
        float top = bottom - bh;
        var rect = new SKRect(cx - (bw / 2f), top, cx + (bw / 2f), bottom);

        // Drop shadow.
        _fill.Color = new SKColor(0, 0, 0, (byte)(70 * MathUtil.Clamp01(alpha)));
        canvas.DrawRoundRect(new SKRect(rect.Left, rect.Top + (1.5f * dpi), rect.Right, rect.Bottom + (2.5f * dpi)), r, r, _fill);

        // Bubble.
        _fill.Color = BubbleFill.WithAlpha(a);
        canvas.DrawRoundRect(rect, r, r, _fill);

        // Tail — only when the tip actually sits below the bubble (it may have been pushed
        // down over the body when there was no headroom; a tail pointing "up" looks wrong).
        if (tailTip.Y > bottom + (1f * dpi))
        {
            using var tailPath = new SKPath();
            float baseX = MathUtil.Clamp(tailTip.X, rect.Left + (10f * dpi), rect.Right - (10f * dpi));
            tailPath.MoveTo(baseX - (6f * dpi), bottom - (1f * dpi));
            tailPath.LineTo(MathUtil.Clamp(tailTip.X, rect.Left + (6f * dpi), rect.Right - (6f * dpi)), tailTip.Y);
            tailPath.LineTo(baseX + (6f * dpi), bottom - (1f * dpi));
            tailPath.Close();
            canvas.DrawPath(tailPath, _fill);
        }

        // Text, line by line, centred.
        _text.Color = BubbleInk.WithAlpha(a);
        float baseline = rect.Top + padY - fm.Ascent;
        foreach (string line in lines)
        {
            float lw = _text.MeasureText(line);
            canvas.DrawText(line, cx - (lw / 2f), baseline, _text);
            baseline += lineH;
        }
    }

    /// <summary>Greedily wraps text to fit <paramref name="maxWidth"/> (canvas px) per line.</summary>
    private List<string> WrapText(string text, float maxWidth)
    {
        var lines = new List<string>();
        string[] words = text.Split(' ');
        var current = new System.Text.StringBuilder();

        foreach (string word in words)
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (_text.MeasureText(candidate) <= maxWidth || current.Length == 0)
            {
                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(word);
            }
            else
            {
                lines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines.Count == 0 ? new List<string> { text } : lines;
    }
}
