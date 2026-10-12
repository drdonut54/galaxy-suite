# Galaxy Suite

Windows 11 customization by **drdonut54**.

> **All rights reserved.** This code is published for viewing only. You may not
> copy, modify, redistribute or sell it. See [LICENSE](LICENSE).

📖 **New here? Read the step-by-step [GUIDE.md](GUIDE.md)**. It covers setup, every button, themes, troubleshooting and uninstalling.

## What's inside

| Folder | What it is |
| --- | --- |
| `GalaxyTaskbar/` | Animated taskbar: drifting particles, a flowing background and a crackling electric "aura" along the top edge, drawn behind a transparent Windows 11 taskbar. |
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

## Building

Requires only the .NET Framework 4.x compiler that ships with Windows. Run:

```
build.bat
```
