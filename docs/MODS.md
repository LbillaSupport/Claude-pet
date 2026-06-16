# Mod Creation Guide

Mods extend Claude Buddy **without recompiling**. Today a mod is a *behaviour pack*: it
adds new autonomous behaviours to the mascot's brain via JSON. The architecture is built
so future mod types (particle packs, sound packs, full skins) slot into the same folder.

## 1. Make a folder

Create a folder under one of:

- `Mods/` next to `ClaudeBuddy.exe`, or
- `%AppData%\ClaudeBuddy\Mods\` (per-user).

e.g. `Mods/My First Mod/`. (Right-click → **Mods…** opens the per-user folder for you.)

## 2. Add `mod.json`

```json
{
  "name": "My First Mod",
  "author": "you",
  "version": "1.0.0",
  "description": "Teaches Claude Buddy two new tricks.",
  "enabled": true,
  "behaviors": [
    {
      "id": "my-mod.victory-dance",
      "displayName": "Victory Dance",
      "animation": "Dance",
      "movement": "None",
      "category": "Playful",
      "mood": "Excited",
      "weight": 1.0,
      "minDuration": 4,
      "maxDuration": 7,
      "cooldown": 20,
      "particle": "Confetti",
      "sound": "celebrate"
    }
  ]
}
```

Restart the app — your behaviours join the selection pool and the mascot will start
performing them on its own.

## Behaviour fields

| Field | Type | Notes |
| --- | --- | --- |
| `id` | string | **Required**, unique. Prefix with your mod name to avoid clashes. |
| `displayName` | string | Friendly label. |
| `animation` | enum | **Required.** One of the `AnimationState` values (see below). |
| `movement` | enum | `None`, `Wander`, `Run`, `ApproachCursor`, `FleeCursor`, `EdgePeek`. |
| `category` | enum | `Idle`, `Active`, `Explore`, `Sleepy`, `Social`, `Playful`, `Special`. |
| `mood` | enum | Mood to nudge on entry: `Happy`, `Excited`, `Curious`, `Sleepy`, … |
| `weight` | number | Relative likelihood (before time-of-day & mood biasing). |
| `minDuration` / `maxDuration` | number | Seconds; a random value in this range is used. |
| `cooldown` | number | Seconds the behaviour can't repeat after finishing. |
| `minHappiness` | number | 0–1 gate; the behaviour only unlocks at/above this happiness. |
| `particle` | enum | One-shot burst on entry: `Heart`, `Star`, `Sparkle`, `Confetti`, `Magic`, `Note`. |
| `sound` | string | Sound key to play on entry (needs a matching `.wav`). |

### Valid `animation` values

```
Idle, WalkLeft, WalkRight, RunLeft, RunRight, Jump, Fall, Land, Roll,
Sit, Stand, Sleep, WakeUp, Blink, Wave, Dance, Celebrate, Think, Read,
Drink, Stretch, Yawn, Spin, Pet, Dragged, LookUp, LookDown, LookAround,
Surprised, Scared, Trip, Happy, Sad
```

## How selection works

Each behaviour's effective weight is its `weight` multiplied by biases for the time of
day (its `category`), the mascot's energy and mood, minus a penalty for having just run.
So a `Sleepy` behaviour naturally dominates at 2 a.m., and a `Playful` one shines in a
high-energy afternoon — your mod inherits all of that for free.

## Robustness

- Unknown enum values fall back to sensible defaults; a typo in one behaviour never
  takes down the app or other mods.
- Set `"enabled": false` to disable a mod without deleting it.

## Coming soon

Particle packs, sound packs, and skin bundles inside the same `mod.json` — and a Mods
manager window. See [ROADMAP.md](ROADMAP.md).
