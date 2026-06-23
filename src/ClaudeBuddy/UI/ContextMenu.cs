using ClaudeBuddy.Core;
using ClaudeBuddy.Utilities;

namespace ClaudeBuddy.UI;

/// <summary>A skin entry shown in the menu.</summary>
public readonly record struct SkinMenuItem(string Id, string Name, bool IsCurrent);

/// <summary>
/// One playable animation in the "Play Animation" submenu. <see cref="Id"/> is the
/// behaviour id the engine force-plays; <see cref="Category"/> groups items into
/// sub-sub-menus; <see cref="Name"/> is the human label.
/// </summary>
public readonly record struct AnimationMenuItem(string Id, string Name, string Category);

/// <summary>Everything the menu needs to render its current toggle/selection states.</summary>
public sealed class MenuState
{
    public bool AlwaysOnTop { get; init; }
    public bool Muted { get; init; }
    public bool LaunchOnStartup { get; init; }
    public bool PhotoMode { get; init; }
    public bool ShowBattery { get; init; }
    public bool WorldData { get; init; }
    public float AnimationSpeed { get; init; } = 1f;
    public float Volume { get; init; } = 0.7f;
    public float BehaviorFrequency { get; init; } = 1f;
    public IReadOnlyList<SkinMenuItem> Skins { get; init; } = Array.Empty<SkinMenuItem>();
    public IReadOnlyList<AnimationMenuItem> Animations { get; init; } = Array.Empty<AnimationMenuItem>();

    /// <summary>The user's language setting (may be <see cref="Core.Language.Auto"/>).</summary>
    public Core.Language Language { get; init; } = Core.Language.Auto;
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
    private readonly ClaudeBuddy.Content.Strings _str;

    public ContextMenu(ClaudeBuddy.Content.Strings str) => _str = str;

    // Command-id ranges keep dynamic sub-menu items distinct from the fixed commands.
    private const uint SkinBase = 1000;
    private const uint SpeedBase = 1100;
    private const uint VolumeBase = 1200;
    private const uint FreqBase = 1300;
    private const uint LangBase = 1400; // "Language" items: LangBase + (int)Language
    private const uint AnimBase = 2000; // "Play Animation" items: AnimBase + index into MenuState.Animations

    private static readonly float[] SpeedPresets = { 0.5f, 0.75f, 1f, 1.5f, 2f };
    private static readonly float[] FreqPresets = { 0.5f, 1f, 1.5f, 2f };
    private static readonly string[] FreqKeys = { "freq.calm", "freq.normal", "freq.lively", "freq.hyper" };

    // The languages offered, in menu order (Auto first). Labels come from Strings/native names.
    private static readonly (Core.Language Lang, string Key, string Native)[] Languages =
    {
        (Core.Language.Auto, "menu.language_auto", ""),
        (Core.Language.English, "", "English"),
        (Core.Language.Spanish, "", "Español"),
        (Core.Language.Portuguese, "", "Português"),
        (Core.Language.French, "", "Français"),
        (Core.Language.German, "", "Deutsch"),
        (Core.Language.Italian, "", "Italiano"),
    };

    public MenuSelection Show(IntPtr hwnd, int x, int y, MenuState state)
    {
        IntPtr menu = NativeMethods.CreatePopupMenu();
        var subMenus = new List<IntPtr>();

        try
        {
            Add(menu, MenuCommand.OpenClaude, _str.T("menu.open_claude"));
            Separator(menu);

            AppendSub(menu, _str.T("menu.change_skin"), BuildSkinMenu(state, subMenus));
            AppendSub(menu, _str.T("menu.animation_speed"), BuildPresetMenu(SpeedBase, SpeedPresets, state.AnimationSpeed, v => $"{v:0.##}x", subMenus));
            AppendSub(menu, _str.T("menu.behaviour_frequency"), BuildFreqMenu(state, subMenus));
            AppendSub(menu, _str.T("menu.language"), BuildLanguageMenu(state, subMenus));
            // No Volume / Mute entries: the app is silent by design (no audio).
            Separator(menu);

            // Dev/showcase: force-play any single animation to review (and tweak) it.
            AppendSub(menu, _str.T("menu.play_animation"), BuildAnimationMenu(state, subMenus));
            Separator(menu);

            Check(menu, MenuCommand.ToggleAlwaysOnTop, _str.T("menu.always_on_top"), state.AlwaysOnTop);
            Check(menu, MenuCommand.ToggleLaunchOnStartup, _str.T("menu.launch_on_startup"), state.LaunchOnStartup);
            Check(menu, MenuCommand.PhotoMode, _str.T("menu.photo_mode"), state.PhotoMode);
            Check(menu, MenuCommand.ToggleBattery, _str.T("menu.show_battery"), state.ShowBattery);
            Check(menu, MenuCommand.ToggleWorldData, _str.T("menu.world_data"), state.WorldData);
            Add(menu, MenuCommand.ResetPosition, _str.T("menu.reset_position"));
            Separator(menu);

            Add(menu, MenuCommand.Achievements, _str.T("menu.achievements"));
            Add(menu, MenuCommand.Mods, _str.T("menu.mods"));
            Add(menu, MenuCommand.Settings, _str.T("menu.settings_folder"));
            Add(menu, MenuCommand.About, _str.T("menu.about"));
            Separator(menu);

            Add(menu, MenuCommand.Exit, _str.T("menu.exit"));

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

        if (id is >= FreqBase and < LangBase)
        {
            return new MenuSelection(MenuCommand.BehaviorFrequency, Value: FreqPresets[(int)(id - FreqBase)]);
        }

        if (id is >= LangBase and < AnimBase)
        {
            int li = (int)(id - LangBase);
            return li >= 0 && li < Languages.Length
                ? new MenuSelection(MenuCommand.SetLanguage, Value: (int)Languages[li].Lang)
                : new MenuSelection(MenuCommand.None);
        }

        if (id >= AnimBase)
        {
            int i = (int)(id - AnimBase);
            return i >= 0 && i < state.Animations.Count
                ? new MenuSelection(MenuCommand.PlayAnimation, state.Animations[i].Id)
                : new MenuSelection(MenuCommand.None);
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

    /// <summary>
    /// Builds the "Play Animation" submenu: items are grouped into per-category sub-sub-menus
    /// (Idle, Playful, …) so the long list stays browsable. Each leaf carries AnimBase + its
    /// index into <see cref="MenuState.Animations"/>, decoded back to the behaviour id.
    /// </summary>
    private static IntPtr BuildAnimationMenu(MenuState state, List<IntPtr> owned)
    {
        IntPtr root = NativeMethods.CreatePopupMenu();
        owned.Add(root);

        // Group by category, preserving first-seen order of both categories and items so the
        // menu mirrors the catalogue's curated ordering.
        var categories = new List<string>();
        var byCategory = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < state.Animations.Count; i++)
        {
            string cat = state.Animations[i].Category;
            if (!byCategory.TryGetValue(cat, out List<int>? indices))
            {
                indices = new List<int>();
                byCategory[cat] = indices;
                categories.Add(cat);
            }

            indices.Add(i);
        }

        foreach (string cat in categories)
        {
            IntPtr sub = NativeMethods.CreatePopupMenu();
            owned.Add(sub);
            foreach (int i in byCategory[cat])
            {
                NativeMethods.AppendMenu(sub, NativeMethods.MF_STRING, (UIntPtr)(AnimBase + (uint)i), state.Animations[i].Name);
            }

            AppendSub(root, cat, sub);
        }

        return root;
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

    private IntPtr BuildFreqMenu(MenuState state, List<IntPtr> owned)
    {
        IntPtr sub = NativeMethods.CreatePopupMenu();
        owned.Add(sub);
        int nearest = NearestIndex(FreqPresets, state.BehaviorFrequency);
        for (int i = 0; i < FreqPresets.Length; i++)
        {
            uint flags = NativeMethods.MF_STRING | (i == nearest ? NativeMethods.MF_CHECKED : 0);
            NativeMethods.AppendMenu(sub, flags, (UIntPtr)(FreqBase + (uint)i), _str.T(FreqKeys[i]));
        }

        return sub;
    }

    private IntPtr BuildLanguageMenu(MenuState state, List<IntPtr> owned)
    {
        IntPtr sub = NativeMethods.CreatePopupMenu();
        owned.Add(sub);
        for (int i = 0; i < Languages.Length; i++)
        {
            (Core.Language lang, string key, string native) = Languages[i];
            string label = key.Length > 0 ? _str.T(key) : native; // "Auto (system)" localized; rest are native names
            uint flags = NativeMethods.MF_STRING | (lang == state.Language ? NativeMethods.MF_CHECKED : 0);
            NativeMethods.AppendMenu(sub, flags, (UIntPtr)(LangBase + (uint)i), label);
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
