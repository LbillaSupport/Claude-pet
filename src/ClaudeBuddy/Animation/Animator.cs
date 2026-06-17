using ClaudeBuddy.Core;
using ClaudeBuddy.Emotions;
using ClaudeBuddy.Engine;

namespace ClaudeBuddy.Animation;

/// <summary>
/// Turns the mascot's discrete <see cref="AnimationState"/> plus its mood into a
/// continuous, blended <see cref="Pose"/>. This is the project's "animation engine":
/// instead of pre-baked sprite frames it synthesises poses every frame and eases the
/// current pose toward the target, giving smooth, never-robotic transitions, an
/// always-on breathing idle, and automatic blinking.
/// </summary>
public sealed class Animator
{
    private readonly Rng _rng;
    private readonly Pose _current = new();
    private readonly Pose _target = new();

    private AnimationState _lastState = AnimationState.Idle;
    private float _phase;       // continuous cyclic clock (scaled by anim speed)
    private float _stateTime;   // seconds since the current state began
    private float _blinkTimer;
    private float _blinkProgress = 1f; // 1 = fully open
    private bool _blinking;
    private float _typingIntensity; // 0..1, how fast the user is currently typing

    // Springy squash/stretch — gives landings and pose changes a little organic
    // overshoot instead of an easing-only "glide" that can feel mechanical.
    private Spring _scaleX = new(1f);
    private Spring _scaleY = new(1f);

    public Animator(Rng rng)
    {
        _rng = rng;
        _blinkTimer = rng.Range(EngineConstants.BlinkMinInterval, EngineConstants.BlinkMaxInterval);
    }

    /// <summary>The blended pose to draw this frame.</summary>
    public Pose Current => _current;

    /// <summary>
    /// Feeds the current typing rate (0 = idle, 1 = furious) so the <c>TypeAlong</c>
    /// pose can tap faster and bounce harder the quicker the user types.
    /// </summary>
    public void SetTypingIntensity(float intensity) => _typingIntensity = MathUtil.Clamp01(intensity);

    /// <summary>
    /// Advances the animator. <paramref name="lookX"/>/<paramref name="lookY"/> are the
    /// desired pupil offset (-1..1) toward the cursor; states that allow free look use
    /// them so the eyes track the mouse.
    /// </summary>
    public void Update(Mascot mascot, EmotionState emotion, float dt, float lookX, float lookY)
    {
        float speed = MathF.Max(0.05f, mascot.AnimationSpeed);
        _phase += dt * speed;

        if (mascot.Animation != _lastState)
        {
            _lastState = mascot.Animation;
            _stateTime = 0f;
        }

        _stateTime += dt * speed;

        BuildTarget(mascot.Animation, emotion, lookX, lookY);
        ApplyMoodFace(mascot.Animation, emotion);
        ApplyBlink(dt, mascot.Animation);
        ApplyIdleLife(mascot.Animation);

        Blend(dt);
    }

    /// <summary>Resets the blend so a hard cut (e.g. Photo Mode pose) snaps instantly.</summary>
    public void Snap()
    {
        _current.CopyFrom(_target);
        _scaleX = new Spring(_target.BodyScaleX);
        _scaleY = new Spring(_target.BodyScaleY);
    }

    // -- Target synthesis ---------------------------------------------------

    private void BuildTarget(AnimationState state, EmotionState emotion, float lookX, float lookY)
    {
        Pose t = _target;

        // Neutral resting pose every frame; states override only what they need.
        t.BodyOffset = Vector2.Zero;
        t.BodyLean = 0f;
        t.WholeBodyRotation = 0f;
        t.BodyScaleX = 1f;
        t.BodyScaleY = 1f;
        t.HeadTilt = 0f;
        t.EyeOpen = 1f;
        t.EyeLookX = lookX * 0.6f;
        t.EyeLookY = lookY * 0.5f;
        t.MouthOpen = 0.05f;
        t.MouthCurve = 0.25f;
        t.BrowAngle = 0f;
        t.Blush = 0f;
        t.ArmLeft = 0f;
        t.ArmRight = 0f;
        t.StrideAmount = 0f;
        t.LegPhase = 0f;
        t.HappyEyes = 0f;
        t.StarEyes = 0f;
        t.Alpha = 1f;
        t.CoffeeProp = 0f;
        t.UmbrellaProp = 0f;
        t.BookProp = 0f;
        t.ThinkBubble = 0f;
        t.SleepBubble = 0f;

        float p = _phase;

        switch (state)
        {
            case AnimationState.Idle:
            case AnimationState.Stand:
                t.MouthCurve = 0.3f;
                break;

            case AnimationState.WalkLeft:
            case AnimationState.WalkRight:
                // Stride & lean set here; the cyclic leg/arm swing is layered on in
                // FillLegCycle() (called below) where the phase clock is in scope.
                t.StrideAmount = 1f;
                t.BodyLean = 0.07f;
                t.MouthCurve = 0.35f;
                break;

            case AnimationState.RunLeft:
            case AnimationState.RunRight:
                t.StrideAmount = 1.4f;
                t.BodyLean = 0.22f;
                t.MouthOpen = 0.2f;
                t.MouthCurve = 0.4f;
                break;

            case AnimationState.Jump:
                t.BodyScaleY = 1.16f;
                t.BodyScaleX = 0.9f;
                t.ArmLeft = -0.8f;
                t.ArmRight = -0.8f;
                t.MouthOpen = 0.4f;
                t.EyeOpen = 1.15f;
                t.BrowAngle = 0.5f;
                break;

            case AnimationState.Fall:
                t.BodyScaleY = 1.1f;
                t.ArmLeft = -1.4f;
                t.ArmRight = -1.4f;
                t.MouthOpen = 0.55f;
                t.EyeOpen = 1.2f;
                t.BrowAngle = 0.7f;
                break;

            case AnimationState.Land:
                t.BodyScaleY = 0.78f;
                t.BodyScaleX = 1.2f;
                t.MouthCurve = 0.4f;
                break;

            case AnimationState.Sit:
                t.BodyOffset = new Vector2(0f, 10f);
                t.BodyScaleY = 0.9f;
                t.BodyScaleX = 1.06f;
                t.MouthCurve = 0.3f;
                break;

            case AnimationState.Sleep:
                t.BodyOffset = new Vector2(0f, 12f);
                t.BodyScaleY = 0.86f;
                t.BodyScaleX = 1.1f;
                t.HeadTilt = 0.28f;
                t.EyeOpen = 0f;
                t.MouthOpen = 0.12f + (0.06f * MathF.Sin(p * 1.6f));
                t.MouthCurve = 0.15f;
                t.SleepBubble = 1f;
                break;

            case AnimationState.WakeUp:
                t.EyeOpen = MathUtil.Clamp01(_stateTime * 1.2f);
                t.ArmLeft = -1.2f * (1f - MathUtil.Clamp01(_stateTime));
                t.ArmRight = -1.2f * (1f - MathUtil.Clamp01(_stateTime));
                t.MouthOpen = 0.3f;
                break;

            case AnimationState.Blink:
                t.EyeOpen = 0f;
                break;

            case AnimationState.Wave:
                t.ArmRight = 2.1f + (0.28f * MathF.Sin(_stateTime * 13f));
                t.HeadTilt = 0.1f;
                t.MouthCurve = 0.8f;
                t.HappyEyes = 0.7f;
                break;

            case AnimationState.Dance:
                t.BodyOffset = new Vector2(MathF.Sin(p * 6f) * 7f, -MathF.Abs(MathF.Sin(p * 12f)) * 5f);
                t.BodyLean = MathF.Sin(p * 6f) * 0.18f;
                t.ArmLeft = 1.6f + (MathF.Sin(p * 12f) * 0.5f);
                t.ArmRight = 1.6f - (MathF.Sin(p * 12f) * 0.5f);
                t.HeadTilt = MathF.Sin(p * 6f) * 0.15f;
                t.MouthOpen = 0.3f;
                t.MouthCurve = 0.7f;
                t.HappyEyes = 1f;
                break;

            case AnimationState.Celebrate:
                t.BodyOffset = new Vector2(0f, -MathF.Abs(MathF.Sin(p * 10f)) * 8f);
                t.ArmLeft = 2.4f;
                t.ArmRight = 2.4f;
                t.MouthOpen = 0.6f;
                t.MouthCurve = 1f;
                t.StarEyes = 1f;
                t.BrowAngle = 0.4f;
                break;

            case AnimationState.Think:
            {
                // Bubble eases in, the chin "finger" taps, and the gaze drifts around
                // as if chasing an idea — never a frozen hold.
                float appear = Easing.OutCubic(MathUtil.Clamp01(_stateTime / 0.5f));
                t.ArmRight = 1.5f + (0.1f * MathF.Sin(_stateTime * 6f));
                t.HeadTilt = -0.1f + (0.06f * MathF.Sin(_stateTime * 0.8f));
                t.EyeLookX = 0.35f + (0.35f * MathF.Sin(_stateTime * 0.7f));
                t.EyeLookY = -0.55f + (0.1f * MathF.Sin(_stateTime * 1.3f));
                t.MouthCurve = 0.05f;
                t.BrowAngle = 0.2f;
                t.ThinkBubble = appear;
                break;
            }

            case AnimationState.Read:
            {
                // Eyes scan left-right across the page, the head bobs gently, and a
                // little page-turn flick happens every few seconds.
                float appear = Easing.OutCubic(MathUtil.Clamp01(_stateTime / 0.6f));
                float pageCycle = _stateTime % 4f;
                float turn = pageCycle < 0.35f ? Easing.OutQuad(pageCycle / 0.35f) * (1f - (pageCycle / 0.35f)) * 4f : 0f;
                t.BodyOffset = new Vector2(0f, 8f);
                t.BodyScaleY = 0.94f;
                t.EyeLookX = MathF.Sin(_stateTime * 1.6f) * 0.5f;
                t.EyeLookY = 0.7f;
                t.HeadTilt = 0.05f + (0.03f * MathF.Sin(_stateTime * 0.5f));
                t.ArmLeft = 0.9f * appear;
                t.ArmRight = (0.9f + (0.5f * turn)) * appear;
                t.MouthCurve = 0.2f;
                t.BookProp = appear;
                break;
            }

            case AnimationState.Drink:
            {
                // Reach the cup up, then take slow repeated sips: head dips, eyes
                // half-close and the mouth opens in rhythm.
                float reach = Easing.OutCubic(MathUtil.Clamp01(_stateTime / 0.7f));
                float sip = 0.5f + (0.5f * MathF.Sin(_stateTime * 2.2f));
                t.ArmRight = (1.3f + (0.3f * sip)) * reach;
                t.HeadTilt = (-0.04f - (0.06f * sip)) * reach;
                t.MouthOpen = 0.05f + (0.13f * sip * reach);
                t.EyeOpen = 1f - (0.3f * sip * reach);
                t.EyeLookY = 0.2f * reach;
                t.MouthCurve = 0.3f;
                t.CoffeeProp = reach;
                break;
            }

            case AnimationState.Stretch:
                float s = Easing.InOutSine(MathUtil.PingPong(_stateTime, 1.2f) / 1.2f);
                t.ArmLeft = 2.6f * s;
                t.ArmRight = 2.6f * s;
                t.BodyScaleY = 1f + (0.12f * s);
                t.BodyScaleX = 1f - (0.06f * s);
                t.EyeOpen = 1f - (0.7f * s);
                t.MouthOpen = 0.3f * s;
                break;

            case AnimationState.Yawn:
                float y = Easing.InOutSine(MathUtil.PingPong(_stateTime, 1.1f) / 1.1f);
                t.MouthOpen = 0.2f + (0.6f * y);
                t.EyeOpen = 1f - (0.85f * y);
                t.HeadTilt = 0.12f * y;
                t.ArmLeft = -0.9f * y;
                t.SleepBubble = y > 0.5f ? 1f : 0f;
                break;

            case AnimationState.Spin:
                t.WholeBodyRotation = _stateTime * 14f;
                t.MouthOpen = 0.3f;
                t.MouthCurve = 0.6f;
                t.HappyEyes = 0.6f;
                break;

            case AnimationState.Roll:
                t.WholeBodyRotation = _phase * 10f;
                t.BodyScaleX = 1.05f;
                t.EyeOpen = 0.6f;
                break;

            case AnimationState.Trip:
                t.WholeBodyRotation = MathF.Sin(_stateTime * 18f) * 0.5f * MathF.Max(0f, 1f - _stateTime);
                t.ArmLeft = -1.6f;
                t.ArmRight = -1.2f;
                t.MouthOpen = 0.5f;
                t.EyeOpen = 1.2f;
                t.BrowAngle = 0.8f;
                break;

            case AnimationState.Pet:
                t.EyeOpen = 0.1f;
                t.HappyEyes = 1f;
                t.MouthCurve = 0.85f;
                t.MouthOpen = 0.15f;
                t.Blush = 1f;
                t.HeadTilt = MathF.Sin(p * 5f) * 0.08f;
                t.BodyOffset = new Vector2(0f, MathF.Sin(p * 5f) * 2f);
                break;

            case AnimationState.Dragged:
                t.ArmLeft = 2.3f;
                t.ArmRight = 2.3f;
                t.StrideAmount = 0.6f;
                t.LegPhase = MathF.Sin(p * 4f);
                t.MouthOpen = 0.3f;
                t.EyeOpen = 1.1f;
                t.BrowAngle = 0.3f;
                t.EyeLookY = -0.3f;
                break;

            case AnimationState.LookUp:
                t.EyeLookY = -1f;
                t.HeadTilt = -0.05f;
                break;

            case AnimationState.LookDown:
                t.EyeLookY = 1f;
                t.HeadTilt = 0.05f;
                break;

            case AnimationState.LookAround:
                t.EyeLookX = MathF.Sin(p * 1.6f);
                t.HeadTilt = MathF.Sin(p * 1.6f) * 0.12f;
                t.BrowAngle = 0.15f;
                break;

            case AnimationState.Surprised:
                t.EyeOpen = 1.4f;
                t.BrowAngle = 1f;
                t.MouthOpen = 0.6f;
                t.BodyScaleY = 1.08f;
                t.BodyOffset = new Vector2(0f, -4f);
                break;

            case AnimationState.Scared:
                t.EyeOpen = 1.3f;
                t.BrowAngle = -0.6f;
                t.MouthCurve = -0.4f;
                t.MouthOpen = 0.3f;
                t.BodyScaleX = 0.9f;
                t.BodyScaleY = 0.92f;
                t.BodyOffset = new Vector2(MathF.Sin(p * 40f) * 1.5f, 0f); // shiver
                break;

            case AnimationState.Happy:
                t.BodyOffset = new Vector2(0f, -MathF.Abs(MathF.Sin(p * 9f)) * 6f);
                t.MouthCurve = 1f;
                t.MouthOpen = 0.3f;
                t.HappyEyes = 1f;
                break;

            case AnimationState.Sad:
                t.BodyOffset = new Vector2(0f, 6f);
                t.HeadTilt = 0.18f;
                t.EyeLookY = 0.6f;
                t.MouthCurve = -0.7f;
                t.BrowAngle = -0.5f;
                t.BodyScaleY = 0.95f;
                break;

            case AnimationState.TypeAlong:
            {
                // "Playing piano": the four legs rise and fall in a slow, pronounced
                // rolling sequence (each is a quarter-cycle behind the last). Faster
                // typing only nudges the tempo so the keystrokes stay clearly readable.
                float keys = _stateTime * (5f + (3.5f * _typingIntensity));
                t.LegPhase = keys;
                t.StrideAmount = 1.5f + (0.5f * _typingIntensity); // big, notorious lift
                t.BodyOffset = new Vector2(0f, 2f - (MathF.Abs(MathF.Sin(keys * 0.5f)) * 1.5f));
                t.BodyScaleY = 0.97f;
                t.BodyLean = 0.05f;
                t.HeadTilt = 0.05f + (MathF.Sin(keys * 0.5f) * 0.05f); // slow head sway in time
                t.EyeOpen = 0.95f;
                t.EyeLookY = 0.35f;          // peeking down at the keys
                t.MouthCurve = 0.5f;
                t.MouthOpen = 0.04f + (0.08f * _typingIntensity);
                t.BrowAngle = 0.3f;
                break;
            }

            case AnimationState.Charge:
            {
                const float buildTime = 0.7f;
                if (_stateTime < buildTime)
                {
                    // Anticipation: crouch low and vibrate, eyes turning to stars.
                    float build = MathUtil.Clamp01(_stateTime / buildTime);
                    t.BodyScaleY = 1f - (0.2f * build);
                    t.BodyScaleX = 1f + (0.16f * build);
                    t.BodyOffset = new Vector2(MathF.Sin(_stateTime * 42f) * 2.2f * build, 4f * build);
                    t.BrowAngle = 0.6f;
                    t.MouthOpen = 0.25f * build;
                    t.StarEyes = build;
                    t.EyeOpen = 1.1f;
                }
                else
                {
                    // Release: spring up tall with a huge grin and star eyes.
                    float rel = MathUtil.Clamp01((_stateTime - buildTime) / 0.4f);
                    t.BodyScaleY = 1f + (0.3f * (1f - rel));
                    t.BodyScaleX = 1f - (0.18f * (1f - rel));
                    t.BodyOffset = new Vector2(0f, -10f * (1f - rel));
                    t.ArmLeft = 2.5f;
                    t.ArmRight = 2.5f;
                    t.MouthOpen = 0.6f;
                    t.MouthCurve = 1f;
                    t.StarEyes = 1f;
                }

                break;
            }

            case AnimationState.Climb:
            {
                // Quick scrabbling legs + a determined little face. The whole body is
                // rotated onto the wall/ceiling by the renderer; here it just climbs.
                float scrabble = _stateTime * 12f;
                t.LegPhase = scrabble;
                t.StrideAmount = 1.1f;
                t.BodyOffset = new Vector2(MathF.Sin(scrabble * 2f) * 1.4f, 0f);
                t.HeadTilt = 0.05f;
                t.EyeOpen = 1.05f;
                t.BrowAngle = 0.35f;
                t.MouthOpen = 0.12f;
                t.MouthCurve = 0.2f;
                break;
            }

            case AnimationState.Groom:
            {
                // Preening: a few unhurried head-tilts left/right with a tidy little
                // raise of a "hand" (outer leg) as if smoothing itself / adjusting a hat.
                float beat = _stateTime * 1.6f;
                t.HeadTilt = MathF.Sin(beat) * 0.16f;
                t.BodyLean = MathF.Sin(beat) * 0.05f;
                t.BodyOffset = new Vector2(0f, MathF.Abs(MathF.Sin(beat * 2f)) * -1.5f);
                // Alternate which side reaches up so it preens both sides.
                bool rightTurn = MathF.Sin(beat) >= 0f;
                t.ArmRight = rightTurn ? 1.9f + (0.25f * MathF.Sin(_stateTime * 9f)) : 0f;
                t.ArmLeft = rightTurn ? 0f : 1.9f + (0.25f * MathF.Sin(_stateTime * 9f));
                t.EyeOpen = 0.9f;
                t.MouthCurve = 0.4f;
                t.Blush = 0.15f;
                break;
            }

            case AnimationState.TapFoot:
            {
                // Standing about, one foot tapping a steady beat — gentle impatience.
                // Only the inner-right leg taps; the rest stay planted (StrideAmount 0).
                float tap = MathF.Max(0f, MathF.Sin(_stateTime * 9f));
                t.LegPhase = MathUtil.HalfPi;       // freeze the rolling walk cycle
                t.StrideAmount = 0f;
                t.ArmRight = 0f;
                // Drive the tap through a tiny vertical body bob synced to the beat.
                t.BodyOffset = new Vector2(0f, tap * -1.2f);
                t.HeadTilt = MathF.Sin(_stateTime * 1.2f) * 0.08f;
                t.EyeLookX = MathF.Sin(_stateTime * 0.9f) * 0.4f;
                t.EyeOpen = 1f;
                t.MouthCurve = 0.25f;
                break;
            }

            case AnimationState.Wiggle:
            {
                // A small, happy side-to-side sway in place — a "mini dance" that's
                // calmer than the full Dance, good as a frequent idle.
                float w = _stateTime * 5f;
                t.BodyOffset = new Vector2(MathF.Sin(w) * 4f, MathF.Abs(MathF.Sin(w * 2f)) * -2f);
                t.BodyLean = MathF.Sin(w) * 0.12f;
                t.HeadTilt = MathF.Sin(w) * 0.12f;
                t.MouthCurve = 0.7f;
                t.HappyEyes = 0.6f;
                t.Blush = 0.1f;
                break;
            }
        }
    }

    private void ApplyMoodFace(AnimationState state, EmotionState emotion)
    {
        // Re-derive the leg cycle here where we have access to _phase, and layer the
        // mood onto faces that are otherwise neutral.
        if (state is AnimationState.WalkLeft or AnimationState.WalkRight)
        {
            FillLegCycle(9f, 0.5f, 3.2f);
        }
        else if (state is AnimationState.RunLeft or AnimationState.RunRight)
        {
            FillLegCycle(15f, 0.8f, 5.5f);
        }

        bool neutralFace = state is AnimationState.Idle or AnimationState.Stand
            or AnimationState.WalkLeft or AnimationState.WalkRight
            or AnimationState.LookAround or AnimationState.LookUp or AnimationState.LookDown;

        if (!neutralFace)
        {
            return;
        }

        switch (emotion.Mood)
        {
            case Mood.Happy:
                _target.MouthCurve = MathF.Max(_target.MouthCurve, 0.7f);
                _target.HappyEyes = MathF.Max(_target.HappyEyes, 0.4f);
                break;
            case Mood.Excited:
                _target.MouthCurve = MathF.Max(_target.MouthCurve, 0.8f);
                _target.MouthOpen = MathF.Max(_target.MouthOpen, 0.25f);
                _target.BrowAngle = 0.4f;
                break;
            case Mood.Playful:
                _target.MouthCurve = MathF.Max(_target.MouthCurve, 0.6f);
                _target.Blush = MathF.Max(_target.Blush, 0.3f);
                break;
            case Mood.Curious:
                _target.BrowAngle = 0.5f;
                _target.HeadTilt += 0.1f;
                break;
            case Mood.Sleepy:
                _target.EyeOpen = MathF.Min(_target.EyeOpen, 0.55f);
                _target.MouthCurve = 0.1f;
                break;
            case Mood.Lazy:
                _target.EyeOpen = MathF.Min(_target.EyeOpen, 0.7f);
                break;
            case Mood.Proud:
                _target.MouthCurve = MathF.Max(_target.MouthCurve, 0.5f);
                _target.BrowAngle = 0.3f;
                _target.HeadTilt = -0.06f;
                break;
            case Mood.Confused:
                _target.BrowAngle = -0.2f;
                _target.HeadTilt += 0.16f;
                _target.MouthCurve = -0.1f;
                break;
            case Mood.Sad:
                _target.MouthCurve = MathF.Min(_target.MouthCurve, -0.3f);
                _target.BrowAngle = -0.4f;
                break;
            case Mood.Scared:
                _target.BrowAngle = -0.5f;
                _target.EyeOpen = 1.2f;
                break;
        }

        if (emotion.RareContentUnlocked)
        {
            // High-affection sparkle in the eyes during calm moments.
            _target.StarEyes = MathF.Max(_target.StarEyes, 0.25f);
        }
    }

    private void FillLegCycle(float frequency, float armSwing, float bob)
    {
        float lp = _phase * frequency;
        _target.LegPhase = lp;
        _target.BodyOffset = _target.BodyOffset.WithY(-MathF.Abs(MathF.Sin(lp)) * bob);
        _target.ArmLeft = MathF.Sin(lp) * armSwing;
        _target.ArmRight = -MathF.Sin(lp) * armSwing;
    }

    private void ApplyBlink(float dt, AnimationState state)
    {
        // Don't override states that deliberately close or widen the eyes.
        bool eyesScripted = state is AnimationState.Sleep or AnimationState.Blink
            or AnimationState.Yawn or AnimationState.Stretch or AnimationState.WakeUp
            or AnimationState.Pet or AnimationState.Surprised or AnimationState.Scared;

        _blinkTimer -= dt;
        if (!_blinking && _blinkTimer <= 0f)
        {
            _blinking = true;
            _blinkProgress = 0f;
        }

        if (_blinking)
        {
            // A quick down-up: 0 -> closed -> open across ~0.16s.
            _blinkProgress += dt / 0.16f;
            if (_blinkProgress >= 1f)
            {
                _blinking = false;
                _blinkProgress = 1f;
                _blinkTimer = _rng.Range(EngineConstants.BlinkMinInterval, EngineConstants.BlinkMaxInterval);
            }

            if (!eyesScripted)
            {
                float closed = 1f - MathF.Abs((_blinkProgress * 2f) - 1f); // 0..1..0
                _target.EyeOpen = MathUtil.Lerp(_target.EyeOpen, 0f, closed);
            }
        }
    }

    private void ApplyIdleLife(AnimationState state)
    {
        // Layered onto otherwise-calm states so the mascot is *always* subtly moving:
        // breathing, a slow weight shift, a tiny head sway and a wandering glance.
        bool calm = state is AnimationState.Idle or AnimationState.Stand or AnimationState.Sit
            or AnimationState.LookAround or AnimationState.LookUp or AnimationState.LookDown;

        if (!calm)
        {
            return;
        }

        float breath = MathF.Sin(_phase * 1.7f);
        _target.BodyScaleY *= 1f + (breath * 0.02f);
        _target.BodyScaleX *= 1f - (breath * 0.013f);
        _target.BodyOffset = _target.BodyOffset.WithY(_target.BodyOffset.Y + (breath * 1.5f));

        // Slow weight shift from foot to foot + a matching micro head tilt.
        float sway = MathF.Sin(_phase * 0.6f);
        _target.BodyOffset = _target.BodyOffset.WithX(_target.BodyOffset.X + (sway * 1.8f));
        _target.HeadTilt += sway * 0.03f;

        // An occasional idle glance so the eyes never lock dead-ahead.
        _target.EyeLookX += MathF.Sin(_phase * 0.33f) * 0.12f;
    }

    private void Blend(float dt)
    {
        Pose c = _current;
        Pose t = _target;

        c.BodyOffset = MathUtil.Damp(c.BodyOffset, t.BodyOffset, 15f, dt);
        c.BodyLean = MathUtil.Damp(c.BodyLean, t.BodyLean, 12f, dt);
        c.WholeBodyRotation = t.WholeBodyRotation; // rotations are driven directly

        // Squash & stretch springs slightly past the target then settle — the bit of
        // overshoot reads as soft, weighty, organic motion (Apple/Pixar feel).
        _scaleX.Step(t.BodyScaleX, 240f, 22f, dt);
        _scaleY.Step(t.BodyScaleY, 240f, 22f, dt);
        c.BodyScaleX = _scaleX.Value;
        c.BodyScaleY = _scaleY.Value;

        c.HeadTilt = MathUtil.Damp(c.HeadTilt, t.HeadTilt, 12f, dt);
        c.EyeOpen = MathUtil.Damp(c.EyeOpen, t.EyeOpen, 26f, dt);
        c.EyeLookX = MathUtil.Damp(c.EyeLookX, t.EyeLookX, 11f, dt);
        c.EyeLookY = MathUtil.Damp(c.EyeLookY, t.EyeLookY, 11f, dt);
        c.MouthOpen = MathUtil.Damp(c.MouthOpen, t.MouthOpen, 17f, dt);
        c.MouthCurve = MathUtil.Damp(c.MouthCurve, t.MouthCurve, 10f, dt);
        c.BrowAngle = MathUtil.Damp(c.BrowAngle, t.BrowAngle, 11f, dt);
        c.Blush = MathUtil.Damp(c.Blush, t.Blush, 10f, dt);
        c.ArmLeft = MathUtil.Damp(c.ArmLeft, t.ArmLeft, 15f, dt);
        c.ArmRight = MathUtil.Damp(c.ArmRight, t.ArmRight, 15f, dt);
        c.LegPhase = t.LegPhase; // cyclic, set directly
        c.StrideAmount = MathUtil.Damp(c.StrideAmount, t.StrideAmount, 13f, dt);
        c.HappyEyes = MathUtil.Damp(c.HappyEyes, t.HappyEyes, 16f, dt);
        c.StarEyes = MathUtil.Damp(c.StarEyes, t.StarEyes, 13f, dt);
        c.Alpha = MathUtil.Damp(c.Alpha, t.Alpha, 10f, dt);
        c.CoffeeProp = MathUtil.Damp(c.CoffeeProp, t.CoffeeProp, 9f, dt);
        c.UmbrellaProp = MathUtil.Damp(c.UmbrellaProp, t.UmbrellaProp, 9f, dt);
        c.BookProp = MathUtil.Damp(c.BookProp, t.BookProp, 9f, dt);
        c.ThinkBubble = MathUtil.Damp(c.ThinkBubble, t.ThinkBubble, 9f, dt);
        c.SleepBubble = MathUtil.Damp(c.SleepBubble, t.SleepBubble, 8f, dt);
    }
}
