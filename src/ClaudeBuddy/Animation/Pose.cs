using ClaudeBuddy.Core;

namespace ClaudeBuddy.Animation;

/// <summary>
/// The complete procedural description of how to draw the character on a given
/// frame. Every value is normalised and relative, so the same pose works at any
/// scale or skin. The <see cref="Rendering.CharacterArtist"/> reads this; the
/// <see cref="Animator"/> writes it (and smoothly blends between targets).
/// </summary>
public sealed class Pose
{
    /// <summary>Whole-body bob/sway offset, in logical px.</summary>
    public Vector2 BodyOffset;

    /// <summary>Body lean in radians (positive = leaning toward facing direction).</summary>
    public float BodyLean;

    /// <summary>Whole-character Z rotation in radians (spin, roll, trip).</summary>
    public float WholeBodyRotation;

    /// <summary>Procedural squash applied on top of physics squash (1 = neutral).</summary>
    public float BodyScaleX = 1f;
    public float BodyScaleY = 1f;

    /// <summary>Head tilt in radians.</summary>
    public float HeadTilt;

    /// <summary>Eyelid openness, 0 = closed (blink), 1 = wide.</summary>
    public float EyeOpen = 1f;

    /// <summary>Pupil look direction, each component -1..1.</summary>
    public float EyeLookX;
    public float EyeLookY;

    /// <summary>0 = closed mouth, 1 = wide open.</summary>
    public float MouthOpen;

    /// <summary>Mouth shape, -1 = frown, 0 = neutral, 1 = big smile.</summary>
    public float MouthCurve;

    /// <summary>Brow mood, -1 = worried/angry, 0 = relaxed, 1 = raised/surprised.</summary>
    public float BrowAngle;

    /// <summary>Blush opacity 0..1.</summary>
    public float Blush;

    /// <summary>Arm raise angles in radians (0 = resting at side).</summary>
    public float ArmLeft;
    public float ArmRight;

    /// <summary>Walk-cycle phase for the legs (radians).</summary>
    public float LegPhase;

    /// <summary>How far the legs actually stride (0 = standing still).</summary>
    public float StrideAmount;

    /// <summary>Eyes drawn as happy upward arcs (^_^) when near 1.</summary>
    public float HappyEyes;

    /// <summary>Eyes drawn as sparkly stars when near 1 (excited / rare reaction).</summary>
    public float StarEyes;

    /// <summary>Whole-character opacity for fades.</summary>
    public float Alpha = 1f;

    // ---- Props -----------------------------------------------------------
    public float CoffeeProp;     // 0..1 visibility of the tiny coffee cup
    public float UmbrellaProp;   // 0..1 visibility of the umbrella
    public float BookProp;       // 0..1 visibility of the book
    public float ThinkBubble;    // 0..1 thought bubble
    public float SleepBubble;    // 0..1 Zzz bubble near head

    /// <summary>Copies another pose's values into this instance (no allocation).</summary>
    public void CopyFrom(Pose other)
    {
        BodyOffset = other.BodyOffset;
        BodyLean = other.BodyLean;
        WholeBodyRotation = other.WholeBodyRotation;
        BodyScaleX = other.BodyScaleX;
        BodyScaleY = other.BodyScaleY;
        HeadTilt = other.HeadTilt;
        EyeOpen = other.EyeOpen;
        EyeLookX = other.EyeLookX;
        EyeLookY = other.EyeLookY;
        MouthOpen = other.MouthOpen;
        MouthCurve = other.MouthCurve;
        BrowAngle = other.BrowAngle;
        Blush = other.Blush;
        ArmLeft = other.ArmLeft;
        ArmRight = other.ArmRight;
        LegPhase = other.LegPhase;
        StrideAmount = other.StrideAmount;
        HappyEyes = other.HappyEyes;
        StarEyes = other.StarEyes;
        Alpha = other.Alpha;
        CoffeeProp = other.CoffeeProp;
        UmbrellaProp = other.UmbrellaProp;
        BookProp = other.BookProp;
        ThinkBubble = other.ThinkBubble;
        SleepBubble = other.SleepBubble;
    }
}
