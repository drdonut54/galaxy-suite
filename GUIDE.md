# Galaxy Suite: How to Use Everything

Copyright (c) 2026 drdonut54. All rights reserved. See [LICENSE](LICENSE).

This guide covers:

1. [Building the programs](#1-building-the-programs)
2. [Galaxy Taskbar: animated taskbar](#2-galaxy-taskbar)
3. [The website](#3-the-website)
4. [Troubleshooting](#4-troubleshooting)
5. [Uninstalling / undoing everything](#5-uninstalling--undoing-everything)

---

## 1. Building the programs

You don't need to install anything to build. Windows already includes the compiler.

1. Download this repo: on GitHub, click **Code → Download ZIP** and unzip it somewhere, for example `C:\GalaxySuite`.
2. Double-click **`build.bat`**.
3. You should see: `Built GalaxyTaskbar\GalaxyTaskbar.exe`.

If it says the compiler wasn't found, see [Troubleshooting](#4-troubleshooting).

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

### Everyday use

Right-click the star icon in the system tray for quick controls:

| Menu item | What it does |
| --- | --- |
| **Settings...** | Opens the Settings window. Double-clicking the tray icon does the same. |
| **Theme ▸** | Switch theme instantly. |
| **Presets ▸** | Chill, Aura Max, Inferno, Rave or Default, in one click. |
| **Music reactive / Fire curtain / Lightning** | Quick on/off toggles. |
| **Pause / Resume** | Stops the animation. |
| **Exit** | Closes it. |

Tip: running `GalaxyTaskbar.exe` again while it's already running also opens Settings.

### The Settings window

Every change applies **instantly** and is saved automatically.

**Look**
- **Quick presets**:
  - **Chill**: slow and calm
  - **Aura Max**: huge glow, lightning, plasma, sparks and fire
  - **Inferno**: everything on fire
  - **Rave**: music-reactive visualizer and plasma
  - **Default**
- **Theme** and **Particles**. Particles can follow the theme, or you can force any style on any theme.
- **Particle amount** and **Speed**.
- **Shooting stars**, and **Auto-cycle themes** every N minutes.

**Aura**

| Setting | What it does |
| --- | --- |
| **Glow**, **Glow strength**, **Glow height** | The coloured light rising off the taskbar. |
| **Live wire** | A jittering white-hot line along the edge. |
| **Lightning arcs** and **Lightning amount** | Crackling electric arcs. |
| **Plasma waves** | Glowing waves weaving along the edge. |
| **Sparks** | Sparks shooting up and falling back. |
| **Fire curtain** | Flames licking up from the taskbar, in your theme's colours. |
| **Flame particles** | Individual flames rising off the edge. |
| **Fire height** | How tall the fire gets. |
| **Music reactive** and **Music sensitivity** | Everything pulses to the music playing on your PC. Turn sensitivity up if it barely moves. |
| **Visualizer bars** | Equalizer bars bouncing above the taskbar. |

**Colors: the theme builder**
1. Click **Start from current theme**.
2. Name it, then click **Pick...** next to each colour. Use **+ Add colour** for more; rainbow-style themes use 6.
3. Choose a particle style and click **Save and use this theme**.

Your theme appears in the theme list and the tray menu. **Delete this custom theme** removes it.

**Performance**
- **Frames per second**, and a separate **On battery** frame rate for laptops.
- **Open config.json**, and **Reset everything** (custom themes are kept).

### The themes

Galaxy · Matrix · Fire · Blood · Frost · Synthwave · Ocean · Toxic · Sunset · Rainbow · Vaporwave · Gold ·
Aurora · Lava · Cyberpunk · Void · Candy · Halloween · Christmas · Neon · Love · Ice

### config.json (optional)

Everything in Settings is stored in `config.json` next to the exe. You can edit it by hand. It reloads within a second of saving, and colours use `#RRGGBB` or `#AARRGGBB`.
Most people never need to; the Settings window covers it all.

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

## 3. The website

`website\index.html` is a single galaxy-themed page with moving stars. Double-click it to open it in your browser.
To change the text or email address, open it in Notepad and edit the lines near the top of `<main>`.

To put it online for free, upload `index.html` to any static host (GitHub Pages, Netlify and others).

---

## 4. Troubleshooting

| Problem | Fix |
| --- | --- |
| `build.bat` says the compiler wasn't found | Turn on **.NET Framework 3.5/4.8** under *Control Panel → Programs → Turn Windows features on or off*. |
| Taskbar shows my desktop but no animation | `GalaxyTaskbar.exe` isn't running. Start it, and check for the star icon in the tray. |
| Animation shows but covers my icons | The Taskbar Styler settings weren't applied. Redo Setup Step 2 and click **Load**. |
| Pasting into Mod settings does nothing | Make sure you click **Load**, not Save, and replace the entire box (Ctrl+A first). |
| Taskbar went back to normal after a Windows update | Windows sometimes resets Explorer. Open Windhawk and check the Taskbar Styler mod is still enabled. |
| Game crashed after setting a value | It was the wrong address. Restart the game and try a different candidate. |

---

## 5. Uninstalling / undoing everything

- **Stop the animation:** tray star icon → **Exit**. Delete its shortcut from `shell:startup` if you added one.
- **Normal taskbar background:** in Windhawk, turn off (or uninstall) the **Windows 11 Taskbar Styler** mod.
- **Remove Windhawk:** *Settings → Apps → Installed apps → Windhawk → Uninstall*.
- Nothing else is installed: no services, drivers or registry entries.
