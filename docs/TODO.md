# TODO / Status

An honest account of what is implemented in this build versus what is scaffolded or
planned. The core experience is complete and runs; breadth items are noted clearly.

## ✅ Done and working

- [x] Native transparent, click-through, always-on-top window (`UpdateLayeredWindow`)
- [x] 60 FPS delta-time game loop with high-res pacing
- [x] Procedural SkiaSharp character — expressive face, blinking, squash & stretch
- [x] Animation engine: 30+ pose archetypes, mood-driven faces, eased blending
- [x] Behaviour state machine: 45+ data-driven behaviours, weighted + cooldowns + routine/mood biasing, anti-repetition
- [x] Daily routine (morning/afternoon/evening/night/late-night) altering behaviour
- [x] Emotion system (12 moods) affecting face, speed and behaviour selection
- [x] Physics: gravity, momentum, friction, ground/wall collision, bounce, drag & throw
- [x] Particle system: hearts, sparkles, stars, confetti, dust, Zzz, notes, magic, weather motes
- [x] Mouse interaction: eye-tracking, startle on fast flick, smile on click
- [x] Petting (double-click) with hearts, blush and a happiness meter
- [x] Happiness-gated rare reactions (secret dance, star-eyes)
- [x] Single-click opens Claude Desktop (with fallbacks) and celebrates; waves goodbye on close
- [x] Right-click context menu (skins, speed, volume, mute, frequency, on-top, startup, photo, achievements, mods, settings, about, exit)
- [x] JSON settings persistence (`%AppData%`) with atomic writes
- [x] Skin system — drop-in folders, live menu rescan, two example skins
- [x] Mod system — behaviour-pack JSON loaded at startup, example mod
- [x] Achievements (11) with persistence and reward content flags
- [x] Photo Mode — pauses and exports a transparent PNG
- [x] Launch-on-startup (HKCU Run), Always-On-Top toggle, Reset Position
- [x] Fake weather (snow / leaves / petals) drifting past
- [x] Single-instance guard, crash logging, Per-Monitor-V2 DPI awareness
- [x] DI, SOLID structure, XML-documented classes, no magic numbers
- [x] Full documentation set (README, build, architecture, animation, skins, mods, roadmap)

## 🟡 Scaffolded — works but intentionally simple

- [ ] **Audio** — playback layer is complete, but no `.wav` assets ship yet (silent
      until you add them); volume is mute-only (no per-sound gain). See ROADMAP.
- [ ] **Achievements UI** — shown via a native message box; a styled panel is planned.
- [ ] **Mods manager** — mods load from JSON; an enable/disable UI is planned.
- [ ] **Window/icon awareness** — taskbar/title-bar/icon behaviours are stylised rather
      than reading real window rectangles.
- [ ] **Skin format** — palette-only today; shape/accessory parameters are planned.

## 🔵 Next up (see ROADMAP.md for the full plan)

- [ ] Bundled audio pack + WASAPI volume mixer
- [ ] Achievement toast banners
- [ ] Photo Mode overlay UI (pose sliders, hide-effects)
- [ ] Skin preview thumbnails in the menu
- [ ] Multi-monitor DPI re-resolution mid-walk
- [ ] Real window-geometry awareness for climbing/sitting
- [ ] Settings window mirroring the context menu
