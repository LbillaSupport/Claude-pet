using ClaudeBuddy.Core;
using SkiaSharp;

namespace ClaudeBuddy.Rendering;

/// <summary>Small conversions so model types stay free of any SkiaSharp reference.</summary>
public static class SkiaExtensions
{
    public static SKColor ToSk(this RgbaColor c) => new(c.R, c.G, c.B, c.A);

    public static SKColor ToSk(this RgbaColor c, float alphaMultiplier) =>
        new(c.R, c.G, c.B, (byte)MathUtil.Clamp(c.A * alphaMultiplier, 0f, 255f));
}
