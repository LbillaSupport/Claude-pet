# The Animation Engine

Claude Buddy has no animation frames. Instead it has a **procedural pose engine**: every
frame, a target body pose is synthesised from the current state and the *displayed* pose
is eased toward it. This is what makes transitions seamless and motion organic.

## The `Pose`

A `Pose` (see [`Animation/Pose.cs`](../src/ClaudeBuddy/Animation/Pose.cs)) is a flat bag
of normalised parameters describing the whole character on one frame:

| Group | Fields |
| --- | --- |
| Body | `BodyOffset`, `BodyLean`, `WholeBodyRotation`, `BodyScaleX/Y`, `HeadTilt` |
| Eyes | `EyeOpen`, `EyeLookX/Y`, `HappyEyes`, `StarEyes` |
| Face | `MouthOpen`, `MouthCurve`, `BrowAngle`, `Blush` |
| Limbs | `ArmLeft`, `ArmRight`, `LegPhase`, `StrideAmount` |
| Props | `CoffeeProp`, `BookProp`, `UmbrellaProp`, `ThinkBubble`, `SleepBubble` |
| Global | `Alpha` |

The `CharacterArtist` reads *only* the `Pose` (plus a `SkinPalette`) to draw — so the
renderer and the animator are fully decoupled.

## The pipeline (per frame)

```
AnimationState  ──►  BuildTarget()     synthesise the ideal pose for this state
EmotionState    ──►  ApplyMoodFace()   layer the mood onto neutral faces
                ──►  ApplyBlink()      automatic, randomly-timed blinks
                ──►  ApplyBreathing()  subtle idle breathing overlay
current pose    ──►  Blend()           ease each field toward the target
```

### 1. `BuildTarget`

A `switch` over the 30+ `AnimationState`s sets the raw target values. Cyclic motion uses
two clocks the animator keeps:

- `_phase` — a continuous clock scaled by the mascot's animation speed (used for walk
  cycles, dancing, breathing).
- `_stateTime` — resets whenever the state changes (used for one-shot timing like a wave
  or a stretch).

For example, the walk cycle drives the legs with `sin(LegPhase)`, bobs the body with
`|sin|`, and swings the arms in anti-phase.

### 2. `ApplyMoodFace`

For neutral states (idle, walking, looking around) the current `Mood` nudges the mouth,
brows, blush and eye style — so a *happy* idle and a *sleepy* idle look different without
needing separate states. High happiness adds a subtle star-sparkle to the eyes.

### 3. `ApplyBlink`

A timer fires a quick eyelid close/open every 2–6 seconds, skipped for states that
already script the eyes (sleep, yawn, surprised…).

### 4. `ApplyBreathing`

Idle/sit/stand get a tiny `sin`-based scale and bob so the character is never perfectly
still.

### 5. `Blend`

This is the heart of the smoothness. Each field is moved toward its target with
`MathUtil.Damp` — a frame-rate-independent exponential ease:

```csharp
current = Lerp(current, target, 1 - exp(-speed * dt));
```

Different fields use different speeds (eyes snap quickly at ~26, the body sways slowly at
~16), which yields natural, layered motion. Continuous values like `LegPhase` and
`WholeBodyRotation` are copied directly so cycles stay perfectly periodic.

## Squash & stretch

Two squash sources multiply together:

- **Physics squash** (`Mascot.SquashX/Y`) — an impulse set by the `PhysicsSystem` on
  landing or jump anticipation, eased back to neutral over time.
- **Pose squash** (`Pose.BodyScaleX/Y`) — authored per state (e.g. a tall stretch).

The artist applies them as a scale **pivoted at the feet**, so a landing squashes the
character down onto the floor exactly like a bouncy ball.

## Adding a new animation

1. Add a value to the `AnimationState` enum.
2. Add a `case` in `Animator.BuildTarget` that poses it.
3. Reference it from a `BehaviorDefinition` in the catalogue (or a mod).

No art assets, no atlases — just a few lines describing how the body should sit.
