// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

interface ICrazyHost : IHost
{
    void Trigger(string eventName);   // null = random
}

// Settings window with the extra "Crazy" page.
class CrazySettings : SettingsForm
{
    readonly ICrazyHost crazy;

    public CrazySettings(ICrazyHost h) : base(h) { crazy = h; }

    protected override void AddSections()
    {
        base.AddSections();
        AddSection("Crazy", PageCrazy, 2);
    }

    Config C { get { return host.Cfg; } }

    void PageCrazy()
    {
        Head("Crazy mode", "Random events take over the taskbar every few seconds: dancing memes, fireworks, black holes, disco, a snake, glitches and more.");
        Check("Crazy mode", () => C.Chaos, v => C.Chaos = v);
        Check("Meme mode", () => C.Meme, v => C.Meme = v, "Real memes bouncing, raining, popping up and marching across the taskbar, plus Comic Sans text, MLG hitmarkers and 'deal with it' shades. Works on its own or together with crazy mode.");
        Slider("How crazy", 0, 1, 0.05, () => C.ChaosLevel, v => C.ChaosLevel = v, "0.00");
        Slider("Space above taskbar", 30, 300, 5, () => C.EffectsHeight, v => C.EffectsHeight = (int)v, "0");
        page_note("'How crazy' controls how often events happen and how many run at once. 'Space above taskbar' is how far events can reach up over your windows (they're click-through).");
        Row(Btn("Trigger a random event", () => crazy.Trigger(null), Color.FromArgb(255, 90, 200), 210));

        Head("Meme images", "Meme events use real meme images: the most popular templates on Imgflip, downloaded once and kept on this PC. If they can't be downloaded, drawn versions are used instead.");
        Check("Use real meme images", () => C.MemeImages, v => { C.MemeImages = v; if (v) MemeLibrary.EnsureLoaded(); });
        var status = new Label { Text = MemeLibrary.Status, AutoSize = true, ForeColor = Color.FromArgb(120, 255, 170), Margin = new Padding(0, 8, 8, 0) };
        var t = new Timer { Interval = 500 };
        t.Tick += (o, e) => { if (status.IsDisposed) { t.Dispose(); return; } status.Text = MemeLibrary.Status; };
        t.Start();
        Row(Btn("Get new memes", () => MemeLibrary.Refresh(), null, 140),
            Btn("Open meme folder", () => { System.IO.Directory.CreateDirectory(MemeLibrary.CacheDir); System.Diagnostics.Process.Start("explorer.exe", MemeLibrary.CacheDir); }, null, 150),
            status);

        Head("Events", "Untick any you don't want. Press ▶ to try one right now, even with crazy mode off.");
        foreach (var k in Director.Kinds)
        {
            var kind = k;
            var cb = new CheckBox { Text = kind.Name + (kind.Meme ? "  (meme)" : ""), Width = 190, Checked = !C.DisabledEvents.Contains(kind.Name), ForeColor = ForeColor, Font = new Font("Segoe UI", 10f) };
            cb.CheckedChanged += (o, e) => { if (cb.Checked) C.DisabledEvents.Remove(kind.Name); else C.DisabledEvents.Add(kind.Name); host.Changed(); };
            var play = Btn("▶", () => crazy.Trigger(kind.Name), null, 40);
            var desc = new Label { Text = kind.Description, AutoSize = true, MaximumSize = new Size(330, 0), ForeColor = Color.FromArgb(170, 155, 205), Margin = new Padding(0, 8, 0, 0) };
            Row(cb, play, desc);
        }
    }

    void page_note(string s)
    {
        Row(new Label { Text = s, AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = Color.FromArgb(170, 155, 205) });
    }
}
