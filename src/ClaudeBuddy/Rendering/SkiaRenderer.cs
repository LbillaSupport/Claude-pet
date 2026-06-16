using System.Runtime.InteropServices;
using ClaudeBuddy.Utilities;
using SkiaSharp;

namespace ClaudeBuddy.Rendering;

/// <summary>
/// Bridges SkiaSharp to a Win32 layered window. It owns a top-down 32-bit premultiplied
/// BGRA DIB section, points an <see cref="SKSurface"/> straight at that memory (zero
/// copy), and presents each frame with <c>UpdateLayeredWindow</c>. The result is true
/// per-pixel alpha over the live desktop — soft anti-aliased edges, real shadows, and
/// automatic click-through on fully transparent pixels — with no flicker.
/// </summary>
public sealed class SkiaRenderer : IDisposable
{
    private readonly int _size;
    private IntPtr _memDc;
    private IntPtr _dib;
    private IntPtr _oldBitmap;
    private IntPtr _bits;
    private SKSurface? _surface;

    public SkiaRenderer(int canvasSizePx)
    {
        _size = Math.Max(64, canvasSizePx);
        CreateResources();
    }

    /// <summary>The square canvas edge length in physical pixels.</summary>
    public int Size => _size;

    /// <summary>
    /// Clears the canvas to transparent, lets <paramref name="draw"/> paint the scene,
    /// then blits the result to <paramref name="hwnd"/> positioned at
    /// (<paramref name="screenX"/>, <paramref name="screenY"/>).
    /// </summary>
    public void Render(IntPtr hwnd, int screenX, int screenY, byte globalAlpha, Action<SKCanvas> draw)
    {
        if (_surface is null)
        {
            return;
        }

        SKCanvas canvas = _surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        draw(canvas);
        canvas.Flush();

        var dst = new NativeMethods.POINT(screenX, screenY);
        var size = new NativeMethods.SIZE(_size, _size);
        var src = new NativeMethods.POINT(0, 0);
        var blend = new NativeMethods.BLENDFUNCTION
        {
            BlendOp = NativeMethods.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = globalAlpha,
            AlphaFormat = NativeMethods.AC_SRC_ALPHA,
        };

        NativeMethods.UpdateLayeredWindow(
            hwnd, IntPtr.Zero, ref dst, ref size, _memDc, ref src, 0, ref blend, NativeMethods.ULW_ALPHA);
    }

    private void CreateResources()
    {
        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        _memDc = NativeMethods.CreateCompatibleDC(screenDc);
        NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);

        var bmi = new NativeMethods.BITMAPINFO
        {
            bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = _size,
                biHeight = -_size, // negative = top-down, matching Skia's layout
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
            },
        };

        _dib = NativeMethods.CreateDIBSection(
            _memDc, ref bmi, NativeMethods.DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
        _oldBitmap = NativeMethods.SelectObject(_memDc, _dib);

        var info = new SKImageInfo(_size, _size, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info, _bits, info.RowBytes);
    }

    public void Dispose()
    {
        _surface?.Dispose();
        _surface = null;

        if (_memDc != IntPtr.Zero)
        {
            if (_oldBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(_memDc, _oldBitmap);
                _oldBitmap = IntPtr.Zero;
            }

            if (_dib != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(_dib);
                _dib = IntPtr.Zero;
            }

            NativeMethods.DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }
    }
}
