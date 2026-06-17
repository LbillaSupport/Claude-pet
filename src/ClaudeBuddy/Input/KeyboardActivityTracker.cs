using System.Threading;
using ClaudeBuddy.Core;
using ClaudeBuddy.Utilities;

namespace ClaudeBuddy.Input;

/// <summary>
/// Notices keyboard activity system-wide so the mascot can react while you type.
///
/// PRIVACY — read me: this installs a low-level keyboard hook, but it is deliberately
/// NOT a key logger. The hook callback only ever <em>counts</em> key-down events and
/// notes their timing; it never reads, decodes, or stores which key was pressed (it
/// does not touch the key code in <c>lParam</c>). No keystroke content is kept,
/// buffered, or sent anywhere. The whole feature can be turned off in settings
/// (<c>KeyboardReactions = false</c>), in which case the hook is never installed.
/// </summary>
public sealed class KeyboardActivityTracker
{
    private const float TypingTimeoutSeconds = 1.1f;
    private const float EnergyTimeConstant = 0.55f;     // how quickly typing energy decays
    private const float FullIntensityEnergy = 5.5f;     // energy that maps to intensity 1.0

    // The delegate must be kept alive in a field, or the GC will collect it and the
    // hook will silently stop firing (a classic P/Invoke hook bug).
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hook;

    private int _rawCount;            // bumped on the hook thread, drained each Update
    private float _sinceLastKey = 999f;
    private float _typingEnergy;
    private bool _wasTyping;

    public KeyboardActivityTracker()
    {
        _proc = HookCallback;
    }

    /// <summary>True for a short window after the most recent key press.</summary>
    public bool IsTyping { get; private set; }

    /// <summary>Smoothed typing speed, mapped to 0 (idle) .. 1 (furious).</summary>
    public float Intensity { get; private set; }

    /// <summary>Number of keys pressed since the previous <see cref="Update"/>.</summary>
    public int NewKeystrokes { get; private set; }

    /// <summary>True on the single frame typing begins.</summary>
    public bool JustStarted { get; private set; }

    /// <summary>True on the single frame typing stops.</summary>
    public bool JustStopped { get; private set; }

    public bool Installed => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        IntPtr module = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, module, 0);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Update(float dt)
    {
        int keys = Interlocked.Exchange(ref _rawCount, 0);
        NewKeystrokes = keys;

        _sinceLastKey = keys > 0 ? 0f : _sinceLastKey + dt;

        // Leaky-integrator "typing energy": each keystroke adds 1, the pile decays
        // smoothly, so steady typing settles at a stable intensity and a lone tap fades.
        _typingEnergy = (_typingEnergy * MathF.Exp(-dt / EnergyTimeConstant)) + keys;

        bool typing = _sinceLastKey < TypingTimeoutSeconds;
        Intensity = MathUtil.Clamp01(_typingEnergy / FullIntensityEnergy);

        JustStarted = typing && !_wasTyping;
        JustStopped = !typing && _wasTyping;
        IsTyping = typing;
        _wasTyping = typing;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == NativeMethods.HC_ACTION)
        {
            uint msg = (uint)wParam.ToInt64();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                // Count only. We intentionally never inspect lParam (the key identity).
                Interlocked.Increment(ref _rawCount);
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}
