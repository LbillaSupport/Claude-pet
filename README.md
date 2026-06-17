<div align="center">

# 🧡 Claude Buddy

**An adorable desktop companion that lives on your Windows desktop.**

A wholesome, open-source desktop pet built with C# / .NET 9 and SkiaSharp —
in the spirit of Desktop Goose, Tamagotchi and Clippy, but cute, calm and customizable.

*Single click opens Claude. Double click pets it. Drag it around. Watch it live its little life.*

</div>

---

## ✨ Why it feels alive

Claude Buddy isn't a looping sprite — it's a tiny simulation:

- **A real brain.** A weighted behaviour state machine picks from **50+ behaviours**
  (idle, walk, run, jump, sleep, dance, read, drink coffee, peek from screen edges,
  chase sparkles, dream, climb the walls, backflip, power-up…) with probabilities,
  cooldowns and anti-repetition.
- **A daily routine.** Energetic and coffee-carrying in the morning, exploratory in the
  afternoon, yawny in the evening, deeply asleep in the small hours — driven by your
  real clock.
- **Emotions.** Happy, sleepy, curious, excited, proud, scared and more. Mood changes
  its face, its walk, its animation speed and what it chooses to do.
- **It notices you.** Its eyes follow your cursor, it gets startled by fast mouse
  flicks, smiles when clicked, and can be picked up and thrown with real momentum.
- **Affection that matters.** Petting fills a happiness meter that unlocks rare
  reactions, secret dances and sparkly eyes.
- **Buttery motion.** Everything runs at 60 FPS with easing on every transition,
  squash-&-stretch landings, soft shadows and tiny particles. Nothing moves in a
  straight line.

## 🎨 Drawn entirely from vectors

There are **no sprite sheets**. The character is painted every frame from SkiaSharp
shapes, which means it's crisp at any DPI, animates infinitely smoothly, and a brand
new **skin is just seven colours** in a tiny JSON file. Drop a folder in `/Skins` and
it shows up in the menu instantly.

A few skins go further and change the whole silhouette — these ship built-in and are
always in the **Change Skin** menu:

| Skin | Look |
| --- | --- |
| **Classic Claude** | The iconic terracotta "Claw'd" block with square eyes |
| **Creeper** | The Minecraft creeper face |
| **Ghast** | The floaty Minecraft ghast with dangling tentacles |
| **Nicolaia** | A dapper fellow in a black top hat, three-piece suit and side-curls |
| **Galgo (Bondi 34)** | A smiley cartoon city bus (line 34 Liniers–Palermo) in a Vélez Sarsfield hat |

## 🕹️ Controls

| Action | Result |
| --- | --- |
| **Single click** | Open Claude Desktop (celebrates when it appears!) |
| **Double click** | Pet the mascot — hearts, blush and a happy wiggle |
| **Click & drag** | Pick it up; release with a flick to throw it |
| **Right click** | Open the full menu (skins, speed, volume, startup, photo mode…) |
| **Move mouse fast** | Startle it |

## ⬇️ Install (no coding needed)

Grab the installer from the **[latest release](https://github.com/LbillaSupport/Claude-pet/releases/latest)**
— download **`ClaudeBuddy-win-Setup.exe`** and double-click it. No admin rights, no .NET to
install (it's self-contained). After that it **updates itself automatically** in the
background whenever a new version is published — you never have to reinstall.

Direct download:
<https://github.com/LbillaSupport/Claude-pet/releases/latest/download/ClaudeBuddy-win-Setup.exe>

## 🚀 Run from source (developers)

> **Requirements:** Windows 10/11 (x64) and the **.NET 9 SDK**.

```powershell
# from the repository root
dotnet run --project src/ClaudeBuddy/ClaudeBuddy.csproj -c Release
```

Or open **`ClaudeBuddy.sln`** in **Visual Studio 2022** (17.12+) and press **F5**.

To cut a new release (installer + auto-update packages), run
`.\build-release.ps1 -Version X.Y.Z` and upload `.\Releases\*` to a GitHub Release tagged
`vX.Y.Z`. Full instructions are in [`docs/BUILD.md`](docs/BUILD.md) and `CLAUDE.md`.

## 📚 Documentation

| Guide | What's inside |
| --- | --- |
| [Build instructions](docs/BUILD.md) | Prerequisites, building, publishing |
| [Architecture](docs/ARCHITECTURE.md) | Diagram, project layout, the frame loop |
| [Animation engine](docs/ANIMATION_ENGINE.md) | How poses are synthesised & blended |
| [Skin creation guide](docs/SKINS.md) | Make a skin in two minutes |
| [Mod creation guide](docs/MODS.md) | Add behaviours with pure JSON |
| [Roadmap](docs/ROADMAP.md) | Where it's going |
| [TODO](docs/TODO.md) | Honest list of what's done & what's next |

## 🧩 Tech stack

- **C# 13 / .NET 9** (`net9.0-windows`), native — no game engine.
- **SkiaSharp** software rendering into a **Win32 layered window**
  (`UpdateLayeredWindow`) for true per-pixel transparency and click-through.
- **MVVM-friendly, SOLID, DI** (`Microsoft.Extensions.DependencyInjection`).
- **JSON** configuration & content (`System.Text.Json`).

## 🪶 Performance

Idle CPU stays well under 1% on a modern machine, memory under ~100 MB. The simulation
is delta-time based, the particle pool is capped, and the canvas that follows the
mascot is small — so it's a good desktop citizen.

## ⚖️ License

MIT — see [LICENSE](LICENSE). Claude Buddy is a fan-made, wholesome companion app.

---

<div align="center">
<em>Made to make you smile. 🧡</em>
</div>
