# Galaxy Suite: How to Use Everything

Copyright (c) 2026 drdonut54. All rights reserved. See [LICENSE](LICENSE).

This guide covers:

1. [Building the programs](#1-building-the-programs)
2. [Galaxy Taskbar: animated taskbar](#2-galaxy-taskbar)
3. [Galaxy Trainer: single-player game trainer](#3-galaxy-trainer)
4. [The website](#4-the-website)
5. [Troubleshooting](#5-troubleshooting)
6. [Uninstalling / undoing everything](#6-uninstalling--undoing-everything)

---

## 1. Building the programs

You don't need to install anything to build. Windows already includes the compiler.

1. Download this repo: on GitHub, click **Code → Download ZIP** and unzip it somewhere, for example `C:\GalaxySuite`.
2. Double-click **`build.bat`**.
3. You should see: `Built GalaxyTaskbar\GalaxyTaskbar.exe and GalaxyTrainer\GalaxyTrainer.exe`.

If it says the compiler wasn't found, see [Troubleshooting](#5-troubleshooting).

---

## 2. Galaxy Taskbar

An animated background for the Windows 11 taskbar. It draws drifting particles, a slowly flowing
background and a glowing, crackling electric line along the top edge of the taskbar. Your icons
stay on top and work normally.

### How it works (in one sentence)

The free program **Windhawk** makes the taskbar's own background see-through, and Galaxy Taskbar draws
the animation in a click-through window just behind it.

### First-time setup

**Step 1: Install Windhawk**

1. Go to [windhawk.net](https://windhawk.net), download it and install it.
   (Or open a terminal and run `winget install RamenSoftware.Windhawk`.)
2. Open Windhawk, click **Explore**, search for **Windows 11 Taskbar Styler**, click it, then click **Install**.
   Click **Yes** on the Windows admin prompt.

**Step 2: Make the taskbar see-through**

1. In Windhawk, open the **Windows 11 Taskbar Styler** mod and go to the **Advanced** tab.
2. Scroll down to **Mod settings**.
3. Open `TaskbarStyler\animated-settings.json` from this repo in Notepad, and copy everything in it.
4. Click inside the Mod settings box, press **Ctrl+A**, then **Ctrl+V** to replace what's there.
5. Click **Load**, not Save. "Save" only exports the settings to a file.

Your taskbar will now show your desktop through it. That's expected; the animation fills it in next.

**Step 3: Start the animation**

1. Double-click `GalaxyTaskbar\GalaxyTaskbar.exe`.
2. The taskbar should come alive within a second. A small **star icon** appears in the system tray
   (click the **^** arrow next to the clock if you don't see it).

**Step 4 (optional): Start it automatically when you log in**

1. Press **Win+R**, type `shell:startup` and press Enter. A folder opens.
2. Right-click `GalaxyTaskbar.exe`, choose **Show more options → Create shortcut**, and move the shortcut into that folder.

### Everyday use: the tray icon

Right-click the star icon in the system tray:

| Menu item | What it does |
| --- | --- |
| **Theme ▸** | Switch theme instantly: Galaxy, Matrix, Fire, Blood, Frost, plus any you make. |
| **Pause / Resume** | Stops the animation (for example, to save battery). |
| **Edit config.json** | Opens the settings file in Notepad. |
| **Exit** | Closes the animation. |

### The themes

| Theme | Look |
| --- | --- |
| **Galaxy** | Purple/pink/blue nebula, twinkling stars, shooting stars, violet-cyan-pink aura. |
| **Matrix** | Black-green background with falling green code and a green aura. |
| **Fire** | Dark red background with glowing embers rising up and an orange-yellow aura. |
| **Blood** | Deep red with red stars and a crimson aura. |
| **Frost** | See-through pale glass with slow white sparkles. |

### Customizing: `config.json`

`config.json` is created next to `GalaxyTaskbar.exe` the first time it runs. Edit it in Notepad and
**save**. Changes apply within a second, with no restart needed.

| Setting | Default | Meaning |
| --- | --- | --- |
| `theme` | `"Galaxy"` | Which theme is active. The tray menu changes this for you. |
| `fps` | `30` | Animation smoothness (5–60). Lower uses less CPU. |
| `batteryFps` | `15` | Frame rate used automatically when a laptop is unplugged. |
| `auraHeight` | `18` | How far the glow rises above the taskbar, in pixels. `0` turns the glow and lightning off. |
| `starDensity` | `1.0` | How many particles. `0.5` is half, `2.0` is double. |
| `lightning` | `true` | The crackling electric arcs along the top edge. |
| `shootingStars` | `true` | Occasional shooting stars. |
| `customThemes` | example | Your own themes (see below). |

**Making your own theme**: add an entry under `customThemes`:

```json
"customThemes": {
  "Synthwave": {
    "background": "#12002A",
    "colors": ["#FF2BD6", "#00E5FF", "#FFB000"],
    "star": "#FFFFFF",
    "particles": "stars"
  },
  "Ocean": {
    "background": "#001020",
    "colors": ["#00A8FF", "#00FFD0", "#0050FF"],
    "star": "#CCF6FF",
    "particles": "frost"
  }
}
```

- `background`: the base colour. Use `#AARRGGBB` to make it see-through (for example `#80000000` is half-transparent black).
- `colors`: three colours used for the clouds, the glow and the lightning.
- `star`: the particle colour.
- `particles`: `stars` (drift sideways), `embers` (float up), `code` (fall down) or `frost` (slow sparkles).

Save the file and your theme appears in the tray's **Theme** menu.

### Good to know

- **Clicks pass straight through** the animation, so it never blocks anything.
- It **hides itself** when a game or video goes full-screen, and when the taskbar is set to auto-hide.
- It works on **every monitor's taskbar** automatically.
- The glow rises a little above the taskbar, so it softly overlaps the bottom edge of maximized windows (`auraHeight` controls this).
- CPU use is well under 1% of one core at 30 fps.

### Static galaxy (no animation)

If you just want a still galaxy picture on the taskbar without running Galaxy Taskbar:

1. Open `TaskbarStyler\static-galaxy-settings.json` in Notepad.
2. Replace `PATH_TO_REPO` with the folder you unzipped to, using forward slashes. For example: `file:///C:/GalaxySuite/TaskbarStyler/galaxy.png`.
3. Load it into the Taskbar Styler mod the same way as in Step 2 above.

---

## 3. Galaxy Trainer

A Cheat-Engine-style memory editor for **single-player** games, both desktop games and games running in a browser tab.
It finds values like health, ammo and money in the game's memory, then lets you set or freeze them.

> ⚠️ **Single-player only.** Galaxy Trainer refuses to attach to online games and to games that
> ship with anti-cheat (EasyAntiCheat, BattlEye, Vanguard and others). Cheating online ruins the game
> for other people and gets accounts banned. For browser games, only use it on single-player ones.

### Opening it and attaching to a game

1. Start your game first.
2. Double-click `GalaxyTrainer\GalaxyTrainer.exe`.
3. Pick your game in the drop-down at the top. Browser games are listed as **[Browser] ...**.
   Click **Refresh list** if your game isn't there.
4. Click **Attach**. The status bar shows "Attached: ...".

If it says it couldn't open the game's memory, close it and run it as administrator
(right-click → **Run as administrator**).

### Option A: Smart auto-detect (easiest)

The trainer finds health, ammo and money **by itself, by process of elimination**, while you play.

1. Click **Start smart auto-detect (just play)**.
2. **Stand still for about 5 seconds.** It's calibrating: it throws out everything that changes on its own, such as timers, positions and animations.
3. **Play normally.** Get hurt, heal, shoot, reload, buy things, pick up coins.
4. When it's confident, it **adds the value to the cheat table automatically** and you'll hear a sound.
   A small box in the top-left corner of your screen tells you what it found.

The results list shows what it's watching, with **Looks like** and **Confidence** columns:
- **Green** (70%+): very likely.
- **Yellow** (45–69%): possible.

Tips:
- **Health** is usually found after you've been hit **3 or more times** over at least 20 seconds. It needs a matching "max health" value next to it in memory, which most games have.
- **Ammo** is found after you fire a few shots and then reload.
- **Money** is harder to tell apart from random numbers, so it often only appears as a *possible* match. Use the money wizard (Option B) for money; it's fast and exact.
- Click **Stop auto-detect** when you're done.

### Option B: Wizards

**Find money / ammo / count**: best for any number you can *see* on screen.

1. Click **Find money / ammo / count**.
2. Type the exact number shown in the game (for example `250`) and click **OK**.
3. In the game, make the number change: spend some, earn some, fire a shot.
4. Type the **new** number in the **Value** box and click **Next: exact value**.
5. Repeat steps 3–4 once or twice. When only a few results are left, they're **added to the cheat table automatically**.

**Find health / bars**: for values shown as a bar, without a number.

1. Click **Find health / bars**.
2. Go back to the game and use these hotkeys. They work while the game is open:
   - Took damage → **Ctrl+Alt+D**
   - Walked around without getting hurt → **Ctrl+Alt+U** (do this 2–3 times)
   - Healed → **Ctrl+Alt+I**
3. You'll hear a sound after each one. Keep going until it says **Found it!**

### Option C: Manual scanning (full control)

For anyone who has used Cheat Engine before.

1. Tick the **value types** to search: `Int32` (whole numbers), `Float` / `Double` (decimals, common for health bars), `JS Int` (browser games).
2. Start with **First scan: exact** (type a value) or **First scan: unknown** (no value).
3. Narrow it down with the **Next** buttons:
   - **exact value**: is now this number
   - **changed** / **unchanged**
   - **increased** / **decreased**
   - **up by value** / **down by value**: changed by the amount in the Value box
4. **Double-click** a result to add it to the cheat table.
5. **New scan (reset)** starts over.

The hotkeys (Ctrl+Alt+D / I / U / C) run the matching "Next" scan at any time.

### Using the cheat table

The bottom half of the window. Each row is one value in the game.

| Action | How |
| --- | --- |
| **Freeze** (lock the value) | Tick the box next to it. The game can't change it, for example infinite health. |
| **Set a value** | Select the row → **Set value...** (or double-click it). |
| **Max it** | Select the row → **Max it** (sets 999999; Float values get 9999). |
| **Freeze / unfreeze everything** | **Freeze all** / **Unfreeze all**. |
| **Rename** | Select → **Rename** (for example "Health"). |
| **Remove** | Select → **Remove**. |
| **Save / load** | **Save table** / **Load table** (saved in `Documents\GalaxyTrainer\`). |

**If changing a value doesn't seem to work:**
- Games often keep a *display copy* of a value plus the *real* one. Try each candidate, or freeze them all.
- Some games only update the on-screen number after something happens, like taking another hit.
- Saved tables often stop working after you restart the game, because games put values in a different place in memory each time.
  Just run the wizard or auto-detect again; it only takes a minute.

### Browser games

1. Open the game in Chrome, Edge, Brave, Opera, Vivaldi or Firefox.
2. In the trainer, pick **[Browser] ...** for your browser and click **Attach**. It scans the browser's tab processes.
3. Use auto-detect or the wizards as normal. **Double** and **JS Int** are ticked for you, which is how JavaScript stores numbers.
4. Close other tabs first to make scans faster.

### Hotkey summary

| Hotkey | Meaning |
| --- | --- |
| **Ctrl+Alt+D** | The value went **down**. |
| **Ctrl+Alt+I** | The value went **up**. |
| **Ctrl+Alt+U** | The value did **not change**. |
| **Ctrl+Alt+C** | The value **changed** (either way). |

---

## 4. The website

`website\index.html` is a single galaxy-themed page with moving stars. Double-click it to open it in your browser.
To change the text or email address, open it in Notepad and edit the lines near the top of `<main>`.

To put it online for free, upload `index.html` to any static host (GitHub Pages, Netlify and others).

---

## 5. Troubleshooting

| Problem | Fix |
| --- | --- |
| `build.bat` says the compiler wasn't found | Turn on **.NET Framework 3.5/4.8** under *Control Panel → Programs → Turn Windows features on or off*. |
| Taskbar shows my desktop but no animation | `GalaxyTaskbar.exe` isn't running. Start it, and check for the star icon in the tray. |
| Animation shows but covers my icons | The Taskbar Styler settings weren't applied. Redo Setup Step 2 and click **Load**. |
| Pasting into Mod settings does nothing | Make sure you click **Load**, not Save, and replace the entire box (Ctrl+A first). |
| Taskbar went back to normal after a Windows update | Windows sometimes resets Explorer. Open Windhawk and check the Taskbar Styler mod is still enabled. |
| Trainer: "Not allowed" | That game is online or uses anti-cheat. This is on purpose. |
| Trainer: "Couldn't open the game's memory" | Run Galaxy Trainer as administrator. |
| Trainer: 0 results | The value may be stored differently. Tick more value types, click **New scan**, and try again. |
| Trainer: auto-detect never finds health | Get hit a few more times. Some games store health unusually; use the **Find health / bars** wizard instead. |
| Game crashed after setting a value | It was the wrong address. Restart the game and try a different candidate. |

---

## 6. Uninstalling / undoing everything

- **Stop the animation:** tray star icon → **Exit**. Delete its shortcut from `shell:startup` if you added one.
- **Normal taskbar background:** in Windhawk, turn off (or uninstall) the **Windows 11 Taskbar Styler** mod.
- **Remove Windhawk:** *Settings → Apps → Installed apps → Windhawk → Uninstall*.
- **Galaxy Trainer:** close it and delete its folder. Saved cheat tables are in `Documents\GalaxyTrainer\`.
- Nothing else is installed: no services, drivers or registry entries.
