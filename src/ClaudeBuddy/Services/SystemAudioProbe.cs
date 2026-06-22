using System.Runtime.InteropServices;

namespace ClaudeBuddy.Services;

/// <summary>
/// Best-effort, READ-ONLY probe of the system master volume via Windows Core Audio
/// (<c>IAudioEndpointVolume</c>). Lets the mascot notice when you turn the volume up/down or
/// mute. Like the rest of the desktop probing it never changes anything — it only reads the
/// current level — and degrades gracefully: on any failure (no default device, an exotic
/// build, COM unavailable) <see cref="TryGetMasterVolume"/> returns false and the volume
/// reaction simply never fires. Works the same on Windows 10 and 11 (Core Audio is identical).
/// </summary>
public sealed class SystemAudioProbe
{
    // --- Minimal Core Audio COM interop (only the three calls we actually need). ---

    private static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IID_IMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    private const int ERender = 0;     // EDataFlow.eRender (output devices)
    private const int EConsole = 0;    // ERole.eConsole
    private const uint ClsCtxAll = 23; // CLSCTX_INPROC_SERVER | _HANDLER | _LOCAL_SERVER | _REMOTE_SERVER

    /// <summary>
    /// Reads the current master output volume (0..1) and mute state. Returns false if the
    /// system audio endpoint can't be read for any reason (then the caller does nothing).
    /// </summary>
    public bool TryGetMasterVolume(out float level, out bool muted)
    {
        level = 0f;
        muted = false;

        object? enumObj = null;
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioEndpointVolume? volume = null;
        try
        {
            Type? t = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator);
            if (t is null)
            {
                return false;
            }

            enumObj = Activator.CreateInstance(t);
            enumerator = enumObj as IMMDeviceEnumerator;
            if (enumerator is null || enumerator.GetDefaultAudioEndpoint(ERender, EConsole, out device) != 0 || device is null)
            {
                return false;
            }

            Guid iid = IID_IAudioEndpointVolume;
            if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out object volObj) != 0 || volObj is not IAudioEndpointVolume v)
            {
                return false;
            }

            volume = v;
            if (volume.GetMasterVolumeLevelScalar(out float lvl) != 0)
            {
                return false;
            }

            muted = volume.GetMute(out bool m) == 0 && m;
            level = lvl;
            return true;
        }
        catch
        {
            return false; // COM hiccup / no device — treat as "couldn't read".
        }
        finally
        {
            if (volume is not null) { Marshal.ReleaseComObject(volume); }
            if (device is not null) { Marshal.ReleaseComObject(device); }
            if (enumerator is not null) { Marshal.ReleaseComObject(enumerator); }
            else if (enumObj is not null) { Marshal.ReleaseComObject(enumObj); }
        }
    }

    // Only the vtable slots up to the methods we call are declared; the rest are padded with
    // PreserveSig stubs so the COM vtable layout stays correct.

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? endpoint);
        // (remaining methods unused)
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        // (remaining methods unused)
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr cb);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr cb);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        // (remaining methods unused)
    }
}
