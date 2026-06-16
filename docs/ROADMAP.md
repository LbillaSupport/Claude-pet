# Roadmap

Claude Buddy ships with a complete, living core. This is where it's headed. Items are
roughly ordered by priority within each horizon.

## Near term

- **Audio pack** — bundle a set of cute royalty-free `.wav` effects (`pet`, `jump`,
  `yawn`, `sleep`, `happy`, `celebrate`, `magic`) and a real volume mixer (per-sound gain
  via WASAPI rather than the current mute-only `winmm` playback).
- **Skin previews & metadata** — show a thumbnail and author/description in the menu;
  add a tiny in-app skin picker window.
- **Achievement toasts** — a small animated banner when an achievement unlocks (today it
  celebrates with confetti only).
- **Multi-monitor polish** — track the monitor under the mascot and re-resolve DPI when
  it crosses a boundary mid-walk.

## Mid term

- **Photo Mode UI** — a proper overlay: pause, pose sliders, hide-effects toggle, and an
  export button (transparent-PNG export already works on toggle).
- **Window & icon awareness** — read top-level window rectangles so the mascot can truly
  sit on title bars, climb borders and hide behind real icons (currently stylised).
- **Richer skin format** — shape parameters (ear style, roundness, accessory layers) on
  top of the palette, staying backward-compatible.
- **Mod manager window** — enable/disable mods, see what each contributes, browse a
  gallery folder.
- **Settings window** — a friendly UI mirroring everything in the context menu.

## Long term

- **Speech bubbles** — occasional wholesome one-liners and reactions to the time/day.
- **Mini-interactions** — toss it a treat, give it a tiny toy, seasonal events.
- **Behaviour-tree authoring** — a visual editor that emits behaviour-pack JSON.
- **Localization** — externalised strings for the menu and About box.
- **Accessibility** — reduced-motion mode, high-contrast skins, configurable click
  targets.

## Explicitly out of scope

To keep the project wholesome and trustworthy, Claude Buddy will **never** include:
ads, telemetry/tracking, network calls other than launching Claude, intrusive pranks, or
anything that interferes with the user's work without consent.
