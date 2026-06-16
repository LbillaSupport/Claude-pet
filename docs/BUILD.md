# Build Instructions

## Prerequisites

- **Windows 10 or 11 (x64).**
- **.NET 9 SDK** — download from <https://dotnet.microsoft.com/download/dotnet/9.0>.
  Verify with:
  ```powershell
  dotnet --version   # should print 9.x
  ```
- *(Optional)* **Visual Studio 2022 17.12+** with the **.NET desktop development**
  workload, if you prefer an IDE. The solution also opens in **Visual Studio 2026 /
  Build Tools 18** and in **VS Code** with the C# Dev Kit.

There is nothing else to install — the only third-party dependency, **SkiaSharp**,
is restored automatically from NuGet and ships its own native renderer for `win-x64`.

## Run from the command line

```powershell
git clone <your-fork-url> ClaudeBuddy
cd ClaudeBuddy
dotnet run --project src/ClaudeBuddy/ClaudeBuddy.csproj -c Release
```

The mascot appears at the bottom-centre of your primary monitor.
**Right-click → Exit** (or end the `ClaudeBuddy` process) to close it.

## Run from Visual Studio

1. Open **`ClaudeBuddy.sln`**.
2. Make sure **`ClaudeBuddy`** is the startup project (it is by default).
3. Press **F5** (Debug) or **Ctrl+F5** (Run).

## Publish a single self-contained .exe

To produce one portable executable you can hand to a friend (no .NET install needed):

```powershell
dotnet publish src/ClaudeBuddy/ClaudeBuddy.csproj -c Release -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true
```

The result is in
`src/ClaudeBuddy/bin/Release/net9.0-windows/win-x64/publish/ClaudeBuddy.exe`.
Copy the `Skins/` and `Mods/` folders next to it (and an optional `Assets/Audio/`
folder of `.wav` files) to ship content with it.

## Where your data lives

| Path | Contents |
| --- | --- |
| `%AppData%\ClaudeBuddy\settings.json` | All settings, stats, achievements |
| `%AppData%\ClaudeBuddy\Skins\` | User-installed skins |
| `%AppData%\ClaudeBuddy\Mods\` | User-installed mods |
| `%AppData%\ClaudeBuddy\crash.log` | Created only if something goes wrong |
| `%UserProfile%\Pictures\ClaudeBuddy\` | Photo Mode PNG exports |

Deleting `settings.json` resets the mascot to a fresh state.

## Troubleshooting

- **A black or invisible box instead of the mascot** — this build uses
  `UpdateLayeredWindow`, which requires the desktop to be running normally (not a
  Remote Desktop session with bitmap caching disabled). Try a local session.
- **Sounds are silent** — that's expected until you add `.wav` files to
  `Assets/Audio/` (e.g. `pet.wav`, `jump.wav`). The app is designed to run perfectly
  with no audio assets at all.
- **Clicking passes through the mascot** — clicks only register on visible pixels;
  fully transparent areas intentionally fall through to whatever is behind.
