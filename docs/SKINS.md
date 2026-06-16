# Skin Creation Guide

A skin re-colours Claude Buddy. Because the character is drawn from vectors, that's all
a skin *is* — a palette in a small JSON file. No image editor required.

## 1. Make a folder

Create a folder anywhere under one of:

- `Skins/` next to `ClaudeBuddy.exe` (ships with the app), or
- `%AppData%\ClaudeBuddy\Skins\` (per-user, survives updates).

Name the folder whatever you want the skin to be called, e.g. `Ocean Claude`.

## 2. Add `skin.json`

```json
{
  "name": "Ocean Claude",
  "author": "you",
  "version": "1.0.0",
  "description": "Cool blues for a calm desktop.",
  "colors": {
    "body":       "#5AA9E6",
    "bodyShadow": "#3E84BD",
    "belly":      "#EAF6FF",
    "pupil":      "#0E2A47",
    "mouth":      "#13496B",
    "blush":      "#FF9CB0",
    "accent":     "#FFE08A"
  }
}
```

That's it. Right-click the mascot → **Change Skin** → your skin appears (the menu
rescans every time it opens, so no restart is needed).

## Colour reference

| Key | What it paints |
| --- | --- |
| `body` | The main blob colour |
| `bodyShadow` | Lower-body shading, feet and hand undersides |
| `belly` | The lighter belly oval and the centre of the chest badge |
| `pupil` | Eyes and closed-eye lines |
| `mouth` | Mouth line / open-mouth fill |
| `blush` | Cheek blush and the tongue |
| `accent` | The chest sunburst badge and star-eyes |

- Colours are `#RRGGBB` or `#RRGGBBAA` hex strings.
- **Every field is optional.** Anything you omit falls back to the built-in *Classic
  Claude* palette, so the minimum valid skin is `{ "name": "X", "colors": { "body": "#7AA2FF" } }`.
- A malformed colour or file is skipped gracefully — it can never break the app or other
  skins.

## Optional: per-skin sounds

If you ship `.wav` files in your skin folder, you can map them to sound keys:

```json
{
  "name": "Ocean Claude",
  "sounds": {
    "pet": "sounds/splash.wav",
    "jump": "sounds/bubble.wav"
  }
}
```

Recognised keys today: `pet`, `jump`, `yawn`, `sleep`, `happy`, `celebrate`, `magic`.

## Tips

- Keep `body` and `bodyShadow` close in hue but different in lightness for nice volume.
- A light `belly` reads best against a saturated `body`.
- The example skins **Midnight Neon** and **Spring Sprout** in `/Skins` are great
  starting points — copy one and tweak.

## Future skins (planned)

The skin format will grow to support shape tweaks (ears, roundness), accessory layers
and preview thumbnails — see [ROADMAP.md](ROADMAP.md). The current palette format will
remain valid.
