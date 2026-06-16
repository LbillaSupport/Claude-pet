using ClaudeBuddy.Core;
using ClaudeBuddy.Utilities;

namespace ClaudeBuddy.Engine;

/// <summary>
/// Describes the "stage" the mascot performs on: the bounds of the virtual desktop,
/// the taskbar-aware ground line, and the current DPI scale. Screen geometry can
/// change at runtime (monitor hot-plug, DPI change, taskbar resize) so this is
/// refreshed on the relevant Win32 messages.
/// </summary>
public sealed class World
{
    // These expose the internal interop RECT type, so they are internal too (the whole
    // app is one assembly). Consumers use the float helpers below for geometry.

    /// <summary>Full virtual-desktop rectangle in physical pixels.</summary>
    internal NativeMethods.RECT VirtualBounds { get; private set; }

    /// <summary>Primary monitor work area (excludes the taskbar) in physical pixels.</summary>
    internal NativeMethods.RECT WorkArea { get; private set; }

    /// <summary>DPI scale of the monitor the mascot currently lives on (1.0 = 96 DPI).</summary>
    public float DpiScale { get; private set; } = 1.0f;

    /// <summary>The Y coordinate (physical px) the mascot's feet rest on by default.</summary>
    public float GroundY => WorkArea.Bottom;

    public float LeftWall => VirtualBounds.Left;

    public float RightWall => VirtualBounds.Right;

    public WeatherKind Weather { get; set; } = WeatherKind.Clear;

    public World() => Refresh();

    public void SetDpiScale(float scale) => DpiScale = MathUtil.Clamp(scale, 0.5f, 4f);

    /// <summary>Re-reads display metrics. Cheap; safe to call on display-change events.</summary>
    public void Refresh()
    {
        int vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        if (vw <= 0 || vh <= 0)
        {
            // Extremely defensive fallback for headless/odd sessions.
            vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        }

        VirtualBounds = new NativeMethods.RECT { Left = vx, Top = vy, Right = vx + vw, Bottom = vy + vh };

        var work = default(NativeMethods.RECT);
        if (NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETWORKAREA, 0, ref work, 0) &&
            work.Width > 0 && work.Height > 0)
        {
            WorkArea = work;
        }
        else
        {
            WorkArea = VirtualBounds;
        }
    }

    /// <summary>Clamps an X position so the mascot stays on-screen.</summary>
    public float ClampX(float x, float halfWidth) =>
        MathUtil.Clamp(x, LeftWall + halfWidth, RightWall - halfWidth);
}
