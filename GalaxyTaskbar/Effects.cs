// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// An aura effect drawn along the taskbar's top edge.
abstract class Fx
{
    public abstract bool Enabled(Config c);
    public virtual void Update(Ctx c) { }
    public abstract void Draw(Ctx c);

    // Back to front.
    public static List<Fx> All()
    {
        return new List<Fx> { new EqualizerFx(), new FireCurtainFx(), new FlamesFx(), new PlasmaFx(), new WireFx(), new LightningFx(), new SparksFx() };
    }
}

// Flickering white-hot wire running along the edge.
class WireFx : Fx
{
    public override bool Enabled(Config c) { return c.Wire; }
    public override void Draw(Ctx c)
    {
        var pts = new PointF[c.W / 16 + 2];
        float amp = 1.6f + c.Audio * 3;
        for (int i = 0; i < pts.Length; i++)
        {
            float x = i * 16;
            pts[i] = new PointF(x, c.Edge + 0.8f * (float)Math.Sin(x * 0.04f + c.T * 11) + ((float)c.R.NextDouble() - 0.5f) * amp);
        }
        float pulse = 0.65f + 0.35f * (float)Math.Sin(c.T * 2.4f) + c.Beat * 0.3f;
        c.Stroke(pts, Ctx.A(c.Col(1), 0.35f * pulse), 4f);
        c.Stroke(pts, Ctx.A(Color.White, Math.Min(1, 0.75f * pulse)), 1.2f);
    }
}

// Crackling electric arcs along the edge.
class LightningFx : Fx
{
    class Bolt { public float X0, Len, Life, Age; public Color C; public PointF[] Pts; }
    readonly List<Bolt> bolts = new List<Bolt>();
    float next;

    public override bool Enabled(Config c) { return c.Lightning; }

    public override void Update(Ctx c)
    {
        next -= c.Dt * (float)c.Cfg.LightningRate * (1 + c.Audio * 3);
        if (next <= 0 || c.Beat > 0.95f)
        {
            next = 0.15f + (float)c.R.NextDouble() * 0.6f;
            var b = new Bolt { Len = c.Rnd(120, 640), Life = c.Rnd(0.25f, 0.85f), C = c.Col(c.R.Next(8)) };
            b.X0 = c.Rnd(0, Math.Max(1, c.W - b.Len));
            bolts.Add(b);
        }
        for (int i = bolts.Count - 1; i >= 0; i--)
        {
            var b = bolts[i];
            b.Age += c.Dt;
            if (b.Age >= b.Life) { bolts.RemoveAt(i); continue; }
            if (b.Pts == null || c.R.Next(2) == 0) b.Pts = c.Jagged(b.X0, b.Len, c.Edge, 7f + c.Audio * 8);
        }
    }

    public override void Draw(Ctx c)
    {
        foreach (var b in bolts)
        {
            float a = (1 - b.Age / b.Life) * (0.7f + 0.3f * (float)c.R.NextDouble());
            c.Stroke(b.Pts, Ctx.A(b.C, 0.18f * a), 8f);
            c.Stroke(b.Pts, Ctx.A(b.C, 0.55f * a), 3f);
            c.Stroke(b.Pts, Ctx.A(Color.White, 0.95f * a), 1.1f);
            var bp = b.Pts[b.Pts.Length / 2];
            c.Stroke(c.Jagged(bp.X, c.Rnd(30, 70), bp.Y, 5f), Ctx.A(b.C, 0.5f * a), 1f);
        }
    }
}

// Curtain of flames licking up from the edge, in the theme's colours.
class FireCurtainFx : Fx
{
    public override bool Enabled(Config c) { return c.FireCurtain; }

    public override void Draw(Ctx c)
    {
        float maxH = (12 + 20 * (float)c.Cfg.FireHeight) * (1 + c.Audio * 0.8f + c.Beat * 0.3f);
        maxH = Math.Min(maxH, c.Edge + 6);
        var layers = new[]
        {
            new { Scale = 1.00f, Col = c.Col(0), Alpha = 0.45f, Seed = 0.0f },
            new { Scale = 0.72f, Col = c.Col(2), Alpha = 0.60f, Seed = 1.7f },
            new { Scale = 0.45f, Col = Ctx.Lerp(c.Col(1), Color.White, 0.45f), Alpha = 0.85f, Seed = 3.1f },
        };
        float baseY = c.Edge + 3;
        foreach (var L in layers)
        {
            var pts = new List<PointF> { new PointF(0, baseY) };
            for (int x = 0; x <= c.W + 6; x += 6)
            {
                float n = 0.5f + 0.5f * (float)(Math.Sin(x * 0.021 + c.T * 3.3 + L.Seed) * 0.5 + Math.Sin(x * 0.057 - c.T * 5.1 + L.Seed * 2) * 0.3 + Math.Sin(x * 0.11 + c.T * 8.7) * 0.2);
                float h = maxH * L.Scale * (0.3f + 0.7f * n * n);
                pts.Add(new PointF(x, c.Edge - h));
            }
            pts.Add(new PointF(c.W, baseY));
            float top = c.Edge - maxH * L.Scale - 1;
            using (var lb = new LinearGradientBrush(new PointF(0, baseY), new PointF(0, top), Ctx.A(L.Col, L.Alpha), Ctx.A(L.Col, 0)))
                c.G.FillPolygon(lb, pts.ToArray());
            // bright tips outline
            var tips = pts.GetRange(1, pts.Count - 2).ToArray();
            c.Stroke(tips, Ctx.A(L.Col, L.Alpha * 0.35f), 1f);
        }
    }
}

// Individual flame particles rising off the edge.
class FlamesFx : Fx
{
    class F { public float X, Y, VX, VY, Age, Life, Size; }
    readonly List<F> fs = new List<F>();
    float acc;

    public override bool Enabled(Config c) { return c.Flames; }

    public override void Update(Ctx c)
    {
        acc += c.Dt * c.W / 7f * (float)c.Cfg.FireHeight * (1 + c.Audio * 2);
        while (acc > 1 && fs.Count < 900)
        {
            acc -= 1;
            fs.Add(new F { X = c.Rnd(0, c.W), Y = c.Edge + 2, VX = c.Rnd(-8, 8), VY = -c.Rnd(25, 60) * (float)Math.Sqrt(c.Cfg.FireHeight), Life = c.Rnd(0.45f, 1.1f), Size = c.Rnd(2, 5) });
        }
        if (acc > 1) acc = 0;
        for (int i = fs.Count - 1; i >= 0; i--)
        {
            var f = fs[i];
            f.Age += c.Dt;
            if (f.Age >= f.Life) { fs.RemoveAt(i); continue; }
            f.X += (f.VX + (float)Math.Sin(f.Age * 9 + f.X) * 10) * c.Dt;
            f.Y += f.VY * c.Dt;
        }
    }

    public override void Draw(Ctx c)
    {
        var hot = Ctx.Lerp(c.Col(1), Color.White, 0.6f);
        foreach (var f in fs)
        {
            float k = f.Age / f.Life;
            Color col = k < 0.25f ? Ctx.Lerp(hot, c.Col(1), k / 0.25f) : k < 0.6f ? Ctx.Lerp(c.Col(1), c.Col(0), (k - 0.25f) / 0.35f) : c.Col(0);
            float a = k < 0.6f ? 0.8f : 0.8f * (1 - (k - 0.6f) / 0.4f);
            c.Dot(f.X, f.Y, f.Size * (1 - k * 0.6f), Ctx.A(col, a));
        }
    }
}

// Glowing sine waves weaving along the edge.
class PlasmaFx : Fx
{
    public override bool Enabled(Config c) { return c.Plasma; }

    public override void Draw(Ctx c)
    {
        float amp = 4 + c.Audio * 10 + c.Beat * 4;
        for (int k = 0; k < 3; k++)
        {
            var pts = new PointF[c.W / 10 + 2];
            for (int i = 0; i < pts.Length; i++)
            {
                float x = i * 10;
                float y = c.Edge - 5 - amp * (float)(Math.Sin(x * 0.006 * (k + 1) + c.T * (1.5 + k)) * 0.6 + Math.Sin(x * 0.017 - c.T * 2.3 * (k + 1)) * 0.4);
                pts[i] = new PointF(x, y);
            }
            c.Stroke(pts, Ctx.A(c.Col(k), 0.16f), 6f);
            c.Stroke(pts, Ctx.A(c.Col(k), 0.8f), 1.4f);
        }
    }
}

// Sparks shooting up off the edge and falling back.
class SparksFx : Fx
{
    class S { public float X, Y, PX, PY, VX, VY, Age, Life; public Color C; }
    readonly List<S> ss = new List<S>();
    float acc;

    public override bool Enabled(Config c) { return c.Sparks; }

    void Emit(Ctx c, int n, float x0, float x1)
    {
        for (int i = 0; i < n && ss.Count < 500; i++)
        {
            float x = c.Rnd(x0, x1);
            ss.Add(new S { X = x, Y = c.Edge, PX = x, PY = c.Edge, VX = c.Rnd(-45, 45), VY = -c.Rnd(60, 150), Life = c.Rnd(0.7f, 1.4f), C = c.R.Next(3) == 0 ? Color.White : c.Col(c.R.Next(8)) });
        }
    }

    public override void Update(Ctx c)
    {
        acc += c.Dt * (10 + c.Audio * 60);
        while (acc > 1) { acc -= 1; Emit(c, 1, 0, c.W); }
        if (c.Beat > 0.95f) { float x = c.Rnd(0, c.W); Emit(c, 14, x - 40, x + 40); }
        for (int i = ss.Count - 1; i >= 0; i--)
        {
            var s = ss[i];
            s.Age += c.Dt;
            s.PX = s.X; s.PY = s.Y;
            s.VY += 220 * c.Dt;
            s.X += s.VX * c.Dt; s.Y += s.VY * c.Dt;
            if (s.Age > s.Life || (s.VY > 0 && s.Y > c.Edge + 2) || s.Y < 0) ss.RemoveAt(i);
        }
    }

    public override void Draw(Ctx c)
    {
        foreach (var s in ss)
        {
            float a = 1 - s.Age / s.Life;
            c.Line(s.PX - s.VX * 0.03f, s.PY - s.VY * 0.03f, s.X, s.Y, Ctx.A(s.C, a), 1.3f);
            c.Dot(s.X, s.Y, 1.1f, Ctx.A(Color.White, a));
        }
    }
}

// Music visualizer bars rising from the edge (gently idles when nothing is playing).
class EqualizerFx : Fx
{
    float[] h = new float[0], peak = new float[0];

    public override bool Enabled(Config c) { return c.Equalizer; }

    public override void Update(Ctx c)
    {
        int n = Math.Max(1, c.W / 16);
        if (h.Length != n) { h = new float[n]; peak = new float[n]; }
        float maxH = Math.Min(38, c.Edge - 2);
        for (int i = 0; i < n; i++)
        {
            float noise = 0.5f + 0.5f * (float)(Math.Sin(i * 1.7 + c.T * 7.3) * Math.Sin(i * 0.37 - c.T * 3.1));
            float idle = 0.08f + 0.06f * (float)Math.Sin(i * 0.3 + c.T * 2);
            float target = (Math.Max(idle, c.Audio * (0.2f + 0.8f * noise)) + c.Beat * 0.25f * noise) * maxH;
            h[i] += (target - h[i]) * Math.Min(1, c.Dt * (target > h[i] ? 25 : 6));
            peak[i] = Math.Max(h[i], peak[i] - c.Dt * 25);
        }
    }

    public override void Draw(Ctx c)
    {
        for (int i = 0; i < h.Length; i++)
        {
            float x = i * 16 + 3, bh = Math.Max(1, h[i]);
            var col = c.Col(i * c.Theme.Colors.Length / Math.Max(1, h.Length));
            using (var lb = new LinearGradientBrush(new PointF(0, c.Edge + 1), new PointF(0, c.Edge - bh - 1), Ctx.A(col, 0.75f), Ctx.A(c.Col(2), 0.85f)))
                c.G.FillRectangle(lb, x, c.Edge - bh, 10, bh);
            c.Brush.Color = Ctx.A(Color.White, 0.8f);
            c.G.FillRectangle(c.Brush, x, c.Edge - peak[i] - 3, 10, 2);
            c.Brush.Color = Ctx.A(col, 0.18f);
            c.G.FillRectangle(c.Brush, x, c.Edge, 10, bh * 0.4f);   // reflection behind the taskbar
        }
    }
}
