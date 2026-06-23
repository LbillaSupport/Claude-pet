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

    /// <summary>
    /// The square canvas (logical px) that follows the mascot around. Sized generously
    /// so the body still fits when rotated onto a wall or the ceiling.
    /// </summary>
    public const int CanvasDesignSize = 480;

    /// <summary>
    /// Where the contact point (feet) sits inside the canvas, as a fraction from the
    /// top. Centred so the body has equal room to extend up, down, or sideways — which
    /// is what lets the crab cling to any edge without clipping.
    /// </summary>
    public const float CanvasFeetAnchor = 0.5f;

    /// <summary>Nominal character height in logical px at scale 1.0.</summary>
    public const float CharacterHeight = 150f;

    // ---- Physics ---------------------------------------------------------

    /// <summary>Downward acceleration (logical px / s²).</summary>
    public const float Gravity = 2600f;

    public const float WalkSpeed = 46f;
    public const float RunSpeed = 150f;
    public const float JumpVelocity = 920f;

    /// <summary>Speed (logical px/s) the crab climbs walls and crawls the ceiling.</summary>
    public const float ClimbSpeed = 70f;

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

    // ---- Drag & impact ("living drag") -----------------------------------
    // The grab follows the cursor with a touch of lag and tilts to hang from the point
    // you took hold of — but the model is an unconditionally-stable exponential ease, not
    // a stiff spring, so it can never explode (no teleporting / vanishing) regardless of
    // frame timing. Tuned for cartoon weight, not realism: soft, snappy, expressive.

    /// <summary>How quickly the held point chases the cursor (per second). Higher = tighter
    /// follow with less lag; this is a frame-rate-independent ease so it's always stable.</summary>
    public const float DragFollow = 26f;

    /// <summary>How strongly an off-centre, fast pull tilts the body to hang from the grab
    /// point (radians of target lean per unit of lever×pull).</summary>
    public const float DragTiltScale = 0.0009f;

    /// <summary>Hard cap on the hang tilt while dragging (radians).</summary>
    public const float DragMaxTilt = 1.2f;

    /// <summary>How fast the body eases toward its hang-tilt target (per second).</summary>
    public const float DragTiltStiffness = 9f;

    /// <summary>Hard cap on spin speed (rad/s) so it can't blur out.</summary>
    public const float DragMaxAngularVel = 18f;

    /// <summary>Spin settle after release (per second) — never an instant snap.</summary>
    public const float AngularReleaseDamping = 1.7f;

    /// <summary>Once grounded &amp; slow, how firmly the body rights itself to upright.</summary>
    public const float AngularRestStiffness = 9f;

    /// <summary>|AngularVelocity| (rad/s) under which a grounded body starts righting up.</summary>
    public const float AngularRestThreshold = 4.5f;

    /// <summary>How much of the body's stretch maps to the grab-direction squash (0..~).</summary>
    public const float DragSquashScale = 0.0016f;

    // Dizziness: a 0..1 meter that accumulates from spins and hard impacts and slowly
    // recovers. Tiny impacts add almost nothing; heavy ones add a lot (soft curve).
    public const float DizzyRecoveryPerSecond = 0.22f;
    public const float DizzyTriggerThreshold = 0.85f;   // crossing this triggers the dizzy reaction
    public const float DizzySpinPerSecond = 0.20f;      // gained per second of fast spinning
    public const float DizzyImpactHeavyBoost = 0.55f;   // a heavy hit's contribution at the soft cap

    /// <summary>Spin speed (rad/s) that turns the eyes into helicopter spirals.</summary>
    public const float HelicopterAngularVel = 15f;

    /// <summary>Chance the crab playfully resists when dragged into a screen edge.</summary>
    public const float EdgeResistChance = 0.5f;

    // Collision speed bands (px/s) that select the reaction (blink → pancake).
    public const float ImpactTinySpeed = 260f;
    public const float ImpactMediumSpeed = 620f;
    public const float ImpactHeavySpeed = 1200f;
    public const float ImpactPancakeSpeed = 1900f;

    /// <summary>Minimum time (s) between collision reactions, so a fast bounce that re-hits
    /// the same wall several frames running fires ONE reaction, not a particle storm.</summary>
    public const float ImpactReactionCooldown = 0.25f;

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

    // ---- "Give a paw" mini-game -----------------------------------------

    /// <summary>How close (logical px) the cursor must rest for the "give a paw" coax.</summary>
    public const float GivePawRadius = 120f;

    /// <summary>Cursor speed (px/s) under which it counts as "held still" beside the mascot.</summary>
    public const float GivePawMaxCursorSpeed = 60f;

    /// <summary>Seconds the cursor must rest still beside it before it offers a paw.</summary>
    public const float GivePawHoldSeconds = 0.9f;

    /// <summary>Debounce (s) so the paw offer doesn't repeat back-to-back.</summary>
    public const float GivePawCooldown = 6f;

    // ---- "Keep-up" juggling mini-game -----------------------------------

    /// <summary>Min release speed (px/s) for letting go to start a juggling round. Low on
    /// purpose: the game is "drop it and re-catch it before it lands", so even a gentle release
    /// while it's up in the air counts — it just can't be a near-zero set-down on the floor.</summary>
    public const float KeepUpMinThrowSpeed = 60f;

    /// <summary>Seconds the combo number stays up after the last catch before fading.</summary>
    public const float KeepUpComboHoldSeconds = 2.2f;

    // ---- Real-desktop reactions (active window + volume) -----------------

    /// <summary>Debounce (s) for the active-window-switch glance so app-flipping isn't spammy.</summary>
    public const float WindowReactCooldown = 14f;

    /// <summary>How often (s) we read the system master volume (a cheap COM call, but not per-frame).</summary>
    public const float VolumePollSeconds = 0.4f;

    /// <summary>Minimum master-volume change (0..1) that counts as "you changed it".</summary>
    public const float VolumeReactDelta = 0.05f;

    /// <summary>Debounce (s) so a volume slide doesn't fire a reaction on every step.</summary>
    public const float VolumeReactCooldown = 4f;

    // ---- Ambient chatter -------------------------------------------------

    /// <summary>Cursor speed (px/s) below which the user counts as idle (no movement).</summary>
    public const float IdleCursorSpeed = 6f;

    /// <summary>Seconds of user inactivity before Claw'd starts wondering where you went.</summary>
    public const float IdleChatterSeconds = 35f;

    /// <summary>Shortest / longest gap (s) between ambient chatter bubbles.</summary>
    public const float ChatterMinGap = 26f;
    public const float ChatterMaxGap = 52f;

    /// <summary>Shortest / longest gap (s) between spontaneous behaviour "stories".</summary>
    public const float ChainMinGap = 75f;
    public const float ChainMaxGap = 160f;

    /// <summary>Shortest / longest gap (s) between rare "special moments" (~20–75 min).</summary>
    public const float RareMinGap = 1200f;
    public const float RareMaxGap = 4500f;

    /// <summary>Shortest / longest gap (s) between real-desktop interactions (e.g. the clock).</summary>
    public const float DesktopMinGap = 150f;
    public const float DesktopMaxGap = 320f;
}
