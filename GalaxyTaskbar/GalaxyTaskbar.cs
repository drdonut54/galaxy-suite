// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
// Galaxy Taskbar: animated particles + electric aura drawn behind transparent Windows 11 taskbars
// (taskbar backgrounds are made transparent by Windhawk's Windows 11 Taskbar Styler).
// One click-through overlay per taskbar (primary + secondary monitors), themes from config.json.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class Win
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte Op, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string c, string n);
    [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string c, string n);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref POINT pDst, ref SIZE sz,
        IntPtr src, ref POINT pSrc, int key, ref BLENDFUNCTION bf, int flags);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

    public const uint SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
}

// ---------- themes & config ----------

class Theme
{
    public string Name;
    public Color Bg;
    public Color[] Colors;     // aura / nebula / lightning colours
    public Color Star;
    public string Particles;   // stars | embers | code | frost
    public bool NebulaPng;

    public static List<Theme> BuiltIn()
    {
        return new List<Theme>
        {
            new Theme { Name = "Galaxy", Bg = C(6, 3, 18), Star = Color.White, Particles = "stars", NebulaPng = true,
                Colors = new[] { C(166, 108, 245), C(79, 195, 255), C(232, 63, 191) } },
            new Theme { Name = "Matrix", Bg = C(0, 7, 2), Star = C(140, 255, 160), Particles = "code",
                Colors = new[] { C(0, 255, 90), C(0, 170, 60), C(170, 255, 170) } },
            new Theme { Name = "Fire", Bg = C(16, 4, 1), Star = C(255, 220, 120), Particles = "embers",
                Colors = new[] { C(255, 90, 20), C(255, 175, 40), C(210, 25, 10) } },
            new Theme { Name = "Blood", Bg = C(10, 0, 1), Star = C(255, 120, 120), Particles = "stars",
                Colors = new[] { C(190, 0, 20), C(255, 45, 45), C(110, 0, 10) } },
            new Theme { Name = "Frost", Bg = Color.FromArgb(120, 200, 212, 228), Star = Color.White, Particles = "frost",
                Colors = new[] { C(255, 255, 255), C(190, 225, 255), C(225, 235, 250) } },
        };
    }

    static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
}

class Config
{
    public string ThemeName = "Galaxy";
    public int Fps = 30, BatteryFps = 15, AuraHeight = 18;
    public double StarDensity = 1.0;
    public bool Lightning = true, ShootingStars = true;
    public List<Theme> Themes = Theme.BuiltIn();

    public Theme Theme
    {
        get { return Themes.FirstOrDefault(t => t.Name.Equals(ThemeName, StringComparison.OrdinalIgnoreCase)) ?? Themes[0]; }
    }

    public const string Default =
@"{
  ""theme"": ""Galaxy"",
  ""fps"": 30,
  ""batteryFps"": 15,
  ""auraHeight"": 18,
  ""starDensity"": 1.0,
  ""lightning"": true,
  ""shootingStars"": true,
  ""customThemes"": {
    ""Synthwave"": {
      ""background"": ""#12002A"",
      ""colors"": [""#FF2BD6"", ""#00E5FF"", ""#FFB000""],
      ""star"": ""#FFFFFF"",
      ""particles"": ""stars""
    }
  }
}
";

    public static Config Load(string path)
    {
        var cfg = new Config();
        if (!File.Exists(path)) File.WriteAllText(path, Default);
        try
        {
            var root = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
            if (root == null) return cfg;
            object v;
            if (root.TryGetValue("theme", out v) && v is string) cfg.ThemeName = (string)v;
            if (root.TryGetValue("fps", out v)) cfg.Fps = Clamp(Convert.ToInt32(v), 5, 60);
            if (root.TryGetValue("batteryFps", out v)) cfg.BatteryFps = Clamp(Convert.ToInt32(v), 1, 60);
            if (root.TryGetValue("auraHeight", out v)) cfg.AuraHeight = Clamp(Convert.ToInt32(v), 0, 60);
            if (root.TryGetValue("starDensity", out v)) cfg.StarDensity = Math.Max(0, Math.Min(4, Convert.ToDouble(v, CultureInfo.InvariantCulture)));
            if (root.TryGetValue("lightning", out v) && v is bool) cfg.Lightning = (bool)v;
            if (root.TryGetValue("shootingStars", out v) && v is bool) cfg.ShootingStars = (bool)v;
            if (root.TryGetValue("customThemes", out v) && v is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)v)
                {
                    var d = kv.Value as Dictionary<string, object>;
                    if (d == null) continue;
                    var t = new Theme { Name = kv.Key, Bg = Hex(d, "background", Color.Black), Star = Hex(d, "star", Color.White), Particles = "stars" };
                    object p;
                    if (d.TryGetValue("particles", out p) && p is string) t.Particles = ((string)p).ToLowerInvariant();
                    var cols = new List<Color>();
                    object arr;
                    if (d.TryGetValue("colors", out arr) && arr is object[])
                        foreach (var c in (object[])arr) { Color col; if (TryHex(c as string, out col)) cols.Add(col); }
                    while (cols.Count < 3) cols.Add(cols.Count > 0 ? cols[0] : Color.White);
                    t.Colors = cols.ToArray();
                    cfg.Themes.RemoveAll(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase));
                    cfg.Themes.Add(t);
                }
        }
        catch { }
        return cfg;
    }

    public static void SaveThemeName(string path, string name)
    {
        string text = File.Exists(path) ? File.ReadAllText(path) : Default;
        var re = new Regex("\"theme\"\\s*:\\s*\"[^\"]*\"");
        text = re.IsMatch(text) ? re.Replace(text, "\"theme\": \"" + name + "\"", 1) : Default.Replace("\"Galaxy\"", "\"" + name + "\"");
        File.WriteAllText(path, text);
    }

    static int Clamp(int v, int a, int b) { return Math.Max(a, Math.Min(b, v)); }

    static Color Hex(Dictionary<string, object> d, string key, Color def)
    {
        object v; Color c;
        return d.TryGetValue(key, out v) && TryHex(v as string, out c) ? c : def;
    }

    static bool TryHex(string s, out Color c)
    {
        c = Color.Empty;
        if (string.IsNullOrEmpty(s)) return false;
        s = s.TrimStart('#');
        uint n;
        if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n)) return false;
        if (s.Length == 6) { c = Color.FromArgb(255, (int)(n >> 16) & 255, (int)(n >> 8) & 255, (int)n & 255); return true; }
        if (s.Length == 8) { c = Color.FromArgb((int)(n >> 24), (int)(n >> 16) & 255, (int)(n >> 8) & 255, (int)n & 255); return true; }
        return false;
    }
}

// ---------- one overlay per taskbar ----------

class Overlay : Form
{
    class Particle { public float X, Y, VX, VY, Size, Phase, Twinkle, Base; public Color C; public bool Sparkle; }
    class Bolt { public float X0, Len, Life, Age; public Color C; public PointF[] Pts; }
    class Meteor { public float X, Y, VX, VY, Age, Life; }

    public readonly IntPtr Taskbar;
    readonly Random rng = new Random(Guid.NewGuid().GetHashCode());
    readonly List<Particle> parts = new List<Particle>();
    readonly List<Bolt> bolts = new List<Bolt>();
    Meteor meteor;
    float nextMeteor = 4f, nextBolt = 0.5f;

    Config cfg;
    Theme theme;
    Bitmap nebulaSrc, background;
    Bitmap[] glow;                       // aura pre-baked at several pulse brightnesses
    int aura;
    Win.RECT tb;
    bool shown, needRebuild = true;
    int zTick;

    // persistent DIB: GDI+ draws straight into the memory UpdateLayeredWindow reads (no per-frame copies)
    IntPtr memDc, dib, dibOld, bits;
    Bitmap frame;
    Graphics g;
    int fw, fh;

    readonly SolidBrush brush = new SolidBrush(Color.White);
    readonly Pen pen = new Pen(Color.White, 1f) { LineJoin = LineJoin.Round };

    public Overlay(IntPtr taskbar, Config c, Bitmap nebula)
    {
        Taskbar = taskbar;
        nebulaSrc = nebula;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Apply(c);
        var unused = Handle;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // LAYERED | TRANSPARENT (click-through) | TOOLWINDOW | NOACTIVATE | TOPMOST
            cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x8000000 | 0x8;
            return cp;
        }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    public void Apply(Config c)
    {
        cfg = c;
        theme = c.Theme;
        aura = c.AuraHeight;
        needRebuild = true;
    }

    public void HideOverlay() { if (shown) { Win.ShowWindow(Handle, 0); shown = false; } }

    bool ForegroundIsFullscreenOn(Rectangle scr)
    {
        IntPtr fg = Win.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        var sb = new StringBuilder(64);
        Win.GetClassName(fg, sb, 64);
        string c = sb.ToString();
        if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return false;
        Win.RECT r;
        Win.GetWindowRect(fg, out r);
        return r.L <= scr.Left && r.T <= scr.Top && r.R >= scr.Right && r.B >= scr.Bottom;
    }

    public void Tick(float t, float dt)
    {
        if (!Win.IsWindow(Taskbar) || !Win.IsWindowVisible(Taskbar)) { HideOverlay(); return; }
        Win.RECT r;
        Win.GetWindowRect(Taskbar, out r);
        var scr = Screen.FromHandle(Taskbar).Bounds;
        bool autoHidden = r.T >= scr.Bottom - 4;
        if (autoHidden || ForegroundIsFullscreenOn(scr)) { HideOverlay(); return; }

        int w = r.R - r.L, h = r.B - r.T;
        if (w <= 0 || h <= 0) return;
        bool resized = w != tb.R - tb.L || h != tb.B - tb.T;
        bool moved = resized || r.L != tb.L || r.T != tb.T;
        tb = r;
        if (resized || needRebuild) Rebuild(w, h);

        if (moved || !shown || ++zTick >= 30)
        {
            zTick = 0;
            // directly beneath its taskbar so the icons stay on top
            Win.SetWindowPos(Handle, Taskbar, r.L, r.T - aura, w, h + aura, Win.SWP_NOACTIVATE | Win.SWP_SHOWWINDOW);
            shown = true;
        }

        Simulate(dt, w, h);
        Render(t, w, h);
        Present(r.L, r.T - aura);
    }

    // ---------- build cached resources ----------

    void Rebuild(int w, int h)
    {
        needRebuild = false;
        AllocFrame(w, h + aura);

        if (background != null) background.Dispose();
        background = theme.NebulaPng && nebulaSrc != null ? ScaledNebula(h) : ProceduralBackground(Math.Max(w, 1600), h);

        DisposeGlow();
        glow = BuildGlowLevels(w, aura + 8);

        parts.Clear();
        int n = (int)(w / 7 * cfg.StarDensity);
        for (int i = 0; i < n; i++) parts.Add(NewParticle(w, h, true));
        bolts.Clear();
    }

    void AllocFrame(int w, int h)
    {
        FreeFrame();
        fw = w; fh = h;
        var bi = new Win.BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf(typeof(Win.BITMAPINFOHEADER)), biWidth = w, biHeight = -h, // top-down
            biPlanes = 1, biBitCount = 32
        };
        IntPtr screen = Win.GetDC(IntPtr.Zero);
        memDc = Win.CreateCompatibleDC(screen);
        dib = Win.CreateDIBSection(screen, ref bi, 0, out bits, IntPtr.Zero, 0);
        Win.ReleaseDC(IntPtr.Zero, screen);
        dibOld = Win.SelectObject(memDc, dib);
        frame = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits);
        g = Graphics.FromImage(frame);
    }

    void FreeFrame()
    {
        if (g != null) { g.Dispose(); g = null; }
        if (frame != null) { frame.Dispose(); frame = null; }
        if (memDc != IntPtr.Zero) { Win.SelectObject(memDc, dibOld); Win.DeleteDC(memDc); memDc = IntPtr.Zero; }
        if (dib != IntPtr.Zero) { Win.DeleteObject(dib); dib = IntPtr.Zero; }
    }

    Bitmap ScaledNebula(int h)
    {
        int nw = (int)(nebulaSrc.Width * (h / (float)nebulaSrc.Height));
        var b = new Bitmap(Math.Max(nw, 1), h, PixelFormat.Format32bppPArgb);
        using (var gg = Graphics.FromImage(b))
        {
            gg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            gg.DrawImage(nebulaSrc, 0, 0, nw, h);
        }
        return b;
    }

    // Soft colour clouds in the theme's colours; drawn three times across so it tiles seamlessly.
    Bitmap ProceduralBackground(int w, int h)
    {
        var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var gg = Graphics.FromImage(b))
        {
            gg.SmoothingMode = SmoothingMode.AntiAlias;
            gg.Clear(theme.Bg);
            for (int i = 0; i < 45; i++)
            {
                float x = (float)rng.NextDouble() * w, y = (float)rng.NextDouble() * h;
                float rx = 60 + (float)rng.NextDouble() * 260, ry = rx * 0.35f;
                var c = theme.Colors[rng.Next(theme.Colors.Length)];
                int a = theme.Bg.A < 255 ? 60 : 30 + rng.Next(50);
                foreach (float ox in new[] { -w, 0f, (float)w })
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(x + ox - rx, y - ry, rx * 2, ry * 2);
                        using (var pg = new PathGradientBrush(path) { CenterColor = Color.FromArgb(a, c), SurroundColors = new[] { Color.FromArgb(0, c) } })
                            gg.FillPath(pg, path);
                    }
            }
            for (int i = 0; i < w * h / 60; i++)
            {
                int v = 30 + rng.Next(40);
                b.SetPixel(rng.Next(w), rng.Next(h), Blend(b.GetPixel(0, 0), theme.Star, v / 255f));
            }
        }
        return b;
    }

    static Color Blend(Color a, Color b, float f)
    {
        return Color.FromArgb(Math.Max(a.A, (int)(255 * f)), (int)(a.R + (b.R - a.R) * f), (int)(a.G + (b.G - a.G) * f), (int)(a.B + (b.B - a.B) * f));
    }

    // The aura glow, baked once: colour bands flowing along x, fading upward. Drawn per frame with a pulse alpha.
    const float PERIOD = 900;
    const int GLOW_LEVELS = 12;

    Bitmap[] BuildGlowLevels(int w, int h)
    {
        var levels = new Bitmap[GLOW_LEVELS];
        using (var full = BuildGlow(w, h))
        using (var attr = new ImageAttributes())
            for (int i = 0; i < GLOW_LEVELS; i++)
            {
                var b = new Bitmap(full.Width, full.Height, PixelFormat.Format32bppPArgb);
                attr.SetColorMatrix(new ColorMatrix { Matrix33 = (i + 1) / (float)GLOW_LEVELS });
                using (var gg = Graphics.FromImage(b))
                    gg.DrawImage(full, new Rectangle(0, 0, b.Width, b.Height), 0, 0, full.Width, full.Height, GraphicsUnit.Pixel, attr);
                levels[i] = b;
            }
        return levels;
    }

    void DisposeGlow()
    {
        if (glow == null) return;
        foreach (var b in glow) b.Dispose();
        glow = null;
    }

    Bitmap BuildGlow(int w, int h)
    {
        var b = new Bitmap(w + (int)PERIOD * 2, Math.Max(h, 1), PixelFormat.Format32bppPArgb);
        if (aura == 0) return b;
        using (var gg = Graphics.FromImage(b))
        {
            var cols = new[] { theme.Colors[0], theme.Colors[1], theme.Colors[2], theme.Colors[0] };
            var pos = new[] { 0f, 0.33f, 0.66f, 1f };
            for (int y = 0; y < h; y++)
            {
                float alpha = y < aura
                    ? (float)Math.Pow((y + 1) / (float)aura, 3) * 0.75f      // glow rising above the edge
                    : (1 - (y - aura) / 8f) * 0.45f;                         // spilling into the taskbar
                using (var lb = new LinearGradientBrush(new PointF(0, 0), new PointF(PERIOD, 0), Color.Black, Color.Black))
                {
                    lb.InterpolationColors = new ColorBlend { Colors = cols.Select(c => Color.FromArgb((int)(alpha * 255), c)).ToArray(), Positions = pos };
                    lb.WrapMode = WrapMode.TileFlipX;
                    gg.FillRectangle(lb, 0, y, b.Width, 1);
                }
            }
        }
        return b;
    }

    // ---------- particles ----------

    Particle NewParticle(int w, int h, bool anywhere)
    {
        float layer = (float)rng.NextDouble();
        var p = new Particle
        {
            X = (float)rng.NextDouble() * w,
            Y = aura + 2 + (float)rng.NextDouble() * (h - 4),
            Size = 0.8f + layer * 1.8f,
            Base = 0.35f + layer * 0.65f,
            Phase = (float)(rng.NextDouble() * Math.PI * 2),
            Twinkle = 1f + (float)rng.NextDouble() * 3f,
            Sparkle = layer > 0.88,
            C = theme.Star
        };
        switch (theme.Particles)
        {
            case "embers":
                p.VX = (float)(rng.NextDouble() * 10 - 5);
                p.VY = -(6 + layer * 22);
                p.C = theme.Colors[rng.Next(theme.Colors.Length)];
                p.Sparkle = false;
                if (!anywhere) p.Y = aura + h + 2;
                break;
            case "code":
                p.VX = 0;
                p.VY = 18 + layer * 45;
                p.X = (float)Math.Round(p.X / 9) * 9;          // columns, like falling code
                p.Sparkle = false;
                if (!anywhere) p.Y = aura - 8;
                break;
            case "frost":
                p.VX = -(1 + layer * 5);
                p.VY = (float)(rng.NextDouble() * 4 - 2);
                p.Sparkle = layer > 0.75;
                if (!anywhere) p.X = w + 4;
                break;
            default:
                p.VX = -(3 + layer * layer * 28);              // parallax: near stars move faster
                p.VY = 0;
                if (!anywhere) p.X = w + 4;
                break;
        }
        return p;
    }

    void Simulate(float dt, int w, int h)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            bool gone = p.X < -6 || p.X > w + 6 || p.Y < (theme.Particles == "embers" ? 0 : aura - 10) || p.Y > aura + h + 4;
            if (gone) parts[i] = NewParticle(w, h, false);
        }

        if (cfg.Lightning && aura > 0)
        {
            nextBolt -= dt;
            if (nextBolt <= 0)
            {
                nextBolt = 0.15f + (float)rng.NextDouble() * 0.6f;
                var b = new Bolt { Len = 120 + (float)rng.NextDouble() * 520, Life = 0.25f + (float)rng.NextDouble() * 0.6f };
                b.X0 = (float)rng.NextDouble() * Math.Max(1, w - b.Len);
                b.C = theme.Colors[rng.Next(theme.Colors.Length)];
                bolts.Add(b);
            }
        }
        for (int i = bolts.Count - 1; i >= 0; i--)
        {
            var b = bolts[i];
            b.Age += dt;
            if (b.Age >= b.Life) { bolts.RemoveAt(i); continue; }
            if (b.Pts == null || rng.Next(2) == 0) b.Pts = Jagged(b.X0, b.Len, aura, 7f);
        }

        if (cfg.ShootingStars && theme.Particles != "code")
        {
            nextMeteor -= dt;
            if (meteor == null && nextMeteor <= 0)
            {
                nextMeteor = 5 + (float)rng.NextDouble() * 9;
                meteor = new Meteor { X = w * (0.3f + (float)rng.NextDouble() * 0.7f), Y = aura + 2, VX = -(500 + (float)rng.NextDouble() * 400), VY = 40 + (float)rng.NextDouble() * 30, Life = 1.2f };
            }
            if (meteor != null)
            {
                meteor.X += meteor.VX * dt; meteor.Y += meteor.VY * dt; meteor.Age += dt;
                if (meteor.Age > meteor.Life || meteor.Y > aura + h) meteor = null;
            }
        }
        else meteor = null;
    }

    PointF[] Jagged(float x0, float len, float y, float amp)
    {
        int n = Math.Max(3, (int)(len / 10));
        var pts = new PointF[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float f = i / (float)n;
            float env = (float)Math.Sin(f * Math.PI);                     // pinned at both ends
            float dy = ((float)rng.NextDouble() * 2 - 1.3f) * amp * env;  // biased upward
            pts[i] = new PointF(x0 + f * len, y + dy);
        }
        return pts;
    }

    static Color A(Color c, float a) { return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(a * 255))), c); }

    // ---------- drawing ----------

    void Render(float t, int w, int h)
    {
        g.CompositingMode = CompositingMode.SourceCopy;
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver;

        // drifting background
        g.SmoothingMode = SmoothingMode.None;
        int bw = background.Width;
        int off = (int)(t * 6) % bw;
        for (int x = -off; x < w; x += bw) g.DrawImageUnscaled(background, x, aura);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        foreach (var p in parts) DrawParticle(p, t);

        if (meteor != null)
        {
            float fade = 1 - meteor.Age / meteor.Life;
            var head = new PointF(meteor.X, meteor.Y);
            var tail = new PointF(meteor.X - meteor.VX * 0.12f, meteor.Y - meteor.VY * 0.12f);
            using (var lg = new LinearGradientBrush(tail, head, Color.Transparent, A(theme.Star, fade)))
            using (var mp = new Pen(lg, 2f)) g.DrawLine(mp, tail, head);
            brush.Color = A(theme.Star, fade);
            g.FillEllipse(brush, head.X - 2, head.Y - 2, 4, 4);
        }

        if (aura <= 0) return;

        // pulsing, flowing glow
        float pulse = (0.65f + 0.35f * (float)Math.Sin(t * 2.4f)) * (0.85f + 0.15f * (float)rng.NextDouble());
        var gl = glow[Math.Max(0, Math.Min(GLOW_LEVELS - 1, (int)Math.Round(pulse * GLOW_LEVELS) - 1))];
        int gx = (int)(t * 120 % (PERIOD * 2));
        g.DrawImage(gl, new Rectangle(0, 0, w, gl.Height), gx, 0, w, gl.Height, GraphicsUnit.Pixel);

        // live wire along the edge
        var wire = new PointF[w / 16 + 2];
        for (int i = 0; i < wire.Length; i++)
        {
            float x = i * 16;
            wire[i] = new PointF(x, aura + 0.8f * (float)Math.Sin(x * 0.04f + t * 11) + ((float)rng.NextDouble() - 0.5f) * 1.6f);
        }
        Stroke(wire, A(theme.Colors[1], 0.35f * pulse), 4f);
        Stroke(wire, A(Color.White, 0.75f * pulse), 1.2f);

        // crackling arcs
        foreach (var b in bolts)
        {
            float a = (1 - b.Age / b.Life) * (0.7f + 0.3f * (float)rng.NextDouble());
            Stroke(b.Pts, A(b.C, 0.18f * a), 8f);
            Stroke(b.Pts, A(b.C, 0.55f * a), 3f);
            Stroke(b.Pts, A(Color.White, 0.95f * a), 1.1f);
            var bp = b.Pts[b.Pts.Length / 2];
            Stroke(Jagged(bp.X, 30 + (float)rng.NextDouble() * 40, bp.Y, 5f), A(b.C, 0.5f * a), 1f);
        }
    }

    void Stroke(PointF[] pts, Color c, float width)
    {
        pen.Color = c;
        pen.Width = width;
        g.DrawLines(pen, pts);
    }

    void DrawParticle(Particle p, float t)
    {
        float a = p.Base * (0.55f + 0.45f * (float)Math.Sin(p.Phase + t * p.Twinkle));
        float r = p.Size;
        switch (theme.Particles)
        {
            case "code":
                {
                    float len = 4 + r * 3;
                    using (var lg = new LinearGradientBrush(new PointF(p.X, p.Y - len - 0.1f), new PointF(p.X, p.Y), Color.Transparent, A(theme.Colors[0], a)))
                        g.FillRectangle(lg, p.X, p.Y - len, 1.6f, len);
                    brush.Color = A(theme.Star, Math.Min(1, a + 0.3f));
                    g.FillRectangle(brush, p.X - 0.2f, p.Y - 1, 2, 2);
                    return;
                }
            case "embers":
                {
                    float flick = 0.6f + 0.4f * (float)Math.Sin(p.Phase + t * p.Twinkle * 4);
                    brush.Color = A(p.C, a * 0.3f * flick);
                    g.FillEllipse(brush, p.X - r * 2, p.Y - r * 2, r * 4, r * 4);
                    brush.Color = A(theme.Star, a * flick);
                    g.FillEllipse(brush, p.X - r / 2, p.Y - r / 2, r, r);
                    return;
                }
        }
        if (r < 1.3f)
        {
            brush.Color = A(p.C, a);
            g.FillRectangle(brush, p.X, p.Y, 1, 1);
            return;
        }
        brush.Color = A(theme.Colors[0], a * 0.25f);
        g.FillEllipse(brush, p.X - r * 2, p.Y - r * 2, r * 4, r * 4);
        brush.Color = A(p.C, a);
        g.FillEllipse(brush, p.X - r / 2, p.Y - r / 2, r, r);
        if (p.Sparkle)
        {
            float l = r * 3.5f * (0.6f + 0.4f * (float)Math.Sin(p.Phase + t * p.Twinkle * 1.3f));
            pen.Color = A(p.C, a * 0.8f);
            pen.Width = 1f;
            g.DrawLine(pen, p.X - l, p.Y, p.X + l, p.Y);
            g.DrawLine(pen, p.X, p.Y - l * 0.8f, p.X, p.Y + l * 0.8f);
        }
    }

    void Present(int x, int y)
    {
        g.Flush(FlushIntention.Sync);
        IntPtr screen = Win.GetDC(IntPtr.Zero);
        var dst = new Win.POINT(x, y);
        var src = new Win.POINT(0, 0);
        var sz = new Win.SIZE(fw, fh);
        var bf = new Win.BLENDFUNCTION { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
        Win.UpdateLayeredWindow(Handle, screen, ref dst, ref sz, memDc, ref src, 0, ref bf, 2);
        Win.ReleaseDC(IntPtr.Zero, screen);
    }

    protected override void Dispose(bool disposing)
    {
        FreeFrame();
        if (disposing)
        {
            if (background != null) background.Dispose();
            DisposeGlow();
            brush.Dispose(); pen.Dispose();
        }
        base.Dispose(disposing);
    }
}

// ---------- app: tray menu, config, one overlay per taskbar ----------

class App : ApplicationContext
{
    readonly string dir = Path.GetDirectoryName(Application.ExecutablePath);
    readonly string cfgPath;
    Config cfg;
    DateTime cfgTime;
    readonly Bitmap nebula;
    readonly Dictionary<IntPtr, Overlay> overlays = new Dictionary<IntPtr, Overlay>();
    readonly NotifyIcon icon;
    readonly ToolStripMenuItem themeMenu = new ToolStripMenuItem("Theme");
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Stopwatch clock = Stopwatch.StartNew();
    double last, lastScan = -99, lastHousekeeping = -99;
    bool paused, onBattery;

    public App()
    {
        cfgPath = Path.Combine(dir, "config.json");
        string neb = Path.Combine(dir, "nebula.png");
        if (File.Exists(neb)) nebula = new Bitmap(neb);
        cfg = Config.Load(cfgPath);
        cfgTime = File.GetLastWriteTimeUtc(cfgPath);

        var menu = new ContextMenuStrip();
        menu.Items.Add(themeMenu);
        menu.Items.Add("Pause / Resume", null, (s, e) => { paused = !paused; if (paused) foreach (var o in overlays.Values) o.HideOverlay(); });
        menu.Items.Add("Edit config.json", null, (s, e) => Process.Start("notepad.exe", "\"" + cfgPath + "\""));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (s, e) => ExitThread());
        icon = new NotifyIcon { Icon = MakeIcon(), Text = "Galaxy Taskbar", ContextMenuStrip = menu, Visible = true };
        BuildThemeMenu();

        timer.Tick += (s, e) => Tick();
        SetFps();
        timer.Start();
    }

    void BuildThemeMenu()
    {
        themeMenu.DropDownItems.Clear();
        foreach (var t in cfg.Themes)
        {
            var name = t.Name;
            var item = new ToolStripMenuItem(name, null, (s, e) => PickTheme(name)) { Checked = cfg.Theme.Name == name };
            themeMenu.DropDownItems.Add(item);
        }
    }

    void PickTheme(string name)
    {
        try { Config.SaveThemeName(cfgPath, name); } catch { }
        Reload();
    }

    void Reload()
    {
        cfg = Config.Load(cfgPath);
        cfgTime = File.GetLastWriteTimeUtc(cfgPath);
        foreach (var o in overlays.Values) o.Apply(cfg);
        BuildThemeMenu();
        SetFps();
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

    void Tick()
    {
        double now = clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(0.25, now - last);
        last = now;

        if (now - lastHousekeeping > 1)
        {
            lastHousekeeping = now;
            // pick up hand edits to config.json
            try { var wt = File.GetLastWriteTimeUtc(cfgPath); if (wt != cfgTime) Reload(); } catch { }
            bool battery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
            if (battery != onBattery) { onBattery = battery; SetFps(); }
        }

        if (now - lastScan > 2)
        {
            lastScan = now;
            var live = new HashSet<IntPtr>(Taskbars());
            foreach (var h in overlays.Keys.Where(k => !live.Contains(k)).ToList()) { overlays[h].Dispose(); overlays.Remove(h); }
            foreach (var h in live) if (!overlays.ContainsKey(h)) overlays[h] = new Overlay(h, cfg, nebula);
        }

        if (paused) return;
        foreach (var o in overlays.Values) o.Tick((float)now, dt);
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
        icon.Visible = false;
        foreach (var o in overlays.Values) o.Dispose();
        base.ExitThreadCore();
    }

    [STAThread]
    static void Main()
    {
        bool created;
        using (var m = new Mutex(true, "GalaxyTaskbarOverlay", out created))
        {
            if (!created) return;
            Win.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.Run(new App());
        }
    }
}
