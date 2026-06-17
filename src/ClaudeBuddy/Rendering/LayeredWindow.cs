using System.Diagnostics;
using ClaudeBuddy.Core;
using ClaudeBuddy.Utilities;

namespace ClaudeBuddy.Rendering;

/// <summary>
/// The native, borderless, click-through-aware, always-on-top host window for the
/// mascot. It owns the Win32 message pump and the fixed-cadence game loop, and turns
/// raw window messages into clean C# events for the engine to consume.
/// </summary>
public sealed class LayeredWindow
{
    private const string ClassName = "ClaudeBuddyWindowClass";

    // The delegate must be kept alive for the lifetime of the window or the GC will
    // collect it and Windows will call into freed memory.
    private readonly NativeMethods.WndProc _wndProcDelegate;
    private IntPtr _hwnd;
    private bool _running;

    // Identifies the timer that keeps the simulation alive during a modal menu loop.
    private static readonly UIntPtr ModalFrameTimerId = (UIntPtr)1;

    // The per-frame callback, stashed so WM_TIMER can drive a frame while a modal menu
    // (TrackPopupMenuEx) has hijacked the message pump and RunLoop is blocked.
    private Action? _onFrame;

    public LayeredWindow() => _wndProcDelegate = WndProc;

    // -- Input events (screen coordinates) ---------------------------------
    public event Action<int, int>? LeftButtonDown;
    public event Action<int, int>? LeftButtonUp;
    public event Action<int, int>? LeftDoubleClick;
    public event Action<int, int>? RightButtonUp;
    public event Action? DisplayChanged;
    public event Action? Closed;

    public IntPtr Handle => _hwnd;

    /// <summary>DPI scale of the window's monitor (1.0 = 96 DPI).</summary>
    public float DpiScale { get; private set; } = 1.0f;

    public void Create(int canvasSize)
    {
        // Become Per-Monitor-V2 aware as early as possible (belt-and-braces with the manifest).
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        IntPtr hInstance = NativeMethods.GetModuleHandle(null);

        var wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            style = NativeMethods.CS_DBLCLKS, // we want WM_LBUTTONDBLCLK for petting
            lpfnWndProc = _wndProcDelegate,
            hInstance = hInstance,
            hCursor = NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW),
            lpszClassName = ClassName,
        };

        NativeMethods.RegisterClassEx(ref wndClass);

        uint exStyle = NativeMethods.WS_EX_LAYERED
            | NativeMethods.WS_EX_TOPMOST
            | NativeMethods.WS_EX_TOOLWINDOW; // keep it out of Alt-Tab / taskbar

        _hwnd = NativeMethods.CreateWindowEx(
            exStyle, ClassName, "Claude Buddy", NativeMethods.WS_POPUP,
            0, 0, canvasSize, canvasSize,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
        DpiScale = dpi <= 0 ? 1.0f : dpi / 96f;

        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNOACTIVATE);
    }

    public void SetTopmost(bool topmost)
    {
        IntPtr after = topmost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST;
        NativeMethods.SetWindowPos(_hwnd, after, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Runs the simulation while a modal menu owns the message pump. Call before
    /// <c>TrackPopupMenuEx</c> and pair with <see cref="EndModalTicks"/> afterwards.
    /// While the timer is alive, WM_TIMER fires the same per-frame callback, so the
    /// mascot keeps breathing/walking instead of freezing under the open menu.
    /// </summary>
    public void BeginModalTicks() =>
        NativeMethods.SetTimer(_hwnd, ModalFrameTimerId, 1000 / EngineConstants.TargetFps, IntPtr.Zero);

    /// <summary>Stops the modal keep-alive timer (see <see cref="BeginModalTicks"/>).</summary>
    public void EndModalTicks() => NativeMethods.KillTimer(_hwnd, ModalFrameTimerId);

    /// <summary>Runs the pump + simulation until the window closes.</summary>
    public void RunLoop(Action onFrame)
    {
        _onFrame = onFrame;
        _running = true;
        NativeMethods.timeBeginPeriod(1);
        var stopwatch = Stopwatch.StartNew();
        double targetMs = 1000.0 / EngineConstants.TargetFps;

        try
        {
            while (_running)
            {
                double frameStart = stopwatch.Elapsed.TotalMilliseconds;

                // Drain all pending window messages.
                while (NativeMethods.PeekMessage(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0, NativeMethods.PM_REMOVE))
                {
                    if (msg.message == NativeMethods.WM_QUIT)
                    {
                        _running = false;
                        break;
                    }

                    NativeMethods.TranslateMessage(ref msg);
                    NativeMethods.DispatchMessage(ref msg);
                }

                if (!_running)
                {
                    break;
                }

                onFrame();

                // Pace to the target frame rate; period(1) makes Sleep ~1ms accurate.
                double elapsed = stopwatch.Elapsed.TotalMilliseconds - frameStart;
                int sleep = (int)(targetMs - elapsed);
                if (sleep > 0)
                {
                    Thread.Sleep(sleep);
                }
            }
        }
        finally
        {
            NativeMethods.timeEndPeriod(1);
        }
    }

    public void Stop()
    {
        _running = false;
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(_hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_LBUTTONDOWN:
                // Capture the mouse so a fast drag keeps sending us moves + the eventual
                // button-up even when the cursor leaves the mascot's opaque pixels.
                NativeMethods.SetCapture(hWnd);
                Raise(LeftButtonDown);
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONUP:
                NativeMethods.ReleaseCapture();
                Raise(LeftButtonUp);
                return IntPtr.Zero;

            case NativeMethods.WM_LBUTTONDBLCLK:
                Raise(LeftDoubleClick);
                return IntPtr.Zero;

            case NativeMethods.WM_RBUTTONUP:
                Raise(RightButtonUp);
                return IntPtr.Zero;

            case NativeMethods.WM_TIMER:
                // Only fires while a modal menu has parked RunLoop (see BeginModalTicks);
                // drive a frame so the mascot stays animated under the open menu.
                if ((ulong)wParam == (ulong)ModalFrameTimerId)
                {
                    _onFrame?.Invoke();
                }

                return IntPtr.Zero;

            case NativeMethods.WM_DISPLAYCHANGE:
                DisplayChanged?.Invoke();
                return IntPtr.Zero;

            case NativeMethods.WM_CLOSE:
                NativeMethods.DestroyWindow(hWnd);
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                Closed?.Invoke();
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static void Raise(Action<int, int>? handler)
    {
        if (handler is null)
        {
            return;
        }

        // Use the global cursor position so coordinates are unambiguous even though
        // the window itself constantly moves underneath the mascot.
        if (NativeMethods.GetCursorPos(out NativeMethods.POINT p))
        {
            handler(p.X, p.Y);
        }
    }
}
