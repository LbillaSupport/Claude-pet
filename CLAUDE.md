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

### Distribution & auto-update (Velopack) — verified end-to-end
Shipping to non-technical users is done with **Velopack** (modern Squirrel successor) —
NOT a `.bat`/MSI. `VelopackApp.Build().Run()` is the **first line of `Main`** (intercepts
install/update/uninstall hooks; no-op on a dev run). `Services/UpdateService.cs` polls
**GitHub Releases** in the background: a first check ~30 s after launch, then **every 4 h**
for the whole session (`PeriodicTimer`), so a long-running buddy updates without ever being
relaunched. When a newer release exists it downloads it and calls **`ApplyUpdatesAndRestart`
immediately** — it does NOT wait for a clean exit, because a force-kill / PC shutdown would
otherwise strand the staged update. The app silently relaunches into the new version (no
dialog). Gated by `AppSettings.AutoUpdate` (re-read each round; default true). `Stop()`
cancels the poller on exit. `manager.IsInstalled` is false on a plain dev run (from `bin\`),
so updates only ever happen for an installed copy.
- **User config survives updates:** settings live in `%AppData%\ClaudeBuddy\settings.json`
  (Roaming), totally separate from the install dir (`%LocalAppData%\ClaudeBuddy\`). Velopack
  never touches AppData, so updates/reinstalls keep the user's skin/position/achievements; a
  fresh install (no settings.json) starts on classic Claude (`CurrentSkin=""`).
- **GitHub API rate limit (gotcha):** the unauthenticated GitHub API allows **60 req/h per
  IP**. Hammering many install/check cycles from one IP during testing exhausts it and the
  check then fails silently (CheckForUpdates returns null / throws → swallowed) until the
  hour resets. In real use each user is on their own IP doing ~1 check/4 h, so it's a
  non-issue — but don't be fooled in a test loop. (`GithubSource` can take a PAT if ever
  needed.)
- **App icon:** `src/ClaudeBuddy/appicon.ico` (multi-res, rendered from classic Claw'd) is
  embedded via `<ApplicationIcon>` and passed to `vpk pack --icon` (installer + shortcuts).
- **Repo**: `UpdateService.RepoUrl` = `https://github.com/LbillaSupport/Claude-pet`.
- **Keep `Velopack` (NuGet) and the `vpk` CLI on the SAME version** (both 1.2.0) — a
  mismatch logs a runtime compatibility warning at pack time.
- **Cut a release**: `.\build-release.ps1 -Version X.Y.Z` — publishes self-contained, then
  `vpk pack` produces `.\Releases\` (`ClaudeBuddy-win-Setup.exe` = the friendly installer +
  full **and delta** `.nupkg` + `releases.win.json`/`RELEASES`/`assets.win.json`). Then
  `gh release create vX.Y.Z <all of Releases\*> --target main`. Installs to
  `%LocalAppData%\ClaudeBuddy\` (`current\`, `packages\`), makes Desktop+Start-Menu
  shortcuts and an "Add/Remove Programs" entry. `.\Releases\` and `.\dist\` are gitignored.
- Running `Setup.exe` over an existing install shows Velopack's "already installed →
  Update?" dialog (expected); the *silent background* updater shows no dialog.
- The script auto-installs the `vpk` global tool if missing. Verified: a 1.1.2→1.1.3
  background update downloaded + applied + relaunched on its own.

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
`Claud`/`Creeper`/`Ghast`/`Nicolaia`/`Galgo`/`AmongUs`/`Pikachu`/`Mate`/`Ghost`) lets a skin
change the silhouette/face. `CharacterArtist.Draw` branches on it: Ghast uses `DrawTentacles`
(dangling) instead of `DrawLegs`; the face is `DrawFace` (Claud), `DrawCreeperFace` (square
eyes + the iconic frown blocks), or `DrawGhastFace` (sad slanted eyes + frown, opens to a red
maw when `MouthOpen>0.3`). Built-in skins
`Skin.Creeper`/`Skin.Ghast`/`Skin.Nicolaia`/`Skin.Galgo`/`Skin.AmongUs`/`Skin.Pikachu`/`Skin.Mate`/`Skin.Ghost`
are always available (added in `SkinManager.Discover` alongside `BuiltIn`). JSON skins can
set `"style":"creeper|ghast|nicolaia|galgo|amongus|pikachu|mate|ghost"` (parsed in
`SkinManager.ResolvePalette`).

- **AmongUs / Mate / Ghost** = their OWN silhouette (not the block+legs), drawn by
  `DrawAmongUs` / `DrawMateChar` / `DrawGhost`, which **short-circuit** the pipeline (draw
  body+face, then `DrawProps`, then `canvas.Restore(); return;`). UNLIKE the rigid Galgo bus
  they **keep** the soft squash/sway transform (they branch *after* it's applied), so they
  still feel alive. AmongUs = capsule body + visor (pupils track cursor) + backpack + 2 legs;
  Mate = brown gourd + green yerba cap + metal bombilla + classic square eyes/cheeks/smile;
  Ghost = Pac-Man-style dome with a scalloped skirt + big white eyes with cursor-tracking blue
  pupils. **Pikachu** instead reuses the classic block+legs and adds `DrawPikachuBackparts`
  (long ears with dark tips + a lightning-bolt tail, drawn *before* the body) +
  `DrawPikachuFace` (round glinty eyes, big red cheeks, a small smile). All four honour the
  **universal `Pose.SpiralEyes>0.5` dizzy override** (`DrawDizzyPair`) — add that check to any
  new face. Per-skin `HudHeadroom` raised for the tall ones (Pikachu ears 1.42, Mate bombilla
  1.30, AmongUs 1.18). Skin-aware self-talk pools added in `Phrasebook` (`SelfAmongUs`/
  `SelfPikachu`/`SelfMate`/`SelfGhost`) so each one talks in character. Verify any of them
  with `--render <id> out.png 480` (and `... 480 Dizzy` / `... 480 bubble`).

- **Nicolaia** = the classic block dressed as a dapper fellow: `DrawNicolaiaSuit` (black
  jacket over the lower torso + a wide white shirt V with lapels, a bow-tie and waistcoat
  buttons; all clipped to the body) + `DrawFace` (classic square eyes) + `DrawNicolaiaCrown`
  (brown side-curls/peyot down each cheek + a tall black **top hat**). Palette mapping:
  `Body`=skin tone, `BodyShadow`=black suit/hat, `Belly`=white shirt, `Accent`=curl brown.
  **Gotcha:** the top-hat brim is sunk slightly INTO the face (`brimBottom = BodyTop+0.06h`)
  and is wider than the block — otherwise a sliver of skin shows between hat and head. The
  mouth is pulled up via `DrawFace(..., mouthDrop: 0.55f)` so it lands on the face strip,
  not on the white shirt (`DrawMouth` takes a `drop` factor; default 1.0 for every other skin).
- **Galgo** = a whole **cartoon city bus** (line 34 Liniers–Palermo) in a **Vélez Sarsfield
  bucket hat** — modelled on a real sticker. It does NOT use the block/legs: `DrawGalgo`
  short-circuits the pipeline and draws its own shell, windows, door, wheels, smiley
  windshield-face and `DrawGalgoHat` (the piluso: navy brim, white crown band with
  `DrawVelezShield` (vector CAFVS crest) + "VÉLEZ SARSFIELD", all clipped to the crown so
  nothing spills). Palette: `Body`=white shell, `BodyShadow`=Vélez navy, `Accent`=red
  stripe, `Belly`=light-blue glass, `Pupil`=black, `Mouth`=red smile. **Two key fixes:**
  (1) it's hand-drawn facing LEFT like the sticker, so the branch **cancels the facing
  mirror** (`canvas.Scale(-1,1)`) — otherwise the lettering renders backwards; (2) a rigid
  vehicle must not wobble/squash like a blob, so the branch **discards the whole-body
  transform** (BodyOffset/rotation/squash) and re-anchors with only a gentle vertical bob —
  this killed the "drunk" look on double-click/pet. Only the pupils track the cursor.
- **Per-skin HUD headroom:** `SkinPalette.HudHeadroom` (default 0.98, Nicolaia 1.34, Galgo
  1.55) is how far above the feet (×height) the battery/bubble floats. `MascotEngine.DrawUsageHud`
  reads it so the battery clears tall headgear (top hat / bucket hat) instead of sitting on it.

### `--render` CLI (deterministic skin screenshots)
`ClaudeBuddy --render <skinId> <out.png> [sizePx]` (handled in `Program.Main`/`RenderSkinToFile`)
draws ONE skin to a centred PNG and exits — no window, no game loop, no wandering. Use this to
verify a skin's look instead of screenshotting the live mascot (which walks out of frame). It
instantiates `CharacterArtist` + a neutral `Pose` directly and renders at `height = size*0.42`
with feet at `size*0.62` (leaves headroom for hats/buses).

### `--render-frames` + the demo GIF (README marketing visuals)
`ClaudeBuddy --render-frames <skinId|showcase> <outDir> [sizePx]` runs the **real `Animator`**
and writes numbered PNG frames (`frame_0000.png`…) — deterministic, window-free, NO live screen
capture (the user explicitly does not want capture bursts). `skinId` = a little story of several
animations on one skin; the special id **`showcase`** = a continuous full-turn spin that cycles
through every skin (one 360° turn each — the README hero GIF). `tools/GifAssembler.ps1` then
stitches the frames into a looping GIF using the **GDI+ (`System.Drawing`) native encoder**
(`Save`+`SaveAdd`), then byte-patches in the per-frame delay + a NETSCAPE2.0 loop block (GDI+
writes neither). No ffmpeg/ImageMagick needed. **Gotcha:** to verify a frame OUT of the finished
GIF, extract it with `Graphics.DrawImage` onto a fresh `Bitmap` — the `new Bitmap(image)` overload
copies the raw indexed buffer and renders as colour noise (this cost a lot of debugging time; the
GIF itself was fine). The demo GIF lives at `docs/assets/showcase.gif`; the skin gallery PNGs at
`docs/assets/skins/`. `_frames/` and `_*.gif` are gitignored.

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

## World data — weather/dollar/crypto reactions (`Services/WorldDataService`)

Pulls fun real-world data from **free, key-less public APIs** so the buddy reacts to it,
and chats about it in speech bubbles. Background thread, slow polling, all best-effort.
- **Sources:** location from `ip-api.com` (approx city/lat-lon from public IP) → weather
  from **Open-Meteo** (no key); ARS blue dollar from `dolarapi.com`; BTC from CoinGecko.
  Weather refreshed every 30 min, rates every 15 min. Thread-safe `Current` `WorldDataSnapshot`.
- **Privacy + gating:** only an approximate city is derived, nothing is sent out. Gated by
  `AppSettings.WorldData` (default true; menu toggle `MenuCommand.ToggleWorldData` →
  Start()/Stop()). Off = **zero network requests**.
- **Reactions** (`MascotEngine.UpdateWorldDataReactions`): `WeatherMood` → Cold triggers the
  `shiver` behaviour (`AnimationState.Shiver`: hug + tremble + chattering, `Blush`, and the
  icy `ThermometerProp`); Hot → `too-hot` (`AnimationState.Hot`: droop + sweat + fast fanning
  arm + `FanProp`); Rain/Snow → bubble (+ sparkles for snow). Plus rotating data bubbles:
  greeting (by hour, "¡Feliz viernes!"), `Dólar blue: $…`, `Bitcoin: U$D …`, and a fun-facts
  pool — all Spanish, **no emoji** (Skia software raster won't colour-render them).
- **New poses/props:** `AnimationState.Shiver`/`Hot` in `Animator.BuildTarget`;
  `Pose.ThermometerProp`/`FanProp` (added to `CopyFrom`+`Blend`) drawn by
  `CharacterArtist.DrawThermometer` (glass tube + icy bulb + snowflakes) / `DrawFan` (folding
  hand-fan wedge). Weather behaviours `shiver`/`too-hot`/`rainy` are weight-0 (engine-triggered).
- **`--render` preview:** `ClaudeBuddy --render <skin> <out.png> [size] [thermometer|fan]`
  forces the weather prop on so they can be screenshotted deterministically.

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
  screen-upright** (never rotated with the body). The battery + bubble are **stacked straight
  UP in SCREEN space** above the body (`anchorX` follows the body centre via `sin(ang)*0.46h`;
  `stackY = feet.Y - headroom*height`), NOT along the body's rotating up-vector — anchoring to
  the up-vector made the battery fly off sideways (read as "vertical") when clinging to a wall.
  `DrawBubble` **word-wraps** and clamps its rect inside the **on-screen slice** of the canvas
  (`visLeft`/`visRight` = the canvas intersected with `World.LeftWall/RightWall`), so it grows
  taller (not wider) AND slides away from a monitor edge instead of spilling off it — the crab
  hugging the right screen edge no longer clips the bubble against the screen. Verify with
  `--render <skin> out.png 480 bubble` (centred) and `... bubbleedge` (simulates the right edge).
- **Skin-aware self-talk:** `Phrasebook.SelfReferential(SkinStyle)` mixes universal lines with
  skin-specific ones so the Creeper never says "soy terracota" (the Galgo bus talks about line 34,
  etc.). Engine passes `_skins.Current.Palette.Style`.
- **Desktop-anchored (#4)** — Claw'd interacts with the *real* desktop. `Services/DesktopProbe`
  is **read-only, best-effort** Win32: `TryGetClockX` walks `Shell_TrayWnd → TrayNotifyWnd →
  TrayClockWClass` for the taskbar clock's true screen X, falling back to the taskbar's right
  corner (**Windows 11 has no `TrayClockWClass` HWND** — the clock is UWP — so the fallback is
  the normal path there). `MascotEngine.MaybeDesktopInteraction` walks the crab to that X and
  reads the real time. New `BehaviorMovement.ApproachPoint` (+ `BehaviorController.SetApproachX`)
  walks to a specific engine-set world X. `push-cursor` (`AnimationState.Push`) strains against
  the mouse pointer via `ApproachCursor`. New P/Invokes (FindWindow/FindWindowEx/GetWindowRect)
  live in `NativeMethods`. **Gotcha:** `NativeMethods` is internal, so a public probe method
  can't expose `NativeMethods.RECT` — return primitives (the clock returns just `out float X`).
  `MaybeDesktopInteraction` now also (half the time) calls `PeekAtCorner` → the weight-0
  `peek-corner` behaviour walks to the nearer screen-edge corner and peeks (no Win32, safe on
  any build).
- **Desktop reactions (#4, safe subset)** — `MascotEngine.UpdateDesktopReactions(dt)`, called
  each brain tick, adds two read-only reactions that need no walking: (1) **active-window
  switch** via `NativeMethods.GetForegroundWindow()` — when the foreground HWND changes it does
  a brief `look-around` + sometimes a bubble (first sighting is seeded, not a "change";
  `WindowReactCooldown` debounce); (2) **system volume change** via `Services/SystemAudioProbe`
  (`TryGetMasterVolume` — minimal Core Audio `IAudioEndpointVolume` COM interop, **identical on
  Win10/Win11**, all best-effort/try-catch so a missing endpoint just no-ops): polled every
  `VolumePollSeconds`, a delta past `VolumeReactDelta` triggers `wiggle` (louder) or `surprised`
  (quieter/mute) + a bubble. Both gated by `IsCalmGrounded()`. Verified Core Audio reads the
  real level on this machine. Still deferred (most fragile): recycle-bin icon position,
  notification reactions.
- **Portal clone event (#6)** — a second Claw'd drops out of a **Portal-game-style portal**
  ANYWHERE on screen and falls in with the **real physics**. It has its OWN everything so it's
  independent of the main mascot: a passive `LayeredWindow` (`new LayeredWindow(class, passive:true)`
  — the constructor + `_passive` flag make it pure click-through, no input, **no `PostQuitMessage`
  on destroy** so closing it never kills the app) that **follows the clone** (so it's not stuck in
  Claw'd's 480px window), its own `SkiaRenderer`, `Mascot`, `Animator`, `EmotionState`, and its own
  `PhysicsSystem` instance (no subscribers → its landings never fire Claw'd's `OnImpact`/`OnLanded`).
  State machine in `MascotEngine.UpdateCloneEvent`: `Opening → Drop (gravity+bounce) → Linger
  (look around, wave) → ExitOpen → ExitClose`. `CharacterArtist.DrawPortal` draws the glowing
  cyan oval (centre-based, can float in the air). The clone fades via an `SKCanvas.SaveLayer`
  alpha layer around `_artist.Draw`. Rendered in `RenderClone` (its window follows the clone).
  Fires ~40 s after launch (a demo), then rare (`Rare{Min,Max}Gap`). The real Claw'd does a
  double-take (`MaybeReactToClone`) if the clone lands nearby. Preview the art: `--render <skin>
  out.png 480 portal`. **Gotcha:** the clone window must be a DIFFERENT window class than the main
  one (`ClaudeBuddyCloneWindowClass`) so it dispatches to its own passive WndProc.
- **Reactions**: `MascotEngine.UpdateUsageReactions` fires bubbles on charge-bucket crossings,
  a periodic flavour line, and a **renewal celebration** (confetti + bubble + Excited) when
  `WindowId` increments; low battery nudges Sleepy/Lazy mood. Phrases are Spanish string pools
  in `MascotEngine`. Toggle with `AppSettings.ShowBattery` (gates both the log-reading and the
  draw). First read seeds baselines so it never "celebrates" at launch.

## "Living drag" — soft grab, free rotation, dizziness & impacts

Dragging is a **third locomotion mode** alongside Floor (gravity) and Climbing. Instead of
gluing `Position` to the cursor, the body **eases** toward it **from the local point you
grabbed**, carries a free-body rotation, and hands linear + angular velocity to gravity on
release — so it feels like handling a tiny, stubborn, soft creature (Pixar/Shimeji/Goose).

- **Stability is the rule.** An early version used a stiff explicit spring; it exploded on a
  slow frame → the body teleported, tunnelled walls and vanished. The shipped solve uses an
  **unconditionally-stable exponential ease** (`MathUtil.Damp`, frame-rate independent) — it
  can never blow up regardless of frame timing. If you touch `StepDrag`, keep that property.
- **Mascot drag state** (`Engine/Mascot.cs`, pure data): `GrabLocalOffset` (body-local, DPI-
  normalised grab point so it hangs from a corner/leg), `BodyAngle` + `AngularVelocity` (a
  dedicated free-body rotation channel, **separate** from `SurfaceAngle` and `Pose.WholeBodyRotation`
  so crab-climbing is untouched), `Dizziness` (0..1 meter), `DragRotated`. **`ResetPosition`
  clears all of these.**
- **The solve** (`PhysicsSystem.StepDrag`): eases the feet toward the position that puts the
  grabbed point on `PhysicsSystem.DragTarget` (the engine sets it = cursor each frame), then
  **derives** the body's own velocity from the real per-frame displacement (smoothed + clamped
  to `MaxThrowSpeed`) so a flick throws cleanly. A gentle, clamped tilt (`DragTiltScale` /
  `DragMaxTilt`) makes it hang from the grab point — **not** the old runaway torque. `SettleAngle`
  (Floor branch) keeps the spin during flight and springs `BodyAngle` back to upright once
  grounded & slow — **never a snap**. On release the throw uses the body's smoothed velocity,
  and `Throw` leaves `AngularVelocity` alone (spinning flight).
- **No freeze-frame on impact.** A "bonk" stun-timer was tried and **removed** — at speed it
  read as the pet *lagging/sticking* to the wall (confirmed by a frame-time log: FPS never
  dropped; it was the deliberate pause). Impacts now rebound instantly; the squash + particles
  sell the hit.
- **Collisions** raise `PhysicsSystem.Impact` (`ImpactEvent{Surface,Speed,At}` in
  `Physics/ImpactEvent.cs`) at floor/wall/ceiling. `MascotEngine.OnImpact` maps `Speed` to bands
  (`Impact*Speed`): tiny→squash, medium→stars+recoil+surprised, heavy→pancake+dust+dizziness,
  extreme→full pancake + "birds". `RecoilFrom`/`ApplyImpactSquash` shape it per surface. A
  **debounce** (`ImpactReactionCooldown`, one reaction per ~0.25 s) stops a fast bounce that
  re-hits the same wall from flooding particles. (`Landed` is kept for the old dust hook.)
- **Dizziness** (`MascotEngine.UpdateDragReactions`) accumulates from fast spin + heavy impacts,
  recovers when grounded/settled; crossing `DizzyTriggerThreshold` forces the weight-0 `dizzy`
  behaviour → `AnimationState.Dizzy` (spiral eyes via new `Pose.SpiralEyes`; wobbling head; the
  Animator also laces an unstable-walk wobble onto any walk while `Dizziness` is high). **`SpiralEyes`
  is a new Pose field → it's in `CopyFrom` AND `Blend`.**
- **Spiral eyes are universal.** `Pose.SpiralEyes > 0.5` shows the dizzy `@`-spiral on **every**
  skin: `CharacterArtist.DrawSpiralEye` (one eye) + `DrawDizzyPair` (both), called as an early
  override inside `DrawEye` (Claud/Nicolaia), `DrawCreeperFace`, `DrawGhastFace`, and the Galgo
  windshield eyes. Add the same check to any new face.
- **Frame gating:** while held (dragging or the pre-throw panic beat) **physics + animator still
  run** (the ease moves the body) but the **behaviour brain is parked**; "refuse to move"
  (`_refuseTimer`) parks the brain too. Photo mode freezes everything. The render adds **one** new
  pivot: when `BodyAngle != 0` it rotates the artist about the **body centre** (not the grab point
  — pivoting off-centre swung parts of the body out of the 480px canvas and clipped them). It
  composes with, but never co-fires with, the surface-angle pivot (a drag forces `Surface=Floor`).
- **Personality / flourishes** (`MascotEngine`): `_abuseMeter` rises with rough handling → grumpy
  face + a Spanish bubble (`AbuseLines`, no emoji) + sometimes a `_refuseTimer` sulk; edge-resistance
  (`EdgeResistChance`); helicopter spirals at `HelicopterAngularVel`; an occasional panic-cling
  before a very fast fling (`_panicTimer` delays the `Throw`). `Animator.SetDragSpeed` paddles the
  legs in mid-air.
- **All tunables** live under `// ---- Drag & impact ----` in `EngineConstants.cs` (`DragFollow`,
  tilt, angular caps, dizzy curve, impact speed bands, `ImpactReactionCooldown`).
- **Verify the dizzy look deterministically:** `ClaudeBuddy --render <skin> out.png 420 Dizzy`
  (the `--render` CLI runs the real Animator for the named `AnimationState`).

## The app is silent (no audio)

The user asked for no sound, so `Services/AudioService.Play` is a **deliberate no-op** kept behind
`IAudioService` (rather than ripping every `_audio.Play(...)` call out of the engine) — re-enabling
sound later is a one-file change. The right-click menu has **no Mute/Volume** entries (they'd be dead
controls); `AppSettings.Muted`/`Volume` still exist but are unused.

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

### "Play Animation" submenu (review/showcase every animation)
The right-click menu has a **Play Animation ▸** submenu that force-plays ANY single animation
on demand — handy to review (and tune) each one, and to demo them. It's built **from the
catalogue**: `MascotEngine.BuildMenuState` lists `_catalog.All` (every behaviour, including
the weight-0 reaction-only ones like `pet`/`dizzy`/`shiver`) as `AnimationMenuItem`s grouped
by `BehaviorCategory`, plus one synthetic **"Portal Clone Event"** entry (`PortalAnimId =
"__portal__"`). `ContextMenu.BuildAnimationMenu` renders per-category sub-sub-menus; leaves
carry `AnimBase (2000) + index`, decoded back to the behaviour id in `MenuSelection.SkinId`
(the string field is reused). `MascotEngine.PlayAnimationFromMenu` drops any climb/drag to a
clean upright floor stance then `Trigger(id)`s it; the Portal sentinel calls `StartCloneEvent()`
(no-op if one's already running; `EndCloneEvent` restores the normal rare cadence afterward).
Adding a behaviour to the catalogue therefore makes it appear in this menu automatically.

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
- **"Performances"** (vNext): `sneeze`/`cough`/`look-under`/`count-legs`/`balance`/`dust-off`/
  `somersault`/`slip` are autonomous; `embarrassed`/`pout` are weight-0 (reaction/chain only).
  Each is a new `AnimationState` with phased sub-motion in `Animator.BuildTarget` reusing only
  existing `Pose` fields (no new fields, no new art). `Sneeze`/`Cough` are in `ApplyBlink`'s
  `eyesScripted` set. **Gotcha:** a frown only renders when `MouthOpen > 0.14` — `Pout` sets
  `0.2` so the puchero shows.

## vNext — "feels alive" systems (Phrasebook, chatter, memory, chains, micro-interactions)

A push to make Claw'd surprise you for hours without adding "features". All procedural, all
reusing the existing `BehaviorCatalog`/`Animator`/`Pose`/`Physics`.

- **`Content/Phrasebook.cs`** (new, DI singleton) owns Claw'd's *entire* spoken repertoire as
  data: categorised Spanish pools (observations, time-of-day ×5, absurd, self-referential
  generic + per-skin, welcome, annoyed) + a **400+ fun-fact** database grouped by topic
  (animals, space, history, programming, games, physics, chemistry, body, food, geography,
  art, OS/internet/AI, useless trivia…). All **emoji-free** (Skia software raster can't
  colour-render them); keep new lines plain ASCII-ish Spanish (acented chars OK via Segoe UI,
  but **no emoji**). `Pick()` keeps a rolling 18-line history so no pool repeats back-to-back
  across categories. Pools are just `static readonly string[]` — append lines freely.
- **Ambient chatter** (`MascotEngine.UpdateChatter`): an **always-on** speech driver,
  independent of WorldData/battery, on a `Chatter{Min,Max}Gap` cooldown. `NextChatterLine`
  weights fun-fact/absurd/self-ref/time + an **idle observation** when `_idleSeconds` (reset on
  any cursor move/keypress) crosses `IdleChatterSeconds`, plus a "welcome back" after an absence.
  Never talks over an active bubble. The old WorldData fact-rotation was folded into this; that
  method now only does weather/holiday. `DataLine` returns dollar/crypto/greeting (or null).
- **Memory** (`Stats` extended: `ThrowCount`/`BackflipCount`/`GreetCount`/`MaxThrowHeightPx`/
  `LastPettedUtc`/`LastSeenUtc`). Incremented in `ReleaseThrow`/`OnBehaviorStarted`/`Pet`/
  `AccumulateStats` (altitude tracked while airborne & not climbing). `MemoryLine()` builds
  lines like "Ya me lanzaste N veces"; `Initialize` captures days-since-`LastSeenUtc` for the
  welcome-back. All persists in `settings.json` (survives updates, see AppData note above).
- **Behaviour chains** (`BehaviorController.RunChain` + a `Queue<string> _chain`): sequenced
  "stories" — each step runs to its natural end, then the next `Begin`s; the queue drains back
  to autonomous selection. **`Force` (any interaction) clears the chain.** Engine data in
  `MascotEngine.Chains` + `MaybeStartChain` (fires from a calm grounded state on `Chain{Min,Max}Gap`).
- **Rare "special moments"** (`MascotEngine.MaybeRareEvent`, `Rare{Min,Max}Gap` ≈ 20–75 min):
  a flashy chain + confetti/stars/magic + bubble. Pure reuse of existing behaviours.
- **Micro-interactions** (`MascotEngine.UpdateCursorMicro` + `OnDoubleClick`): circling the
  cursor round the mascot feeds the existing **dizziness** meter (→ dizzy reaction for free); a
  fast flick passing close → a startled **ruffle** (`surprised` + dust); 3 quick double-clicks
  → a **tickle** giggle (`laugh` + hearts); rough handling already builds `_abuseMeter` → now a
  **`Pout`** sulk (`AnimationState.Pout`) during the `_refuseTimer`. **Give a paw**
  (`UpdateGivePaw`): rest the cursor still right beside it (within `GivePawRadius`, speed under
  `GivePawMaxCursorSpeed`) for `GivePawHoldSeconds` and it offers a paw — reuses the `wave` pose
  (lifts an outer leg toward the cursor) + hearts, `GivePawCooldown` debounce; decays fast if
  the cursor moves away.
- **Imaginary props** (#5) ride a **single generic channel** rather than 20 Pose fields:
  `Pose.HeldProp` (a `Core.HeldPropKind` enum, set directly — not blended) + `Pose.HeldPropAmount`
  (faded). One `AnimationState.HoldProp` pose presents whatever prop the engine selected via
  `Animator.SetHeldProp`, fed from `BehaviorDefinition.HeldProp` in `OnBehaviorStarted` (cleared
  for every non-prop behaviour so it fades out). `CharacterArtist.DrawHeldProp` switches on the
  kind → 16 procedural draws (magnifier/balloon/flag/flashlight/ice-cream/mate/binoculars/
  paintbrush/toy-hammer/sword/kite/watering-can/umbrella/guitar/camera/trophy). **Adding a
  prop = one draw method + one catalogue line.** **Gotchas:** Skia's `RotateRadians` has **no
  pivot overload** (translate→rotate→translate); and prop draws that touch `_stroke.StrokeCap`
  must restore it to **Round** (the shared paint's default) or they corrupt later strokes.
  `DrawSparkleStar` is a shared 4-point twinkle helper (used by camera/trophy).
  `--render <skin> out.png <sz> <PropName>` previews any prop (the CLI parses a `HeldPropKind`
  name onto a neutral presenting pose).
- All tunables live under `// ---- Ambient chatter ----` in `EngineConstants.cs`.

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
