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

- **22 themes**, including Galaxy, Matrix, Fire, Blood, Frost, Synthwave, Ocean, Toxic, Sunset, Rainbow, Vaporwave, Gold, Aurora, Lava, Cyberpunk, Void, Candy, Halloween, Christmas, Neon, Love and Ice, plus your own.
- **8 particle styles**: stars, frost, embers, falling code, snow, bubbles, hearts and confetti.
- **Aura effects**, each switchable:
  - glow
  - live wire
  - lightning arcs
  - fire curtain
  - flame particles
  - plasma waves
  - sparks
  - music visualizer bars
- **Music reactive**: the glow, lightning, fire and sparks pulse to whatever is playing.
- **Crazy mode & meme mode**: random events take over the taskbar. Ideas include:
  - real memes dancing, raining down, popping up, marching and bouncing like a DVD logo
  - fireworks, a black hole, a disco ball, a snake
  - lightning storms, glitches, jelly wobble, earthquakes, gravity flips, theme morphs
  - Comic Sans meme text, MLG hitmarkers and "deal with it" shades
- **Real meme images**: popular meme templates are downloaded from [Imgflip](https://imgflip.com)'s public meme list on first use and cached on your PC. No meme images are included in this repository.
- **Settings window**: double-click the tray icon, or run the exe again while it's running. It has presets (Chill, Aura Max, Inferno, Rave, Meme Lord, Total Chaos), sliders for everything and a theme builder with colour pickers.
- One overlay per taskbar, so secondary monitors work too. Click-through, hides for full-screen apps, and drops its frame rate on battery.
- Everything is saved to `config.json` next to the exe, which you can also hand-edit; it reloads automatically.

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
