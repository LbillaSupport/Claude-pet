namespace ClaudeBuddy.Core;

/// <summary>
/// A tiny, render-agnostic RGBA colour so model layers (particles, skins) never
/// need to reference SkiaSharp. The renderer converts this to its own colour type.
/// </summary>
public readonly struct RgbaColor
{
    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
    public readonly byte A;

    public RgbaColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public RgbaColor WithAlpha(byte a) => new(R, G, B, a);

    public RgbaColor WithAlpha(float a01) => new(R, G, B, (byte)MathUtil.Clamp(a01 * 255f, 0f, 255f));

    public static RgbaColor FromHex(string hex)
    {
        ReadOnlySpan<char> s = hex.AsSpan().TrimStart('#');
        byte r = Convert.ToByte(s.Slice(0, 2).ToString(), 16);
        byte g = Convert.ToByte(s.Slice(2, 2).ToString(), 16);
        byte b = Convert.ToByte(s.Slice(4, 2).ToString(), 16);
        byte a = s.Length >= 8 ? Convert.ToByte(s.Slice(6, 2).ToString(), 16) : (byte)255;
        return new RgbaColor(r, g, b, a);
    }

    public static RgbaColor Lerp(RgbaColor a, RgbaColor b, float t) => new(
        (byte)MathUtil.Lerp(a.R, b.R, t),
        (byte)MathUtil.Lerp(a.G, b.G, t),
        (byte)MathUtil.Lerp(a.B, b.B, t),
        (byte)MathUtil.Lerp(a.A, b.A, t));

    // The Claude / Anthropic warm-clay palette used by the default skin.
    public static RgbaColor ClaudeClay => new(0xD9, 0x7A, 0x5A);
    public static RgbaColor ClaudeClayDark => new(0xC2, 0x62, 0x43);
    public static RgbaColor Cream => new(0xF7, 0xF3, 0xEC);
    public static RgbaColor Ink => new(0x2B, 0x24, 0x20);
    public static RgbaColor Blush => new(0xF2, 0x9E, 0x8E);
    public static RgbaColor HeartPink => new(0xFF, 0x6B, 0x8A);
    public static RgbaColor StarGold => new(0xFF, 0xD1, 0x66);
}
