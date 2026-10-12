# Galaxy Suite

Windows 11 customization and tools by **drdonut54**.

> **All rights reserved.** This code is published for viewing only. You may not
> copy, modify, redistribute or sell it. See [LICENSE](LICENSE).

## What's inside

| Folder | What it is |
| --- | --- |
| `GalaxyTaskbar/` | Animated taskbar: drifting particles, a flowing background and a crackling electric "aura" along the top edge, drawn behind a transparent Windows 11 taskbar. |
| `GalaxyTrainer/` | Memory scanner / trainer for **single-player** games (desktop and browser), with a smart auto-detector for health, ammo and money. |
| `TaskbarStyler/` | Settings for the Windhawk *Windows 11 Taskbar Styler* mod used by Galaxy Taskbar. |
| `website/` | A single-page galaxy-themed website. |

## Galaxy Taskbar

- Themes: **Galaxy**, **Matrix**, **Fire**, **Blood**, **Frost**, plus your own in `config.json`.
- Pick a theme from the tray icon. `config.json` is created next to the exe on first run and reloads automatically when you edit it.
- One overlay per taskbar, so secondary monitors work too.
- Click-through, hides for full-screen apps, and drops to `batteryFps` on battery.
- Uses well under 1% of one CPU core.

`config.json`:

```json
{
  "theme": "Galaxy",
  "fps": 30,
  "batteryFps": 15,
  "auraHeight": 18,
  "starDensity": 1.0,
  "lightning": true,
  "shootingStars": true,
  "customThemes": {
    "Synthwave": {
      "background": "#12002A",
      "colors": ["#FF2BD6", "#00E5FF", "#FFB000"],
      "star": "#FFFFFF",
      "particles": "stars"
    }
  }
}
```

`particles` can be `stars`, `embers`, `code` or `frost`. Colours are `#RRGGBB` or `#AARRGGBB`.

**Setup**

1. Install [Windhawk](https://windhawk.net) and its *Windows 11 Taskbar Styler* mod.
2. Paste `TaskbarStyler/animated-settings.json` into the mod's **Advanced → Mod settings** box and click **Load**.
   This makes the taskbar background transparent so the animation shows behind your icons.
3. Run `build.bat`, then start `GalaxyTaskbar\GalaxyTaskbar.exe`.

For a static (non-animated) galaxy taskbar instead, use `TaskbarStyler/static-galaxy-settings.json`.
Replace `PATH_TO_REPO` in it with where you put this folder.

## Galaxy Trainer

- Attach to a game, then click **Start smart auto-detect** and just play. It works by process of elimination:
  - It throws out values that change on their own (timers, positions, animation), float jitter and mass memory rewrites (loading screens).
  - It follows values that stay quiet and then change like a stat.
  - Health needs a stable, round-numbered "max" value right next to it, several hits of different sizes and 20+ seconds of play before it's auto-added.
  - Ammo is spotted by drops of 1 followed by a refill. Money moves both ways in chunks and has no max.
- Wizards and a full manual scanner (exact, unknown, increased, decreased, changed, unchanged, by amount) are also included. In-game hotkeys:
  - `Ctrl+Alt+D`: the value went down
  - `Ctrl+Alt+I`: the value went up
  - `Ctrl+Alt+U`: the value didn't change
  - `Ctrl+Alt+C`: the value changed
- In the cheat table you can set, max or freeze values, and save tables per game.
- Browser games: scans the browser's tab processes, including V8 small-integer encoding.

**Single-player only.** The trainer refuses to attach to known online games and to games that ship with
EasyAntiCheat, BattlEye, Vanguard or similar anti-cheat. Editing online games ruins them for other players
and gets accounts banned.

`GalaxyTrainer/test/` holds fake games (one with health/ammo/money and one with only noise) plus a
headless harness used to check the detector finds real stats and stays quiet on games without them.

## Building

Requires only the .NET Framework 4.x compiler that ships with Windows. Run:

```
build.bat
```
