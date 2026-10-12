// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Everything an effect needs to draw one frame.
// Frame layout: rows [0, Edge) are the "headroom" above the taskbar; rows [Edge, Edge+H) are behind the taskbar.
class Ctx
{
    public Graphics G;
    public int W, H, Edge;
    public float T, Dt;
    public Theme Theme;
    public Config Cfg;
    public Random R;
    public float Audio, Beat;
    public SolidBrush Brush;
    public Pen Pen;

    public Color Col(int i) { var c = Theme.Colors; return c[((i % c.Length) + c.Length) % c.Length]; }
    public float Rnd(float a, float b) { return a + (float)R.NextDouble() * (b - a); }

    public static Color A(Color c, float a) { return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(a * 255))), c); }

    public static Color Lerp(Color a, Color b, float f)
    {
        f = Math.Max(0, Math.Min(1, f));
        return Color.FromArgb((int)(a.A + (b.A - a.A) * f), (int)(a.R + (b.R - a.R) * f), (int)(a.G + (b.G - a.G) * f), (int)(a.B + (b.B - a.B) * f));
    }

    public static Color Hue(float h, float a)
    {
        h = (h % 1 + 1) % 1 * 6;
        int i = (int)h; float f = h - i;
        int q = (int)(255 * (1 - f)), u = (int)(255 * f);
        int r, g, b;
        switch (i) { case 0: r = 255; g = u; b = 0; break; case 1: r = q; g = 255; b = 0; break; case 2: r = 0; g = 255; b = u; break;
            case 3: r = 0; g = q; b = 255; break; case 4: r = u; g = 0; b = 255; break; default: r = 255; g = 0; b = q; break; }
        return Color.FromArgb(Math.Max(0, Math.Min(255, (int)(a * 255))), r, g, b);
    }

    public void Stroke(PointF[] pts, Color c, float width)
    {
        if (pts == null || pts.Length < 2) return;
        Pen.Color = c; Pen.Width = width;
        G.DrawLines(Pen, pts);
    }

    public void Line(float x0, float y0, float x1, float y1, Color c, float width)
    {
        Pen.Color = c; Pen.Width = width;
        G.DrawLine(Pen, x0, y0, x1, y1);
    }

    public void Dot(float x, float y, float r, Color c)
    {
        Brush.Color = c;
        G.FillEllipse(Brush, x - r, y - r, r * 2, r * 2);
    }

    public void Glow(float x, float y, float r, Color c, float a)
    {
        Dot(x, y, r * 2.2f, A(c, a * 0.18f));
        Dot(x, y, r, A(c, a));
    }

    // Electric zig-zag between two x positions, pinned at both ends.
    public PointF[] Jagged(float x0, float len, float y, float amp)
    {
        int n = Math.Max(3, (int)(Math.Abs(len) / 10));
        var pts = new PointF[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float f = i / (float)n;
            float env = (float)Math.Sin(f * Math.PI);
            pts[i] = new PointF(x0 + f * len, y + ((float)R.NextDouble() * 2 - 1.3f) * amp * env);
        }
        return pts;
    }

    public PointF[] JaggedV(float x, float y0, float y1, float amp)
    {
        int n = Math.Max(3, (int)(Math.Abs(y1 - y0) / 8));
        var pts = new PointF[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float f = i / (float)n;
            pts[i] = new PointF(x + ((float)R.NextDouble() * 2 - 1) * amp * (float)Math.Sin(f * Math.PI), y0 + f * (y1 - y0));
        }
        return pts;
    }
}

class Particle
{
    public static readonly string[] Types = { "stars", "frost", "embers", "code", "snow", "bubbles", "hearts", "confetti" };

    public float X, Y, VX, VY, Size, Phase, Twinkle, Base, Spin;
    public Color C;
    public bool Sparkle;
}

// One click-through overlay sitting directly beneath one taskbar.
class Overlay : Form
{
    class Meteor { public float X, Y, VX, VY, Age, Life; }

    public readonly IntPtr Taskbar;
    public readonly List<Particle> Particles = new List<Particle>();
    public readonly Random Rng = new Random(Guid.NewGuid().GetHashCode());
    public int ShakeX, ShakeY;

    Meteor meteor;
    float nextMeteor = 4f;
    readonly List<Fx> effects = Fx.All();

    Config cfg;
    Theme theme;
    string ptype;
    readonly Bitmap nebulaSrc;
    Bitmap background;
    Bitmap[] glow;
    int headroom, aura;
    Win.RECT tb;
    bool shown;
    int zTick;
    string sig = "", partSig = "";

    // persistent DIB: GDI+ draws straight into the memory UpdateLayeredWindow reads (no per-frame copies)
    IntPtr memDc, dib, dibOld, bits;
    Bitmap frame;
    Graphics g;
    int fw, fh;

    readonly Ctx ctx = new Ctx();

    public Overlay(IntPtr taskbar, Config c, Bitmap nebula)
    {
        Taskbar = taskbar;
        nebulaSrc = nebula;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ctx.R = Rng;
        ctx.Brush = new SolidBrush(Color.White);
        ctx.Pen = new Pen(Color.White, 1f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
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

    public void Apply(Config c) { cfg = c; }

    protected virtual Theme EffectiveTheme() { return cfg.Theme; }

    public static int Headroom(Config c)
    {
        int h = c.Glow || c.Lightning || c.Wire ? c.AuraHeight : 0;
        if (c.FireCurtain || c.Flames) h = Math.Max(h, (int)(14 + 22 * c.FireHeight));
        if (c.Sparks) h = Math.Max(h, 34);
        if (c.Plasma) h = Math.Max(h, 20);
        if (c.Equalizer) h = Math.Max(h, 42);
        return h;
    }

    protected virtual int HeadroomFor(Config c) { return Headroom(c); }

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

    public void Tick(float t, float dt, float audio, float beat)
    {
        if (!Win.IsWindow(Taskbar) || !Win.IsWindowVisible(Taskbar)) { HideOverlay(); return; }
        Win.RECT r;
        Win.GetWindowRect(Taskbar, out r);
        var scr = Screen.FromHandle(Taskbar).Bounds;
        bool autoHidden = r.T >= scr.Bottom - 4;
        if (autoHidden || ForegroundIsFullscreenOn(scr)) { HideOverlay(); return; }

        int w = r.R - r.L, h = r.B - r.T;
        if (w <= 0 || h <= 0) return;

        theme = EffectiveTheme();
        headroom = HeadroomFor(cfg);
        aura = cfg.AuraHeight;
        string s = theme.Name + "|" + aura + "|" + headroom + "|" + w + "|" + h + "|" + Config.ToHex(theme.Bg) + string.Join(",", theme.Colors.Select(Config.ToHex));
        if (s != sig) { sig = s; Rebuild(w, h); }
        ptype = cfg.ParticleType(theme);
        string ps = s + "|" + ptype + "|" + cfg.StarDensity + "|" + cfg.Speed;
        if (ps != partSig) { partSig = ps; RebuildParticles(w, h); }

        bool moved = r.L != tb.L || r.T != tb.T || r.R != tb.R || r.B != tb.B;
        tb = r;
        if (moved || !shown || ++zTick >= 30)
        {
            zTick = 0;
            // directly beneath its taskbar so the icons stay on top
            Win.SetWindowPos(Handle, Taskbar, r.L, r.T - headroom, w, h + headroom, Win.SWP_NOACTIVATE | Win.SWP_SHOWWINDOW);
            shown = true;
        }

        ctx.G = g; ctx.W = w; ctx.H = h; ctx.Edge = headroom; ctx.T = t; ctx.Dt = dt;
        ctx.Theme = theme; ctx.Cfg = cfg; ctx.Audio = audio; ctx.Beat = beat;

        Simulate(dt, w, h);
        foreach (var fx in effects) if (fx.Enabled(cfg)) fx.Update(ctx);
        UpdateExtras(ctx);
        Render(t, w, h);
        Present(r.L + ShakeX, r.T - headroom + ShakeY);
    }

    // hooks for extra layers (used by crazy mode)
    protected virtual void UpdateExtras(Ctx c) { }
    protected virtual void DrawBehind(Ctx c) { }
    protected virtual void DrawFront(Ctx c) { }
    protected virtual void PostProcess(Ctx c) { }

    // ---------- cached resources ----------

    void Rebuild(int w, int h)
    {
        AllocFrame(w, h + headroom);
        if (background != null) background.Dispose();
        background = theme.NebulaPng && nebulaSrc != null ? ScaledNebula(h) : ProceduralBackground(Math.Max(w, 1600), h);
        DisposeGlow();
        glow = BuildGlowLevels(w, Math.Max(1, aura) + 8);
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
        if (scratch != null) { scratch.Dispose(); scratch = null; }
    }

    void FreeFrame()
    {
        if (g != null) { g.Dispose(); g = null; }
        if (frame != null) { frame.Dispose(); frame = null; }
        if (memDc != IntPtr.Zero) { Win.SelectObject(memDc, dibOld); Win.DeleteDC(memDc); memDc = IntPtr.Zero; }
        if (dib != IntPtr.Zero) { Win.DeleteObject(dib); dib = IntPtr.Zero; }
    }

    Bitmap scratch;

    // A copy of the current frame, for effects that distort it.
    public Bitmap Snapshot()
    {
        g.Flush(FlushIntention.Sync);
        if (scratch == null) scratch = new Bitmap(fw, fh, PixelFormat.Format32bppPArgb);
        using (var sg = Graphics.FromImage(scratch))
        {
            sg.CompositingMode = CompositingMode.SourceCopy;
            sg.DrawImageUnscaled(frame, 0, 0);
        }
        return scratch;
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
                float x = (float)Rng.NextDouble() * w, y = (float)Rng.NextDouble() * h;
                float rx = 60 + (float)Rng.NextDouble() * 260, ry = rx * 0.35f;
                var c = theme.Colors[Rng.Next(theme.Colors.Length)];
                int a = theme.Bg.A < 255 ? 60 : 30 + Rng.Next(50);
                foreach (float ox in new[] { -w, 0f, (float)w })
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(x + ox - rx, y - ry, rx * 2, ry * 2);
                        using (var pg = new PathGradientBrush(path) { CenterColor = Color.FromArgb(a, c), SurroundColors = new[] { Color.FromArgb(0, c) } })
                            gg.FillPath(pg, path);
                    }
            }
            using (var dust = new SolidBrush(Color.White))
                for (int i = 0; i < w * h / 60; i++)
                {
                    dust.Color = Color.FromArgb(25 + Rng.Next(40), theme.Star);
                    gg.FillRectangle(dust, Rng.Next(w), Rng.Next(h), 1, 1);
                }
        }
        return b;
    }

    // The aura glow, baked once at several brightnesses: colour bands flowing along x, fading upward.
    const float PERIOD = 900;
    const int GLOW_LEVELS = 16;

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
        var b = new Bitmap(w + (int)PERIOD * 2, h, PixelFormat.Format32bppPArgb);
        int a = Math.Max(1, aura);
        using (var gg = Graphics.FromImage(b))
        {
            var cols = theme.Colors.Concat(new[] { theme.Colors[0] }).ToArray();
            var pos = Enumerable.Range(0, cols.Length).Select(i => i / (float)(cols.Length - 1)).ToArray();
            for (int y = 0; y < h; y++)
            {
                float alpha = y < a ? (float)Math.Pow((y + 1) / (float)a, 3) : (1 - (y - a) / 8f) * 0.6f;
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

    void RebuildParticles(int w, int h)
    {
        Particles.Clear();
        int n = (int)(w / 7 * cfg.StarDensity);
        for (int i = 0; i < n; i++) Particles.Add(NewParticle(w, h, true));
    }

    public Particle NewParticle(int w, int h, bool anywhere)
    {
        float layer = (float)Rng.NextDouble();
        float sp = (float)cfg.Speed;
        var p = new Particle
        {
            X = (float)Rng.NextDouble() * w,
            Y = headroom + 2 + (float)Rng.NextDouble() * (h - 4),
            Size = 0.8f + layer * 1.8f,
            Base = 0.35f + layer * 0.65f,
            Phase = (float)(Rng.NextDouble() * Math.PI * 2),
            Twinkle = 1f + (float)Rng.NextDouble() * 3f,
            Sparkle = layer > 0.88,
            C = theme.Star,
            Spin = (float)(Rng.NextDouble() * 6 - 3)
        };
        Color themed = theme.Colors[Rng.Next(theme.Colors.Length)];
        switch (ptype)
        {
            case "embers": p.VX = (float)(Rng.NextDouble() * 10 - 5); p.VY = -(6 + layer * 22); p.C = themed; p.Sparkle = false; break;
            case "code": p.VY = 18 + layer * 45; p.X = (float)Math.Round(p.X / 9) * 9; p.Sparkle = false; break;
            case "frost": p.VX = -(1 + layer * 5); p.VY = (float)(Rng.NextDouble() * 4 - 2); p.Sparkle = layer > 0.75; break;
            case "snow": p.VX = (float)(Rng.NextDouble() * 6 - 3); p.VY = 5 + layer * 14; p.Size = 0.8f + layer * 2.2f; p.Sparkle = false; break;
            case "bubbles": p.VX = 0; p.VY = -(5 + layer * 15); p.Size = 1.5f + layer * 3f; p.C = themed; p.Sparkle = false; break;
            case "hearts": p.VX = (float)(Rng.NextDouble() * 4 - 2); p.VY = -(6 + layer * 12); p.Size = 2f + layer * 2.5f; p.C = themed; p.Sparkle = false; break;
            case "confetti": p.VX = (float)(Rng.NextDouble() * 20 - 10); p.VY = 10 + layer * 25; p.Size = 1.5f + layer * 2f; p.C = themed; p.Sparkle = false; break;
            default: p.VX = -(3 + layer * layer * 28); break;   // stars: near ones move faster (parallax)
        }
        p.VX *= sp; p.VY *= sp;
        if (!anywhere) Place(p, w, h);
        return p;
    }

    // Put a fresh particle on the edge it should enter from, based on its direction.
    void Place(Particle p, int w, int h)
    {
        if (Math.Abs(p.VY) > Math.Abs(p.VX) * 0.6f) p.Y = p.VY > 0 ? headroom - 6 : headroom + h + 4;
        else p.X = p.VX < 0 ? w + 4 : -4;
    }

    public void Respawn(int i) { Particles[i] = NewParticle(ctx.W, ctx.H, false); }

    bool Rising { get { return ptype == "embers" || ptype == "bubbles" || ptype == "hearts"; } }

    void Simulate(float dt, int w, int h)
    {
        // rising particles may drift a little above the edge, but never fill the crazy-mode space
        float top = Rising ? Math.Max(0, headroom - Math.Max(aura + 10, 24)) : headroom - 10;
        for (int i = 0; i < Particles.Count; i++)
        {
            var p = Particles[i];
            p.X += p.VX * dt;
            p.Y += p.VY * dt;
            if (ptype == "snow" || ptype == "bubbles" || ptype == "confetti") p.X += (float)Math.Sin(p.Phase + ctx.T * p.Twinkle) * 8 * dt;
            if (p.X < -8 || p.X > w + 8 || p.Y < top || p.Y > headroom + h + 6) Respawn(i);
        }

        if (cfg.ShootingStars && ptype != "code")
        {
            nextMeteor -= dt;
            if (meteor == null && nextMeteor <= 0)
            {
                nextMeteor = 5 + (float)Rng.NextDouble() * 9;
                meteor = new Meteor { X = w * (0.3f + (float)Rng.NextDouble() * 0.7f), Y = headroom + 2, VX = -(500 + (float)Rng.NextDouble() * 400), VY = 40 + (float)Rng.NextDouble() * 30, Life = 1.2f };
            }
            if (meteor != null)
            {
                meteor.X += meteor.VX * dt; meteor.Y += meteor.VY * dt; meteor.Age += dt;
                if (meteor.Age > meteor.Life || meteor.Y > headroom + h) meteor = null;
            }
        }
        else meteor = null;
    }

    // ---------- drawing ----------

    void Render(float t, int w, int h)
    {
        g.CompositingMode = CompositingMode.SourceCopy;
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver;

        g.SmoothingMode = SmoothingMode.None;
        int bw = background.Width;
        int off = (int)(t * 6 * cfg.Speed) % bw;
        for (int x = -off; x < w; x += bw) g.DrawImageUnscaled(background, x, headroom);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        DrawBehind(ctx);
        foreach (var p in Particles) DrawParticle(p, t);

        if (meteor != null)
        {
            float fade = 1 - meteor.Age / meteor.Life;
            var head = new PointF(meteor.X, meteor.Y);
            var tail = new PointF(meteor.X - meteor.VX * 0.12f, meteor.Y - meteor.VY * 0.12f);
            using (var lg = new LinearGradientBrush(tail, head, Color.Transparent, Ctx.A(theme.Star, fade)))
            using (var mp = new Pen(lg, 2f)) g.DrawLine(mp, tail, head);
            ctx.Dot(head.X, head.Y, 2, Ctx.A(theme.Star, fade));
        }

        if (cfg.Glow && aura > 0)
        {
            float pulse = (0.65f + 0.35f * (float)Math.Sin(t * 2.4f)) * (0.85f + 0.15f * (float)Rng.NextDouble());
            float level = pulse * 0.75f * (float)cfg.GlowStrength * (1 + ctx.Audio * 0.9f + ctx.Beat * 0.4f);
            var gl = glow[Math.Max(0, Math.Min(GLOW_LEVELS - 1, (int)Math.Round(level * GLOW_LEVELS) - 1))];
            int gx = (int)(t * 120 * cfg.Speed % (PERIOD * 2));
            g.DrawImage(gl, new Rectangle(0, headroom - aura, w, gl.Height), gx, 0, w, gl.Height, GraphicsUnit.Pixel);
        }

        foreach (var fx in effects) if (fx.Enabled(cfg)) fx.Draw(ctx);
        DrawFront(ctx);
        PostProcess(ctx);
    }

    void DrawParticle(Particle p, float t)
    {
        float a = p.Base * (0.55f + 0.45f * (float)Math.Sin(p.Phase + t * p.Twinkle));
        float r = p.Size;
        switch (ptype)
        {
            case "code":
                {
                    float len = 4 + r * 3;
                    using (var lg = new LinearGradientBrush(new PointF(p.X, p.Y - len - 0.1f), new PointF(p.X, p.Y), Color.Transparent, Ctx.A(theme.Colors[0], a)))
                        g.FillRectangle(lg, p.X, p.Y - len, 1.6f, len);
                    ctx.Brush.Color = Ctx.A(theme.Star, Math.Min(1, a + 0.3f));
                    g.FillRectangle(ctx.Brush, p.X - 0.2f, p.Y - 1, 2, 2);
                    return;
                }
            case "embers":
                {
                    float flick = 0.6f + 0.4f * (float)Math.Sin(p.Phase + t * p.Twinkle * 4);
                    ctx.Dot(p.X, p.Y, r * 2, Ctx.A(p.C, a * 0.3f * flick));
                    ctx.Dot(p.X, p.Y, r / 2, Ctx.A(theme.Star, a * flick));
                    return;
                }
            case "snow":
                ctx.Dot(p.X, p.Y, r * 0.7f, Ctx.A(theme.Star, 0.4f + a * 0.6f));
                return;
            case "bubbles":
                ctx.Pen.Color = Ctx.A(p.C, 0.35f + a * 0.4f); ctx.Pen.Width = 1f;
                g.DrawEllipse(ctx.Pen, p.X - r, p.Y - r, r * 2, r * 2);
                ctx.Dot(p.X - r * 0.35f, p.Y - r * 0.35f, Math.Max(0.6f, r * 0.25f), Ctx.A(Color.White, 0.6f));
                return;
            case "hearts":
                {
                    ctx.Brush.Color = Ctx.A(p.C, 0.4f + a * 0.5f);
                    g.FillEllipse(ctx.Brush, p.X - r, p.Y - r * 0.6f, r * 1.1f, r * 1.1f);
                    g.FillEllipse(ctx.Brush, p.X - r * 0.1f, p.Y - r * 0.6f, r * 1.1f, r * 1.1f);
                    g.FillPolygon(ctx.Brush, new[] { new PointF(p.X - r, p.Y), new PointF(p.X + r, p.Y), new PointF(p.X, p.Y + r * 1.2f) });
                    return;
                }
            case "confetti":
                {
                    float ang = p.Phase + t * p.Spin;
                    float sx = r * 1.6f * Math.Abs((float)Math.Cos(ang)) + 0.5f;
                    ctx.Brush.Color = Ctx.A(p.C, 0.85f);
                    g.FillRectangle(ctx.Brush, p.X - sx / 2, p.Y - r / 2, sx, r);
                    return;
                }
        }
        if (r < 1.3f)
        {
            ctx.Brush.Color = Ctx.A(p.C, a);
            g.FillRectangle(ctx.Brush, p.X, p.Y, 1, 1);
            return;
        }
        ctx.Dot(p.X, p.Y, r * 2, Ctx.A(theme.Colors[0], a * 0.25f));
        ctx.Dot(p.X, p.Y, r / 2, Ctx.A(p.C, a));
        if (p.Sparkle)
        {
            float l = r * 3.5f * (0.6f + 0.4f * (float)Math.Sin(p.Phase + t * p.Twinkle * 1.3f));
            ctx.Line(p.X - l, p.Y, p.X + l, p.Y, Ctx.A(p.C, a * 0.8f), 1f);
            ctx.Line(p.X, p.Y - l * 0.8f, p.X, p.Y + l * 0.8f, Ctx.A(p.C, a * 0.8f), 1f);
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
            if (scratch != null) scratch.Dispose();
            DisposeGlow();
            ctx.Brush.Dispose(); ctx.Pen.Dispose();
        }
        base.Dispose(disposing);
    }
}
