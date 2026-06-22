using ClaudeBuddy.Utilities;

namespace ClaudeBuddy.Services;

/// <summary>
/// Best-effort, READ-ONLY probing of real Windows desktop elements (the taskbar clock) so
/// the mascot can walk over and interact with them. Everything degrades gracefully: if an
/// element can't be located (a different Windows build, the taskbar auto-hidden, a locked-
/// down machine…) the <c>Try*</c> method simply returns false and the behaviour that wanted
/// it just doesn't fire. Nothing on the desktop is ever moved or modified.
/// </summary>
public sealed class DesktopProbe
{
    /// <summary>
    /// Finds the horizontal screen centre (physical px) of the taskbar clock. Walks the shell
    /// window tree <c>Shell_TrayWnd → TrayNotifyWnd → TrayClockWClass</c>, with fallbacks for
    /// layouts that parent the clock differently, and finally the right end of the taskbar.
    /// (Only the X is exposed because <c>NativeMethods.RECT</c> is internal and the X is all
    /// the engine needs — Claw'd walks along the floor to it.)
    /// </summary>
    public bool TryGetClockX(out float centerX)
    {
        centerX = 0f;
        try
        {
            IntPtr tray = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero)
            {
                return false;
            }

            IntPtr notify = NativeMethods.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
            IntPtr clock = notify != IntPtr.Zero
                ? NativeMethods.FindWindowEx(notify, IntPtr.Zero, "TrayClockWClass", null)
                : IntPtr.Zero;

            // Some builds parent the clock straight under the tray.
            if (clock == IntPtr.Zero)
            {
                clock = NativeMethods.FindWindowEx(tray, IntPtr.Zero, "TrayClockWClass", null);
            }

            if (clock != IntPtr.Zero && NativeMethods.GetWindowRect(clock, out NativeMethods.RECT cr) && cr.Width > 0)
            {
                centerX = (cr.Left + cr.Right) * 0.5f;
                return true;
            }

            // Last resort: the clock lives at the far end of the taskbar — aim near that corner.
            if (NativeMethods.GetWindowRect(tray, out NativeMethods.RECT t) && t.Width > 0)
            {
                centerX = t.Right - 60f;
                return true;
            }
        }
        catch
        {
            // P/Invoke hiccup on an exotic shell — treat as "not found".
        }

        return false;
    }
}
