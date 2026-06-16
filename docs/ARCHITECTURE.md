# Architecture

Claude Buddy is a small real-time simulation rendered into a transparent, always-on-top
Win32 window. The design follows SOLID and clean-architecture ideas: many small,
single-responsibility systems, wired together by dependency injection, orchestrated by
one conductor (`MascotEngine`).

## High-level diagram

```
                         ┌──────────────────────────────────────────────┐
                         │                  Program.cs                   │
                         │  builds the DI container, creates the window  │
                         │  + renderer, runs the loop                    │
                         └───────────────────────┬──────────────────────┘
                                                 │
                                   ┌─────────────▼─────────────┐
                                   │       MascotEngine        │  the conductor
                                   │  (advances systems / frame)│
                                   └──┬───────┬───────┬───────┬─┘
            input / window           │       │       │       │            rendering
   ┌──────────────────────┐         │       │       │       │   ┌────────────────────────┐
   │ LayeredWindow (Win32) │◄────────┘       │       │       └──►│ SkiaRenderer (DIB+ULW) │
   │  message pump + loop  │  events         │       │           │ CharacterArtist        │
   │ CursorTracker         │                 │       │           │ ParticleRenderer       │
   └──────────────────────┘                  │       │           └────────────────────────┘
                                              │       │
              simulation                      │       │              content & services
   ┌───────────────────────────────┐         │       │     ┌──────────────────────────────┐
   │ BehaviorController + Selector  │◄────────┘       └────►│ SettingsService (JSON)        │
   │ BehaviorCatalog (data)         │                       │ SkinManager / ModManager      │
   │ EmotionState · DailyRoutine    │                       │ ClaudeLauncher · StartupService│
   │ PhysicsSystem · ParticleSystem │                       │ AudioService · Achievements    │
   │ Animator (Pose synthesis)      │                       └──────────────────────────────┘
   │ World · Mascot (state)         │
   └───────────────────────────────┘
```

Data flows **one way** each frame: input → behaviour → physics → emotion → animation →
particles → render. Nothing reaches back up, which keeps the simulation predictable.

## The frame loop

`LayeredWindow.RunLoop` pumps Win32 messages and calls `MascotEngine.Frame()` at a paced
60 FPS (using `timeBeginPeriod(1)` for accurate sleeps). Each frame:

1. `GameTime.Tick()` produces a clamped delta time.
2. `CursorTracker` polls the global mouse position & velocity.
3. Drag / click-arbitration timers advance (single-vs-double-click, throw).
4. `BehaviorController` picks/continues a behaviour and steers locomotion.
5. `PhysicsSystem` integrates gravity, momentum, collisions and squash-&-stretch.
6. `EmotionState` and `DailyRoutine` update mood and energy.
7. `Animator` synthesises a target `Pose` and **eases** the current pose toward it.
8. `ParticleSystem` and weather advance.
9. Housekeeping (Claude open/close polling, achievements, autosave) runs ~1×/second.
10. `SkiaRenderer` clears the canvas, the `CharacterArtist` and `ParticleRenderer` paint
    it, and `UpdateLayeredWindow` blits it to the desktop with per-pixel alpha.

## Why a Win32 layered window (not WinUI 3)

A desktop pet needs a borderless, **per-pixel transparent**, click-through,
always-on-top surface. `UpdateLayeredWindow` with a premultiplied BGRA DIB is the
battle-tested way to get that on Windows: clean anti-aliased edges over any wallpaper,
automatic click-through on transparent pixels, and no flicker. SkiaSharp renders
straight into the DIB's memory (zero-copy), so the whole pipeline is just:

```
SkiaSharp  ──draws──►  DIB section (BGRA, premultiplied)  ──UpdateLayeredWindow──►  Desktop
```

## Project layout

```
ClaudeBuddy.sln
src/ClaudeBuddy/
├─ Program.cs                 # composition root (DI) + entry point
├─ app.manifest               # Per-Monitor-V2 DPI, asInvoker
├─ appsettings.default.json   # shipped defaults
├─ Core/                      # Vector2, Easing, Rng, GameTime, MathUtil, enums, constants
├─ Engine/                    # World, Mascot, MascotEngine (orchestration)
├─ Animation/                 # Pose, Animator (pose synthesis + blending)
├─ Behaviors/                 # Definition, Catalog (data), Selector, Controller
├─ Emotions/                  # EmotionState
├─ Routine/                   # DailyRoutine, RoutineProfile
├─ Physics/                   # PhysicsSystem (gravity, throw, collisions)
├─ Particles/                 # ParticleSystem, Particle
├─ Input/                     # CursorTracker
├─ Rendering/                 # LayeredWindow, SkiaRenderer, CharacterArtist, ParticleRenderer
├─ Services/                  # Settings, ClaudeLauncher, Startup, Audio
├─ Achievements/              # AchievementService + catalogue
├─ Skins/                     # Skin, SkinManifest, SkinManager
├─ Mods/                      # ModManifest, ModManager
├─ Settings/                  # AppSettings model
├─ UI/                        # ContextMenu (TrackPopupMenu)
└─ Utilities/                 # NativeMethods (all P/Invoke lives here)
Skins/                        # sample skins (copied next to the exe)
Mods/                         # sample behaviour-pack mod
docs/                         # this documentation
```

## Key design choices

- **Behaviours are data.** The 45+ behaviours are `BehaviorDefinition` records in a
  catalogue, mapped onto a handful of animation/locomotion archetypes. Adding one is
  data, not code — which is exactly how mods extend the character at runtime.
- **The character is maths.** No assets to go missing; DPI independence for free.
- **Everything eases.** `MathUtil.Damp` (frame-rate-independent smoothing) and the
  `Easing` library are used pervasively so motion is always organic.
- **All interop is quarantined** in `Utilities/NativeMethods.cs`; the rest of the code
  is portable, testable C#.
