// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

interface IHost
{
    Config Cfg { get; }
    string ConfigPath { get; }
    void Changed();          // live-apply + save soon
}

// Dark settings window. Every control applies instantly.
class SettingsForm : Form
{
    static readonly Color Bg = Color.FromArgb(14, 8, 30), Panel2 = Color.FromArgb(26, 14, 52), Fg = Color.FromArgb(236, 228, 255),
        Accent = Color.FromArgb(166, 108, 245), Cyan = Color.FromArgb(79, 195, 255), Muted = Color.FromArgb(170, 155, 205);

    protected readonly IHost host;
    readonly FlowLayoutPanel nav = new FlowLayoutPanel();
    readonly FlowLayoutPanel page = new FlowLayoutPanel();
    readonly List<KeyValuePair<string, Action>> sections = new List<KeyValuePair<string, Action>>();
    string current;

    Config C { get { return host.Cfg; } }

    public SettingsForm(IHost h)
    {
        host = h;
        Text = "Galaxy Taskbar Settings";
        Size = new Size(820, 640);
        MinimumSize = new Size(700, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = Fg;
        Font = new Font("Segoe UI", 9.5f);

        page.Dock = DockStyle.Fill;
        page.FlowDirection = FlowDirection.TopDown;
        page.WrapContents = false;
        page.AutoScroll = true;
        page.Padding = new Padding(18, 12, 18, 18);
        Controls.Add(page);

        nav.Dock = DockStyle.Left;
        nav.Width = 170;
        nav.FlowDirection = FlowDirection.TopDown;
        nav.Padding = new Padding(10, 14, 10, 10);
        nav.BackColor = Panel2;
        Controls.Add(nav);

        AddSections();
        foreach (var s in sections)
        {
            var name = s.Key;
            var b = new Button { Text = name, Width = 148, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = Panel2, ForeColor = Fg, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6) };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (o, e) => ShowPage(name);
            nav.Controls.Add(b);
        }
        ShowPage(sections[0].Key);
    }

    protected virtual void AddSections()
    {
        sections.Add(new KeyValuePair<string, Action>("Look", PageLook));
        sections.Add(new KeyValuePair<string, Action>("Aura", PageAura));
        sections.Add(new KeyValuePair<string, Action>("Colors", PageColors));
        sections.Add(new KeyValuePair<string, Action>("Performance", PagePerformance));
    }

    protected void AddSection(string name, Action build, int index)
    {
        sections.Insert(Math.Min(index, sections.Count), new KeyValuePair<string, Action>(name, build));
    }

    void ShowPage(string name)
    {
        current = name;
        foreach (Button b in nav.Controls) b.BackColor = b.Text == name ? Color.FromArgb(60, 30, 110) : Panel2;
        page.SuspendLayout();
        page.Controls.Clear();
        sections.First(s => s.Key == name).Value();
        page.ResumeLayout();
    }

    protected void Refresh2() { ShowPage(current); }

    void Changed() { host.Changed(); }

    // ---------- control helpers ----------

    protected void Head(string text, string sub = null)
    {
        page.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = Cyan, Font = new Font("Segoe UI", 13f, FontStyle.Bold), Margin = new Padding(0, page.Controls.Count == 0 ? 0 : 14, 0, 2) });
        if (sub != null) page.Controls.Add(new Label { Text = sub, AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Muted, Margin = new Padding(0, 0, 0, 6) });
    }

    protected CheckBox Check(string label, Func<bool> get, Action<bool> set, string tip = null)
    {
        var cb = new CheckBox { Text = label, AutoSize = true, Checked = get(), ForeColor = Fg, Margin = new Padding(2, 4, 0, tip == null ? 4 : 0), Font = new Font("Segoe UI", 10f) };
        cb.CheckedChanged += (o, e) => { set(cb.Checked); Changed(); };
        page.Controls.Add(cb);
        if (tip != null) page.Controls.Add(new Label { Text = tip, AutoSize = true, MaximumSize = new Size(540, 0), ForeColor = Muted, Margin = new Padding(22, 0, 0, 4) });
        return cb;
    }

    protected void Slider(string label, double min, double max, double step, Func<double> get, Action<double> set, string fmt = "0.0")
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 2) };
        var l = new Label { Text = label, Width = 170, ForeColor = Fg, TextAlign = ContentAlignment.MiddleLeft, Height = 30 };
        int steps = (int)Math.Round((max - min) / step);
        var tb = new TrackBar { Minimum = 0, Maximum = steps, Width = 300, TickStyle = TickStyle.None, BackColor = Bg, Height = 30 };
        tb.Value = Math.Max(0, Math.Min(steps, (int)Math.Round((get() - min) / step)));
        var v = new Label { Width = 60, ForeColor = Cyan, TextAlign = ContentAlignment.MiddleLeft, Height = 30, Text = get().ToString(fmt) };
        tb.ValueChanged += (o, e) => { double val = min + tb.Value * step; set(val); v.Text = val.ToString(fmt); Changed(); };
        row.Controls.Add(l); row.Controls.Add(tb); row.Controls.Add(v);
        page.Controls.Add(row);
    }

    protected ComboBox Combo(string label, string[] items, Func<string> get, Action<string> set)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
        row.Controls.Add(new Label { Text = label, Width = 170, ForeColor = Fg, TextAlign = ContentAlignment.MiddleLeft, Height = 28 });
        var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, BackColor = Panel2, ForeColor = Fg, FlatStyle = FlatStyle.Flat };
        cb.Items.AddRange(items);
        cb.SelectedItem = items.FirstOrDefault(i => i.Equals(get(), StringComparison.OrdinalIgnoreCase)) ?? items[0];
        cb.SelectedIndexChanged += (o, e) => { set((string)cb.SelectedItem); Changed(); };
        row.Controls.Add(cb);
        page.Controls.Add(row);
        return cb;
    }

    protected Button Btn(string text, Action click, Color? border = null, int width = 160)
    {
        var b = new Button { Text = text, Width = width, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Panel2, ForeColor = Fg, Margin = new Padding(0, 4, 8, 4), Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = border ?? Accent;
        b.Click += (o, e) => click();
        return b;
    }

    protected FlowLayoutPanel Row(params Control[] cs)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(600, 0), Margin = new Padding(0, 2, 0, 2) };
        row.Controls.AddRange(cs);
        page.Controls.Add(row);
        return row;
    }

    // ---------- pages ----------

    void PageLook()
    {
        Head("Quick presets", "One click to change the whole vibe. You can still tweak everything after.");
        Row(Config.Presets.Select(p => (Control)Btn(p, () => { C.ApplyPreset(p); Changed(); Refresh2(); }, null, 128)).ToArray());

        Head("Theme");
        Combo("Theme", C.Themes.Select(t => t.Name).ToArray(), () => C.ThemeName, v => C.ThemeName = v);
        Combo("Particles", new[] { "Theme default" }.Concat(Particle.Types).ToArray(),
            () => string.IsNullOrEmpty(C.ParticlesOverride) ? "Theme default" : C.ParticlesOverride,
            v => C.ParticlesOverride = v == "Theme default" ? "" : v);
        Slider("Particle amount", 0, 4, 0.1, () => C.StarDensity, v => C.StarDensity = v);
        Slider("Speed", 0.1, 4, 0.1, () => C.Speed, v => C.Speed = v);
        Check("Shooting stars", () => C.ShootingStars, v => C.ShootingStars = v);
        Check("Auto-cycle themes", () => C.AutoCycle, v => C.AutoCycle = v, "Moves to the next theme on a timer.");
        Slider("Cycle every (min)", 1, 60, 1, () => C.CycleMinutes, v => C.CycleMinutes = (int)v, "0");
    }

    void PageAura()
    {
        Head("Glow", "The coloured glow rising off the top of the taskbar.");
        Check("Glow", () => C.Glow, v => C.Glow = v);
        Slider("Glow strength", 0, 2, 0.05, () => C.GlowStrength, v => C.GlowStrength = v, "0.00");
        Slider("Glow height (px)", 0, 60, 1, () => C.AuraHeight, v => C.AuraHeight = (int)v, "0");

        Head("Electric");
        Check("Live wire", () => C.Wire, v => C.Wire = v, "A jittering white-hot line along the edge.");
        Check("Lightning arcs", () => C.Lightning, v => C.Lightning = v);
        Slider("Lightning amount", 0.1, 5, 0.1, () => C.LightningRate, v => C.LightningRate = v);
        Check("Plasma waves", () => C.Plasma, v => C.Plasma = v, "Glowing waves weaving along the edge.");
        Check("Sparks", () => C.Sparks, v => C.Sparks = v, "Sparks shooting up and falling back.");

        Head("Fire");
        Check("Fire curtain", () => C.FireCurtain, v => C.FireCurtain = v, "Flames licking up from the taskbar, in the theme's colours.");
        Check("Flame particles", () => C.Flames, v => C.Flames = v);
        Slider("Fire height", 0.2, 4, 0.1, () => C.FireHeight, v => C.FireHeight = v);

        Head("Music", "Reacts to whatever is playing through your speakers or headphones.");
        Check("Music reactive", () => C.MusicReactive, v => C.MusicReactive = v, "Glow, lightning, fire and sparks pulse to the beat.");
        Slider("Music sensitivity", 0.1, 6, 0.1, () => C.AudioSensitivity, v => C.AudioSensitivity = v);
        Check("Visualizer bars", () => C.Equalizer, v => C.Equalizer = v, "Equalizer bars bouncing above the taskbar.");
    }

    // custom theme being edited
    Theme draft;

    void PageColors()
    {
        if (draft == null) { draft = C.Theme.Clone(); if (!draft.Custom) draft.Name = "My " + draft.Name; draft.NebulaPng = false; draft.Custom = true; }

        Head("Theme builder", "Start from any theme, pick your colours, and save it as your own. It shows up in the theme list and the tray menu.");
        Row(Btn("Start from current theme", () => { draft = null; Refresh2(); }, null, 200));

        var name = new TextBox { Text = draft.Name, Width = 300, BackColor = Panel2, ForeColor = Fg, BorderStyle = BorderStyle.FixedSingle };
        name.TextChanged += (o, e) => draft.Name = name.Text.Trim();
        Row(new Label { Text = "Name", Width = 170, ForeColor = Fg, TextAlign = ContentAlignment.MiddleLeft, Height = 26 }, name);

        ColorRow("Background", () => draft.Bg, v => draft.Bg = v, true);
        for (int i = 0; i < draft.Colors.Length; i++)
        {
            int k = i;
            ColorRow("Colour " + (k + 1), () => draft.Colors[k], v => draft.Colors[k] = v, false);
        }
        Row(Btn("+ Add colour", () => { draft.Colors = draft.Colors.Concat(new[] { Color.White }).ToArray(); Refresh2(); }, null, 120),
            Btn("- Remove colour", () => { if (draft.Colors.Length > 3) { draft.Colors = draft.Colors.Take(draft.Colors.Length - 1).ToArray(); Refresh2(); } }, null, 130));
        ColorRow("Particle colour", () => draft.Star, v => draft.Star = v, false);

        var pc = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, BackColor = Panel2, ForeColor = Fg, FlatStyle = FlatStyle.Flat };
        pc.Items.AddRange(Particle.Types);
        pc.SelectedItem = Particle.Types.Contains(draft.Particles) ? draft.Particles : "stars";
        pc.SelectedIndexChanged += (o, e) => draft.Particles = (string)pc.SelectedItem;
        Row(new Label { Text = "Particles", Width = 170, ForeColor = Fg, TextAlign = ContentAlignment.MiddleLeft, Height = 26 }, pc);

        Row(Btn("Save and use this theme", () =>
        {
            if (string.IsNullOrEmpty(draft.Name)) { MessageBox.Show(this, "Give your theme a name first."); return; }
            var t = draft.Clone();
            C.Custom.RemoveAll(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase));
            C.Custom.Add(t);
            C.ThemeName = t.Name;
            Changed();
        }, Color.FromArgb(120, 255, 170), 200),
        Btn("Delete this custom theme", () =>
        {
            if (C.Custom.RemoveAll(x => x.Name.Equals(draft.Name, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                if (C.ThemeName.Equals(draft.Name, StringComparison.OrdinalIgnoreCase)) C.ThemeName = "Galaxy";
                draft = null;
                Changed();
                Refresh2();
            }
        }, Color.FromArgb(255, 90, 120), 200));

        page.Controls.Add(new Label { Text = "Tip: a background with transparency (lower the alpha in the colour picker's custom colours, or type #AARRGGBB in config.json) lets your desktop show through.", AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Muted, Margin = new Padding(0, 8, 0, 0) });
    }

    void ColorRow(string label, Func<Color> get, Action<Color> set, bool keepAlpha)
    {
        var sw = new Panel { Width = 46, Height = 24, BackColor = Color.FromArgb(255, get()), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 3, 8, 3) };
        var hex = new Label { Text = Config.ToHex(get()), AutoSize = true, ForeColor = Muted, Margin = new Padding(0, 7, 8, 0) };
        var pick = Btn("Pick...", () =>
        {
            using (var cd = new ColorDialog { Color = get(), FullOpen = true })
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    var c = keepAlpha ? Color.FromArgb(get().A, cd.Color) : cd.Color;
                    set(c);
                    sw.BackColor = Color.FromArgb(255, c);
                    hex.Text = Config.ToHex(c);
                }
        }, null, 80);
        Row(new Label { Text = label, Width = 170, ForeColor = Fg, TextAlign = ContentAlignment.MiddleLeft, Height = 30 }, sw, hex, pick);
    }

    void PagePerformance()
    {
        Head("Performance");
        Slider("Frames per second", 5, 60, 1, () => C.Fps, v => C.Fps = (int)v, "0");
        Slider("On battery (fps)", 1, 60, 1, () => C.BatteryFps, v => C.BatteryFps = (int)v, "0");
        page.Controls.Add(new Label { Text = "It hides itself in full-screen apps and games, and uses the battery frame rate automatically when a laptop is unplugged.", AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Muted });

        Head("Files");
        Row(Btn("Open config.json", () => Process.Start("notepad.exe", "\"" + host.ConfigPath + "\"")),
            Btn("Reset everything", () =>
            {
                if (MessageBox.Show(this, "Reset all settings to default? Your custom themes are kept.", "Galaxy Taskbar", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                var keep = C.Custom;
                C.ApplyPreset("Default");
                C.ThemeName = "Galaxy"; C.ParticlesOverride = ""; C.Fps = 30; C.BatteryFps = 15; C.AutoCycle = false; C.ShootingStars = true;
                C.Custom = keep;
                Changed();
                Refresh2();
            }, Color.FromArgb(255, 90, 120)));
    }
}
