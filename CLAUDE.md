# CLAUDE.md — Claude Buddy

Working notes for Claude Code. Read this first; it captures the decisions, file map,
and gotchas so you don't have to re-derive them. Keep it updated when you change
architecture-level things.

> Audience: the assistant. The user (`lautabill@gmail.com`) writes in Spanish — reply
> in Spanish. Code, comments and docs stay in English to match the existing codebase.

---

## What this is

**Claude Buddy** — a wholesome native-Windows desktop mascot. The character is
**"Claw'd"**, the classic Claude Code critter: a flat **terracotta square block** with
two **black square eyes** and **four stubby legs**. It walks the desktop, reacts to the
cursor, can be petted/thrown, opens Claude Desktop on click, and reacts while you type.

The whole character is **drawn procedurally with vector shapes (SkiaSharp)** — there are
**no sprite/PNG assets**. A "skin" is just a colour palette (JSON), which is why skins are
tiny and infinite.

---

## Build & run (verified working)

- **No `dotnet` on PATH.** Use the full path: `C:\Program Files\dotnet\dotnet.exe`
  (SDK 9.0.315 installed).
- Visual Studio: only **Build Tools 18** are present at
  `C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools` (MSBuild exists; no full IDE).
  Prefer the `dotnet` CLI.

```bash
# Build
"C:\Program Files\dotnet\dotnet.exe" build "src\ClaudeBuddy\ClaudeBuddy.csproj" -c Release

# Output exe
src\ClaudeBuddy\bin\Release\net9.0-windows\win-x64\ClaudeBuddy.exe
```

- It's a long-running desktop app: **launch with `Start-Process`** (don't block the tool).
  **Stop with `Stop-Process -Name ClaudeBuddy -Force`** before rebuilding (file lock).
- **Single-instance** guarded by a mutex (`ClaudeBuddy.SingleInstance.v1`) — a second
  launch silently exits.
- Stable footprint: ~40 MB RAM, no crash log on a clean run.

### Distribution & auto-update (Velopack)
Shipping to non-technical users is done with **Velopack** (modern Squirrel successor) —
NOT a `.bat`/MSI. `VelopackApp.Build().Run()` is the **first line of `Main`** (intercepts
install/update/uninstall hooks; no-op on a dev run). `Services/UpdateService.cs` checks
**GitHub Releases** in the background on startup (`GithubSource(RepoUrl)`), downloads any
newer release, and `ApplyUpdatesAndRestart` swaps it in **on exit** (in `engine.Shutdown`
path). Gated by `AppSettings.AutoUpdate` (default true). `manager.IsInstalled` is false on
a plain dev run, so updates only ever happen for an installed copy.
- **Set the repo**: `UpdateService.RepoUrl` constant (`https://github.com/<owner>/<repo>`).
- **Cut a release**: `.\build-release.ps1 -Version X.Y.Z` — publishes self-contained, then
  `vpk pack` produces `.\Releases\` (`ClaudeBuddy-win-Setup.exe` = the friendly installer +
  full/delta `.nupkg` + `releases.win.json`). Upload **all** of `Releases\` to a GitHub
  Release tagged `vX.Y.Z`. Users run `Setup.exe`; existing installs self-update on restart.
- The script auto-installs the `vpk` global tool if missing.

### Verifying a visual change (the screenshot loop)
The mascot is small and wanders, so capture the **primary screen** and crop:
```powershell
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
$p=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp=New-Object System.Drawing.Bitmap $p.Width,$p.Height
$g=[System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($p.X,$p.Y,0,0,(New-Object System.Drawing.Size($p.Width,$p.Height)))
$bmp.Save("$PWD\_cap.png")
```
Then `Read` the PNG, find Claw'd, and crop+upscale (NearestNeighbor) a ~300px region
around it for a clear look. Delete the temp `_*.png` files afterward — keep the repo tidy.
To exercise the **typing** reaction safely, send `keybd_event` for **VK_SHIFT (0x10)**
repeatedly — it produces no characters but the hook counts it.

---

## Tech stack & the one big decision

- **.NET 9** (`net9.0-windows`, `win-x64`, `WinExe`), C# latest, nullable on.
- **NOT WinUI 3 / WPF / WinForms** even though the original brief asked for WinUI.
  Reason: a per-pixel-alpha, click-through, always-on-top desktop overlay is exactly what
  WinUI fights you on. Instead we host a **raw Win32 layered window** and blit a
  Skia-rendered ARGB bitmap with **`UpdateLayeredWindow`**. This gives true transparency,
  soft anti-aliased edges over any wallpaper, and automatic click-through on fully
  transparent pixels (so clicks land only on the mascot). If asked to "switch to WinUI,"
  push back — this is deliberate.
- **SkiaSharp 2.88.8** is the only rendering dep (ships native `libSkiaSharp` for win-x64;
  software raster works out of the box). Plus `Microsoft.Extensions.DependencyInjection`.
- **DPI:** process is Per-Monitor-V2 aware (`app.manifest` + `SetProcessDpiAwarenessContext`).
  All world coordinates are **physical pixels**. The renderer multiplies design units by
  `window.DpiScale`.
- Settings persist to **`%AppData%\ClaudeBuddy\settings.json`**; user Skins/Mods live in
  `%AppData%\ClaudeBuddy\Skins` and `...\Mods`. Crashes log to `%AppData%\ClaudeBuddy\crash.log`.

---

## Architecture (one file = one job)

Composition root: `src/ClaudeBuddy/Program.cs` — builds the DI container (everything is a
singleton), creates the `LayeredWindow`, the `SkiaRenderer`, then `MascotEngine.Initialize`
+ `window.RunLoop(engine.Frame)`. **Register new services in `Program.cs`** and add them to
the `MascotEngine` constructor (DI resolves by type; param order is irrelevant but the
assignment must match).

Game loop: single-threaded `PeekMessage`-based loop in `Rendering/LayeredWindow.cs`,
paced to 60 FPS with a Stopwatch (`timeBeginPeriod(1)`), delta-time fed to `engine.Frame`.
Input (`WM_*` mouse, display change) is dispatched on this same thread → no threading
hazards. The keyboard hook callback also fires on this thread (it pumps messages).

`Engine/MascotEngine.cs` — **the conductor**. The only class that knows about everything.
`Frame()` order each tick: cursor+keyboard update → routine eval → interaction timing →
surprise check → `behavior.Update` + `physics.Step` + `UpdateTypingReaction` → emotion →
`animator.Update` → particles/weather/stats → periodic poll/save → `Render`.

| Area | Folder | Key files / notes |
|---|---|---|
| Procedural art | `Rendering/` | `CharacterArtist.cs` draws Claw'd. `LayeredWindow.cs` = Win32 window + loop + input events. `SkiaRenderer.cs` = DIB + `UpdateLayeredWindow`. `ParticleRenderer.cs`. |
| Animation engine | `Animation/` | `Animator.cs` (target synthesis + blend), `Pose.cs` (the full per-frame description the artist reads). |
| Behaviour brain | `Behaviors/` | `BehaviorCatalog.cs` (all behaviours as **data**), `BehaviorSelector.cs` (weighted pick), `BehaviorController.cs` (runtime brain + locomotion). |
| Emotion / day | `Emotions/`, `Routine/` | `EmotionState`, `DailyRoutine` (maps wall-clock → `DayPhase`). |
| Physics | `Physics/` | `PhysicsSystem` (gravity, throw, squash, ground = taskbar-aware work area). `Engine/World.cs` = screen metrics. |
| Input | `Input/` | `CursorTracker.cs` (global mouse), `KeyboardActivityTracker.cs` (count-only key hook). |
| Particles | `Particles/` | `ParticleSystem` (`EmitHearts/Sparkles/Stars/Confetti/Dust/Magic`, weather motes). |
| Content | `Skins/`, `Mods/`, `Achievements/` | `SkinManager`, `ModManager` (append to catalog), `AchievementService`. |
| Services | `Services/` | `ClaudeLauncher`, `StartupService` (registry), `AudioService` (winmm `PlaySound`), `SettingsService`. |
| UI | `UI/` | `ContextMenu` (native popup menu). |
| Primitives | `Core/` | `Vector2`, `RgbaColor`, `MathUtil` (+ `Spring`), `Easing`, `Enums`, `EngineConstants`, `Rng`, `GameTime`. |
| Win32 | `Utilities/NativeMethods.cs` | **All** P/Invoke lives here; nothing else calls Win32 directly. |

Docs live in `docs/` (ARCHITECTURE, ANIMATION_ENGINE, BUILD, SKINS, MODS, ROADMAP, TODO)
and `README.md`. **No magic numbers** — tunables go in `Core/EngineConstants.cs`.

---

## How the character is drawn — `Rendering/CharacterArtist.cs`

`Draw(canvas, feet, groundY, height, facing, squashX, squashY, pose, palette, dpiScale)`.
Everything is a fraction of `height` (`h`) so it's crisp at any DPI/scale. Origin = feet at
(0,0), body extends upward in **negative Y**. Facing mirrors the whole canvas (`Scale(sign…)`),
and eye-look is corrected by `sign` so the gaze still points at the real cursor.

**Claw'd geometry constants (don't drift these apart — props depend on them):**
- Body: `BodyWidth = 0.78` (half-width `0.39h`), `BodyTop = -0.90`, `BodyBottom = -0.12`,
  `CornerRadius = 0.035` (barely rounded). Flat fill = `palette.Body`, subtle darker bottom
  band + faint top sheen.
- Legs: 4 at `LegCenters = {-0.255,-0.085,0.085,0.255}`, `LegWidth=0.12`, `LegLength=0.13`.
  Walk = rolling 4-beat lift from `pose.LegPhase`+`StrideAmount`. **Wave = outer leg lifts**
  when `pose.ArmLeft`/`ArmRight` > 1.6 (Claw'd has no arms — arm values only trigger this).
- Eyes: black squares, `EyeSize=0.18`, `EyeOffsetX=0.155`, `EyeCenterY=-0.62`. Variants:
  happy `^` chevrons (`HappyEyes>0.5`), star eyes (`StarEyes>0.5`), blink = flat bar
  (`EyeOpen<0.14`), else a square that nudges toward the cursor with a tiny white glint.
- Mouth: **hidden at rest** (Claw'd is mouthless); only drawn when `pose.MouthOpen>0.14`.
- Props (`DrawProps`) — repositioned to NOT overlap the wide square body:
  coffee `x=0.52h` (right of body) with steam; book held **low** `y=-0.18h` so it never
  covers the eyes; think bubble + umbrella float **above** the head (`y≈-1.12h`).
  **If you change `BodyWidth`/`BodyTop`, re-check these offsets.**

Classic skin colours: `Skins/Skin.cs` → `BuiltIn` uses an explicit palette
`Body=#C15F3C`, `BodyShadow=#A14A2B`, `Pupil=#1E1A17` (the iconic Claw'd, punchier than the
global `RgbaColor.ClaudeClay #D97A5A` brand clay which is still used by particles).
Persistence stores the skin **Id** (`""` = classic).

---

## How animation works — `Animation/Animator.cs`

There are **no sprite frames**. Each frame: `BuildTarget()` writes a desired `Pose` for the
current `AnimationState` (a neutral resting pose, then the state overrides only what it
needs), `ApplyMoodFace()` layers the mood, `ApplyBlink()`, `ApplyIdleLife()` (breathing +
weight-shift sway + micro head/eye motion on calm states), then `Blend()` eases `_current`
toward `_target`.

**Smoothness model (this is the "make it less robotic" knob):**
- Most channels use `MathUtil.Damp(current, target, rate, dt)` — exponential ease. Lower
  `rate` = slower, more in-between motion.
- **Squash/stretch (`BodyScaleX/Y`) use `Spring` (`Core/MathUtil.cs`)** — carries velocity
  so landings/pose-changes overshoot a touch and settle (Pixar feel). `Step(target, stiffness,
  damping, dt)`; damping below ~`2·√stiffness` = visible bounce. It sub-steps long frames so
  it can't explode.
- To make a behaviour feel alive, **add continuous sub-motion inside its `BuildTarget` case**
  using `_stateTime` (e.g. Drink = repeated sips, Read = page-scan + page-turn flick, Think =
  chin tap + wandering gaze). Don't just set a static target.

`Pose` fields (all normalised): BodyOffset, BodyLean, WholeBodyRotation, BodyScaleX/Y,
HeadTilt, EyeOpen, EyeLookX/Y, MouthOpen, MouthCurve, BrowAngle, Blush, ArmLeft/Right,
LegPhase, StrideAmount, HappyEyes, StarEyes, Alpha, CoffeeProp/UmbrellaProp/BookProp/
ThinkBubble/SleepBubble. **If you add a field, add it to `Pose.CopyFrom` and `Blend`.**

---

## Crab climbing & surface orientation — `Surface`

Claw'd is a crab: it can climb walls and hang from the ceiling, re-orienting so its feet
always point at the surface.
- `Mascot.Surface` (`Floor`/`LeftWall`/`RightWall`/`Ceiling`) + `Mascot.Climbing` +
  `Mascot.SurfaceAngle` (0 / +π/2 / −π/2 / π). `PhysicsSystem.Step` **early-returns when
  `Climbing`** (no gravity — the behaviour owns the position).
- The `climb` behaviour runs a small state machine in `BehaviorController.StepClimb`
  (`ToWall → Up → [CeilingOut → CeilingBack] → Down`). It walks to the nearest wall, latches
  on, ascends, optionally crawls the ceiling (**only from the left wall**, so the rotation
  only ever sweeps ≤90° at a time), then descends and detaches. It **self-terminates**
  (`_elapsed = _duration`) on reaching the floor, so its catalog duration (120 s) is just a
  backstop and it's **exempt from the behaviour-frequency division** in `Begin`.
- Rendering: `MascotEngine.Render` eases `_renderSurfaceAngle` toward `Mascot.SurfaceAngle`
  with `MathUtil.DampAngle` and rotates the whole canvas **around the contact point (feet)**.
  This is why the canvas is now **480 px with a centred feet anchor (`CanvasFeetAnchor=0.5`)**
  — so the rotated body fits in any orientation. Particles draw after, un-rotated (world space).
- Detach happens automatically: `Begin` drops the wall for any non-climb behaviour, and drag/
  reset force `Surface = Floor` (+ clear `RenderAngleOverride`).
- **Corner transitions are a 360° hop**, not a slide. `StepClimb`'s `CornerHop` phase + `BeginHop`
  arc the contact point from one surface to the next over ~0.5 s while spinning the body a full
  turn. During a hop the climb drives the rotation through `Mascot.RenderAngleOverride` (the
  engine uses that verbatim instead of `DampAngle`); it's cleared at the end / on detach. Used
  for wall→ceiling, ceiling→wall, and the final wall→floor dismount. This replaced an earlier
  bug where the body slid across the screen while rotating around the corner.
- `climb` weight is high (5.5) so the crab spends lots of time on walls/ceiling rather than the
  taskbar.

## Skins with different shapes — `SkinStyle`

Skins are normally just colour palettes, but `SkinPalette.Style` (`Core/Enums.SkinStyle`:
`Claud`/`Creeper`/`Ghast`/`Nicolaia`) lets a skin change the silhouette/face.
`CharacterArtist.Draw` branches on it: Ghast uses `DrawTentacles` (dangling) instead of
`DrawLegs`; the face is `DrawFace` (Claud), `DrawCreeperFace` (square eyes + the iconic
frown blocks), or `DrawGhastFace` (sad slanted eyes + frown, opens to a red maw when
`MouthOpen>0.3`). Built-in skins `Skin.Creeper`/`Skin.Ghast`/`Skin.Nicolaia` are always
available (added in `SkinManager.Discover` alongside `BuiltIn`). JSON skins can set
`"style":"creeper|ghast|nicolaia"` (parsed in `SkinManager.ResolvePalette`).

- **Nicolaia** = the classic block dressed as a dapper fellow: `DrawNicolaiaSuit` (black
  jacket over the lower torso + a wide white shirt V with lapels, a bow-tie and waistcoat
  buttons; all clipped to the body) + `DrawFace` (classic square eyes) + `DrawNicolaiaCrown`
  (brown side-curls/peyot down each cheek + a tall black **top hat**). Palette mapping:
  `Body`=skin tone, `BodyShadow`=black suit/hat, `Belly`=white shirt, `Accent`=curl brown.
  **Gotcha:** the top-hat brim is sunk slightly INTO the face (`brimBottom = BodyTop+0.06h`)
  and is wider than the block — otherwise a sliver of skin shows between hat and head.
- **Per-skin HUD headroom:** `SkinPalette.HudHeadroom` (default 0.98, Nicolaia 1.34) is how
  far above the feet (×height) the battery/bubble floats. `MascotEngine.DrawUsageHud` reads
  it so the battery clears tall headgear (the top hat) instead of sitting mid-hat.

## Session battery is TIME-based (not token-%)

The battery shows **time left in the active 5-hour window**, not a token percentage. Why:
Anthropic doesn't expose the real quota locally, and the token-% auto-calibration read as
"broken" (limit collapsed to current usage → always ~empty). `SessionUsageService.Current`
now sets `Fraction = 1 - clamp01(TimeUntilReset / 5h)`; `Remaining` (= charge) drains over
5h and refills on reset. The service still parses logs for **session detection + reset time**
(`HasActiveSession`, `WindowId`). The token-count/limit code still runs but no longer drives
the level (kept for a possible future read-out). Battery is **hidden unless `HasActiveSession`**.
Toggle via the right-click menu (`MenuCommand.ToggleBattery` → `AppSettings.ShowBattery`),
which also Start()/Stop()s the log reader. `Rendering/UsageHudRenderer` unchanged (green/amber/red).

## Epic moves

- `backflip` behaviour: a big `PhysicsSystem.Jump` then `AnimationState.Spin` while airborne
  (handled with `jump` in `ApplyMovement`).
- `charge` behaviour: `AnimationState.Charge` (anticipation crouch+shake → springy release),
  with a `BehaviorDefinition.ClimaxParticle` fired **mid-behaviour** via the new
  `BehaviorController.BehaviorClimax` event (raised once at 50% of duration) → engine
  `OnBehaviorClimax` emits a layered magic+stars+confetti burst. Session **renewal** also does
  a layered burst now.

## Particles are behaviour-only

There is **no ambient particle emitter** by default. `AppSettings.WeatherEnabled` defaults to
**false** — turning it on is the only thing that makes particles drift around continuously
(`MascotEngine.UpdateWeather`). Otherwise particles only appear from deliberate behaviours
(pet→hearts, celebrate/renewal→confetti, dance→notes, spin→sparkles, …) via `BehaviorDefinition.EnterParticle`
or explicit `_particles.Emit*` calls, plus a tiny sparkle every 8 keystrokes while typing. If a
user reports "particles all the time," it's `WeatherEnabled`.

## Session "battery" + speech bubbles — usage HUD

Shows how much of the user's rolling **5-hour Claude session** is used, as a battery that
floats by the crab, plus mood-driven speech bubbles.
- **Data source** (user chose this): `Services/SessionUsageService.cs` parses Claude Code's
  own local logs `~/.claude/projects/**/*.jsonl` (like `ccusage`). It sums "work" tokens
  (`input + output + cache_creation`, **excludes** `cache_read` — huge & cheap) inside the
  active 5-hour block (ccusage-style blocking: new block after 5h span or >5h gap), and
  finds the reset time (`blockStart + 5h`). Runs on a **background thread**, polls every 30 s,
  reads only files modified in the last 6 h. Exposes a thread-safe `Current` `UsageSnapshot`
  with a live countdown. **Privacy: counts/timestamps only, all local, nothing sent.**
- **Budget is an estimate** — Anthropic doesn't expose the real quota locally. It
  auto-calibrates to the user's historical peak (`AppSettings.ObservedMaxSessionTokens`,
  persisted) with a 2M floor, or a manual `AppSettings.SessionTokenLimit` (>0). The battery
  is therefore approximate by design.
- **Rendering**: `Rendering/UsageHudRenderer.cs` draws the battery (green/amber/red by
  charge, terminal nub, low-charge pulse, charging bolt) + speech bubble (rounded, tail,
  measured text via `SKPaint.MeasureText`/`DrawText`, Segoe UI, **no emoji** — Skia software
  raster won't color-render them). Drawn in `MascotEngine.DrawUsageHud`, **frontmost and
  screen-upright** (never rotated with the body), placed just past the head along the body's
  up-vector so it tracks onto walls/ceiling.
- **Reactions**: `MascotEngine.UpdateUsageReactions` fires bubbles on charge-bucket crossings,
  a periodic flavour line, and a **renewal celebration** (confetti + bubble + Excited) when
  `WindowId` increments; low battery nudges Sleepy/Lazy mood. Phrases are Spanish string pools
  in `MascotEngine`. Toggle with `AppSettings.ShowBattery` (gates both the log-reading and the
  draw). First read seeds baselines so it never "celebrates" at launch.

## Drag "stuck to cursor" fix

A layered window only gets mouse messages over opaque pixels, so a fast drag could lose the
`WM_LBUTTONUP`. Fixed two ways: `SetCapture`/`ReleaseCapture` in `LayeredWindow.WndProc`
(WM_LBUTTONDOWN/UP), **and** a safety net in `MascotEngine.Frame` — if `_leftPressed` but
`GetAsyncKeyState(VK_LBUTTON)` says the button is up, it force-releases via `OnLeftUp`. If you
touch drag logic, keep that safety net.

## Right-click menu freeze fix

`ContextMenu.Show` calls `TrackPopupMenuEx`, a **modal loop** that blocks inside the WndProc
→ `RunLoop` never reaches `onFrame` while the menu is open, so the mascot used to freeze.
Fix: `LayeredWindow.BeginModalTicks()/EndModalTicks()` start/stop a `WM_TIMER` (16 ms);
WM_TIMER **is** delivered during the modal loop, and its handler invokes the stashed
`_onFrame`, keeping the simulation alive under the open menu. `MascotEngine.OnRightUp` wraps
`_menu.Show` in `BeginModalTicks()`/`finally EndModalTicks()`. (`wParam`==timer-id compared
as `(ulong)` — nint vs nuint is ambiguous otherwise.)

## Behaviours — `Behaviors/`

Everything is **data** in `BehaviorCatalog.BuildDefaults()`. A behaviour =
`{ Id, DisplayName, Animation, Movement, Category, Weight, Min/MaxDuration, Cooldown, Mood,
MoodIntensity, MinHappiness, EnterParticle, EnterSound }`.
- `Weight > 0` → autonomously selectable by `BehaviorSelector` (weighted by mood/routine/
  happiness, with cooldowns). `Weight = 0` → **reaction-only**, fired via
  `BehaviorController.Force` (e.g. `pet`, `surprised`, `claude-celebrate`, `greet`,
  `type-along`).
- Add a behaviour: append a `new() {...}` line; map it to an `AnimationState` (add a case in
  `Animator.BuildTarget` if it's a new pose). No engine code change needed for autonomous ones.
- `BehaviorController.Begin` handles one-shot physics kicks (jump/trip) and locomotion goals.
- **Idle variety** (added on request): `groom`/`tap-foot`/`wiggle`/`daydream`/`ponder`/
  `people-watch`/`admire-view`/`wake-stretch`. Three new poses with sub-motion in
  `Animator.BuildTarget`: `AnimationState.Groom` (alternating head-tilts + a "hand" preen),
  `TapFoot` (planted legs, beat bob), `Wiggle` (gentle in-place sway, a calmer mini-dance).

---

## Input details

- `CursorTracker`: `GetCursorPos` every frame + smoothed velocity (fast flick → `surprised`).
- `KeyboardActivityTracker`: installs a **`WH_KEYBOARD_LL`** hook. **PRIVACY — it is NOT a
  keylogger:** the callback only *counts* key-downs and notes timing; it never reads/stores
  *which* key (never touches `lParam`). Exposes `IsTyping`, `Intensity` (0–1 leaky
  integrator), `NewKeystrokes`, `JustStarted/Stopped`. Opt out via `AppSettings.KeyboardReactions`.
  The hook delegate is held in a field (or GC kills the hook). `Start()` in `Initialize`,
  `Stop()` in `Shutdown`.
- Typing reaction: `MascotEngine.UpdateTypingReaction()` forces the `type-along` behaviour
  while typing (unless a short reaction in `TypingNonInterruptible` is running or it's
  mid-air) and sprinkles a sparkle every 8 keystrokes. `Animator.SetTypingIntensity` drives
  tap speed/bounce of the `TypeAlong` pose.

Interaction mapping (in `MascotEngine`): single click → open Claude (deferred past the
double-click time), double click → pet, drag past threshold → pick up & throw, right click →
native context menu.

---

## Gotchas / conventions

- **Stop the process before rebuilding** (exe lock).
- **Prop offsets are coupled to body size** — verify props after changing body geometry.
- **Arm values >1.6 = "wave"** = outer leg lift (no real arms on Claw'd).
- New `Pose` field → update `CopyFrom` **and** `Blend`.
- `TryGet` on the catalog is `[NotNullWhen(true)] out BehaviorDefinition?` — callers use
  `out BehaviorDefinition?`.
- Keep comment density/idiom matching the surrounding files (they're heavily, warmly
  documented). No magic numbers — put tunables in `EngineConstants`.
- Build must stay **0 warnings / 0 errors**; nullable is on.
