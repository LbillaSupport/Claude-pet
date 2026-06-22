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

        // Among Us / Mate / Ghost have their own silhouette (not the block + legs). Unlike the
        // rigid Galgo bus they keep the soft squash/sway built into the transform above, so they
        // still feel alive — they just draw a different body + face here and short-circuit.
        switch (palette.Style)
        {
            case SkinStyle.AmongUs:
                DrawAmongUs(canvas, h, pose, palette, lookX);
                DrawProps(canvas, h, pose, palette);
                canvas.Restore();
                return;
            case SkinStyle.Mate:
                DrawMateChar(canvas, h, pose, palette, lookX);
                DrawProps(canvas, h, pose, palette);
                canvas.Restore();
                return;
            case SkinStyle.Ghost:
                DrawGhost(canvas, h, pose, palette, lookX);
                DrawProps(canvas, h, pose, palette);
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

        // Pikachu's ears + tail sit behind/around the block; draw them before the body.
        if (palette.Style == SkinStyle.Pikachu)
        {
            DrawPikachuBackparts(canvas, h, pose, palette);
        }

        DrawBody(canvas, h, palette);

        switch (palette.Style)
        {
            case SkinStyle.Creeper:
                DrawCreeperFace(canvas, h, pose, palette, lookX);
                break;
            case SkinStyle.Pikachu:
                DrawPikachuFace(canvas, h, pose, palette, lookX);
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

    // ---- Among Us crewmate ---------------------------------------------------
    // Its own capsule silhouette (not the block + legs): a rounded bean body, a wide
    // visor, a little backpack and two stubby legs. Palette mapping:
    //   Body = suit colour · BodyShadow = darker suit (legs/backpack edge)
    //   Belly = visor glass · Pupil = visor frame · Accent = backpack highlight.
    private void DrawAmongUs(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX)
    {
        float topY = -1.02f * h, bottomY = -0.06f * h;
        float halfW = 0.30f * h;

        // Legs (two stubby ones, with a tiny walk lift from the pose).
        _fill.Color = palette.BodyShadow.ToSk();
        for (int i = 0; i < 2; i++)
        {
            float lx = (i == 0 ? -1 : 1) * 0.14f * h;
            float lift = MathF.Max(0f, MathF.Sin((pose.LegPhase + (i * 0.5f)) * MathUtil.Tau)) * pose.StrideAmount * 0.05f * h;
            canvas.DrawRoundRect(new SKRect(lx - (0.07f * h), bottomY - (0.06f * h) - lift, lx + (0.07f * h), bottomY + (0.02f * h) - lift), 0.04f * h, 0.04f * h, _fill);
        }

        // Backpack (behind the body, to the left in unmirrored space).
        _fill.Color = palette.BodyShadow.ToSk();
        canvas.DrawRoundRect(new SKRect(-halfW - (0.10f * h), -0.78f * h, -halfW + (0.06f * h), -0.30f * h), 0.06f * h, 0.06f * h, _fill);
        _fill.Color = palette.Accent.ToSk(0.6f);
        canvas.DrawRoundRect(new SKRect(-halfW - (0.08f * h), -0.74f * h, -halfW + (0.02f * h), -0.40f * h), 0.04f * h, 0.04f * h, _fill);

        // Body: a capsule (rounded top, flat-ish bottom).
        using (var body = new SKPath())
        {
            float r = halfW;
            body.MoveTo(-halfW, bottomY);
            body.LineTo(-halfW, topY + r);
            body.ArcTo(new SKRect(-halfW, topY, halfW, topY + (2f * r)), 180, 180, false);
            body.LineTo(halfW, bottomY);
            body.ArcTo(new SKRect(-halfW, bottomY - (0.06f * h), halfW, bottomY + (0.06f * h)), 0, 180, false);
            body.Close();
            _fill.Color = palette.Body.ToSk();
            canvas.DrawPath(body, _fill);
        }

        // Bottom shadow band + top sheen for a little form.
        _fill.Color = palette.BodyShadow.ToSk(0.4f);
        canvas.DrawRoundRect(new SKRect(-halfW, -0.18f * h, halfW, bottomY), 0.05f * h, 0.05f * h, _fill);
        _fill.Color = new SKColor(255, 255, 255, 30);
        canvas.DrawOval(new SKRect(-halfW * 0.6f, topY + (0.04f * h), halfW * 0.1f, topY + (0.3f * h)), _fill);

        // Visor: a horizontal capsule high on the body, glass with a frame + glint.
        float vy = -0.74f * h, vh = 0.16f * h;
        var visor = new SKRect(-0.05f * h, vy - (vh / 2f), halfW + (0.08f * h), vy + (vh / 2f));
        _fill.Color = palette.Pupil.ToSk(); // frame
        canvas.DrawRoundRect(new SKRect(visor.Left - (0.02f * h), visor.Top - (0.02f * h), visor.Right + (0.02f * h), visor.Bottom + (0.02f * h)), vh, vh, _fill);

        // Dizzy overlay: spiral eyes on the visor glass.
        if (pose.SpiralEyes > 0.5f)
        {
            _fill.Color = palette.Belly.ToSk();
            canvas.DrawRoundRect(visor, vh * 0.5f, vh * 0.5f, _fill);
            DrawDizzyPair(canvas, 0.10f * h, vy, 0.05f * h, palette);
            return;
        }

        _fill.Color = palette.Belly.ToSk();
        canvas.DrawRoundRect(visor, vh * 0.5f, vh * 0.5f, _fill);
        // Glint sweeping across the glass.
        _fill.Color = new SKColor(255, 255, 255, 150);
        canvas.DrawRoundRect(new SKRect(visor.Left + (0.03f * h), visor.Top + (0.02f * h), visor.Left + (0.10f * h), visor.Bottom - (0.02f * h)), vh * 0.4f, vh * 0.4f, _fill);
        // Pupils tracking the cursor inside the visor (so it feels alive).
        _fill.Color = palette.Pupil.ToSk(0.85f);
        float pcx = visor.MidX + (lookX * 0.04f * h);
        canvas.DrawCircle(pcx, vy + (pose.EyeLookY * 0.03f * h), 0.022f * h, _fill);
    }

    // ---- Mate (the Argentine gourd with a bombilla) --------------------------
    // A rounded gourd body, a green "yerba" cap on top with a metal bombilla straw, and
    // a friendly little face. Palette mapping:
    //   Body = gourd brown · BodyShadow = darker brown · Belly = green yerba
    //   Accent = metal bombilla · Pupil = eyes · Mouth = mouth.
    private void DrawMateChar(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX)
    {
        float cx = 0f, cyBody = -0.42f * h, rTop = 0.34f * h, rBottom = 0.40f * h;

        // Gourd body: a circle-ish bulb, slightly fatter at the bottom.
        _fill.Color = palette.Body.ToSk();
        canvas.DrawOval(new SKRect(cx - rBottom, cyBody - rTop, cx + rBottom, cyBody + rBottom), _fill);
        // Bottom shadow + side sheen.
        _fill.Color = palette.BodyShadow.ToSk(0.5f);
        canvas.DrawOval(new SKRect(cx - rBottom, cyBody + (rBottom * 0.2f), cx + rBottom, cyBody + rBottom), _fill);
        _fill.Color = new SKColor(255, 255, 255, 28);
        canvas.DrawOval(new SKRect(cx - (rBottom * 0.7f), cyBody - (rTop * 0.8f), cx - (rBottom * 0.1f), cyBody + (rBottom * 0.1f)), _fill);

        // Green yerba cap on top.
        float capY = cyBody - (rTop * 0.92f);
        using (var cap = new SKPath())
        {
            cap.AddOval(new SKRect(cx - (rTop * 0.82f), capY - (0.10f * h), cx + (rTop * 0.82f), capY + (0.14f * h)));
            _fill.Color = palette.Belly.ToSk();
            canvas.DrawPath(cap, _fill);
        }

        _fill.Color = palette.Belly.ToSk(0.7f); // a few darker yerba flecks
        for (int i = -2; i <= 2; i++)
        {
            canvas.DrawCircle(cx + (i * 0.06f * h), capY - (0.01f * h), 0.012f * h, _fill);
        }

        // Bombilla (metal straw) poking up-right out of the yerba.
        _stroke.Color = palette.Accent.ToSk();
        _stroke.StrokeWidth = 0.035f * h;
        _stroke.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(cx + (0.10f * h), capY - (0.02f * h), cx + (0.30f * h), capY - (0.40f * h), _stroke);
        _fill.Color = palette.Accent.ToSk();
        canvas.DrawCircle(cx + (0.30f * h), capY - (0.40f * h), 0.03f * h, _fill); // mouthpiece

        // Face on the gourd.
        float eyeX = 0.13f * h, eyeY = cyBody - (0.04f * h), s = 0.13f * h;
        if (pose.SpiralEyes > 0.5f)
        {
            DrawDizzyPair(canvas, eyeX, eyeY, s * 0.5f, palette);
            return;
        }

        DrawEye(canvas, -eyeX, eyeY, s, pose, palette, lookX);
        DrawEye(canvas, eyeX, eyeY, s, pose, palette, lookX);
        // Rosy cheeks.
        _fill.Color = palette.Blush.ToSk(0.5f);
        canvas.DrawCircle(-0.20f * h, eyeY + (0.10f * h), 0.045f * h, _fill);
        canvas.DrawCircle(0.20f * h, eyeY + (0.10f * h), 0.045f * h, _fill);
        // A small smile.
        _stroke.Color = palette.Mouth.ToSk();
        _stroke.StrokeWidth = 0.026f * h;
        using (var smile = new SKPath())
        {
            float my = eyeY + (0.16f * h);
            smile.MoveTo(-0.08f * h, my);
            smile.QuadTo(0f, my + (0.06f * h), 0.08f * h, my);
            canvas.DrawPath(smile, _stroke);
        }
    }

    // ---- Ghost (a friendly Pac-Man-style ghost) ------------------------------
    // A dome body with a wavy skirt and big classic ghost eyes. Palette mapping:
    //   Body = ghost colour · BodyShadow = darker · Belly = eye whites · Pupil = pupils.
    private void DrawGhost(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX)
    {
        float halfW = 0.34f * h;
        float topY = -0.98f * h, bottomY = -0.06f * h;
        float domeR = halfW;

        // Body: a dome on top, straight sides, a wavy (scalloped) bottom edge.
        using (var body = new SKPath())
        {
            body.MoveTo(-halfW, bottomY);
            body.LineTo(-halfW, topY + domeR);
            body.ArcTo(new SKRect(-halfW, topY, halfW, topY + (2f * domeR)), 180, 180, false);
            body.LineTo(halfW, bottomY);
            // Scalloped skirt: 4 little upward arcs back to the left.
            const int waves = 4;
            float step = (2f * halfW) / waves;
            for (int i = 0; i < waves; i++)
            {
                float x0 = halfW - (i * step);
                float xm = x0 - (step * 0.5f);
                float x1 = x0 - step;
                body.QuadTo(xm, bottomY - (0.07f * h), x1, bottomY);
            }

            body.Close();
            _fill.Color = palette.Body.ToSk();
            canvas.DrawPath(body, _fill);
        }

        // Soft side sheen.
        _fill.Color = new SKColor(255, 255, 255, 32);
        canvas.DrawOval(new SKRect(-halfW * 0.7f, topY + (0.05f * h), -halfW * 0.05f, topY + (0.4f * h)), _fill);

        // Eyes.
        float eyeX = 0.13f * h, eyeY = -0.66f * h;
        if (pose.SpiralEyes > 0.5f)
        {
            DrawDizzyPair(canvas, eyeX, eyeY, 0.07f * h, palette);
            return;
        }

        float ew = 0.10f * h, eh = 0.13f * h;
        for (int side = -1; side <= 1; side += 2)
        {
            float ex = side * eyeX;
            _fill.Color = palette.Belly.ToSk(); // white
            canvas.DrawOval(new SKRect(ex - ew, eyeY - eh, ex + ew, eyeY + eh), _fill);
            // Pupil looks toward the cursor.
            _fill.Color = palette.Pupil.ToSk();
            float px = ex + (lookX * 0.05f * h);
            float py = eyeY + (pose.EyeLookY * 0.05f * h) + (0.02f * h);
            canvas.DrawOval(new SKRect(px - (ew * 0.55f), py - (eh * 0.5f), px + (ew * 0.55f), py + (eh * 0.5f)), _fill);
        }
    }

    // ---- Pikachu add-ons (ears behind, tail, face) ---------------------------
    // Pikachu reuses the classic block + legs but draws long ears + a lightning tail
    // behind it and its own cheeky face. Palette mapping:
    //   Body = yellow · BodyShadow = ear tips / brown · Blush = red cheeks
    //   Accent = brown back stripes · Pupil = eyes.
    private void DrawPikachuBackparts(SKCanvas canvas, float h, Pose pose, SkinPalette palette)
    {
        float halfW = BodyWidth * 0.5f * h;

        // Two long ears rising from the top corners, with dark tips.
        for (int side = -1; side <= 1; side += 2)
        {
            float baseX = side * 0.22f * h;
            float baseY = BodyTop * h;
            float tilt = side * 0.10f * h;
            using var ear = new SKPath();
            ear.MoveTo(baseX - (0.07f * h), baseY);
            ear.LineTo(baseX + tilt - (0.02f * h), baseY - (0.42f * h));
            ear.LineTo(baseX + tilt + (0.06f * h), baseY - (0.40f * h));
            ear.LineTo(baseX + (0.08f * h), baseY);
            ear.Close();
            _fill.Color = palette.Body.ToSk();
            canvas.DrawPath(ear, _fill);
            // Dark tip.
            using var tip = new SKPath();
            tip.MoveTo(baseX + tilt - (0.02f * h), baseY - (0.42f * h));
            tip.LineTo(baseX + tilt + (0.06f * h), baseY - (0.40f * h));
            tip.LineTo(baseX + tilt + (0.045f * h), baseY - (0.30f * h));
            tip.LineTo(baseX + tilt - (0.01f * h), baseY - (0.31f * h));
            tip.Close();
            _fill.Color = palette.BodyShadow.ToSk();
            canvas.DrawPath(tip, _fill);
        }

        // Lightning-bolt tail poking out to the right behind the body.
        using (var tail = new SKPath())
        {
            float tx = halfW - (0.02f * h), ty = -0.30f * h;
            tail.MoveTo(tx, ty);
            tail.LineTo(tx + (0.16f * h), ty - (0.04f * h));
            tail.LineTo(tx + (0.09f * h), ty - (0.12f * h));
            tail.LineTo(tx + (0.24f * h), ty - (0.16f * h));
            tail.LineTo(tx + (0.16f * h), ty - (0.30f * h));
            tail.LineTo(tx + (0.30f * h), ty - (0.34f * h));
            tail.LineTo(tx + (0.20f * h), ty - (0.50f * h));
            tail.LineTo(tx + (0.10f * h), ty - (0.20f * h));
            tail.LineTo(tx + (0.04f * h), ty - (0.08f * h));
            tail.Close();
            _fill.Color = palette.Accent.ToSk();
            canvas.DrawPath(tail, _fill);
            _fill.Color = palette.BodyShadow.ToSk();
            canvas.DrawPath(tail, _stroke); // subtle edge
        }
    }

    private void DrawPikachuFace(SKCanvas canvas, float h, Pose pose, SkinPalette palette, float lookX)
    {
        float eyeX = EyeOffsetX * h;
        float eyeY = EyeCenterY * h;
        float s = EyeSize * h;

        if (pose.SpiralEyes > 0.5f)
        {
            DrawDizzyPair(canvas, eyeX, eyeY, s * 0.5f, palette);
            return;
        }

        // Round black eyes with a glint (cuter than the square block eyes).
        for (int side = -1; side <= 1; side += 2)
        {
            float ex = side * eyeX;
            float open = MathUtil.Clamp(pose.EyeOpen, 0f, 1.2f);
            if (open < 0.14f)
            {
                _stroke.Color = palette.Pupil.ToSk();
                _stroke.StrokeWidth = s * 0.16f;
                canvas.DrawLine(ex - (s * 0.5f), eyeY, ex + (s * 0.5f), eyeY, _stroke);
                continue;
            }

            float px = ex + (lookX * s * 0.2f);
            float py = eyeY + (pose.EyeLookY * s * 0.2f);
            _fill.Color = palette.Pupil.ToSk();
            canvas.DrawCircle(px, py, s * 0.5f * open, _fill);
            _fill.Color = new SKColor(255, 255, 255, 230);
            canvas.DrawCircle(px - (s * 0.16f), py - (s * 0.16f), s * 0.13f, _fill);
        }

        // Big red cheeks (Pikachu's signature pouches).
        _fill.Color = palette.Blush.ToSk(0.9f);
        canvas.DrawCircle(-0.27f * h, eyeY + (0.12f * h), 0.07f * h, _fill);
        canvas.DrawCircle(0.27f * h, eyeY + (0.12f * h), 0.07f * h, _fill);

        // A small open smile.
        _stroke.Color = palette.Mouth.ToSk();
        _stroke.StrokeWidth = 0.024f * h;
        using var smile = new SKPath();
        float my = eyeY + (0.18f * h);
        smile.MoveTo(-0.05f * h, my);
        smile.QuadTo(0f, my + (0.05f * h), 0.05f * h, my);
        canvas.DrawPath(smile, _stroke);
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

        if (pose.HeldProp != HeldPropKind.None && pose.HeldPropAmount > 0.04f)
        {
            DrawHeldProp(canvas, h, pose.HeldProp, pose.HeldPropAmount);
        }
    }

    // ===================================================================
    //  Portal (Portal-game style) — the clone's entrance/exit
    // ===================================================================

    /// <summary>
    /// Draws a glowing, Portal-style oval portal centred at (<paramref name="cx"/>,
    /// <paramref name="cy"/>) — a bright cyan-blue rim around a dark void with a couple of
    /// swirling energy arcs. <paramref name="scale"/> (0..1) opens/closes it and
    /// <paramref name="alpha"/> fades it; <paramref name="phase"/> drives the swirl.
    /// </summary>
    public void DrawPortal(SKCanvas canvas, float cx, float cy, float h, float scale, float alpha, float phase)
    {
        if (scale <= 0.02f || alpha <= 0.02f)
        {
            return;
        }

        float ry = 0.58f * h * scale;   // tall oval, like the game's portals
        float rx = 0.34f * h * scale;
        var rect = new SKRect(cx - rx, cy - ry, cx + rx, cy + ry);

        // Soft outer glow: a few translucent, expanding blue ovals.
        for (int i = 4; i >= 1; i--)
        {
            float g = 1f + (i * 0.16f);
            _fill.Color = new SKColor(0x3C, 0xB4, 0xFF, (byte)(34 * alpha));
            canvas.DrawOval(new SKRect(cx - (rx * g), cy - (ry * g), cx + (rx * g), cy + (ry * g)), _fill);
        }

        // The dark "void" the clone steps through.
        _fill.Color = new SKColor(0x07, 0x12, 0x24, (byte)(190 * alpha));
        canvas.DrawOval(rect, _fill);

        // Swirling energy arcs inside.
        _stroke.Color = new SKColor(0xAF, 0xE6, 0xFF, (byte)(150 * alpha));
        for (int i = 0; i < 3; i++)
        {
            _stroke.StrokeWidth = h * (0.014f - (i * 0.003f));
            float ir = 0.92f - (i * 0.24f);
            float start = (phase * 150f) + (i * 130f);
            canvas.DrawArc(new SKRect(cx - (rx * ir), cy - (ry * ir), cx + (rx * ir), cy + (ry * ir)), start, 210f, false, _stroke);
        }

        // Bright double rim (white-hot core + cyan halo).
        _stroke.Color = new SKColor(0x4F, 0xC4, 0xFF, (byte)(220 * alpha));
        _stroke.StrokeWidth = h * 0.045f;
        canvas.DrawOval(new SKRect(cx - (rx * 1.05f), cy - (ry * 1.05f), cx + (rx * 1.05f), cy + (ry * 1.05f)), _stroke);
        _stroke.Color = new SKColor(0xEC, 0xFA, 0xFF, (byte)(235 * alpha));
        _stroke.StrokeWidth = h * 0.022f;
        canvas.DrawOval(rect, _stroke);
    }

    // ===================================================================
    //  Imaginary held props — the generic "look what I've got" channel
    // ===================================================================

    private void DrawHeldProp(SKCanvas canvas, float h, HeldPropKind prop, float a)
    {
        switch (prop)
        {
            case HeldPropKind.Magnifier: DrawMagnifier(canvas, h, a); break;
            case HeldPropKind.Balloon: DrawBalloon(canvas, h, a); break;
            case HeldPropKind.Flag: DrawFlag(canvas, h, a); break;
            case HeldPropKind.Flashlight: DrawFlashlight(canvas, h, a); break;
            case HeldPropKind.IceCream: DrawIceCream(canvas, h, a); break;
            case HeldPropKind.Mate: DrawMate(canvas, h, a); break;
            case HeldPropKind.Binoculars: DrawBinoculars(canvas, h, a); break;
            case HeldPropKind.PaintBrush: DrawPaintBrush(canvas, h, a); break;
            case HeldPropKind.ToyHammer: DrawToyHammer(canvas, h, a); break;
            case HeldPropKind.Sword: DrawSword(canvas, h, a); break;
            case HeldPropKind.Kite: DrawKite(canvas, h, a); break;
            case HeldPropKind.WateringCan: DrawWateringCan(canvas, h, a); break;
            case HeldPropKind.Umbrella: DrawHeldUmbrella(canvas, h, a); break;
            case HeldPropKind.Guitar: DrawGuitar(canvas, h, a); break;
            case HeldPropKind.Camera: DrawCamera(canvas, h, a); break;
            case HeldPropKind.Trophy: DrawTrophy(canvas, h, a); break;
        }
    }

    private void DrawMagnifier(SKCanvas canvas, float h, float a)
    {
        // A magnifier held out to the right: glass ring + a stubby handle below it.
        byte alpha = (byte)(255 * a);
        float cx = 0.54f * h, cy = -0.52f * h, r = 0.13f * h;

        // Glass.
        _fill.Color = new SKColor(0xCF, 0xE8, 0xFF, (byte)(120 * a));
        canvas.DrawCircle(cx, cy, r * 0.78f, _fill);
        // Rim.
        _stroke.Color = new SKColor(0x3A, 0x3A, 0x40, alpha);
        _stroke.StrokeWidth = h * 0.026f;
        canvas.DrawCircle(cx, cy, r * 0.78f, _stroke);
        // Handle (down-right from the rim).
        _stroke.Color = new SKColor(0x7A, 0x4A, 0x2C, alpha);
        _stroke.StrokeWidth = h * 0.03f;
        float hx = cx + (r * 0.62f), hy = cy + (r * 0.62f);
        canvas.DrawLine(hx, hy, hx + (0.10f * h), hy + (0.10f * h), _stroke);
        // Glass glint.
        _stroke.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(180 * a));
        _stroke.StrokeWidth = h * 0.012f;
        canvas.DrawArc(new SKRect(cx - (r * 0.5f), cy - (r * 0.5f), cx + (r * 0.1f), cy + (r * 0.1f)), 160, 80, false, _stroke);
    }

    private void DrawBalloon(SKCanvas canvas, float h, float a)
    {
        // A balloon floating above-right on a string down to the body.
        byte alpha = (byte)(255 * a);
        float cx = 0.40f * h, cy = -1.18f * h, rx = 0.16f * h, ry = 0.19f * h;

        // String.
        _stroke.Color = new SKColor(0x55, 0x55, 0x55, alpha);
        _stroke.StrokeWidth = h * 0.008f;
        using (var str = new SKPath())
        {
            str.MoveTo(cx, cy + ry);
            str.QuadTo(cx + (0.05f * h), -0.75f * h, 0.18f * h, -0.5f * h);
            canvas.DrawPath(str, _stroke);
        }

        // Body + knot + sheen.
        _fill.Color = new SKColor(0xE0, 0x49, 0x4B, alpha);
        canvas.DrawOval(new SKRect(cx - rx, cy - ry, cx + rx, cy + ry), _fill);
        using (var knot = new SKPath())
        {
            knot.MoveTo(cx, cy + ry);
            knot.LineTo(cx - (0.03f * h), cy + ry + (0.04f * h));
            knot.LineTo(cx + (0.03f * h), cy + ry + (0.04f * h));
            knot.Close();
            canvas.DrawPath(knot, _fill);
        }

        _fill.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(110 * a));
        canvas.DrawOval(new SKRect(cx - (rx * 0.6f), cy - (ry * 0.7f), cx - (rx * 0.05f), cy - (ry * 0.1f)), _fill);
    }

    private void DrawFlag(SKCanvas canvas, float h, float a)
    {
        // A little pennant on a pole, fluttering to the right.
        byte alpha = (byte)(255 * a);
        float px = 0.50f * h, top = -0.95f * h, bottom = -0.30f * h;

        _stroke.Color = new SKColor(0x8A, 0x6A, 0x4A, alpha);
        _stroke.StrokeWidth = h * 0.018f;
        canvas.DrawLine(px, top, px, bottom, _stroke);
        _fill.Color = new SKColor(0xAB, 0x68, 0x3C, alpha); // finial knob
        canvas.DrawCircle(px, top, h * 0.02f, _fill);

        // Wavy pennant.
        using var flag = new SKPath();
        flag.MoveTo(px, top);
        flag.LineTo(px + (0.26f * h), top + (0.05f * h));
        flag.QuadTo(px + (0.18f * h), top + (0.11f * h), px + (0.27f * h), top + (0.16f * h));
        flag.LineTo(px, top + (0.18f * h));
        flag.Close();
        _fill.Color = new SKColor(0x2E, 0x86, 0xD0, alpha);
        canvas.DrawPath(flag, _fill);
    }

    private void DrawFlashlight(SKCanvas canvas, float h, float a)
    {
        // A torch with a soft light cone shining up-right.
        byte alpha = (byte)(255 * a);
        float bx = 0.46f * h, by = -0.42f * h;

        // Light cone.
        using (var cone = new SKPath())
        {
            cone.MoveTo(bx + (0.06f * h), by - (0.02f * h));
            cone.LineTo(bx + (0.34f * h), by - (0.20f * h));
            cone.LineTo(bx + (0.34f * h), by + (0.14f * h));
            cone.Close();
            _fill.Color = new SKColor(0xFF, 0xF2, 0x9E, (byte)(110 * a));
            canvas.DrawPath(cone, _fill);
        }

        // Barrel + head (tilted up-right; rotate about the barrel since Skia's RotateRadians
        // takes no pivot — translate in, rotate, translate back).
        _fill.Color = new SKColor(0x37, 0x3B, 0x45, alpha);
        canvas.Save();
        canvas.Translate(bx, by);
        canvas.RotateRadians(-0.5f);
        canvas.Translate(-bx, -by);
        canvas.DrawRoundRect(new SKRect(bx - (0.10f * h), by - (0.05f * h), bx + (0.06f * h), by + (0.05f * h)), h * 0.02f, h * 0.02f, _fill);
        _fill.Color = new SKColor(0xB9, 0xC0, 0xCC, alpha);
        canvas.DrawRoundRect(new SKRect(bx + (0.04f * h), by - (0.07f * h), bx + (0.10f * h), by + (0.07f * h)), h * 0.02f, h * 0.02f, _fill);
        canvas.Restore();
    }

    private void DrawIceCream(SKCanvas canvas, float h, float a)
    {
        // A waffle cone with two scoops, held up to the right.
        byte alpha = (byte)(255 * a);
        float cx = 0.52f * h, tipY = -0.30f * h;

        // Cone.
        using (var cone = new SKPath())
        {
            cone.MoveTo(cx, tipY);
            cone.LineTo(cx - (0.08f * h), tipY - (0.16f * h));
            cone.LineTo(cx + (0.08f * h), tipY - (0.16f * h));
            cone.Close();
            _fill.Color = new SKColor(0xD9, 0xA5, 0x5B, alpha);
            canvas.DrawPath(cone, _fill);
            _stroke.Color = new SKColor(0xB6, 0x82, 0x3E, alpha);
            _stroke.StrokeWidth = h * 0.008f;
            canvas.DrawLine(cx - (0.04f * h), tipY - (0.04f * h), cx + (0.02f * h), tipY - (0.13f * h), _stroke);
            canvas.DrawLine(cx + (0.04f * h), tipY - (0.04f * h), cx - (0.02f * h), tipY - (0.13f * h), _stroke);
        }

        // Two scoops.
        _fill.Color = new SKColor(0xF5, 0xB7, 0xC9, alpha);       // strawberry
        canvas.DrawCircle(cx, tipY - (0.18f * h), 0.085f * h, _fill);
        _fill.Color = new SKColor(0xFC, 0xF1, 0xD7, alpha);       // vanilla
        canvas.DrawCircle(cx, tipY - (0.28f * h), 0.075f * h, _fill);
    }

    private void DrawMate(SKCanvas canvas, float h, float a)
    {
        // The Argentine classic: a gourd with a metal bombilla, a little steam.
        byte alpha = (byte)(255 * a);
        float cx = 0.50f * h, cy = -0.40f * h, r = 0.11f * h;

        // Gourd.
        _fill.Color = new SKColor(0x6B, 0x44, 0x26, alpha);
        canvas.DrawCircle(cx, cy, r, _fill);
        _fill.Color = new SKColor(0x4E, 0x30, 0x1A, alpha);       // base shadow
        canvas.DrawOval(new SKRect(cx - r, cy + (r * 0.25f), cx + r, cy + (r * 1.05f)), _fill);
        // Yerba top.
        _fill.Color = new SKColor(0x6F, 0x8A, 0x3C, alpha);
        canvas.DrawOval(new SKRect(cx - (r * 0.8f), cy - (r * 0.95f), cx + (r * 0.8f), cy - (r * 0.35f)), _fill);
        // Bombilla.
        _stroke.Color = new SKColor(0xC9, 0xCE, 0xD6, alpha);
        _stroke.StrokeWidth = h * 0.018f;
        canvas.DrawLine(cx + (r * 0.2f), cy - (r * 0.6f), cx + (r * 1.3f), cy - (r * 1.5f), _stroke);
        // Steam.
        _stroke.Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(90 * a));
        _stroke.StrokeWidth = h * 0.01f;
        using var steam = new SKPath();
        steam.MoveTo(cx, cy - r);
        steam.QuadTo(cx + (0.04f * h), cy - (r * 1.6f), cx, cy - (r * 2.1f));
        canvas.DrawPath(steam, _stroke);
    }

    private void DrawBinoculars(SKCanvas canvas, float h, float a)
    {
        // Two barrels held up to the eyes, with bright glass.
        byte alpha = (byte)(255 * a);
        float ey = -0.64f * h, dx = 0.13f * h, r = 0.10f * h;

        for (int i = -1; i <= 1; i += 2)
        {
            float bx = i * dx;
            _fill.Color = new SKColor(0x2B, 0x2E, 0x36, alpha);
            canvas.DrawRoundRect(new SKRect(bx - (r * 0.8f), ey - (r * 1.1f), bx + (r * 0.8f), ey + (r * 1.1f)), r * 0.4f, r * 0.4f, _fill);
            _fill.Color = new SKColor(0x9B, 0xD2, 0xFF, alpha);  // glass
            canvas.DrawCircle(bx, ey + (r * 0.7f), r * 0.55f, _fill);
        }

        // Bridge.
        _fill.Color = new SKColor(0x2B, 0x2E, 0x36, alpha);
        canvas.DrawRect(new SKRect(-dx, ey - (r * 0.25f), dx, ey + (r * 0.25f)), _fill);
    }

    private void DrawPaintBrush(SKCanvas canvas, float h, float a)
    {
        // A brush held diagonally with a coloured tip and a little paint dab.
        byte alpha = (byte)(255 * a);
        float bx = 0.42f * h, by = -0.34f * h, tx = 0.60f * h, ty = -0.66f * h;

        // Handle.
        _stroke.Color = new SKColor(0x8A, 0x5A, 0x32, alpha);
        _stroke.StrokeWidth = h * 0.03f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(bx, by, tx - (0.05f * h), ty + (0.05f * h), _stroke);
        // Ferrule.
        _stroke.Color = new SKColor(0xB9, 0xC0, 0xCC, alpha);
        _stroke.StrokeWidth = h * 0.034f;
        canvas.DrawLine(tx - (0.08f * h), ty + (0.08f * h), tx - (0.03f * h), ty + (0.03f * h), _stroke);
        _stroke.StrokeCap = SKStrokeCap.Round; // restore the shared paint's default cap
        // Bristles.
        _fill.Color = new SKColor(0xE0, 0x49, 0x4B, alpha);
        using (var tip = new SKPath())
        {
            tip.MoveTo(tx - (0.05f * h), ty + (0.05f * h));
            tip.LineTo(tx + (0.04f * h), ty - (0.03f * h));
            tip.LineTo(tx + (0.01f * h), ty + (0.06f * h));
            tip.Close();
            canvas.DrawPath(tip, _fill);
        }
        // Paint dab.
        canvas.DrawCircle(tx + (0.05f * h), ty - (0.02f * h), h * 0.02f, _fill);
    }

    private void DrawToyHammer(SKCanvas canvas, float h, float a)
    {
        // A chunky toy hammer: wooden handle + a bright two-tone head.
        byte alpha = (byte)(255 * a);
        float hx = 0.50f * h;

        // Handle.
        _fill.Color = new SKColor(0x9A, 0x67, 0x39, alpha);
        canvas.DrawRoundRect(new SKRect(hx - (0.022f * h), -0.58f * h, hx + (0.022f * h), -0.26f * h), h * 0.02f, h * 0.02f, _fill);
        // Head.
        _fill.Color = new SKColor(0xE0, 0x49, 0x4B, alpha);
        canvas.DrawRoundRect(new SKRect(hx - (0.12f * h), -0.70f * h, hx + (0.12f * h), -0.56f * h), h * 0.03f, h * 0.03f, _fill);
        _fill.Color = new SKColor(0x2E, 0x86, 0xD0, alpha);
        canvas.DrawRoundRect(new SKRect(hx - (0.12f * h), -0.63f * h, hx + (0.12f * h), -0.56f * h), h * 0.02f, h * 0.02f, _fill);
    }

    private void DrawSword(SKCanvas canvas, float h, float a)
    {
        // A cardboard sword held up — tan blade, a crossguard and a little grip.
        byte alpha = (byte)(255 * a);
        float gx = 0.44f * h, gy = -0.30f * h;   // grip base
        float tx = 0.66f * h, ty = -0.98f * h;   // blade tip

        // Blade.
        _stroke.Color = new SKColor(0xD8, 0xC4, 0x9A, alpha);
        _stroke.StrokeWidth = h * 0.045f;
        _stroke.StrokeCap = SKStrokeCap.Round;
        canvas.DrawLine(gx + (0.06f * h), gy - (0.06f * h), tx, ty, _stroke);
        // Centre line.
        _stroke.Color = new SKColor(0xB6, 0x9E, 0x6E, alpha);
        _stroke.StrokeWidth = h * 0.012f;
        canvas.DrawLine(gx + (0.07f * h), gy - (0.07f * h), tx - (0.01f * h), ty + (0.02f * h), _stroke);
        _stroke.StrokeCap = SKStrokeCap.Round; // restore the shared paint's default cap
        // Crossguard.
        _stroke.Color = new SKColor(0x9A, 0x67, 0x39, alpha);
        _stroke.StrokeWidth = h * 0.02f;
        canvas.DrawLine(gx - (0.04f * h), gy - (0.12f * h), gx + (0.14f * h), gy + (0.02f * h), _stroke);
        // Grip.
        _stroke.StrokeWidth = h * 0.03f;
        canvas.DrawLine(gx, gy, gx + (0.06f * h), gy - (0.06f * h), _stroke);
    }

    private void DrawKite(SKCanvas canvas, float h, float a)
    {
        // A diamond kite up and to the right, with a little bow tail and a string.
        byte alpha = (byte)(255 * a);
        float cx = 0.56f * h, cy = -1.05f * h, w = 0.16f * h, ht = 0.22f * h;

        // String down to the body.
        _stroke.Color = new SKColor(0x66, 0x66, 0x66, alpha);
        _stroke.StrokeWidth = h * 0.007f;
        canvas.DrawLine(cx, cy + ht, 0.22f * h, -0.5f * h, _stroke);

        using (var kite = new SKPath())
        {
            kite.MoveTo(cx, cy - ht);
            kite.LineTo(cx + w, cy);
            kite.LineTo(cx, cy + ht);
            kite.LineTo(cx - w, cy);
            kite.Close();
            _fill.Color = new SKColor(0x46, 0xB1, 0x8A, alpha);
            canvas.DrawPath(kite, _fill);
            _stroke.Color = new SKColor(0x2C, 0x7C, 0x5E, alpha);
            _stroke.StrokeWidth = h * 0.008f;
            canvas.DrawLine(cx, cy - ht, cx, cy + ht, _stroke);
            canvas.DrawLine(cx - w, cy, cx + w, cy, _stroke);
        }

        // Tail bows.
        _fill.Color = new SKColor(0xE7, 0x9A, 0x3C, alpha);
        for (int i = 1; i <= 3; i++)
        {
            float by = cy + ht + (i * 0.06f * h);
            float bx = cx + (MathF.Sin(i * 1.3f) * 0.03f * h);
            canvas.DrawCircle(bx, by, h * 0.014f, _fill);
        }
    }

    private void DrawWateringCan(SKCanvas canvas, float h, float a)
    {
        // A watering can tilted to pour, with a few drops falling.
        byte alpha = (byte)(255 * a);
        float cx = 0.46f * h, cy = -0.42f * h, w = 0.15f * h, ht = 0.13f * h;

        // Body.
        _fill.Color = new SKColor(0x4F, 0x9E, 0xC4, alpha);
        canvas.DrawRoundRect(new SKRect(cx - (w / 2f), cy - (ht / 2f), cx + (w / 2f), cy + (ht / 2f)), h * 0.02f, h * 0.02f, _fill);
        // Handle.
        _stroke.Color = new SKColor(0x3C, 0x7E, 0x9E, alpha);
        _stroke.StrokeWidth = h * 0.016f;
        canvas.DrawArc(new SKRect(cx - (w * 0.3f), cy - (ht * 1.3f), cx + (w * 0.3f), cy - (ht * 0.1f)), 200, 140, false, _stroke);
        // Spout.
        _stroke.StrokeWidth = h * 0.02f;
        canvas.DrawLine(cx + (w * 0.4f), cy, cx + (w * 0.95f), cy - (ht * 0.4f), _stroke);
        // Rose (sprinkler head).
        _fill.Color = new SKColor(0x3C, 0x7E, 0x9E, alpha);
        canvas.DrawCircle(cx + (w * 0.95f), cy - (ht * 0.4f), h * 0.022f, _fill);
        // Drops.
        _fill.Color = new SKColor(0x9B, 0xD2, 0xFF, alpha);
        for (int i = 0; i < 3; i++)
        {
            canvas.DrawCircle(cx + (w * 0.95f) + (i * 0.02f * h), cy + (i * 0.05f * h), h * 0.012f, _fill);
        }
    }

    private void DrawHeldUmbrella(SKCanvas canvas, float h, float a)
    {
        // An open umbrella held up-right: a scalloped red canopy on a thin pole with a J-handle.
        byte alpha = (byte)(255 * a);
        float topX = 0.46f * h, topY = -1.04f * h;  // apex of the canopy
        float r = 0.24f * h;                          // canopy radius
        float baseY = topY + (0.16f * h);             // where the canopy rim sits

        // Pole down to a curved handle.
        _stroke.Color = new SKColor(0x6A, 0x4A, 0x32, alpha);
        _stroke.StrokeWidth = h * 0.018f;
        float poleBottom = -0.34f * h;
        canvas.DrawLine(topX, topY, topX, poleBottom, _stroke);
        canvas.DrawArc(new SKRect(topX - (0.10f * h), poleBottom - (0.02f * h), topX, poleBottom + (0.06f * h)), 0, 180, false, _stroke);

        // Canopy: a half-dome with a scalloped lower edge.
        using var dome = new SKPath();
        dome.MoveTo(topX - r, baseY);
        dome.ArcTo(new SKRect(topX - r, topY, topX + r, baseY + (r * 0.9f)), 180, 180, false);
        // Scallops along the bottom rim (4 little arcs).
        const int scallops = 4;
        float step = (2f * r) / scallops;
        for (int i = 0; i < scallops; i++)
        {
            float sx = topX + r - (i * step);
            dome.QuadTo(sx - (step * 0.5f), baseY + (0.05f * h), sx - step, baseY);
        }

        dome.Close();
        _fill.Color = new SKColor(0xD2, 0x3F, 0x3F, alpha);
        canvas.DrawPath(dome, _fill);

        // A couple of rib lines + a tip finial for definition.
        _stroke.Color = new SKColor(0xA8, 0x2E, 0x2E, alpha);
        _stroke.StrokeWidth = h * 0.01f;
        canvas.DrawLine(topX, topY, topX - (r * 0.55f), baseY, _stroke);
        canvas.DrawLine(topX, topY, topX + (r * 0.55f), baseY, _stroke);
        _fill.Color = new SKColor(0xA8, 0x2E, 0x2E, alpha);
        canvas.DrawCircle(topX, topY - (0.02f * h), h * 0.018f, _fill);
    }

    private void DrawGuitar(SKCanvas canvas, float h, float a)
    {
        // A little acoustic guitar held across the body, neck pointing up-right.
        byte alpha = (byte)(255 * a);
        float bx = 0.30f * h, by = -0.30f * h;       // body (sound box) centre
        float bodyR = 0.16f * h;

        // Body: a figure-eight from two overlapping ovals (lower bout bigger).
        _fill.Color = new SKColor(0xC8, 0x88, 0x3E, alpha);
        canvas.DrawOval(new SKRect(bx - (bodyR * 0.78f), by - (bodyR * 0.55f), bx + (bodyR * 0.78f), by + (bodyR * 0.1f)), _fill);
        canvas.DrawOval(new SKRect(bx - bodyR, by - (bodyR * 0.1f), bx + bodyR, by + bodyR), _fill);

        // Sound hole.
        _fill.Color = new SKColor(0x4A, 0x2E, 0x16, alpha);
        canvas.DrawCircle(bx, by + (bodyR * 0.28f), h * 0.04f, _fill);

        // Neck + head up to the right.
        _stroke.Color = new SKColor(0x7A, 0x52, 0x2C, alpha);
        _stroke.StrokeWidth = h * 0.05f;
        float nx = bx + (0.34f * h), ny = by - (0.34f * h);
        canvas.DrawLine(bx + (bodyR * 0.5f), by - (bodyR * 0.4f), nx, ny, _stroke);
        _fill.Color = new SKColor(0x4A, 0x2E, 0x16, alpha);
        canvas.DrawCircle(nx, ny, h * 0.035f, _fill); // headstock

        // A few strings along the neck.
        _stroke.Color = new SKColor(0xF0, 0xE8, 0xD8, (byte)(200 * a));
        _stroke.StrokeWidth = h * 0.006f;
        for (int i = -1; i <= 1; i++)
        {
            float off = i * 0.012f * h;
            canvas.DrawLine(bx + (bodyR * 0.45f) + off, by - (bodyR * 0.45f) - off, nx + off, ny + off, _stroke);
        }
    }

    private void DrawCamera(SKCanvas canvas, float h, float a)
    {
        // A retro camera held up to "take a photo", with a flash pop.
        byte alpha = (byte)(255 * a);
        float cx = 0.50f * h, cy = -0.52f * h, w = 0.20f * h, ht = 0.13f * h;

        // Body.
        _fill.Color = new SKColor(0x35, 0x38, 0x40, alpha);
        canvas.DrawRoundRect(new SKRect(cx - (w / 2f), cy - (ht / 2f), cx + (w / 2f), cy + (ht / 2f)), h * 0.02f, h * 0.02f, _fill);
        // Top viewfinder hump.
        canvas.DrawRoundRect(new SKRect(cx - (w * 0.18f), cy - (ht * 0.85f), cx + (w * 0.12f), cy - (ht * 0.4f)), h * 0.01f, h * 0.01f, _fill);
        // Lens.
        _fill.Color = new SKColor(0x1B, 0x1D, 0x22, alpha);
        canvas.DrawCircle(cx, cy + (ht * 0.05f), h * 0.05f, _fill);
        _fill.Color = new SKColor(0x4F, 0x7E, 0xB0, (byte)(220 * a)); // glass
        canvas.DrawCircle(cx, cy + (ht * 0.05f), h * 0.03f, _fill);
        // Shutter button.
        _fill.Color = new SKColor(0xC0, 0x40, 0x40, alpha);
        canvas.DrawCircle(cx + (w * 0.32f), cy - (ht * 0.55f), h * 0.014f, _fill);
        // Flash pop (up-right of the camera).
        _fill.Color = new SKColor(0xFF, 0xFB, 0xD0, (byte)(190 * a));
        DrawSparkleStar(canvas, cx + (w * 0.45f), cy - (ht * 1.1f), h * 0.06f, _fill.Color);
    }

    private void DrawTrophy(SKCanvas canvas, float h, float a)
    {
        // A golden trophy held up proudly, with a sparkle.
        byte alpha = (byte)(255 * a);
        float cx = 0.50f * h, cupTop = -0.74f * h, cupBottom = -0.52f * h, cupW = 0.18f * h;
        var gold = new SKColor(0xE6, 0xB8, 0x3A, alpha);
        var goldDark = new SKColor(0xB8, 0x8E, 0x22, alpha);

        // Cup bowl (a trapezoid that narrows downward).
        using (var cup = new SKPath())
        {
            cup.MoveTo(cx - (cupW / 2f), cupTop);
            cup.LineTo(cx + (cupW / 2f), cupTop);
            cup.LineTo(cx + (cupW * 0.28f), cupBottom);
            cup.LineTo(cx - (cupW * 0.28f), cupBottom);
            cup.Close();
            _fill.Color = gold;
            canvas.DrawPath(cup, _fill);
        }

        // Handles (two C-arcs).
        _stroke.Color = goldDark;
        _stroke.StrokeWidth = h * 0.018f;
        canvas.DrawArc(new SKRect(cx - (cupW * 0.85f), cupTop, cx - (cupW * 0.3f), cupBottom), 90, 180, false, _stroke);
        canvas.DrawArc(new SKRect(cx + (cupW * 0.3f), cupTop, cx + (cupW * 0.85f), cupBottom), 270, 180, false, _stroke);

        // Stem + base.
        _fill.Color = goldDark;
        canvas.DrawRect(new SKRect(cx - (0.02f * h), cupBottom, cx + (0.02f * h), cupBottom + (0.06f * h)), _fill);
        canvas.DrawRoundRect(new SKRect(cx - (0.07f * h), cupBottom + (0.06f * h), cx + (0.07f * h), cupBottom + (0.10f * h)), h * 0.01f, h * 0.01f, _fill);

        // Shine + a tiny sparkle.
        _fill.Color = new SKColor(0xFF, 0xF4, 0xC8, (byte)(150 * a));
        canvas.DrawRect(new SKRect(cx - (cupW * 0.32f), cupTop + (0.02f * h), cx - (cupW * 0.18f), cupBottom - (0.02f * h)), _fill);
        DrawSparkleStar(canvas, cx + (cupW * 0.5f), cupTop - (0.02f * h), h * 0.045f, new SKColor(0xFF, 0xFF, 0xFF, (byte)(220 * a)));
    }

    /// <summary>A simple 4-point twinkle (two crossed tapered spokes) used by props that pop.</summary>
    private void DrawSparkleStar(SKCanvas canvas, float x, float y, float r, SKColor color)
    {
        _stroke.Color = color;
        _stroke.StrokeWidth = r * 0.28f;
        canvas.DrawLine(x - r, y, x + r, y, _stroke);
        canvas.DrawLine(x, y - r, x, y + r, _stroke);
        _stroke.StrokeWidth = r * 0.16f;
        float d = r * 0.6f;
        canvas.DrawLine(x - d, y - d, x + d, y + d, _stroke);
        canvas.DrawLine(x - d, y + d, x + d, y - d, _stroke);
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
