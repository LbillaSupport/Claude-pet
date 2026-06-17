using ClaudeBuddy.Core;
using ClaudeBuddy.Utilities;

namespace ClaudeBuddy.UI;

/// <summary>A skin entry shown in the menu.</summary>
public readonly record struct SkinMenuItem(string Id, string Name, bool IsCurrent);

/// <summary>Everything the menu needs to render its current toggle/selection states.</summary>
public sealed class MenuState
{
    public bool AlwaysOnTop { get; init; }
    public bool Muted { get; init; }
    public bool LaunchOnStartup { get; init; }
    public bool PhotoMode { get; init; }
    public bool ShowBattery { get; init; }
    public float AnimationSpeed { get; init; } = 1f;
    public float Volume { get; init; } = 0.7f;
    public float BehaviorFrequency { get; init; } = 1f;
    public IReadOnlyList<SkinMenuItem> Skins { get; init; } = Array.Empty<SkinMenuItem>();
}

/// <summary>The user's choice, decoded back into something the engine can act on.</summary>
public readonly record struct MenuSelection(MenuCommand Command, string? SkinId = null, float Value = 0f);

/// <summary>
/// Builds and shows the native right-click menu with <c>TrackPopupMenuEx</c>. Using the
/// real Win32 menu means it looks and behaves exactly like every other Windows context
/// menu (theming, keyboard, sub-menus) for free. Selections are returned synchronously
/// via <c>TPM_RETURNCMD</c> and decoded back into <see cref="MenuSelection"/>s.
/// </summary>
public sealed class ContextMenu
{
    // Command-id ranges keep dynamic sub-menu items distinct from the fixed commands.
    private const uint SkinBase = 1000;
    private const uint SpeedBase = 1100;
    private const uint VolumeBase = 1200;
    private const uint FreqBase = 1300;

    private static readonly float[] SpeedPresets = { 0.5f, 0.75f, 1f, 1.5f, 2f };
    private static readonly float[] VolumePresets = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    private static readonly float[] FreqPresets = { 0.5f, 1f, 1.5f, 2f };
    private static readonly string[] FreqNames = { "Calm", "Normal", "Lively", "Hyper" };

    public MenuSelection Show(IntPtr hwnd, int x, int y, MenuState state)
    {
        IntPtr menu = NativeMethods.CreatePopupMenu();
        var subMenus = new List<IntPtr>();

        try
        {
            Add(menu, MenuCommand.OpenClaude, "Open Claude");
            Separator(menu);

            AppendSub(menu, "Change Skin", BuildSkinMenu(state, subMenus));
            AppendSub(menu, "Animation Speed", BuildPresetMenu(SpeedBase, SpeedPresets, state.AnimationSpeed, v => $"{v:0.##}x", subMenus));
            AppendSub(menu, "Behaviour Frequency", BuildFreqMenu(state, subMenus));
            AppendSub(menu, "Volume", BuildVolumeMenu(state, subMenus));
            Check(menu, MenuCommand.ToggleMute, "Mute", state.Muted);
            Separator(menu);

            Check(menu, MenuCommand.ToggleAlwaysOnTop, "Always On Top", state.AlwaysOnTop);
            Check(menu, MenuCommand.ToggleLaunchOnStartup, "Launch On Startup", state.LaunchOnStartup);
            Check(menu, MenuCommand.PhotoMode, "Photo Mode", state.PhotoMode);
            Check(menu, MenuCommand.ToggleBattery, "Show Session Battery", state.ShowBattery);
            Add(menu, MenuCommand.ResetPosition, "Reset Position");
            Separator(menu);

            Add(menu, MenuCommand.Achievements, "Achievements…");
            Add(menu, MenuCommand.Mods, "Mods…");
            Add(menu, MenuCommand.Settings, "Settings Folder…");
            Add(menu, MenuCommand.About, "About Claude Buddy");
            Separator(menu);

            Add(menu, MenuCommand.Exit, "Exit");

            // The documented dance so a tool/topmost window can host a tracking menu.
            NativeMethods.SetForegroundWindow(hwnd);
            uint id = NativeMethods.TrackPopupMenuEx(
                menu,
                NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD,
                x, y, hwnd, IntPtr.Zero);
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            return Decode(id, state);
        }
        finally
        {
            foreach (IntPtr sub in subMenus)
            {
                NativeMethods.DestroyMenu(sub);
            }

            NativeMethods.DestroyMenu(menu);
        }
    }

    private static MenuSelection Decode(uint id, MenuState state)
    {
        if (id == 0)
        {
            return new MenuSelection(MenuCommand.None);
        }

        if (id is >= SkinBase and < SpeedBase)
        {
            int i = (int)(id - SkinBase);
            return i >= 0 && i < state.Skins.Count
                ? new MenuSelection(MenuCommand.ChangeSkin, state.Skins[i].Id)
                : new MenuSelection(MenuCommand.None);
        }

        if (id is >= SpeedBase and < VolumeBase)
        {
            return new MenuSelection(MenuCommand.AnimationSpeed, Value: SpeedPresets[(int)(id - SpeedBase)]);
        }

        if (id is >= VolumeBase and < FreqBase)
        {
            return new MenuSelection(MenuCommand.Volume, Value: VolumePresets[(int)(id - VolumeBase)]);
        }

        if (id is >= FreqBase and < FreqBase + 100)
        {
            return new MenuSelection(MenuCommand.BehaviorFrequency, Value: FreqPresets[(int)(id - FreqBase)]);
        }

        return new MenuSelection((MenuCommand)id);
    }

    private static IntPtr BuildSkinMenu(MenuState state, List<IntPtr> owned)
    {
        IntPtr sub = NativeMethods.CreatePopupMenu();
        owned.Add(sub);
        for (int i = 0; i < state.Skins.Count; i++)
        {
            SkinMenuItem s = state.Skins[i];
            uint flags = NativeMethods.MF_STRING | (s.IsCurrent ? NativeMethods.MF_CHECKED : 0);
            NativeMethods.AppendMenu(sub, flags, (UIntPtr)(SkinBase + (uint)i), s.Name);
        }

        return sub;
    }

    private static IntPtr BuildPresetMenu(uint baseId, float[] presets, float current, Func<float, string> label, List<IntPtr> owned)
    {
        IntPtr sub = NativeMethods.CreatePopupMenu();
        owned.Add(sub);
        int nearest = NearestIndex(presets, current);
        for (int i = 0; i < presets.Length; i++)
        {
            uint flags = NativeMethods.MF_STRING | (i == nearest ? NativeMethods.MF_CHECKED : 0);
            NativeMethods.AppendMenu(sub, flags, (UIntPtr)(baseId + (uint)i), label(presets[i]));
        }

        return sub;
    }

    private static IntPtr BuildFreqMenu(MenuState state, List<IntPtr> owned)
    {
        IntPtr sub = NativeMethods.CreatePopupMenu();
        owned.Add(sub);
        int nearest = NearestIndex(FreqPresets, state.BehaviorFrequency);
        for (int i = 0; i < FreqPresets.Length; i++)
        {
            uint flags = NativeMethods.MF_STRING | (i == nearest ? NativeMethods.MF_CHECKED : 0);
            NativeMethods.AppendMenu(sub, flags, (UIntPtr)(FreqBase + (uint)i), FreqNames[i]);
        }

        return sub;
    }

    private static IntPtr BuildVolumeMenu(MenuState state, List<IntPtr> owned)
    {
        IntPtr sub = NativeMethods.CreatePopupMenu();
        owned.Add(sub);
        int nearest = NearestIndex(VolumePresets, state.Volume);
        for (int i = 0; i < VolumePresets.Length; i++)
        {
            uint flags = NativeMethods.MF_STRING | (i == nearest && !state.Muted ? NativeMethods.MF_CHECKED : 0);
            NativeMethods.AppendMenu(sub, flags, (UIntPtr)(VolumeBase + (uint)i), $"{(int)(VolumePresets[i] * 100)}%");
        }

        return sub;
    }

    private static int NearestIndex(float[] values, float target)
    {
        int best = 0;
        float bestDelta = float.MaxValue;
        for (int i = 0; i < values.Length; i++)
        {
            float d = MathF.Abs(values[i] - target);
            if (d < bestDelta)
            {
                bestDelta = d;
                best = i;
            }
        }

        return best;
    }

    private static void Add(IntPtr menu, MenuCommand cmd, string text) =>
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, (UIntPtr)(uint)cmd, text);

    private static void Check(IntPtr menu, MenuCommand cmd, string text, bool isChecked) =>
        NativeMethods.AppendMenu(menu,
            NativeMethods.MF_STRING | (isChecked ? NativeMethods.MF_CHECKED : NativeMethods.MF_UNCHECKED),
            (UIntPtr)(uint)cmd, text);

    private static void Separator(IntPtr menu) =>
        NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);

    private static void AppendSub(IntPtr menu, string text, IntPtr sub) =>
        NativeMethods.AppendMenu(menu, NativeMethods.MF_POPUP, (UIntPtr)(ulong)sub.ToInt64(), text);
}
