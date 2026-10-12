// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
// Crazy mode & meme mode: random events that take over the taskbar for a few seconds.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

abstract class ChaosEvent
{
    public float Age, Life = 6;
    public bool Done { get { return Age >= Life; } }
    protected float Fade { get { return Math.Max(0, Math.Min(1, Math.Min(Age / 0.4f, (Life - Age) / 0.6f))); } }

    public virtual void Start(Ctx c, CrazyOverlay o) { }
    public virtual void Update(Ctx c, CrazyOverlay o) { }
    public virtual void DrawBack(Ctx c) { }      // behind the particles
    public virtual void DrawFront(Ctx c) { }     // on top of everything
    public virtual void Post(Ctx c, CrazyOverlay o) { }  // distort the finished frame
    public virtual void End(Ctx c, CrazyOverlay o) { }

    protected static readonly Font Meme = new Font("Comic Sans MS", 15f, FontStyle.Bold);
    protected static readonly Font MemeBig = new Font("Comic Sans MS", 22f, FontStyle.Bold);
    protected static readonly Font Bold = new Font("Segoe UI", 11f, FontStyle.Bold | FontStyle.Italic);
    protected static readonly Font Impact = new Font("Impact", 16f, FontStyle.Regular);

    protected static void Text(Ctx c, string s, Font f, float x, float y, Color col, float a, float angle = 0)
    {
        var st = c.G.Save();
        c.G.TranslateTransform(x, y);
        if (angle != 0) c.G.RotateTransform(angle);
        var sz = c.G.MeasureString(s, f);
        c.Brush.Color = Ctx.A(Color.Black, a * 0.7f);
        c.G.DrawString(s, f, c.Brush, -sz.Width / 2 + 2, -sz.Height / 2 + 2);
        c.Brush.Color = Ctx.A(col, a);
        c.G.DrawString(s, f, c.Brush, -sz.Width / 2, -sz.Height / 2);
        c.G.Restore(st);
    }

    static readonly ImageAttributes fadeAttr = new ImageAttributes();
    static readonly ColorMatrix fadeMatrix = new ColorMatrix();

    // Draws a downloaded meme centred on (x, y) with a border, rotation, squash and fade.
    protected static void DrawMeme(Ctx c, Bitmap b, float x, float y, float height, float angle, float alpha, float sx = 1, float sy = 1, Color? border = null)
    {
        if (b == null || alpha <= 0.01f) return;
        float h = height, w = h * b.Width / b.Height;
        var st = c.G.Save();
        c.G.TranslateTransform(x, y);
        if (angle != 0) c.G.RotateTransform(angle);
        if (sx != 1 || sy != 1) c.G.ScaleTransform(sx, sy);
        var dst = new Rectangle((int)(-w / 2), (int)(-h / 2), (int)w, (int)h);
        c.G.InterpolationMode = InterpolationMode.Bilinear;
        if (alpha >= 0.99f) c.G.DrawImage(b, dst);
        else
        {
            fadeMatrix.Matrix33 = alpha;
            fadeAttr.SetColorMatrix(fadeMatrix);
            c.G.DrawImage(b, dst, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, fadeAttr);
        }
        c.Pen.Color = Ctx.A(border ?? Color.White, alpha * 0.9f); c.Pen.Width = 2;
        c.G.DrawRectangle(c.Pen, dst);
        c.G.Restore(st);
    }

    protected static bool UseMemes(Ctx c) { return c.Cfg.MemeImages && MemeLibrary.Count > 0; }

    // y range events can use: the headroom above the taskbar plus the taskbar itself
    protected static float Top(Ctx c) { return 4; }
    protected static float Bottom(Ctx c) { return c.Edge + c.H - 4; }
}

class Director
{
    public class Kind
    {
        public string Name, Description;
        public bool Meme;
        public Func<ChaosEvent> Make;
    }

    public static readonly List<Kind> Kinds = new List<Kind>
    {
        K("Dance Party", "Memes climb out of the taskbar and dance (to your music if it's on).", false, () => new DanceParty()),
        K("Fireworks", "Rockets launch off the taskbar and explode.", false, () => new Fireworks()),
        K("Black Hole", "A black hole opens and swallows the stars, then blows them back out.", false, () => new BlackHole()),
        K("Disco", "A disco ball drops in and sweeps spotlights across the taskbar.", false, () => new Disco()),
        K("Snake", "A snake slithers around the taskbar eating glowing dots.", false, () => new Snake()),
        K("Lightning Storm", "A burst of lightning with screen flashes.", false, () => new Storm()),
        K("Earthquake", "The whole thing shakes.", false, () => new Earthquake()),
        K("Glitch", "Digital glitch: slices jump around and the colours split.", false, () => new Glitch()),
        K("Jelly Wobble", "The taskbar background wobbles like jelly.", false, () => new Jelly()),
        K("Gravity Flip", "Everything starts going the other way.", false, () => new GravityFlip()),
        K("Theme Morph", "Suddenly switches to a random theme for a few seconds.", false, () => new ThemeMorph()),
        K("Bouncing Meme", "A meme bounces around like an old DVD logo. Wait for it to hit a corner...", true, () => new LogoBounce()),
        K("Meme Rain", "Memes rain down and bounce off the taskbar.", true, () => new MemeRain()),
        K("Meme Pop-up", "A huge meme bursts up out of the taskbar. Boom.", true, () => new MemePopup()),
        K("Meme Parade", "A line of memes marches across the taskbar.", true, () => new MemeParade()),
        K("Rainbow Meme", "A meme flies by riding a rainbow.", true, () => new RainbowToast()),
        K("Meme Text", "Wow. Such taskbar. Very aura.", true, () => new MemeText()),
        K("Hitmarkers", "MLG hitmarkers and +100s everywhere.", true, () => new Hitmarkers()),
        K("Deal With It", "Pixel sunglasses drop onto the taskbar.", true, () => new DealWithIt()),
    };

    static Kind K(string n, string d, bool meme, Func<ChaosEvent> make) { return new Kind { Name = n, Description = d, Meme = meme, Make = make }; }

    readonly List<KeyValuePair<Kind, ChaosEvent>> active = new List<KeyValuePair<Kind, ChaosEvent>>();
    readonly Queue<string> requests = new Queue<string>();
    float next = 2.5f;

    public IEnumerable<ChaosEvent> Active { get { return active.Select(a => a.Value); } }

    public void Request(string name) { requests.Enqueue(name ?? ""); }

    public void Update(Ctx c, CrazyOverlay o)
    {
        var cfg = c.Cfg;
        while (requests.Count > 0)
        {
            string n = requests.Dequeue();
            var pool = Kinds.Where(k => n == "" ? !cfg.DisabledEvents.Contains(k.Name) : k.Name.Equals(n, StringComparison.OrdinalIgnoreCase)).ToList();
            if (pool.Count > 0) Start(pool[c.R.Next(pool.Count)], c, o);
        }

        if (cfg.CrazyOn)
        {
            next -= c.Dt * (1 + c.Beat * 0.5f);
            if (next <= 0)
            {
                float lvl = (float)cfg.ChaosLevel;
                next = (9 - 7.5f * lvl) * c.Rnd(0.6f, 1.4f);
                int cap = 1 + (int)(lvl * 3);
                if (active.Count < cap)
                {
                    var pool = new List<Kind>();
                    foreach (var k in Kinds)
                    {
                        if (cfg.DisabledEvents.Contains(k.Name) || active.Any(a => a.Key == k)) continue;
                        if (!cfg.Chaos && !k.Meme && k.Name != "Dance Party" && k.Name != "Fireworks") continue;   // meme-only mode
                        int weight = cfg.Meme && k.Meme ? 3 : 1;
                        for (int i = 0; i < weight; i++) pool.Add(k);
                    }
                    if (pool.Count > 0) Start(pool[c.R.Next(pool.Count)], c, o);
                }
            }
        }

        for (int i = active.Count - 1; i >= 0; i--)
        {
            var e = active[i].Value;
            e.Age += c.Dt;
            if (e.Done) { e.End(c, o); active.RemoveAt(i); continue; }
            e.Update(c, o);
        }
    }

    public void Start(Kind k, Ctx c, CrazyOverlay o)
    {
        var e = k.Make();
        active.Add(new KeyValuePair<Kind, ChaosEvent>(k, e));
        e.Start(c, o);
    }

    public void StopAll(Ctx c, CrazyOverlay o)
    {
        foreach (var a in active) a.Value.End(c, o);
        active.Clear();
    }
}

// The overlay with crazy-mode events layered on top.
class CrazyOverlay : Overlay
{
    public readonly Director Director = new Director();
    public Theme ThemeOverride;
    public float Flash;

    public CrazyOverlay(IntPtr taskbar, Config c, Bitmap nebula) : base(taskbar, c, nebula) { }

    protected override Theme EffectiveTheme() { return ThemeOverride ?? base.EffectiveTheme(); }

    protected override int HeadroomFor(Config c)
    {
        int h = Headroom(c);
        return c.CrazyOn || Director.Active.Any() ? Math.Max(h, c.EffectsHeight) : h;
    }

    protected override void UpdateExtras(Ctx c)
    {
        Flash = Math.Max(0, Flash - c.Dt * 3);
        Director.Update(c, this);
    }

    protected override void DrawBehind(Ctx c) { foreach (var e in Director.Active) e.DrawBack(c); }

    protected override void DrawFront(Ctx c)
    {
        foreach (var e in Director.Active) e.DrawFront(c);
        if (Flash > 0) { c.Brush.Color = Ctx.A(Color.White, Flash * 0.35f); c.G.FillRectangle(c.Brush, 0, 0, c.W, c.Edge + c.H); }
    }

    protected override void PostProcess(Ctx c) { foreach (var e in Director.Active.ToList()) e.Post(c, this); }
}

// ======================================================================
// Events
// ======================================================================

class DanceParty : ChaosEvent
{
    class Critter { public float X, Size, Phase; public int Kind; public Color C; public bool Flipper; public Bitmap Meme; }
    readonly List<Critter> cs = new List<Critter>();
    float beatPhase;

    public override void Start(Ctx c, CrazyOverlay o)
    {
        Life = 11;
        int n = 5 + c.R.Next(5);
        for (int i = 0; i < n; i++)
            cs.Add(new Critter { X = c.W * (i + 0.5f) / n + c.Rnd(-40, 40), Size = c.Rnd(14, 22), Phase = c.Rnd(0, 6), Kind = c.R.Next(3), C = c.Col(i), Flipper = c.R.Next(3) == 0 });
        if (UseMemes(c))
        {
            var memes = MemeLibrary.PickMany(c.R, n);
            for (int i = 0; i < cs.Count; i++) { cs[i].Meme = memes[i % memes.Count]; cs[i].Size = c.Rnd(42, Math.Max(43, Math.Min(70, c.Edge - 14))); }
        }
    }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        // 128 bpm, or jump on real beats when music reactive is on
        beatPhase += c.Dt * 128 / 60f;
        if (c.Beat > 0.95f) beatPhase = (float)Math.Ceiling(beatPhase);
    }

    public override void DrawFront(Ctx c)
    {
        float rise = Math.Min(1, Age / 0.8f), sink = Math.Min(1, (Life - Age) / 0.8f);
        float show = Math.Min(rise, sink);
        foreach (var k in cs)
        {
            float bp = beatPhase + k.Phase * 0.1f;
            float bounce = Math.Abs((float)Math.Sin(bp * Math.PI)) * (10 + c.Audio * 10);
            float squash = 1 + 0.18f * (float)Math.Cos(bp * Math.PI * 2);
            float s = k.Size;
            float baseY = c.Edge + s * (1 - show) * 1.4f;            // climbs out of the taskbar
            float cy = baseY - s * 0.55f - bounce * show;
            float sway = (float)Math.Sin(bp * Math.PI * 0.5) * 6;
            float angle = 0;
            if (k.Flipper) { float f = bp % 4; if (f > 3) angle = (f - 3) * 360; }

            if (k.Meme != null)
            {
                // the meme itself dances: bounce, squash and stretch, sway, the odd backflip
                float my = c.Edge + s * (1 - show) - s * 0.5f - bounce * show * 1.4f;
                DrawMeme(c, k.Meme, k.X + sway * 2, my, s, angle + sway * 1.5f, show, squash, 1 / squash, c.Col((int)bp));
                continue;
            }

            var st = c.G.Save();
            c.G.TranslateTransform(k.X + sway, cy);
            c.G.RotateTransform(angle);
            float bw = s * squash, bh = s / squash;
            var col = k.C;

            // arms waving to the beat
            float arm = (float)Math.Sin(bp * Math.PI) * 0.8f;
            c.Line(-bw * 0.45f, 0, -bw * 0.9f, -bh * (0.2f + arm * 0.5f), Ctx.A(col, show), 2.2f);
            c.Line(bw * 0.45f, 0, bw * 0.9f, -bh * (0.2f - arm * 0.5f), Ctx.A(col, show), 2.2f);
            // legs
            c.Line(-bw * 0.2f, bh * 0.4f, -bw * 0.3f, bh * 0.62f, Ctx.A(col, show), 2f);
            c.Line(bw * 0.2f, bh * 0.4f, bw * 0.3f, bh * 0.62f, Ctx.A(col, show), 2f);

            c.Brush.Color = Ctx.A(col, show);
            if (k.Kind == 1)
            {   // ghost
                var pts = new List<PointF>();
                for (int i = 0; i <= 12; i++) { double a = Math.PI + i * Math.PI / 12; pts.Add(new PointF((float)Math.Cos(a) * bw / 2, (float)Math.Sin(a) * bh / 2)); }
                for (int i = 0; i <= 6; i++) pts.Add(new PointF(bw / 2 - i * bw / 6, bh * 0.45f + (i % 2 == 0 ? 0 : -bh * 0.15f)));
                c.G.FillPolygon(c.Brush, pts.ToArray());
            }
            else
            {
                c.G.FillEllipse(c.Brush, -bw / 2, -bh / 2, bw, bh);
                if (k.Kind == 2)
                {   // cat ears
                    c.G.FillPolygon(c.Brush, new[] { new PointF(-bw * 0.42f, -bh * 0.2f), new PointF(-bw * 0.35f, -bh * 0.75f), new PointF(-bw * 0.08f, -bh * 0.42f) });
                    c.G.FillPolygon(c.Brush, new[] { new PointF(bw * 0.42f, -bh * 0.2f), new PointF(bw * 0.35f, -bh * 0.75f), new PointF(bw * 0.08f, -bh * 0.42f) });
                }
            }
            // face
            float look = (float)Math.Sin(bp * 0.7) * 1.5f;
            if (c.Cfg.Meme)
            {
                c.Brush.Color = Ctx.A(Color.Black, show);
                c.G.FillRectangle(c.Brush, -bw * 0.38f, -bh * 0.18f, bw * 0.33f, bh * 0.16f);
                c.G.FillRectangle(c.Brush, bw * 0.05f, -bh * 0.18f, bw * 0.33f, bh * 0.16f);
                c.G.FillRectangle(c.Brush, -bw * 0.05f, -bh * 0.16f, bw * 0.1f, bh * 0.05f);
            }
            else
            {
                c.Dot(-bw * 0.18f, -bh * 0.1f, s * 0.13f, Ctx.A(Color.White, show));
                c.Dot(bw * 0.18f, -bh * 0.1f, s * 0.13f, Ctx.A(Color.White, show));
                c.Dot(-bw * 0.18f + look, -bh * 0.1f, s * 0.06f, Ctx.A(Color.Black, show));
                c.Dot(bw * 0.18f + look, -bh * 0.1f, s * 0.06f, Ctx.A(Color.Black, show));
            }
            c.Pen.Color = Ctx.A(Color.Black, show * 0.8f); c.Pen.Width = 1.4f;
            c.G.DrawArc(c.Pen, -bw * 0.15f, bh * 0.0f, bw * 0.3f, bh * 0.22f, 10, 160);
            c.G.Restore(st);
        }
    }
}

class Fireworks : ChaosEvent
{
    class P { public float X, Y, VX, VY, Age, Life; public Color C; public bool Rocket; public float TargetY; }
    readonly List<P> ps = new List<P>();
    float nextRocket;

    public override void Start(Ctx c, CrazyOverlay o) { Life = 7; }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        nextRocket -= c.Dt;
        if (Age < Life - 2 && (nextRocket <= 0 || c.Beat > 0.95f))
        {
            nextRocket = c.Rnd(0.2f, 0.5f);
            float ty = Math.Max(8, c.Edge - c.Rnd(10, Math.Max(12, c.Edge - 8)));
            ps.Add(new P { Rocket = true, X = c.Rnd(40, c.W - 40), Y = c.Edge + 2, VX = c.Rnd(-20, 20), VY = -c.Rnd(160, 240), Life = 3, TargetY = ty, C = c.Col(c.R.Next(8)) });
        }
        for (int i = ps.Count - 1; i >= 0; i--)
        {
            var p = ps[i];
            p.Age += c.Dt;
            p.X += p.VX * c.Dt; p.Y += p.VY * c.Dt;
            if (p.Rocket)
            {
                if (p.Y <= p.TargetY)
                {
                    ps.RemoveAt(i);
                    var col = c.R.Next(3) == 0 ? Ctx.Hue((float)c.R.NextDouble(), 1) : p.C;
                    for (int k = 0; k < 40; k++)
                    {
                        double a = k * Math.PI * 2 / 40; float sp = c.Rnd(40, 110);
                        ps.Add(new P { X = p.X, Y = p.Y, VX = (float)Math.Cos(a) * sp, VY = (float)Math.Sin(a) * sp, Life = c.Rnd(0.8f, 1.4f), C = col });
                    }
                }
            }
            else
            {
                p.VY += 70 * c.Dt;
                p.VX *= 1 - c.Dt * 1.2f;
                if (p.Age > p.Life) ps.RemoveAt(i);
            }
        }
    }

    public override void DrawFront(Ctx c)
    {
        foreach (var p in ps)
        {
            if (p.Rocket) { c.Line(p.X, p.Y, p.X - p.VX * 0.05f, p.Y - p.VY * 0.05f, Ctx.A(Color.White, 0.9f), 2); continue; }
            float a = 1 - p.Age / p.Life;
            c.Line(p.X, p.Y, p.X - p.VX * 0.06f, p.Y - p.VY * 0.06f, Ctx.A(p.C, a), 1.6f);
            if (c.R.Next(4) == 0) c.Dot(p.X, p.Y, 1.2f, Ctx.A(Color.White, a));
        }
    }
}

class BlackHole : ChaosEvent
{
    float cx, cy, spin;

    public override void Start(Ctx c, CrazyOverlay o)
    {
        Life = 6.5f;
        cx = c.Rnd(200, Math.Max(201, c.W - 200));
        cy = c.Edge + c.H / 2f;
    }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        spin += c.Dt * 6;
        bool collapsing = Age < Life - 0.6f;
        for (int i = 0; i < o.Particles.Count; i++)
        {
            var p = o.Particles[i];
            float dx = cx - p.X, dy = cy - p.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f;
            if (collapsing)
            {
                float pull = 9000 / (d + 30) * Fade * c.Dt;
                // spiral in: pull plus a sideways swirl
                p.X += dx / d * pull + -dy / d * pull * 0.8f;
                p.Y += dy / d * pull * 0.6f + dx / d * pull * 0.3f;
                if (d < 7) { p.X = cx + c.Rnd(-2, 2); p.Y = cy + c.Rnd(-2, 2); }
            }
        }
    }

    public override void End(Ctx c, CrazyOverlay o)
    {
        // blow everything back out
        foreach (var p in o.Particles)
        {
            double a = c.R.NextDouble() * Math.PI * 2; float r = c.Rnd(20, c.W / 2f);
            p.X = Math.Max(0, Math.Min(c.W, cx + (float)Math.Cos(a) * r));
            p.Y = c.Edge + 2 + (float)c.R.NextDouble() * (c.H - 4);
        }
        ((CrazyOverlay)o).Flash = 1;
    }

    public override void DrawBack(Ctx c)
    {
        float f = Fade;
        for (int ring = 0; ring < 3; ring++)
        {
            float r = 34 - ring * 9;
            c.Pen.Color = Ctx.A(c.Col(ring), 0.65f * f); c.Pen.Width = 3;
            float start = spin * (60 + ring * 25) % 360;
            c.G.DrawArc(c.Pen, cx - r * 1.6f, cy - r * 0.55f, r * 3.2f, r * 1.1f, start, 250);
        }
        c.Dot(cx, cy, 14 * f, Ctx.A(c.Col(0), 0.25f * f));
        c.Dot(cx, cy, 9 * f, Ctx.A(Color.Black, f));
    }
}

class Disco : ChaosEvent
{
    float cx, cy, hueShift;

    public override void Start(Ctx c, CrazyOverlay o) { Life = 9; cx = c.W / 2f + c.Rnd(-300, 300); }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        float drop = Math.Min(1, Age / 1.2f);
        cy = -20 + (Math.Max(14, c.Edge - 26) + 20) * drop;
        hueShift += c.Dt * (0.15f + c.Beat * 0.4f);
    }

    public override void DrawBack(Ctx c)
    {
        float f = Fade;
        for (int i = 0; i < 6; i++)
        {
            float ang = (float)(Math.PI / 2 + Math.Sin(Age * 1.3 + i * 1.1) * 1.2);
            float len = 900, spread = 0.07f;
            var tip = new PointF(cx, cy);
            var a = new PointF(cx + (float)Math.Cos(ang - spread) * len, cy + (float)Math.Sin(ang - spread) * len);
            var b = new PointF(cx + (float)Math.Cos(ang + spread) * len, cy + (float)Math.Sin(ang + spread) * len);
            var col = Ctx.Hue(hueShift + i / 6f, 1);
            using (var lb = new LinearGradientBrush(tip, new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2), Ctx.A(col, 0.45f * f), Ctx.A(col, 0)))
                c.G.FillPolygon(lb, new[] { tip, a, b });
        }
    }

    public override void DrawFront(Ctx c)
    {
        float f = Fade, r = 13;
        c.Line(cx, 0, cx, cy - r, Ctx.A(Color.Silver, f), 1);
        c.Dot(cx, cy, r, Ctx.A(Color.FromArgb(190, 190, 205), f));
        c.Pen.Color = Ctx.A(Color.FromArgb(90, 90, 110), f); c.Pen.Width = 1;
        for (int i = -2; i <= 2; i++)
        {
            float off = (i * 5 + Age * 20) % 25 - 12.5f;
            if (Math.Abs(off) < r) c.G.DrawLine(c.Pen, cx + off, cy - (float)Math.Sqrt(r * r - off * off), cx + off, cy + (float)Math.Sqrt(r * r - off * off));
            c.G.DrawLine(c.Pen, cx - r, cy + i * 5, cx + r, cy + i * 5);
        }
        for (int i = 0; i < 4; i++)
        {
            double a = c.R.NextDouble() * Math.PI * 2; float d = c.Rnd(4, r);
            c.Dot(cx + (float)Math.Cos(a) * d, cy + (float)Math.Sin(a) * d, 1.3f, Ctx.A(Color.White, f));
        }
    }
}

class Snake : ChaosEvent
{
    readonly List<PointF> trail = new List<PointF>();
    readonly List<PointF> food = new List<PointF>();
    float x, y, ang, length = 60;

    public override void Start(Ctx c, CrazyOverlay o)
    {
        Life = 16;
        x = -10; y = (Top(c) + Bottom(c)) / 2; ang = 0;
        for (int i = 0; i < 6; i++) food.Add(RandomFood(c));
    }

    PointF RandomFood(Ctx c) { return new PointF(c.Rnd(30, c.W - 30), c.Rnd(Top(c) + 6, Bottom(c) - 6)); }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        if (food.Count == 0) food.Add(RandomFood(c));
        var target = food.OrderBy(f => (f.X - x) * (f.X - x) + (f.Y - y) * (f.Y - y)).First();
        double want = Math.Atan2(target.Y - y, target.X - x);
        double diff = Math.Atan2(Math.Sin(want - ang), Math.Cos(want - ang));
        ang += (float)Math.Max(-4 * c.Dt, Math.Min(4 * c.Dt, diff)) + (float)Math.Sin(Age * 7) * 0.02f;
        float sp = 170 + c.Audio * 120;
        x += (float)Math.Cos(ang) * sp * c.Dt;
        y += (float)Math.Sin(ang) * sp * c.Dt;
        y = Math.Max(Top(c), Math.Min(Bottom(c), y));
        trail.Insert(0, new PointF(x, y));
        while (trail.Count > length) trail.RemoveAt(trail.Count - 1);
        for (int i = food.Count - 1; i >= 0; i--)
            if (Math.Abs(food[i].X - x) < 9 && Math.Abs(food[i].Y - y) < 9) { food.RemoveAt(i); length += 14; food.Add(RandomFood(c)); }
    }

    public override void DrawFront(Ctx c)
    {
        float f = Fade;
        foreach (var fd in food) { c.Glow(fd.X, fd.Y, 3 + (float)Math.Sin(Age * 8 + fd.X) * 0.8f, Ctx.Hue(fd.X / 500f, 1), f); }
        for (int i = trail.Count - 1; i >= 0; i -= 2)
        {
            float k = i / (float)Math.Max(1, trail.Count);
            c.Dot(trail[i].X, trail[i].Y, 5.5f - k * 2.5f, Ctx.A(Ctx.Lerp(c.Col(1), c.Col(0), k), f));
        }
        // head with eyes and a flicking tongue
        float ex = (float)Math.Cos(ang), ey = (float)Math.Sin(ang);
        c.Dot(x, y, 7, Ctx.A(c.Col(1), f));
        c.Dot(x + ex * 3 - ey * 3, y + ey * 3 + ex * 3, 1.8f, Ctx.A(Color.White, f));
        c.Dot(x + ex * 3 + ey * 3, y + ey * 3 - ex * 3, 1.8f, Ctx.A(Color.White, f));
        if ((int)(Age * 6) % 2 == 0) c.Line(x + ex * 7, y + ey * 7, x + ex * 13, y + ey * 13, Ctx.A(Color.FromArgb(255, 60, 90), f), 1.4f);
    }
}

class Storm : ChaosEvent
{
    class B { public PointF[] Pts; public float Life, Age; public Color C; }
    readonly List<B> bs = new List<B>();
    float next;

    public override void Start(Ctx c, CrazyOverlay o) { Life = 3.2f; }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        next -= c.Dt;
        if (next <= 0 && Age < Life - 0.4f)
        {
            next = c.Rnd(0.04f, 0.12f);
            bool vertical = c.R.Next(2) == 0 && c.Edge > 30;
            var pts = vertical ? c.JaggedV(c.Rnd(0, c.W), 0, c.Edge + c.H / 2f, 14) : c.Jagged(c.Rnd(-100, c.W - 400), c.Rnd(300, 900), c.Edge, 10);
            bs.Add(new B { Pts = pts, Life = c.Rnd(0.15f, 0.35f), C = c.Col(c.R.Next(8)) });
            if (c.R.Next(4) == 0) ((CrazyOverlay)o).Flash = 0.8f;
        }
        for (int i = bs.Count - 1; i >= 0; i--) { bs[i].Age += c.Dt; if (bs[i].Age > bs[i].Life) bs.RemoveAt(i); }
    }

    public override void DrawFront(Ctx c)
    {
        foreach (var b in bs)
        {
            float a = 1 - b.Age / b.Life;
            c.Stroke(b.Pts, Ctx.A(b.C, 0.25f * a), 10);
            c.Stroke(b.Pts, Ctx.A(b.C, 0.7f * a), 3.5f);
            c.Stroke(b.Pts, Ctx.A(Color.White, a), 1.3f);
        }
    }
}

class Earthquake : ChaosEvent
{
    public override void Start(Ctx c, CrazyOverlay o) { Life = 1.8f; }
    public override void Update(Ctx c, CrazyOverlay o)
    {
        float amp = 7 * Fade;
        o.ShakeX = (int)c.Rnd(-amp, amp);
        o.ShakeY = (int)c.Rnd(-amp * 0.6f, amp * 0.6f);
    }
    public override void End(Ctx c, CrazyOverlay o) { o.ShakeX = 0; o.ShakeY = 0; }
}

class Glitch : ChaosEvent
{
    readonly ImageAttributes red = new ImageAttributes(), cyan = new ImageAttributes();

    public override void Start(Ctx c, CrazyOverlay o)
    {
        Life = 1.1f;
        red.SetColorMatrix(new ColorMatrix(new[] { new float[] { 1, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0.6f, 0 }, new float[] { 0, 0, 0, 0, 1 } }));
        cyan.SetColorMatrix(new ColorMatrix(new[] { new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 1, 0, 0, 0 }, new float[] { 0, 0, 1, 0, 0 }, new float[] { 0, 0, 0, 0.6f, 0 }, new float[] { 0, 0, 0, 0, 1 } }));
    }

    public override void Post(Ctx c, CrazyOverlay o)
    {
        if (c.R.Next(3) == 0) return;          // flickers on and off
        var snap = o.Snapshot();
        int fh = c.Edge + c.H;
        int off = (int)c.Rnd(3, 7);
        c.G.DrawImage(snap, new Rectangle(off, 0, c.W, fh), 0, 0, c.W, fh, GraphicsUnit.Pixel, red);
        c.G.DrawImage(snap, new Rectangle(-off, 0, c.W, fh), 0, 0, c.W, fh, GraphicsUnit.Pixel, cyan);
        for (int i = 0; i < 8; i++)
        {
            int y = c.R.Next(fh), h = 2 + c.R.Next(8), dx = (int)c.Rnd(-60, 60);
            c.G.DrawImage(snap, new Rectangle(dx, y, c.W, h), 0, y, c.W, h, GraphicsUnit.Pixel);
        }
    }

    public override void End(Ctx c, CrazyOverlay o) { red.Dispose(); cyan.Dispose(); }
}

class Jelly : ChaosEvent
{
    public override void Start(Ctx c, CrazyOverlay o) { Life = 4; }

    public override void Post(Ctx c, CrazyOverlay o)
    {
        var snap = o.Snapshot();
        int fh = c.Edge + c.H;
        float amp = 7 * Fade;
        var mode = c.G.CompositingMode;
        c.G.CompositingMode = CompositingMode.SourceCopy;
        c.G.Clear(Color.Transparent);
        for (int x = 0; x < c.W; x += 8)
        {
            int dy = (int)(Math.Sin(x * 0.012 + c.T * 9) * amp);
            c.G.DrawImage(snap, new Rectangle(x, dy, 8, fh), x, 0, 8, fh, GraphicsUnit.Pixel);
        }
        c.G.CompositingMode = mode;
    }
}

class GravityFlip : ChaosEvent
{
    public override void Start(Ctx c, CrazyOverlay o) { Life = 7; Flip(o); }
    public override void End(Ctx c, CrazyOverlay o) { Flip(o); }
    static void Flip(CrazyOverlay o) { foreach (var p in o.Particles) { p.VX = -p.VX; p.VY = -p.VY; } }
    public override void DrawFront(Ctx c) { if (Age < 1.5f) Text(c, "GRAVITY FLIP", Bold, c.W / 2f, Math.Max(12, c.Edge - 14), c.Col(1), Fade * (1 - Age / 1.5f)); }
}

class ThemeMorph : ChaosEvent
{
    public override void Start(Ctx c, CrazyOverlay o)
    {
        Life = 8;
        var all = c.Cfg.Themes.Where(t => t.Name != c.Theme.Name).ToList();
        o.ThemeOverride = all[c.R.Next(all.Count)];
        o.Flash = 1;
    }
    public override void End(Ctx c, CrazyOverlay o) { o.ThemeOverride = null; o.Flash = 1; }
    public override void DrawFront(Ctx c) { if (Age < 2) Text(c, c.Theme.Name.ToUpperInvariant() + " MODE", Bold, c.W / 2f, Math.Max(12, c.Edge - 14), c.Col(1), Math.Min(1, 2 - Age)); }
}

class LogoBounce : ChaosEvent
{
    float x, y, vx, vy, LW = 92, LH = 30;
    int colorIx;
    Bitmap meme;

    public override void Start(Ctx c, CrazyOverlay o)
    {
        Life = 14;
        if (UseMemes(c))
        {
            meme = MemeLibrary.Pick(c.R);
            LH = Math.Max(20, Math.Min(64, Bottom(c) - Top(c) - 4));
            LW = LH * meme.Width / meme.Height;
        }
        x = c.Rnd(0, c.W - LW); y = c.Rnd(Top(c), Math.Max(Top(c) + 1, Bottom(c) - LH));
        vx = 140 * (c.R.Next(2) == 0 ? 1 : -1); vy = 55 * (c.R.Next(2) == 0 ? 1 : -1);
    }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        x += vx * c.Dt; y += vy * c.Dt;
        bool hx = false, hy = false;
        if (x < 0) { x = 0; vx = Math.Abs(vx); hx = true; }
        if (x > c.W - LW) { x = c.W - LW; vx = -Math.Abs(vx); hx = true; }
        if (y < Top(c)) { y = Top(c); vy = Math.Abs(vy); hy = true; }
        if (y > Bottom(c) - LH) { y = Bottom(c) - LH; vy = -Math.Abs(vy); hy = true; }
        if (hx || hy) colorIx++;
        if (hx && hy)
        {   // THE CORNER
            o.Flash = 1;
            o.Director.Request("Fireworks");
            o.Director.Request("Meme Text");
        }
    }

    public override void DrawFront(Ctx c)
    {
        var col = Ctx.Hue(colorIx * 0.17f, 0.9f * Fade);
        if (meme != null)
        {
            c.Brush.Color = Ctx.A(col, 0.5f * Fade);
            c.G.FillRectangle(c.Brush, x - 4, y - 4, LW + 8, LH + 8);     // glow frame that changes colour on every bounce
            DrawMeme(c, meme, x + LW / 2, y + LH / 2, LH, 0, Fade, 1, 1, Color.FromArgb(255, col));
            return;
        }
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(x, y, LW, LH);
            c.Brush.Color = col;
            c.G.FillPath(c.Brush, path);
        }
        Text(c, "AURA", Impact, x + LW / 2, y + LH / 2, Color.White, Fade);
    }
}

class RainbowToast : ChaosEvent
{
    float x, y0;
    Bitmap meme;
    static readonly Color[] Bands = { Color.FromArgb(255, 40, 40), Color.FromArgb(255, 150, 0), Color.FromArgb(255, 235, 0), Color.FromArgb(40, 220, 80), Color.FromArgb(40, 140, 255), Color.FromArgb(150, 60, 255) };

    public override void Start(Ctx c, CrazyOverlay o)
    {
        Life = 5.5f;
        x = c.W + 40;
        if (UseMemes(c)) meme = MemeLibrary.Pick(c.R);
        y0 = c.Edge > 40 ? c.Edge * 0.5f : c.Edge + c.H / 2f;
    }

    public override void Update(Ctx c, CrazyOverlay o) { x -= (c.W + 300) / (Life - 0.3f) * c.Dt; }

    public override void DrawFront(Ctx c)
    {
        float y = y0 + (float)Math.Sin(Age * 6) * 5;
        // stepped rainbow trail behind (to the right)
        for (float tx = x + 30; tx < c.W + 20; tx += 14)
        {
            float wob = ((int)((tx - x) / 14 + Age * 8) % 2 == 0) ? -2 : 2;
            for (int i = 0; i < Bands.Length; i++)
            {
                c.Brush.Color = Ctx.A(Bands[i], 0.9f);
                c.G.FillRectangle(c.Brush, tx, y - 9 + i * 3 + wob, 14, 3);
            }
        }
        if (meme != null)
        {
            DrawMeme(c, meme, x + 14, y, 44, (float)Math.Sin(Age * 6) * 8, 1);
            for (int i = 0; i < 3; i++) c.Glow(x + c.Rnd(40, 300), y + c.Rnd(-16, 16), 1.2f, Color.White, 0.8f);
            return;
        }
        // the toast
        c.Brush.Color = Color.FromArgb(222, 170, 105);
        c.G.FillRectangle(c.Brush, x - 2, y - 13, 34, 26);
        c.Brush.Color = Color.FromArgb(255, 150, 200);
        c.G.FillRectangle(c.Brush, x + 2, y - 9, 26, 18);
        var rng = new Random(7);
        for (int i = 0; i < 6; i++) c.Dot(x + 5 + rng.Next(20), y - 6 + rng.Next(12), 1, Bands[rng.Next(Bands.Length)]);
        c.Dot(x + 9, y - 1, 1.6f, Color.Black);
        c.Dot(x + 19, y - 1, 1.6f, Color.Black);
        c.Pen.Color = Color.Black; c.Pen.Width = 1.2f;
        c.G.DrawArc(c.Pen, x + 10, y, 8, 5, 10, 160);
        // little running legs
        float leg = (float)Math.Sin(Age * 20) * 3;
        c.Line(x + 4, y + 13, x + 2 + leg, y + 17, Color.FromArgb(150, 110, 60), 2);
        c.Line(x + 26, y + 13, x + 28 - leg, y + 17, Color.FromArgb(150, 110, 60), 2);
        // sparkles
        for (int i = 0; i < 3; i++) c.Glow(x + c.Rnd(40, 300), y + c.Rnd(-16, 16), 1.2f, Color.White, 0.8f);
    }
}

class MemeText : ChaosEvent
{
    static readonly string[] Phrases = {
        "wow", "such taskbar", "very aura", "much glow", "so electric", "amaze", "W taskbar", "sheesh", "no way", "bruh",
        "gg", "10/10", "skill issue", "certified banger", "stonks", "main character energy", "it's giving aura", "built different",
        "+1000 aura", "goated", "lowkey fire", "POV: your taskbar", "unhinged", "absolute cinema", "sigma taskbar", "aura farming",
        "who let him cook", "peak", "real", "ok but why is it so hard", "the taskbar is the main character"
    };
    class Line { public string S; public float X, Y, VY, Age, Life, Hue, Angle; public bool Big; }
    readonly List<Line> lines = new List<Line>();
    float next;

    public override void Start(Ctx c, CrazyOverlay o) { Life = 6.5f; }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        next -= c.Dt;
        if (next <= 0 && Age < Life - 1.5f)
        {
            next = c.Rnd(0.35f, 0.8f);
            lines.Add(new Line { S = Phrases[c.R.Next(Phrases.Length)], X = c.Rnd(80, c.W - 80), Y = c.Edge + c.H * 0.5f, VY = -c.Rnd(12, 30), Life = c.Rnd(2.2f, 3.5f), Hue = (float)c.R.NextDouble(), Angle = c.Rnd(-14, 14), Big = c.R.Next(5) == 0 });
        }
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var l = lines[i];
            l.Age += c.Dt; l.Y += l.VY * c.Dt; l.Hue += c.Dt * 0.3f;
            if (l.Age > l.Life) lines.RemoveAt(i);
        }
    }

    public override void DrawFront(Ctx c)
    {
        foreach (var l in lines)
        {
            float a = Math.Min(1, Math.Min(l.Age / 0.3f, (l.Life - l.Age) / 0.6f));
            float y = Math.Max(14, l.Y);
            Text(c, l.S, l.Big ? MemeBig : Meme, l.X, y, Ctx.Hue(l.Hue, 1), a, l.Angle);
        }
    }
}

class Hitmarkers : ChaosEvent
{
    class H { public float X, Y, Age; public string Pop; }
    readonly List<H> hs = new List<H>();
    float next;
    static readonly string[] Pops = { "+100", "+69", "HIT", "+420", "x2", "HEADSHOT", "+1000 AURA" };

    public override void Start(Ctx c, CrazyOverlay o) { Life = 5; }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        next -= c.Dt;
        if ((next <= 0 || c.Beat > 0.95f) && Age < Life - 0.5f)
        {
            next = c.Rnd(0.12f, 0.35f);
            hs.Add(new H { X = c.Rnd(30, c.W - 30), Y = c.Rnd(Top(c) + 10, Bottom(c) - 6), Pop = c.R.Next(3) == 0 ? Pops[c.R.Next(Pops.Length)] : null });
            if (c.R.Next(6) == 0) o.ShakeX = (int)c.Rnd(-4, 4);
            else o.ShakeX = 0;
        }
        for (int i = hs.Count - 1; i >= 0; i--) { hs[i].Age += c.Dt; if (hs[i].Age > 0.7f) hs.RemoveAt(i); }
    }

    public override void End(Ctx c, CrazyOverlay o) { o.ShakeX = 0; }

    public override void DrawFront(Ctx c)
    {
        foreach (var h in hs)
        {
            float a = 1 - h.Age / 0.7f, g = 3, l = 8;
            foreach (var d in new[] { new PointF(-1, -1), new PointF(1, -1), new PointF(-1, 1), new PointF(1, 1) })
            {
                c.Line(h.X + d.X * g, h.Y + d.Y * g, h.X + d.X * (g + l), h.Y + d.Y * (g + l), Ctx.A(Color.Black, a), 4);
                c.Line(h.X + d.X * g, h.Y + d.Y * g, h.X + d.X * (g + l), h.Y + d.Y * (g + l), Ctx.A(Color.White, a), 2);
            }
            if (h.Pop != null) Text(c, h.Pop, Impact, h.X + 26, h.Y - 12 - h.Age * 30, Color.Gold, a);
        }
    }
}

class DealWithIt : ChaosEvent
{
    float cx;

    public override void Start(Ctx c, CrazyOverlay o) { Life = 5.5f; cx = c.W / 2f + c.Rnd(-400, 400); }

    public override void DrawFront(Ctx c)
    {
        float land = Math.Max(14, c.Edge - 10);
        float k = Math.Min(1, Age / 2f);
        float y = -20 + (land + 20) * k;
        float px = 4;   // pixel size
        // pixel-art shades: 16 x 4 grid
        string[] art = { "XXXXXXXXXXXXXXXX", "XWWXXXXXXXWWXXX.", ".XXXXX..XXXXXX..", "..XXX....XXX...." };
        float x0 = cx - 8 * px;
        for (int r = 0; r < art.Length; r++)
            for (int col = 0; col < art[r].Length; col++)
            {
                char ch = art[r][col];
                if (ch == '.') continue;
                c.Brush.Color = Ctx.A(ch == 'W' ? Color.White : Color.Black, Fade);
                c.G.FillRectangle(c.Brush, x0 + col * px, y + r * px, px, px);
            }
        if (k >= 1) Text(c, "DEAL WITH IT", Impact, cx, Math.Max(10, y - 14), Color.White, Fade * Math.Min(1, (Age - 2) * 3));
    }
}

class MemeRain : ChaosEvent
{
    class M { public Bitmap B; public float X, Y, VX, VY, Ang, Spin, H; }
    readonly List<M> ms = new List<M>();
    float next;

    public override void Start(Ctx c, CrazyOverlay o) { Life = UseMemes(c) ? 9 : 0; }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        next -= c.Dt;
        if (next <= 0 && Age < Life - 3)
        {
            next = c.Rnd(0.25f, 0.6f);
            ms.Add(new M { B = MemeLibrary.Pick(c.R), X = c.Rnd(40, c.W - 40), Y = -40, VX = c.Rnd(-40, 40), VY = c.Rnd(0, 60), Spin = c.Rnd(-120, 120), H = c.Rnd(34, Math.Max(35, Math.Min(56, c.Edge - 8))) });
        }
        foreach (var m in ms)
        {
            m.VY += 520 * c.Dt;
            m.X += m.VX * c.Dt; m.Y += m.VY * c.Dt; m.Ang += m.Spin * c.Dt;
            float floor = c.Edge - m.H * 0.5f;
            if (m.Y > floor && m.VY > 0)
            {
                m.Y = floor; m.VY = -m.VY * 0.55f; m.Spin *= 0.6f;
                if (Math.Abs(m.VY) < 40) { m.VY = 0; m.Spin = 0; m.Ang *= 0.9f; m.VX *= 0.8f; }
            }
        }
    }

    public override void DrawFront(Ctx c) { foreach (var m in ms) DrawMeme(c, m.B, m.X, m.Y, m.H, m.Ang, Fade); }
}

class MemePopup : ChaosEvent
{
    Bitmap meme;
    float x, h;
    bool boomed;

    public override void Start(Ctx c, CrazyOverlay o)
    {
        meme = UseMemes(c) ? MemeLibrary.Pick(c.R) : null;
        Life = meme == null ? 0 : 4.5f;
        h = Math.Max(40, Math.Min(c.Edge - 6, 110));
        x = c.Rnd(h, Math.Max(h + 1, c.W - h));
    }

    public override void Update(Ctx c, CrazyOverlay o)
    {
        if (!boomed && Age > 0.45f)
        {
            boomed = true;
            o.Flash = 0.9f;
            o.Director.Request("Earthquake");
        }
    }

    public override void DrawFront(Ctx c)
    {
        // pops up with an overshoot, holds, then drops back into the taskbar
        float up = Math.Min(1, Age / 0.45f);
        float overshoot = 1 + 2.2f * (float)Math.Pow(up - 1, 3) + 1.2f * (float)Math.Pow(up - 1, 2);
        float down = Math.Max(0, (Age - (Life - 0.5f)) / 0.5f);
        float rise = overshoot * (1 - down * down);
        float zoom = Age > 0.45f && Age < 0.7f ? 1 + (0.7f - Age) * 0.6f : 1;
        float y = c.Edge + h * 0.5f - rise * (h + 4);
        DrawMeme(c, meme, x, y, h * zoom, (float)Math.Sin(Age * 3) * 3, 1, 1, 1, c.Col(1));
    }
}

class MemeParade : ChaosEvent
{
    List<Bitmap> memes;
    float x0;

    public override void Start(Ctx c, CrazyOverlay o)
    {
        memes = UseMemes(c) ? MemeLibrary.PickMany(c.R, 7) : new List<Bitmap>();
        Life = memes.Count == 0 ? 0 : 12;
        x0 = c.W + 60;
    }

    public override void Update(Ctx c, CrazyOverlay o) { x0 -= 150 * c.Dt; }

    public override void DrawFront(Ctx c)
    {
        float h = Math.Max(24, Math.Min(48, c.Edge - 8));
        for (int i = 0; i < memes.Count; i++)
        {
            float x = x0 + i * (h * 1.9f);
            if (x < -100 || x > c.W + 100) continue;
            float step = (float)Math.Abs(Math.Sin(Age * 6 + i * 0.9)) * 8;
            DrawMeme(c, memes[i], x, c.Edge - h * 0.5f - 2 - step, h, (float)Math.Sin(Age * 6 + i * 0.9) * 6, Fade);
        }
    }
}
