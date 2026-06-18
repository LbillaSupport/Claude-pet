using ClaudeBuddy.Animation;
using ClaudeBuddy.Core;
using ClaudeBuddy.Skins;
using SkiaSharp;

namespace ClaudeBuddy.Rendering;

/// <summary>
/// Paints Claude Buddy entirely from vector primitives — no sprite sheets. Given a
/// <see cref="Pose"/> and a <see cref="SkinPalette"/>, it draws a soft, rounded, very
/// expressive little creature: squashy body, swinging limbs, blinking eyes that track
/// the cursor, blush, props, and a tiny Anthropic-style sunburst badge. Because it is
/// all maths, it is crisp at any DPI and a new skin is just seven colours.
/// </summary>
public sealed class CharacterArtist
{
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly SKPaint _shadow = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Fill,
        Color = new SKColor(0, 0, 0, 70),
        MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f),
    };

    /// <summary>
    /// Draws the full character into the canvas. <paramref name="feet"/> is the canvas
    /// point where the feet rest; <paramref name="groundY"/> is the canvas Y of the
    /// floor (so the shadow stays put while the body jumps).
    /// </summary>
    public void Draw(
        SKCanvas canvas, SKPoint feet, float groundY, float height,
        Facing facing, float squashX, float squashY, Pose pose, SkinPalette palette, float dpiScale)
    {
        float h = height;
        float airHeight = MathF.Max(0f, groundY - feet.Y);

        DrawGroundShadow(canvas, feet.X, groundY, h, airHeight);

        canvas.Save();
        canvas.Translate(feet.X, feet.Y);
        canvas.Translate(pose.BodyOffset.X * dpiScale, pose.BodyOffset.Y * dpiScale);

        // Whole-body rotation pivots about the body centre (spin/roll/trip).
        float bodyCenterY = -0.46f * h;
        if (MathF.Abs(pose.WholeBodyRotation) > 1e-4f)
        {
            canvas.Translate(0, bodyCenterY);
            canvas.RotateRadians(pose.WholeBodyRotation);
            canvas.Translate(0, -bodyCenterY);
        }

        int sign = (int)facing;
        float sx = sign * squashX * pose.BodyScaleX;
        float sy = squashY * pose.BodyScaleY;
        canvas.Scale(sx, sy); // pivots at the feet, so landings squash from the floor up

        // Eye look is corrected for the horizontal mirror so the gaze always points
        // toward the real cursor regardless of which way the body faces.
        float lookX = pose.EyeLookX * sign;

        // Galgo is a whole cartoon bus, not the block-and-legs body — it draws its own
        // silhouette, wheels and face, so it short-circuits the rest of the pipeline.
        if (palette.Style == SkinStyle.Galgo)
        {
            // A rigid bus shouldn't wobble, squash, rotate or sway like a soft blob — the
            // organic pet/landing/idle motion looks "drunk" on a vehicle. So we discard the
            // whole-body transform built above and re-anchor with only a gentle vertical
            // bob. The art is hand-drawn facing left (like the sticker); only the pupils
            // track the cursor.
            canvas.Restore();                 // drop BodyOffset + rotation + squash
            canvas.Save();
            float bob = pose.BodyOffset.Y * 0.4f * dpiScale; // a soft, damped bounce only
            canvas.Translate(feet.X, feet.Y + bob);
            DrawGalgo(canvas, h, pose, palette, pose.EyeLookX);
            canvas.Restore();
            return;
        }

        // Drawn back-to-front so the body overlaps the limbs. The silhouette and face
        // depend on the skin's style (classic Claw'd, Creeper, or floaty Ghast).
        if (palette.Style == SkinStyle.Ghast)
        {
            DrawTentacles(canvas, h, pose, palette);
        }
        else
        {
            DrawLegs(canvas, h, pose, palette);
        }

        DrawBody(canvas, h, palette);

        switch (palette.Style)
        {
            case SkinStyle.Creeper:
                DrawCreeperFace(canvas, h, pose, palette, lookX);
                break;
            case SkinStyle.Ghast:
                DrawGhastFace(canvas, h, pose, palette, lookX);
                break;
            case SkinStyle.Nicolaia:
                // Suit + shirt sit on the torso below the face; curls + hat go on top.
                // The face strip above the collar is short, so the mouth is pulled up
                // (mouthDrop 0.55) to keep it off the white shirt.
                DrawNicolaiaSuit(canvas, h, palette);
                DrawFace(canvas, h, pose, palette, lookX, mouthDrop: 0.55f);
                DrawNicolaiaCrown(canvas, h, pose, palette);
                break;
            default:
                DrawFace(canvas, h, pose, palette, lookX);
                break;
        }

        DrawProps(canvas, h, pose, palette);

        canvas.Restore();
    }

    private void DrawGroundShadow(SKCanvas canvas, float x, float groundY, float h, float airHeight)
    {
        // The higher the mascot, the smaller and fainter its shadow.
        float lift = MathUtil.Clamp01(airHeight / (h * 1.1f));
        float scale = MathUtil.Lerp(1f, 0.45f, lift);
        float rx = 0.30f * h * scale;
        float ry = 0.07f * h * scale;
        _shadow.Color = new SKColor(0, 0, 0, (byte)(70 * (1f - (0.5f * lift))));
        canvas.DrawOval(new SKRect(x - rx, groundY - ry, x + rx, groundY + ry), _shadow);
    }

    // Claw'd's chunky proportions, expressed as fractions of the character height so it
    // stays crisp at any DPI. The body is a near-square with barely-rounded corners.
    private const float BodyWidth = 0.78f;
    private const float BodyTop = -0.90f;
    private const float BodyBottom = -0.12f;
    private const float CornerRadius = 0.035f;

    private void DrawBody(SKCanvas canvas, float h, SkinPalette palette)
    {
        float halfW = BodyWidth * 0.5f * h;
        var rect = new SKRect(-halfW, BodyTop * h, halfW, BodyBottom * h);
        float r = CornerRadius * h;

        // Flat terracotta block — the signature Claw'd silhouette.
        _fill.Color = palette.Body.ToSk();
        canvas.DrawRoundRect(rect, r, r, _fill);

        // A single darker band along the bottom grounds the block without breaking the
        // flat pixel feel; a faint top sheen keeps it from looking dead.
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(rect, r, r), antialias: true);
        _fill.Color = palette.BodyShadow.ToSk(0.45f);
        canvas.DrawRect(new SKRect(rect.Left, rect.Bottom - (0.12f * h), rect.Right, rect.Bottom), _fill);
        _fill.Color = new SKColor(255, 255, 255, 26);
        canvas.DrawRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + (0.10f * h)), _fill);
        canvas.Restore();
    }

    // Four stubby legs poke out below the body. Layout is symmetric so mirroring the
    // body for facing keeps it looking right.
    private static readonly float[] LegCenters = { -0.255f, -0.085f, 0.085f, 0.255f };
    private const float LegWidth = 0.12f;
    private const float LegLength = 0.13f;

    private void DrawLegs(SKCanvas canvas, float h, Pose pose, SkinPalette palette)
    {
        float top = BodyBottom * h;            // tuck the legs slightly under the body
        float legW = LegWidth * h;
        float fullLen = LegLength * h;
        float corner = legW * 0.32f;

        // A gentle four-beat shuffle while walking; outer legs raise to "wave".
        bool waveLeft = pose.ArmLeft > 1.6f;
        bool waveRight = pose.ArmRight > 1.6f;

        for (int i = 0; i < LegCenters.Length; i++)
        {
            float x = LegCenters[i] * h;
            float phase = pose.LegPhase + (i * MathUtil.HalfPi);
            float walkLift = MathF.Max(0f, MathF.Sin(phase)) * pose.StrideAmount * 0.06f * h;

            float waveLift = 0f;
            if ((waveLeft && i == 0) || (waveRight && i == LegCenters.Length - 1))
            {
                // Tuck the outer leg up against the body like a raised little hand.
                waveLift = fullLen * 0.7f;
            }

            float lift = walkLift + waveLift;
            float legTop = top - 0.001f;       // overlap the body by a hair to avoid a seam
            float legBottom = -lift;           // feet rest at y≈0 (the ground)
            var rect = new SKRect(x - (legW / 2f), legTop, x + (legW / 2f), legBottom);

            _fill.Color = palette.Body.ToSk();
            canvas.DrawRoundRect(rect, corner, corner, _fill);

            // Darker tip so the foot reads against the floor.
            _fill.Color = palette.BodyShadow.ToSk(0.55f);
            canvas.DrawRoundRect(
                new SKRect(rect.Left, rect.Bottom - (legW * 0.4f), rect.Right, rect.Bottom),
                corner, corner, _fill);
        }
    }

    // Two black square eyes, set high on the block like the original Claw'd.
    private const float EyeSize = 0.18f;
    private const float EyeOffsetX = 0.155f;
    private const float EyeCenterY = -0.62f;

    // <paramref name="mouthDrop"/> scales how far below the eyes the mouth sits. The
    // classic block has the whole lower body to work with (1.0); dressed skins like
    // Nicolaia have only a short strip of face above the collar, so they pass a smaller
    // value to keep the mouth on the face instead of on the shirt.
    private void DrawFace(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX, float mouthDrop = 1.0f)
    {
        float eyeX = EyeOffsetX * h;
        float eyeY = EyeCenterY * h;
        float s = EyeSize * h;

        // Soft blush squares appear when embarrassed or being petted.
        if (pose.Blush > 0.02f)
        {
            _fill.Color = palette.Blush.ToSk(pose.Blush * 0.85f);
            float by = eyeY + (s * 1.1f);
            float bw = s * 0.7f;
            float bx = eyeX + (s * 0.45f);
            float corner = bw * 0.3f;
            canvas.DrawRoundRect(new SKRect(-bx - bw, by - (bw * 0.4f), -bx + bw, by + (bw * 0.4f)), corner, corner, _fill);
            canvas.DrawRoundRect(new SKRect(bx - bw, by - (bw * 0.4f), bx + bw, by + (bw * 0.4f)), corner, corner, _fill);
        }

        DrawEye(canvas, -eyeX, eyeY, s, pose, palette, lookX);
        DrawEye(canvas, eyeX, eyeY, s, pose, palette, lookX);

        DrawBrows(canvas, pose, palette, eyeX, eyeY, s);
        DrawMouth(canvas, h, pose, palette, eyeY, s, mouthDrop);
    }

    private void DrawEye(SKCanvas canvas, float ex, float ey, float s, Pose pose, SkinPalette palette, float lookX)
    {
        float half = s * 0.5f;
        float corner = s * 0.14f;

        if (pose.HappyEyes > 0.5f)
        {
            // ^ ^ — a chunky upward chevron.
            _stroke.Color = palette.Pupil.ToSk();
            _stroke.StrokeWidth = s * 0.34f;
            _stroke.StrokeCap = SKStrokeCap.Round;
            canvas.DrawLine(ex - half, ey + (half * 0.4f), ex, ey - (half * 0.5f), _stroke);
            canvas.DrawLine(ex, ey - (half * 0.5f), ex + half, ey + (half * 0.4f), _stroke);
            return;
        }

        if (pose.SpiralEyes > 0.5f)
        {
            DrawSpiralEye(canvas, ex, ey, half, palette);
            return;
        }

        if (pose.StarEyes > 0.5f)
        {
            DrawStarEye(canvas, ex, ey, half, palette);
            return;
        }

        float open = MathUtil.Clamp(pose.EyeOpen, 0f, 1.2f);
        if (open < 0.14f)
        {
            // Closed/blink — a short flat bar.
            _fill.Color = palette.Pupil.ToSk();
            float barH = s * 0.16f;
            canvas.DrawRoundRect(new SKRect(ex - half, ey - (barH / 2f), ex + half, ey + (barH / 2f)), barH * 0.5f, barH * 0.5f, _fill);
            return;
        }

        // Open: a solid black square that nudges toward the cursor.
        float lookOffset = half * 0.34f;
        float px = ex + (lookX * lookOffset);
        float py = ey + (pose.EyeLookY * lookOffset);
        float h2 = half * open;

        _fill.Color = palette.Pupil.ToSk();
        canvas.DrawRoundRect(new SKRect(px - half, py - h2, px + half, py + h2), corner, corner, _fill);

        // A single tiny glint keeps the eyes alive without losing the pixel look.
        _fill.Color = new SKColor(255, 255, 255, 220);
        float g = s * 0.16f;
        canvas.DrawRoundRect(new SKRect(px - (half * 0.55f), py - (h2 * 0.55f), px - (half * 0.55f) + g, py - (h2 * 0.55f) + g), g * 0.3f, g * 0.3f, _fill);
    }

    private void DrawStarEye(SKCanvas canvas, float ex, float ey, float r, SkinPalette palette)
    {
        _fill.Color = palette.Accent.ToSk();
        using var path = new SKPath();
        for (int i = 0; i < 10; i++)
        {
            float ang = (MathF.PI * i) / 5f - MathUtil.HalfPi;
            float radius = (i % 2 == 0) ? r : r * 0.42f;
            float x = ex + (MathF.Cos(ang) * radius);
            float y = ey + (MathF.Sin(ang) * radius);
            if (i == 0)
            {
                path.MoveTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        path.Close();
        canvas.DrawPath(path, _fill);
    }

    // The classic cartoon "I'm seeing stars" dizzy eye: a little @-shaped spiral. Drawn
    // as an Archimedean spiral stroke so it reads instantly as woozy.
    private void DrawSpiralEye(SKCanvas canvas, float ex, float ey, float r, SkinPalette palette)
    {
        _stroke.Color = palette.Pupil.ToSk();
        _stroke.StrokeWidth = r * 0.30f;
        _stroke.StrokeCap = SKStrokeCap.Round;

        using var path = new SKPath();
        const int turns = 3;
        const int steps = turns * 16;
        float maxR = r * 1.05f;
        for (int i = 0; i <= steps; i++)
        {
            float tt = i / (float)steps;          // 0..1 from centre outward
            float ang = tt * turns * MathUtil.Tau;
            float radius = tt * maxR;
            float x = ex + (MathF.Cos(ang) * radius);
            float y = ey + (MathF.Sin(ang) * radius);
            if (i == 0)
            {
                path.MoveTo(x, y);
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        canvas.DrawPath(path, _stroke);
    }

    /// <summary>Draws a pair of dizzy spiral eyes — the universal "I'm woozy" overlay that
    /// every skin (Claud, Creeper, Ghast, Galgo) shows when <c>pose.SpiralEyes</c> is high,
    /// so the dizzy reaction reads the same regardless of the face style.</summary>
    private void DrawDizzyPair(SKCanvas canvas, float eyeX, float eyeY, float half, SkinPalette palette)
    {
        DrawSpiralEye(canvas, -eyeX, eyeY, half, palette);
        DrawSpiralEye(canvas, eyeX, eyeY, half, palette);
    }

    private void DrawBrows(SKCanvas canvas, Pose pose, SkinPalette palette, float eyeX, float eyeY, float s)
    {
        if (MathF.Abs(pose.BrowAngle) < 0.05f || pose.HappyEyes > 0.5f)
        {
            return;
        }

        _stroke.Color = palette.Pupil.ToSk(0.9f);
        _stroke.StrokeWidth = s * 0.22f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        float browY = eyeY - (s * 0.95f);
        float tilt = pose.BrowAngle * s * 0.4f;
        float len = s * 0.55f;

        // Worried/angry (negative) tilts inner ends down; surprised (positive) raises both.
        canvas.DrawLine(-eyeX - len, browY + tilt, -eyeX + len, browY - tilt, _stroke);
        canvas.DrawLine(eyeX - len, browY - tilt, eyeX + len, browY + tilt, _stroke);
    }

    private void DrawMouth(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float eyeY, float s, float drop = 1.0f)
    {
        // Claw'd is mouthless at rest; a little mouth only appears for big moments
        // (yawning, snoring, gasping) so the clean face is preserved the rest of the time.
        if (pose.MouthOpen <= 0.14f)
        {
            return;
        }

        float my = eyeY + (s * 1.6f * drop);
        float openH = pose.MouthOpen * 0.13f * h;
        float mw = 0.075f * h;

        _fill.Color = palette.Pupil.ToSk();
        var rect = new SKRect(-mw, my - (openH * 0.25f), mw, my + openH);
        canvas.DrawRoundRect(rect, mw * 0.45f, openH * 0.45f, _fill);

        _fill.Color = palette.Blush.ToSk();
        canvas.DrawOval(new SKRect(-mw * 0.55f, my + (openH * 0.25f), mw * 0.55f, my + openH), _fill);
    }

    // ---- Creeper -------------------------------------------------------------

    private void DrawCreeperFace(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX)
    {
        float eyeX = 0.18f * h;
        float eyeY = -0.66f * h;
        float s = 0.2f * h;
        float open = MathUtil.Clamp(pose.EyeOpen, 0.12f, 1.2f);

        // Dizzy overlay (universal): spiral eyes replace the menacing face when woozy.
        if (pose.SpiralEyes > 0.5f)
        {
            DrawDizzyPair(canvas, eyeX, eyeY, s * 0.55f, palette);
            return;
        }

        _fill.Color = palette.Pupil.ToSk();

        // Two square eyes (no glint — keep it menacing). They still blink/track a little.
        for (int side = -1; side <= 1; side += 2)
        {
            float ex = (side * eyeX) + (lookX * s * 0.18f);
            float half = s * 0.5f;
            float hy = half * open;
            canvas.DrawRoundRect(new SKRect(ex - half, eyeY - hy, ex + half, eyeY + hy), s * 0.1f, s * 0.1f, _fill);
        }

        // The signature creeper frown: a tall central bar plus two lower side blocks.
        float r = h * 0.012f;
        canvas.DrawRoundRect(new SKRect(-0.07f * h, -0.58f * h, 0.07f * h, -0.30f * h), r, r, _fill);
        canvas.DrawRoundRect(new SKRect(-0.21f * h, -0.40f * h, -0.07f * h, -0.22f * h), r, r, _fill);
        canvas.DrawRoundRect(new SKRect(0.07f * h, -0.40f * h, 0.21f * h, -0.22f * h), r, r, _fill);
    }

    // ---- Ghast ---------------------------------------------------------------

    private static readonly float[] GhastTentacleX = { -0.3f, -0.2f, -0.1f, 0f, 0.1f, 0.2f, 0.3f };
    private static readonly float[] GhastTentacleLen = { 0.16f, 0.24f, 0.3f, 0.26f, 0.3f, 0.24f, 0.16f };

    private void DrawTentacles(SKCanvas canvas, float h, Pose pose, SkinPalette palette)
    {
        float top = (BodyBottom * h) + (0.02f * h);
        float w = 0.07f * h;
        float corner = w * 0.5f;

        for (int i = 0; i < GhastTentacleX.Length; i++)
        {
            float baseX = GhastTentacleX[i] * h;
            float len = GhastTentacleLen[i] * h;
            float sway = MathF.Sin((pose.LegPhase * 0.6f) + (i * 0.8f)) * 0.03f * h;
            float x = baseX + sway;

            _fill.Color = palette.Body.ToSk();
            canvas.DrawRoundRect(new SKRect(x - (w / 2f), top, x + (w / 2f), top + len), corner, corner, _fill);
            _fill.Color = palette.BodyShadow.ToSk(0.6f);
            canvas.DrawRoundRect(new SKRect(x - (w / 2f), top + len - w, x + (w / 2f), top + len), corner, corner, _fill);
        }
    }

    private void DrawGhastFace(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX)
    {
        float eyeX = 0.17f * h;
        float eyeY = -0.62f * h;
        bool agitated = pose.MouthOpen > 0.3f; // shooting / startled

        // Dizzy overlay (universal): spiral eyes replace the sad face when woozy.
        if (pose.SpiralEyes > 0.5f)
        {
            DrawDizzyPair(canvas, eyeX, eyeY, 0.09f * h, palette);
            return;
        }

        _fill.Color = palette.Pupil.ToSk();

        if (agitated)
        {
            // Wide, alarmed dark eyes.
            float half = 0.075f * h;
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = side * eyeX;
                canvas.DrawRoundRect(new SKRect(ex - half, eyeY - half, ex + half, eyeY + half), half * 0.3f, half * 0.3f, _fill);
            }

            // The big red maw.
            _fill.Color = palette.Mouth.ToSk();
            float mw = 0.13f * h;
            float my = -0.36f * h;
            float mh = pose.MouthOpen * 0.2f * h;
            canvas.DrawRoundRect(new SKRect(-mw, my, mw, my + mh), mw * 0.4f, mw * 0.4f, _fill);
        }
        else
        {
            // Sad, slanted little eyes ("\  /").
            _stroke.Color = palette.Pupil.ToSk();
            _stroke.StrokeWidth = 0.05f * h;
            _stroke.StrokeCap = SKStrokeCap.Round;
            float dx = 0.05f * h;
            float dy = 0.045f * h;
            canvas.DrawLine(-eyeX - dx, eyeY - dy, -eyeX + dx, eyeY + dy, _stroke);
            canvas.DrawLine(eyeX + dx, eyeY - dy, eyeX - dx, eyeY + dy, _stroke);

            // A small downturned frown.
            _stroke.StrokeWidth = 0.04f * h;
            using var frown = new SKPath();
            float my = -0.4f * h;
            float mw = 0.1f * h;
            frown.MoveTo(-mw, my);
            frown.QuadTo(0, my - (0.05f * h), mw, my);
            canvas.DrawPath(frown, _stroke);
        }
    }

    // ---- Nicolaia ------------------------------------------------------------
    // The classic block is reused as the face/skin; a black three-piece suit is
    // painted over the lower torso, brown side-curls (peyot) hang past the cheeks,
    // and a tall black top hat sits on the crown. Palette mapping:
    //   Body = skin tone · BodyShadow = black suit/hat · Belly = white shirt
    //   Pupil = eyes · Accent = curl brown.

    private void DrawNicolaiaSuit(SKCanvas canvas, float h, SkinPalette palette)
    {
        float halfW = BodyWidth * 0.5f * h;
        var rect = new SKRect(-halfW, BodyTop * h, halfW, BodyBottom * h);
        float corner = CornerRadius * h;

        // Clip to the body so the suit can't bleed past the block's rounded edge.
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(rect, corner, corner), antialias: true);

        float collarTop = -0.42f * h;   // top of the suit, a little below the eyes
        float bottom = BodyBottom * h;

        // The black jacket fills the lower torso edge-to-edge — no skin peeks through.
        _fill.Color = palette.BodyShadow.ToSk();
        canvas.DrawRect(new SKRect(-halfW, collarTop, halfW, bottom), _fill);

        // A white shirt: a broad band across the top (so no skin shows between the
        // lapels) tapering into a wide V down the chest.
        _fill.Color = palette.Belly.ToSk();
        float collarHalf = 0.22f * h;       // shirt width at the collar
        float vBottom = -0.14f * h;         // where the white V tapers shut
        using (var shirt = new SKPath())
        {
            shirt.MoveTo(-collarHalf, collarTop);
            shirt.LineTo(collarHalf, collarTop);
            shirt.LineTo(0.10f * h, vBottom);
            shirt.LineTo(-0.10f * h, vBottom);
            shirt.Close();
            canvas.DrawPath(shirt, _fill);
        }

        // Jacket lapels: two slim dark wedges on the OUTER edges of the collar, leaving a
        // generous white centre showing between them.
        _fill.Color = palette.Pupil.ToSk(0.9f);
        for (int side = -1; side <= 1; side += 2)
        {
            using var lapel = new SKPath();
            lapel.MoveTo(side * collarHalf, collarTop);          // outer top corner
            lapel.LineTo(side * 0.12f * h, collarTop);           // inner top corner
            lapel.LineTo(side * 0.10f * h, vBottom);             // taper down beside the V
            lapel.Close();
            canvas.DrawPath(lapel, _fill);
        }

        // A small dark bow-tie / collar knot at the very top of the V.
        _fill.Color = palette.Pupil.ToSk();
        float knot = 0.04f * h;
        using (var bow = new SKPath())
        {
            bow.MoveTo(-knot, collarTop + (knot * 0.2f));
            bow.LineTo(0f, collarTop + (knot * 0.9f));
            bow.LineTo(-knot, collarTop + (knot * 1.6f));
            bow.Close();
            bow.MoveTo(knot, collarTop + (knot * 0.2f));
            bow.LineTo(0f, collarTop + (knot * 0.9f));
            bow.LineTo(knot, collarTop + (knot * 1.6f));
            bow.Close();
            canvas.DrawPath(bow, _fill);
        }

        // A couple of tiny waistcoat buttons down the white shirt.
        float bsize = 0.014f * h;
        for (int i = 0; i < 2; i++)
        {
            float by = -0.28f * h + (i * 0.06f * h);
            canvas.DrawCircle(0f, by, bsize, _fill);
        }

        canvas.Restore();
    }

    private void DrawNicolaiaCrown(SKCanvas canvas, float h, Pose pose, SkinPalette palette)
    {
        // ---- Side-curls (peyot): a symmetric wavy lock down each side of the face. ----
        // Anchored just inside the face edge (half-width 0.39h) so they frame the cheeks
        // evenly, mirrored left/right.
        float cheekX = 0.37f * h;
        float curlTop = -0.70f * h;          // just under the hat brim, beside the eyes
        float curlLen = 0.40f * h;           // hangs down past the cheek
        _stroke.Color = palette.Accent.ToSk();
        _stroke.StrokeWidth = 0.065f * h;
        _stroke.StrokeCap = SKStrokeCap.Round;

        // A subtle sway brings the curls to life (re-uses the idle leg phase).
        float sway = MathF.Sin(pose.LegPhase * 0.5f) * 0.012f * h;
        float wave = 0.04f * h;              // how far the lock waves in/out

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * cheekX;
            // Two stacked S-curves, mirrored by `side`. The control points push outward
            // then inward so both curls read as the same shape on each side.
            using var curl = new SKPath();
            curl.MoveTo(x, curlTop);
            curl.QuadTo(x + (side * wave) + sway, curlTop + (curlLen * 0.30f), x, curlTop + (curlLen * 0.55f));
            curl.QuadTo(x - (side * wave) + sway, curlTop + (curlLen * 0.80f), x + (side * wave * 0.4f), curlTop + curlLen);
            canvas.DrawPath(curl, _stroke);
        }

        // ---- Top hat sitting on the crown of the block. ----
        // The brim is pulled DOWN onto the face (its bottom edge sinks slightly into the
        // block) so no sliver of skin shows between the hat and the head. It's also wider
        // than the block so the upper corners are covered.
        float brimY = BodyTop * h;            // the block's top edge
        float brimHalf = 0.44f * h;           // wider than the body half-width (0.39h)
        float brimBottom = brimY + (0.06f * h); // sink into the face — kills the skin line
        float brimTop = brimY - (0.04f * h);
        float crownHalf = 0.24f * h;
        float crownTop = brimY - (0.36f * h);

        _fill.Color = palette.BodyShadow.ToSk();

        // The tall crown (overlaps the brim so there's no seam between them).
        canvas.DrawRoundRect(
            new SKRect(-crownHalf, crownTop, crownHalf, brimBottom),
            crownHalf * 0.12f, crownHalf * 0.12f, _fill);

        // The wide brim, sunk onto the head.
        canvas.DrawRoundRect(
            new SKRect(-brimHalf, brimTop, brimHalf, brimBottom),
            (brimBottom - brimTop) * 0.4f, (brimBottom - brimTop) * 0.4f, _fill);

        // A faint hat band + a soft sheen line so the black hat isn't a flat void.
        _fill.Color = palette.Pupil.ToSk(0.6f);
        canvas.DrawRect(new SKRect(-crownHalf, brimTop - (0.03f * h), crownHalf, brimTop), _fill);
        _fill.Color = new SKColor(255, 255, 255, 22);
        canvas.DrawRect(new SKRect(-crownHalf + (0.02f * h), crownTop + (0.02f * h), -crownHalf + (0.05f * h), brimTop), _fill);
    }

    // ---- Galgo (the cartoon bus, line 34 Liniers–Palermo, Vélez bucket hat) -------
    // A whole-body character: a 3/4-view city bus seen from its smiley front-left. It
    // doesn't use the block/legs at all. Origin is feet at (0,0); the bus sits on its
    // wheels at y=0 and extends up into negative Y. Palette mapping:
    //   Body = white shell · BodyShadow = navy skirt/Vélez blue · Accent = red stripe
    //   Belly = light-blue glass · Pupil = black (tyres, outlines, pupils) · Mouth = red mouth
    private void DrawGalgo(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX)
    {
        // Outline helper.
        _stroke.Color = palette.Pupil.ToSk();
        _stroke.StrokeWidth = 0.018f * h;
        _stroke.StrokeCap = SKStrokeCap.Round;
        _stroke.StrokeJoin = SKStrokeJoin.Round;

        SKColor white = palette.Body.ToSk();
        SKColor navy = palette.BodyShadow.ToSk();
        SKColor red = palette.Accent.ToSk();
        SKColor glass = palette.Belly.ToSk();
        SKColor ink = palette.Pupil.ToSk();

        float bodyTop = -1.06f * h;     // roof
        float bodyBottom = -0.12f * h;  // chassis (wheels hang below)
        float frontX = -0.62f * h;      // nose (the face)
        float rearX = 0.96f * h;        // tail

        // --- Body shell (rounded box, taller/rounder at the nose) --------------
        using (var shell = new SKPath())
        {
            shell.MoveTo(frontX + (0.02f * h), bodyBottom);
            shell.LineTo(frontX, bodyTop + (0.30f * h));
            // rounded windshield brow up to the roof
            shell.QuadTo(frontX, bodyTop + (0.04f * h), frontX + (0.18f * h), bodyTop);
            shell.LineTo(rearX - (0.10f * h), bodyTop + (0.02f * h));
            shell.QuadTo(rearX, bodyTop + (0.04f * h), rearX, bodyTop + (0.22f * h));
            shell.LineTo(rearX, bodyBottom);
            shell.Close();
            _fill.Color = white;
            canvas.DrawPath(shell, _fill);
            canvas.DrawPath(shell, _stroke);
        }

        // --- Coloured stripes (red high, blue low) along the side --------------
        _fill.Color = red;
        canvas.DrawRect(new SKRect(frontX + (0.20f * h), bodyTop + (0.05f * h), rearX, bodyTop + (0.12f * h)), _fill);
        _fill.Color = navy;
        canvas.DrawRect(new SKRect(frontX + (0.06f * h), bodyBottom - (0.20f * h), rearX, bodyBottom), _fill);
        // a thin red accent above the blue skirt
        _fill.Color = red;
        canvas.DrawRect(new SKRect(frontX + (0.06f * h), bodyBottom - (0.235f * h), rearX, bodyBottom - (0.205f * h)), _fill);

        // --- Side: door + windows ---------------------------------------------
        float winTop = bodyTop + (0.22f * h);
        float winBottom = bodyBottom - (0.28f * h);
        // black folding door just behind the nose
        _fill.Color = ink;
        canvas.DrawRoundRect(new SKRect(-0.04f * h, winTop - (0.04f * h), 0.16f * h, bodyBottom - (0.02f * h)), 0.01f * h, 0.01f * h, _fill);
        _stroke.StrokeWidth = 0.01f * h;
        canvas.DrawLine(0.06f * h, winTop - (0.02f * h), 0.06f * h, bodyBottom - (0.04f * h), _stroke);
        // two light-blue side windows
        _fill.Color = glass;
        canvas.DrawRoundRect(new SKRect(0.22f * h, winTop, 0.52f * h, winBottom), 0.02f * h, 0.02f * h, _fill);
        canvas.DrawRoundRect(new SKRect(0.58f * h, winTop, 0.90f * h, winBottom), 0.02f * h, 0.02f * h, _fill);
        _stroke.StrokeWidth = 0.014f * h;
        canvas.DrawRoundRect(new SKRect(0.22f * h, winTop, 0.52f * h, winBottom), 0.02f * h, 0.02f * h, _stroke);
        canvas.DrawRoundRect(new SKRect(0.58f * h, winTop, 0.90f * h, winBottom), 0.02f * h, 0.02f * h, _stroke);

        // --- Front face: route sign, windshield "eyes", smile, bumper ----------
        DrawGalgoFace(canvas, h, pose, palette, lookX, frontX, bodyTop, bodyBottom, white, navy, red, glass, ink);

        // --- Wheels ------------------------------------------------------------
        DrawGalgoWheel(canvas, h, -0.30f * h, bodyBottom, ink, white);
        DrawGalgoWheel(canvas, h, 0.62f * h, bodyBottom, ink, white);

        // --- Vélez bucket hat on the roof, over the nose -----------------------
        DrawGalgoHat(canvas, h, frontX, bodyTop, navy, white, glass, ink);
    }

    private void DrawGalgoFace(
        SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX,
        float frontX, float bodyTop, float bodyBottom,
        SKColor white, SKColor navy, SKColor red, SKColor glass, SKColor ink)
    {
        float faceRight = frontX + (0.40f * h);  // where the nose meets the side

        // Route sign band: "34  LINIERS / PALERMO".
        _fill.Color = navy;
        var sign = new SKRect(frontX + (0.02f * h), bodyTop + (0.10f * h), faceRight, bodyTop + (0.28f * h));
        canvas.DrawRoundRect(sign, 0.01f * h, 0.01f * h, _fill);
        using (var font = new SKPaint { IsAntialias = true, Color = SKColors.White, TextSize = 0.085f * h, Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), TextAlign = SKTextAlign.Left })
        {
            canvas.DrawText("34", frontX + (0.04f * h), bodyTop + (0.225f * h), font);
            using var small = font.Clone();
            small.TextSize = 0.05f * h;
            canvas.DrawText("LINIERS", frontX + (0.155f * h), bodyTop + (0.175f * h), small);
            canvas.DrawText("PALERMO", frontX + (0.155f * h), bodyTop + (0.245f * h), small);
        }

        // Windshield split into two panes = the eyes. Light-blue glass, dark frame.
        float eyeTop = bodyTop + (0.30f * h);
        float eyeBottom = bodyBottom - (0.34f * h);
        var glassRect = new SKRect(frontX + (0.02f * h), eyeTop, faceRight, eyeBottom);
        _fill.Color = glass;
        canvas.DrawRoundRect(glassRect, 0.03f * h, 0.03f * h, _fill);
        _stroke.Color = ink;
        _stroke.StrokeWidth = 0.016f * h;
        canvas.DrawRoundRect(glassRect, 0.03f * h, 0.03f * h, _stroke);

        // Two big white cartoon eyes inside the glass, pupils tracking the cursor.
        float eyeCY = (eyeTop + eyeBottom) * 0.5f - (0.02f * h);
        float eyeR = 0.10f * h;
        float exL = frontX + (0.14f * h);
        float exR = frontX + (0.30f * h);
        bool dizzy = pose.SpiralEyes > 0.5f;
        foreach (float ex in new[] { exL, exR })
        {
            _fill.Color = SKColors.White;
            canvas.DrawCircle(ex, eyeCY, eyeR, _fill);
            _stroke.StrokeWidth = 0.01f * h;
            _stroke.Color = ink;
            canvas.DrawCircle(ex, eyeCY, eyeR, _stroke);

            // Dizzy overlay (universal): a spiral inside the eye instead of a pupil.
            if (dizzy)
            {
                DrawSpiralEye(canvas, ex, eyeCY, eyeR * 0.8f, palette);
                continue;
            }

            float open = MathUtil.Clamp(pose.EyeOpen, 0.2f, 1.2f);
            float px = ex + (lookX * eyeR * 0.4f);
            float py = eyeCY + (pose.EyeLookY * eyeR * 0.4f) + (eyeR * 0.15f);
            _fill.Color = ink;
            canvas.DrawCircle(px, py, eyeR * 0.5f * open, _fill);
            _fill.Color = SKColors.White;
            canvas.DrawCircle(px - (eyeR * 0.18f), py - (eyeR * 0.2f), eyeR * 0.16f, _fill);
        }

        // Big friendly smile below the windshield.
        _stroke.Color = new SKColor(0x6E, 0x20, 0x18);
        _stroke.StrokeWidth = 0.02f * h;
        float smileY = eyeBottom + (0.02f * h);
        float smileW = 0.16f * h;
        float smileCX = frontX + (0.20f * h);
        using (var smile = new SKPath())
        {
            smile.MoveTo(smileCX - smileW, smileY);
            smile.QuadTo(smileCX, smileY + (0.12f * h) + (pose.MouthCurve * 0.02f * h), smileCX + smileW, smileY);
            // fill the open mouth red
            using var lips = new SKPath();
            lips.MoveTo(smileCX - smileW, smileY);
            lips.QuadTo(smileCX, smileY + (0.13f * h), smileCX + smileW, smileY);
            lips.QuadTo(smileCX, smileY + (0.05f * h), smileCX - smileW, smileY);
            lips.Close();
            _fill.Color = palette.Mouth.ToSk();
            canvas.DrawPath(lips, _fill);
            canvas.DrawPath(smile, _stroke);
        }

        // Grey front bumper with a Vélez-themed plate "CAV 1910" (founding year).
        float bumperTop = bodyBottom - (0.10f * h);
        _fill.Color = new SKColor(0xB9, 0xC2, 0xC7);
        canvas.DrawRoundRect(new SKRect(frontX - (0.01f * h), bumperTop, faceRight + (0.04f * h), bodyBottom), 0.03f * h, 0.03f * h, _fill);
        _fill.Color = navy;
        var plate = new SKRect(frontX + (0.085f * h), bumperTop + (0.018f * h), frontX + (0.30f * h), bodyBottom - (0.012f * h));
        canvas.DrawRoundRect(plate, 0.008f * h, 0.008f * h, _fill);
        using (var pf = new SKPaint { IsAntialias = true, Color = SKColors.White, TextSize = 0.04f * h, Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), TextAlign = SKTextAlign.Center })
        {
            canvas.DrawText("CAV 1910", (plate.Left + plate.Right) * 0.5f, plate.MidY + (0.014f * h), pf);
        }

        // restore the default outline stroke colour for callers after us
        _stroke.Color = palette.Pupil.ToSk();
    }

    private void DrawGalgoWheel(SKCanvas canvas, float h, float cx, float groundY, SKColor tyre, SKColor hub)
    {
        float r = 0.13f * h;
        float cy = groundY + (0.02f * h); // dip slightly below the chassis line
        _fill.Color = tyre;
        canvas.DrawCircle(cx, cy, r, _fill);
        _fill.Color = new SKColor(0xC9, 0xCF, 0xD3);
        canvas.DrawCircle(cx, cy, r * 0.5f, _fill);
        _fill.Color = new SKColor(0x8A, 0x93, 0x99);
        canvas.DrawCircle(cx, cy, r * 0.2f, _fill);
    }

    private void DrawGalgoHat(SKCanvas canvas, float h, float frontX, float bodyTop, SKColor navy, SKColor white, SKColor glass, SKColor ink)
    {
        // A Vélez "bucket hat" (piluso) tilted over the bus's forehead, exactly like the
        // sticker: a navy down-turned brim, a tall crown whose UPPER half is white (with the
        // shield + "VÉLEZ SARSFIELD") and lower half navy.
        float cx = frontX + (0.22f * h);      // centred over the face
        float brimY = bodyTop + (0.085f * h); // brim sits down ON the roof (no float gap)
        float crownTop = bodyTop - (0.30f * h);
        float halfW = 0.34f * h;
        float crownHalf = halfW * 0.72f;
        float crownBottom = brimY - (0.015f * h);
        float splitY = (crownTop + crownBottom) * 0.5f; // white above, navy below

        _stroke.Color = ink;
        _stroke.StrokeWidth = 0.014f * h;

        // --- Crown: white top half ---
        _fill.Color = white;
        var crown = new SKRect(cx - crownHalf, crownTop, cx + crownHalf, crownBottom);
        canvas.DrawRoundRect(crown, 0.05f * h, 0.05f * h, _fill);
        // navy lower half of the crown
        _fill.Color = navy;
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(crown, 0.05f * h, 0.05f * h), antialias: true);
        canvas.DrawRect(new SKRect(crown.Left, splitY, crown.Right, crown.Bottom), _fill);
        canvas.Restore();
        canvas.DrawRoundRect(crown, 0.05f * h, 0.05f * h, _stroke);

        // Everything on the crown is clipped to the white panel so no lettering or shield
        // can spill past the hat edge.
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(crown, 0.05f * h, 0.05f * h), antialias: true);

        // --- Vélez Sarsfield shield (escudo) on the white part ---
        // Reproduced in vectors (the character is fully procedural — no bitmaps). The real
        // crest is a blue shield with a scalloped top edge and the "CAFVS" monogram in
        // white. At this size we draw the shield shape faithfully and a legible stylised
        // monogram (CA over VS) rather than the full interlaced lettering.
        float shCx = crown.Left + (crownHalf * 0.42f);
        float shTop = crownTop + (0.02f * h);
        float shW = 0.072f * h;
        float shH = 0.17f * h;
        DrawVelezShield(canvas, shCx, shTop, shW, shH, navy, white);

        // --- "VÉLEZ / SARSFIELD" centred in the space to the right of the shield ---
        float textLeft = shCx + (shW * 1.2f);
        float textCx = (textLeft + crown.Right) * 0.5f;
        using (var vf = new SKPaint { IsAntialias = true, Color = navy, TextSize = 0.046f * h, Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), TextAlign = SKTextAlign.Center })
        {
            canvas.DrawText("VÉLEZ", textCx, crownTop + (0.062f * h), vf);
            canvas.DrawText("SARSFIELD", textCx, crownTop + (0.118f * h), vf);
        }

        canvas.Restore();

        // --- Navy down-turned brim (drawn last so it sits in front of the crown base) ---
        _fill.Color = navy;
        using (var brim = new SKPath())
        {
            brim.MoveTo(cx - halfW, brimY - (0.02f * h));
            brim.QuadTo(cx, brimY + (0.12f * h), cx + halfW, brimY - (0.02f * h));
            brim.QuadTo(cx + (halfW * 0.6f), brimY - (0.08f * h), cx, brimY - (0.07f * h));
            brim.QuadTo(cx - (halfW * 0.6f), brimY - (0.08f * h), cx - halfW, brimY - (0.02f * h));
            brim.Close();
            canvas.DrawPath(brim, _fill);
            canvas.DrawPath(brim, _stroke);
        }

        _stroke.Color = ink;
    }

    // The Vélez Sarsfield crest: a blue shield with a scalloped top, white border, and the
    // CAFVS monogram. (cx, top) is the top-centre; w/h are half-width / full height.
    private void DrawVelezShield(SKCanvas canvas, float cx, float top, float w, float hgt, SKColor navy, SKColor white)
    {
        // Outer (white) shield with a 3-lobe scalloped top edge tapering to a point.
        SKPath Shield(float ww, float tt, float hh)
        {
            var p = new SKPath();
            p.MoveTo(cx - ww, tt + (hh * 0.10f));
            // scalloped top: left lobe, centre dip up, right lobe
            p.QuadTo(cx - (ww * 0.66f), tt - (hh * 0.06f), cx - (ww * 0.33f), tt + (hh * 0.05f));
            p.QuadTo(cx, tt - (hh * 0.08f), cx + (ww * 0.33f), tt + (hh * 0.05f));
            p.QuadTo(cx + (ww * 0.66f), tt - (hh * 0.06f), cx + ww, tt + (hh * 0.10f));
            // right side down to the point
            p.LineTo(cx + ww, tt + (hh * 0.45f));
            p.QuadTo(cx + (ww * 0.85f), tt + (hh * 0.85f), cx, tt + hh);
            p.QuadTo(cx - (ww * 0.85f), tt + (hh * 0.85f), cx - ww, tt + (hh * 0.45f));
            p.Close();
            return p;
        }

        using (SKPath outer = Shield(w, top, hgt))
        {
            _fill.Color = white;
            canvas.DrawPath(outer, _fill);
        }

        using (SKPath inner = Shield(w * 0.78f, top + (hgt * 0.07f), hgt * 0.84f))
        {
            _fill.Color = navy;
            canvas.DrawPath(inner, _fill);
        }

        // Stylised CAFVS monogram: "CA" up top, a big "V" and "S" below — the most legible
        // reduction of the interlaced crest lettering at this tiny size.
        using var top2 = new SKPaint { IsAntialias = true, Color = white, TextSize = hgt * 0.30f, Typeface = SKTypeface.FromFamilyName("Georgia", SKFontStyle.Bold), TextAlign = SKTextAlign.Center };
        canvas.DrawText("CA", cx, top + (hgt * 0.42f), top2);
        using var bot = top2.Clone();
        bot.TextSize = hgt * 0.40f;
        canvas.DrawText("VS", cx, top + (hgt * 0.82f), bot);
    }

    private void DrawProps(SKCanvas canvas, float h, Pose pose, SkinPalette palette)
    {
        if (pose.CoffeeProp > 0.3f)
        {
            DrawCoffee(canvas, h, pose.CoffeeProp);
        }

        if (pose.BookProp > 0.3f)
        {
            DrawBook(canvas, h, pose.BookProp);
        }

        if (pose.ThinkBubble > 0.3f)
        {
            DrawThinkBubble(canvas, h, pose.ThinkBubble);
        }

        if (pose.UmbrellaProp > 0.3f)
        {
            DrawUmbrella(canvas, h, pose.UmbrellaProp, palette);
        }

        if (pose.ThermometerProp > 0.3f)
        {
            DrawThermometer(canvas, h, pose.ThermometerProp);
        }

        if (pose.FanProp > 0.3f)
        {
            DrawFan(canvas, h, pose.FanProp);
        }
    }

    private void DrawThermometer(SKCanvas canvas, float h, float a)
    {
        // A frosty thermometer floating to the right of the head with a low (cold) level
        // and a couple of snowflakes — the "it's freezing" cue.
        byte alpha = (byte)(255 * a);
        float x = 0.50f * h;
        float top = -0.92f * h;
        float bottom = -0.60f * h;
        float w = 0.05f * h;
        float bulbR = w * 0.95f;

        // Glass tube.
        _fill.Color = new SKColor(0xEE, 0xF4, 0xF8, alpha);
        canvas.DrawRoundRect(new SKRect(x - (w / 2f), top, x + (w / 2f), bottom), w * 0.5f, w * 0.5f, _fill);
        // Bulb.
        var icyBlue = new SKColor(0x4F, 0xA8, 0xE0, alpha);
        _fill.Color = icyBlue;
        canvas.DrawCircle(x, bottom + (bulbR * 0.4f), bulbR, _fill);
        // Low mercury (cold) — only fills the bottom third, in icy blue.
        float fillTop = MathUtil.Lerp(bottom, top, 0.28f);
        canvas.DrawRoundRect(new SKRect(x - (w * 0.28f), fillTop, x + (w * 0.28f), bottom), w * 0.2f, w * 0.2f, _fill);
        // Tube outline.
        _stroke.Color = new SKColor(0x8A, 0xB4, 0xCC, alpha);
        _stroke.StrokeWidth = h * 0.01f;
        canvas.DrawRoundRect(new SKRect(x - (w / 2f), top, x + (w / 2f), bottom), w * 0.5f, w * 0.5f, _stroke);

        // Two tiny snowflakes drifting nearby.
        _stroke.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(220 * a));
        _stroke.StrokeWidth = h * 0.009f;
        DrawSnowflake(canvas, x + (0.10f * h), top + (0.06f * h), 0.035f * h);
        DrawSnowflake(canvas, x - (0.10f * h), top + (0.16f * h), 0.026f * h);
    }

    private void DrawSnowflake(SKCanvas canvas, float cx, float cy, float r)
    {
        for (int i = 0; i < 3; i++)
        {
            float ang = i * (MathF.PI / 3f);
            float dx = MathF.Cos(ang) * r;
            float dy = MathF.Sin(ang) * r;
            canvas.DrawLine(cx - dx, cy - dy, cx + dx, cy + dy, _stroke);
        }
    }

    private void DrawFan(SKCanvas canvas, float h, float a)
    {
        // A folding hand-fan held out to the right, opening from a pivot — drawn as a wedge
        // of alternating ribs. Pairs with the Hot pose's fanning arm motion.
        byte alpha = (byte)(255 * a);
        float px = 0.46f * h;   // pivot near the "hand"
        float py = -0.46f * h;
        float radius = 0.22f * h;
        float startDeg = -120f;
        float sweepDeg = 80f;

        var rect = new SKRect(px - radius, py - radius, px + radius, py + radius);
        // Fan leaf.
        using (var leaf = new SKPath())
        {
            leaf.MoveTo(px, py);
            leaf.ArcTo(rect, startDeg, sweepDeg, false);
            leaf.Close();
            _fill.Color = new SKColor(0xFF, 0xE6, 0x9A, alpha);
            canvas.DrawPath(leaf, _fill);
            _stroke.Color = new SKColor(0xC9, 0x9A, 0x3A, alpha);
            _stroke.StrokeWidth = h * 0.01f;
            canvas.DrawPath(leaf, _stroke);
        }
        // Ribs.
        _stroke.StrokeWidth = h * 0.008f;
        for (int i = 0; i <= 4; i++)
        {
            float deg = startDeg + (sweepDeg * i / 4f);
            float rad = deg * MathF.PI / 180f;
            canvas.DrawLine(px, py, px + (MathF.Cos(rad) * radius), py + (MathF.Sin(rad) * radius), _stroke);
        }
    }

    private void DrawCoffee(SKCanvas canvas, float h, float a)
    {
        // Held out to the right of the block (clear of the body's 0.39h half-width).
        float x = 0.52f * h;
        float y = -0.44f * h;
        float w = 0.13f * h;
        float ch = 0.15f * h;
        byte alpha = (byte)(255 * a);

        _fill.Color = new SKColor(0xF7, 0xF3, 0xEC, alpha);
        canvas.DrawRoundRect(new SKRect(x - (w / 2f), y - (ch / 2f), x + (w / 2f), y + (ch / 2f)), w * 0.22f, w * 0.22f, _fill);
        _fill.Color = new SKColor(0x6F, 0x3F, 0x2A, alpha);
        canvas.DrawOval(new SKRect(x - (w * 0.38f), y - (ch * 0.38f), x + (w * 0.38f), y - (ch * 0.1f)), _fill);

        _stroke.Color = new SKColor(0xF7, 0xF3, 0xEC, alpha);
        _stroke.StrokeWidth = h * 0.016f;
        canvas.DrawArc(new SKRect(x + (w * 0.32f), y - (ch * 0.28f), x + (w * 0.85f), y + (ch * 0.32f)), -90, 180, false, _stroke);

        // Two little wisps of steam.
        _stroke.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(120 * a));
        _stroke.StrokeWidth = h * 0.012f;
        for (int i = -1; i <= 1; i += 2)
        {
            using var steam = new SKPath();
            float sx = x + (i * w * 0.2f);
            float top = y - (ch * 0.7f);
            steam.MoveTo(sx, y - (ch * 0.45f));
            steam.QuadTo(sx + (w * 0.18f), (y - (ch * 0.55f) + top) * 0.5f, sx, top);
            canvas.DrawPath(steam, _stroke);
        }
    }

    private void DrawBook(SKCanvas canvas, float h, float a)
    {
        // Held low and forward so it never covers Claw'd's eyes (it reads down at it).
        float y = -0.18f * h;
        float w = 0.5f * h;
        float bh = 0.2f * h;
        byte alpha = (byte)(255 * a);

        _fill.Color = new SKColor(0xF7, 0xF3, 0xEC, alpha);
        canvas.DrawRoundRect(new SKRect(-w / 2f, y - (bh / 2f), w / 2f, y + (bh / 2f)), bh * 0.16f, bh * 0.16f, _fill);
        _stroke.Color = new SKColor(0xC2, 0x62, 0x43, alpha);
        _stroke.StrokeWidth = h * 0.013f;
        canvas.DrawLine(0, y - (bh * 0.46f), 0, y + (bh * 0.46f), _stroke); // spine
        for (int side = -1; side <= 1; side += 2)
        {
            for (int line = 0; line < 3; line++)
            {
                float ly = y - (bh * 0.26f) + (line * bh * 0.26f);
                canvas.DrawLine(side * w * 0.08f, ly, side * w * 0.4f, ly, _stroke);
            }
        }
    }

    private void DrawThinkBubble(SKCanvas canvas, float h, float a)
    {
        // Floats up and to the right, clear of the body top (-0.90h).
        float x = 0.42f * h;
        float y = -1.12f * h;
        byte alpha = (byte)(235 * a);

        _fill.Color = new SKColor(255, 255, 255, alpha);
        canvas.DrawCircle(x, y, 0.12f * h, _fill);
        canvas.DrawCircle(x - (0.16f * h), y + (0.13f * h), 0.05f * h, _fill);
        canvas.DrawCircle(x - (0.24f * h), y + (0.2f * h), 0.03f * h, _fill);
        _fill.Color = new SKColor(0x6B, 0x5B, 0x52, alpha);
        for (int i = -1; i <= 1; i++)
        {
            canvas.DrawCircle(x + (i * 0.045f * h), y, 0.016f * h, _fill);
        }
    }

    private void DrawUmbrella(SKCanvas canvas, float h, float a, SkinPalette palette)
    {
        float topY = -1.12f * h;
        float r = 0.34f * h;
        byte alpha = (byte)(255 * a);

        _stroke.Color = new SKColor(0x6B, 0x5B, 0x52, alpha);
        _stroke.StrokeWidth = h * 0.022f;
        canvas.DrawLine(0, topY, 0, -0.55f * h, _stroke); // shaft, held in front

        _fill.Color = palette.Accent.ToSk(a);
        using var dome = new SKPath();
        dome.MoveTo(-r, topY);
        dome.QuadTo(0, topY - (0.3f * h), r, topY);
        // Scalloped underside for a little charm.
        dome.QuadTo(r * 0.5f, topY + (0.05f * h), 0, topY);
        dome.QuadTo(-r * 0.5f, topY + (0.05f * h), -r, topY);
        dome.Close();
        canvas.DrawPath(dome, _fill);

        _fill.Color = palette.BodyShadow.ToSk(a * 0.5f);
        canvas.DrawCircle(0, topY, r * 0.06f, _fill); // ferrule
    }
}
