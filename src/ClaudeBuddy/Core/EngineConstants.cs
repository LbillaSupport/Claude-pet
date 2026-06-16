namespace ClaudeBuddy.Core;

/// <summary>
/// Central, named home for every tunable constant in the engine. Keeping them here
/// (rather than sprinkling literals through the code) means the whole "feel" of the
/// mascot can be adjusted from one screen, and satisfies the no-magic-numbers rule.
/// All values are expressed in <em>design</em> units (logical pixels at 96 DPI);
/// the renderer multiplies by the active DPI scale.
/// </summary>
public static class EngineConstants
{
    /// <summary>Target simulation/render rate.</summary>
    public const int TargetFps = 60;

    public const float TargetFrameSeconds = 1f / TargetFps;

    /// <summary>The square canvas (logical px) that follows the mascot around.</summary>
    public const int CanvasDesignSize = 440;

    /// <summary>Where the feet sit inside the canvas, as a fraction from the top.</summary>
    public const float CanvasFeetAnchor = 0.80f;

    /// <summary>Nominal character height in logical px at scale 1.0.</summary>
    public const float CharacterHeight = 150f;

    // ---- Physics ---------------------------------------------------------

    /// <summary>Downward acceleration (logical px / s²).</summary>
    public const float Gravity = 2600f;

    public const float WalkSpeed = 46f;
    public const float RunSpeed = 150f;
    public const float JumpVelocity = 920f;

    /// <summary>Horizontal drag applied to free momentum (per second).</summary>
    public const float GroundFriction = 6.5f;
    public const float AirFriction = 0.6f;

    /// <summary>Energy retained after bouncing off the ground (0–1).</summary>
    public const float Bounciness = 0.32f;

    /// <summary>Pixels the cursor must move while pressed before it counts as a drag.</summary>
    public const float DragThreshold = 6f;

    /// <summary>Multiplier applied to release velocity when the mascot is thrown.</summary>
    public const float ThrowImpulseScale = 0.9f;

    public const float MaxThrowSpeed = 2600f;

    // ---- Behaviour -------------------------------------------------------

    /// <summary>Base seconds between autonomous behaviour re-evaluations.</summary>
    public const float BehaviorTickSeconds = 0.5f;

    public const float BlinkMinInterval = 2.2f;
    public const float BlinkMaxInterval = 6.0f;

    // ---- Emotion ---------------------------------------------------------

    /// <summary>Happiness lost per second of being ignored.</summary>
    public const float HappinessDecayPerSecond = 0.0025f;

    /// <summary>Happiness gained per pet.</summary>
    public const float HappinessPerPet = 0.06f;

    /// <summary>Happiness threshold that unlocks rare reactions.</summary>
    public const float HappinessUnlockThreshold = 0.85f;

    // ---- Interaction -----------------------------------------------------

    /// <summary>Cursor speed (px/s) above which the mascot is startled.</summary>
    public const float SurpriseCursorSpeed = 2600f;

    /// <summary>Distance (logical px) under which the mascot notices the cursor.</summary>
    public const float CursorNoticeRadius = 320f;
}
