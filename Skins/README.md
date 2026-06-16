# Skins

Drop a folder in here (or in `%AppData%\ClaudeBuddy\Skins`) containing a `skin.json`
file and it will appear under **Right-click → Change Skin**. No restart needed — the
menu rescans every time it opens.

Because Claude Buddy is drawn from vectors, a skin is just a palette. The minimum
valid skin is:

```json
{ "name": "My Skin", "colors": { "body": "#7AA2FF" } }
```

Any colour you omit falls back to the built-in Classic Claude palette. See
[`docs/SKINS.md`](../docs/SKINS.md) for the full reference and a colour cheat-sheet.

The two folders shipped here — **Midnight Neon** and **Spring Sprout** — are working
examples you can copy and tweak.
