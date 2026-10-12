// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
// Galaxy Taskbar: animated particles + an electric/fire aura drawn behind transparent Windows 11 taskbars
// (taskbar backgrounds are made transparent by Windhawk's Windows 11 Taskbar Styler).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

class App : ApplicationContext, ICrazyHost
{
    readonly string dir = Path.GetDirectoryName(Application.ExecutablePath);
    readonly string cfgPath;
    Config cfg;
    DateTime cfgTime;
    readonly Bitmap nebula;
    readonly Dictionary<IntPtr, Overlay> overlays = new Dictionary<IntPtr, Overlay>();
    readonly NotifyIcon icon;
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Audio audio = new Audio();
    double last, lastScan = -99, lastHousekeeping = -99, saveAt = -1, cycleAt;
    bool paused, onBattery;
    SettingsForm settings;

    public Config Cfg { get { return cfg; } }
    public string ConfigPath { get { return cfgPath; } }

    public App()
    {
        cfgPath = Path.Combine(dir, "config.json");
        string neb = Path.Combine(dir, "nebula.png");
        if (File.Exists(neb)) nebula = new Bitmap(neb);
        cfg = Config.Load(cfgPath);
        cfgTime = File.GetLastWriteTimeUtc(cfgPath);
        cycleAt = cfg.CycleMinutes * 60;

        icon = new NotifyIcon { Icon = MakeIcon(), Text = "Galaxy Taskbar", ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (s, e) => OpenSettings();
        menu.Opening += (s, e) => BuildMenu();
        BuildMenu();

        timer.Tick += (s, e) => Tick();
        SetFps();
        timer.Start();
    }

    // ---------- tray menu ----------

    protected virtual void BuildMenu()
    {
        menu.Items.Clear();
        var open = new ToolStripMenuItem("Settings...", null, (s, e) => OpenSettings()) { Font = new Font(menu.Font, FontStyle.Bold) };
        menu.Items.Add(open);

        var themes = new ToolStripMenuItem("Theme");
        foreach (var t in cfg.Themes)
        {
            var name = t.Name;
            themes.DropDownItems.Add(new ToolStripMenuItem(name, null, (s, e) => { cfg.ThemeName = name; Changed(); }) { Checked = cfg.Theme.Name == name });
        }
        menu.Items.Add(themes);

        var presets = new ToolStripMenuItem("Presets");
        foreach (var p in Config.Presets)
        {
            var name = p;
            presets.DropDownItems.Add(name, null, (s, e) => { cfg.ApplyPreset(name); Changed(); });
        }
        menu.Items.Add(presets);

        menu.Items.Add(new ToolStripSeparator());
        AddToggle("Music reactive", () => cfg.MusicReactive, v => cfg.MusicReactive = v);
        AddToggle("Fire curtain", () => cfg.FireCurtain, v => cfg.FireCurtain = v);
        AddToggle("Lightning", () => cfg.Lightning, v => cfg.Lightning = v);
        AddExtraMenuItems(menu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(paused ? "Resume" : "Pause", null, (s, e) => { paused = !paused; if (paused) foreach (var o in overlays.Values) o.HideOverlay(); });
        menu.Items.Add("Exit", null, (s, e) => ExitThread());
    }

    protected void AddToggle(string text, Func<bool> get, Action<bool> set)
    {
        menu.Items.Add(new ToolStripMenuItem(text, null, (s, e) => { set(!get()); Changed(); }) { Checked = get() });
    }

    protected virtual void AddExtraMenuItems(ContextMenuStrip m)
    {
        AddToggle("Crazy mode", () => cfg.Chaos, v => cfg.Chaos = v);
        AddToggle("Meme mode", () => cfg.Meme, v => cfg.Meme = v);
        var ev = new ToolStripMenuItem("Do something crazy");
        ev.DropDownItems.Add("Surprise me!", null, (s, e) => Trigger(null));
        ev.DropDownItems.Add(new ToolStripSeparator());
        foreach (var k in Director.Kinds)
        {
            var name = k.Name;
            ev.DropDownItems.Add(name, null, (s, e) => Trigger(name));
        }
        m.Items.Add(ev);
    }

    public void Trigger(string eventName)
    {
        foreach (var o in overlays.Values) { var co = o as CrazyOverlay; if (co != null) co.Director.Request(eventName); }
    }

    protected virtual SettingsForm CreateSettings() { return new CrazySettings(this); }

    public void OpenSettings()
    {
        if (settings == null || settings.IsDisposed) settings = CreateSettings();
        settings.Show();
        settings.Activate();
    }

    // ---------- config ----------

    public void Changed()
    {
        foreach (var o in overlays.Values) o.Apply(cfg);
        SetFps();
        saveAt = clock.Elapsed.TotalSeconds + 0.8;     // debounce slider drags
    }

    void SaveNow()
    {
        saveAt = -1;
        try { cfg.Save(cfgPath); cfgTime = File.GetLastWriteTimeUtc(cfgPath); } catch { }
    }

    void SetFps()
    {
        int fps = onBattery ? Math.Min(cfg.BatteryFps, cfg.Fps) : cfg.Fps;
        timer.Interval = Math.Max(1, 1000 / fps);
    }

    static IEnumerable<IntPtr> Taskbars()
    {
        var main = Win.FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero) yield return main;
        IntPtr h = IntPtr.Zero;
        while ((h = Win.FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero) yield return h;
    }

    protected virtual Overlay CreateOverlay(IntPtr taskbar) { return new CrazyOverlay(taskbar, cfg, nebula); }
    protected IEnumerable<Overlay> Overlays { get { return overlays.Values; } }

    void Tick()
    {
        double now = clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(0.25, now - last);
        last = now;

        if (saveAt > 0 && now >= saveAt) SaveNow();

        if (now - lastHousekeeping > 1)
        {
            lastHousekeeping = now;
            // pick up hand edits to config.json
            try
            {
                var wt = File.GetLastWriteTimeUtc(cfgPath);
                if (wt != cfgTime && saveAt < 0)
                {
                    cfg = Config.Load(cfgPath);
                    cfgTime = wt;
                    foreach (var o in overlays.Values) o.Apply(cfg);
                    SetFps();
                    if (settings != null && !settings.IsDisposed) { settings.Close(); settings = null; }
                }
            }
            catch { }
            bool battery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
            if (battery != onBattery) { onBattery = battery; SetFps(); }

            if (cfg.AutoCycle && now >= cycleAt)
            {
                cycleAt = now + cfg.CycleMinutes * 60;
                var all = cfg.Themes;
                int i = all.FindIndex(t => t.Name == cfg.Theme.Name);
                cfg.ThemeName = all[(i + 1) % all.Count].Name;
                Changed();
            }
            else if (!cfg.AutoCycle) cycleAt = now + cfg.CycleMinutes * 60;
        }

        if (now - lastScan > 2)
        {
            lastScan = now;
            var live = new HashSet<IntPtr>(Taskbars());
            foreach (var h in overlays.Keys.Where(k => !live.Contains(k)).ToList()) { overlays[h].Dispose(); overlays.Remove(h); }
            foreach (var h in live) if (!overlays.ContainsKey(h)) overlays[h] = CreateOverlay(h);
        }

        if (paused) return;
        float level = 0, beat = 0;
        if (cfg.MusicReactive)
        {
            audio.Update(now, dt, (float)cfg.AudioSensitivity);
            level = audio.Level; beat = audio.Beat;
        }
        foreach (var o in overlays.Values) o.Tick((float)now, dt, level, beat);
    }

    static Icon MakeIcon()
    {
        var b = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(40, 10, 80));
            using (var p = new Pen(Color.FromArgb(79, 195, 255), 1.5f)) { g.DrawLine(p, 8, 1, 8, 15); g.DrawLine(p, 1, 8, 15, 8); }
            g.FillEllipse(Brushes.White, 5, 5, 6, 6);
        }
        return Icon.FromHandle(b.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        if (saveAt > 0) SaveNow();
        icon.Visible = false;
        foreach (var o in overlays.Values) o.Dispose();
        base.ExitThreadCore();
    }

    [STAThread]
    static void Main()
    {
        bool created;
        using (var m = new Mutex(true, "GalaxyTaskbarOverlay", out created))
        using (var showSettings = new EventWaitHandle(false, EventResetMode.AutoReset, "GalaxyTaskbarShowSettings"))
        {
            string trigger = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "trigger.txt");
            var args = Environment.GetCommandLineArgs();
            int ei = Array.IndexOf(args, "--event");
            string evName = ei >= 0 ? (ei + 1 < args.Length ? args[ei + 1] : "random") : null;
            // already running: launching it again opens settings, or fires an event with --event "Name"
            if (!created)
            {
                if (evName != null) try { File.WriteAllText(trigger, evName); } catch { }
                showSettings.Set();
                return;
            }
            Win.SetProcessDPIAware();
            Application.EnableVisualStyles();
            var app = new App();
            var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            ThreadPool.RegisterWaitForSingleObject(showSettings, (st, to) => ui.Post(_ =>
            {
                string req = null;
                try { if (File.Exists(trigger)) { req = File.ReadAllText(trigger).Trim(); File.Delete(trigger); } } catch { }
                if (req == null) app.OpenSettings();
                else app.Trigger(req.Equals("random", StringComparison.OrdinalIgnoreCase) ? null : req);
            }, null), null, -1, false);
            if (Environment.GetCommandLineArgs().Contains("--settings")) app.OpenSettings();
            Application.Run(app);
        }
    }
}
