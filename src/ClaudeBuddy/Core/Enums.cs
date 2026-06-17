namespace ClaudeBuddy.Core;

/// <summary>Which way the mascot is currently facing.</summary>
public enum Facing
{
    Left = -1,
    Right = 1,
}

/// <summary>
/// The body/face archetype the artist draws. Most skins are just recolours of the
/// classic block (<see cref="Claud"/>); a few change the silhouette — the Creeper's
/// signature face, the Ghast's tentacles and sad eyes, or Nicolaia's top hat, suit and
/// side-curls dressed over the classic block.
/// </summary>
public enum SkinStyle
{
    Claud,
    Creeper,
    Ghast,
    Nicolaia,
    Galgo,
}

/// <summary>
/// Which screen edge the (crab-like) mascot is currently clinging to. Determines the
/// whole-body orientation: on a wall it turns sideways, on the ceiling it hangs upside
/// down. <see cref="Floor"/> is the normal, upright, gravity-bound state.
/// </summary>
public enum Surface
{
    Floor,
    LeftWall,
    RightWall,
    Ceiling,
}

/// <summary>
/// Phases of the simulated day. The <see cref="Routine.DailyRoutine"/> maps the
/// real wall-clock time onto one of these to colour the mascot's behaviour.
/// </summary>
public enum DayPhase
{
    Morning,    // 06:00 - 12:00
    Afternoon,  // 12:00 - 18:00
    Evening,    // 18:00 - 22:00
    Night,      // 22:00 - 01:00
    LateNight,  // 01:00 - 06:00
}

/// <summary>
/// The mascot's internal emotional state. Mood biases which behaviours are chosen,
/// how fast the mascot moves, and how its face is drawn.
/// </summary>
public enum Mood
{
    Content,
    Happy,
    Excited,
    Playful,
    Curious,
    Sleepy,
    Lazy,
    Surprised,
    Confused,
    Proud,
    Scared,
    Sad,
}

/// <summary>
/// The visual animation archetype the <see cref="Animation.Animator"/> should play.
/// Behaviours map onto these; the artist turns them into a procedural pose.
/// </summary>
public enum AnimationState
{
    Idle,
    WalkLeft,
    WalkRight,
    RunLeft,
    RunRight,
    Jump,
    Fall,
    Land,
    Roll,
    Sit,
    Stand,
    Sleep,
    WakeUp,
    Blink,
    Wave,
    Dance,
    Celebrate,
    Think,
    Read,
    Drink,
    Stretch,
    Yawn,
    Spin,
    Pet,
    Dragged,
    LookUp,
    LookDown,
    LookAround,
    Surprised,
    Scared,
    Trip,
    Happy,
    Sad,

    /// <summary>Hunched forward, tapping along while the user types at the keyboard.</summary>
    TypeAlong,

    /// <summary>Scrabbling up a wall / across the ceiling (orientation set by Surface).</summary>
    Climb,

    /// <summary>Anticipation crouch + shake, then an explosive release (power-up).</summary>
    Charge,

    /// <summary>Preening: little head-tilts and a tidy-up wiggle (e.g. adjusting a hat).</summary>
    Groom,

    /// <summary>Idle impatience: a foot taps a steady beat while standing.</summary>
    TapFoot,

    /// <summary>A small, happy in-place wiggle/sway — gentler than a full dance.</summary>
    Wiggle,
}

/// <summary>The visual primitive a particle is rendered as.</summary>
public enum ParticleKind
{
    Heart,
    Star,
    Sparkle,
    Confetti,
    Dust,
    Smoke,
    Snow,
    Leaf,
    Note,
    ZzZ,
    Magic,
}

/// <summary>Lightweight, fake weather events that the world can drift through.</summary>
public enum WeatherKind
{
    Clear,
    Snow,
    Rain,
    Leaves,
    Petals,
    Butterflies,
}

/// <summary>Commands that the right-click context menu can raise.</summary>
public enum MenuCommand
{
    None = 0,
    OpenClaude,
    ChangeSkin,
    AnimationSpeed,
    Volume,
    ToggleMute,
    BehaviorFrequency,
    ToggleAlwaysOnTop,
    ToggleLaunchOnStartup,
    ToggleBattery,
    ResetPosition,
    PhotoMode,
    Achievements,
    Mods,
    Settings,
    About,
    Exit,
}
