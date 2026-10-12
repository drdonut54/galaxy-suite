// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

class Theme
{
    public string Name;
    public Color Bg;
    public Color[] Colors;     // nebula / aura / lightning / fire colours (3 or more)
    public Color Star;
    public string Particles;   // see Particle.Types
    public bool NebulaPng;
    public bool Custom;

    public Theme Clone()
    {
        return new Theme { Name = Name, Bg = Bg, Colors = (Color[])Colors.Clone(), Star = Star, Particles = Particles, NebulaPng = NebulaPng, Custom = Custom };
    }

    static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
    static Theme T(string name, Color bg, Color star, string parts, params Color[] cols)
    {
        return new Theme { Name = name, Bg = bg, Star = star, Particles = parts, Colors = cols };
    }

    public static List<Theme> BuiltIn()
    {
        var w = Color.White;
        var list = new List<Theme>
        {
            T("Galaxy", C(6, 3, 18), w, "stars", C(166, 108, 245), C(79, 195, 255), C(232, 63, 191)),
            T("Matrix", C(0, 7, 2), C(140, 255, 160), "code", C(0, 255, 90), C(0, 170, 60), C(170, 255, 170)),
            T("Fire", C(16, 4, 1), C(255, 220, 120), "embers", C(255, 90, 20), C(255, 175, 40), C(210, 25, 10)),
            T("Blood", C(10, 0, 1), C(255, 120, 120), "stars", C(190, 0, 20), C(255, 45, 45), C(110, 0, 10)),
            T("Frost", Color.FromArgb(120, 200, 212, 228), w, "frost", C(255, 255, 255), C(190, 225, 255), C(225, 235, 250)),
            T("Synthwave", C(18, 0, 42), w, "stars", C(255, 43, 214), C(0, 229, 255), C(255, 176, 0)),
            T("Ocean", C(0, 12, 28), C(204, 246, 255), "bubbles", C(0, 168, 255), C(0, 255, 208), C(0, 80, 255)),
            T("Toxic", C(6, 14, 0), C(200, 255, 150), "bubbles", C(140, 255, 0), C(220, 255, 40), C(0, 200, 80)),
            T("Sunset", C(24, 6, 20), C(255, 230, 200), "stars", C(255, 94, 58), C(255, 190, 80), C(200, 40, 120)),
            T("Rainbow", C(8, 8, 12), w, "confetti", C(255, 40, 40), C(255, 150, 0), C(255, 235, 0), C(40, 220, 80), C(40, 140, 255), C(170, 70, 255)),
            T("Vaporwave", C(30, 10, 50), w, "stars", C(255, 113, 206), C(1, 205, 254), C(185, 103, 255), C(5, 255, 161)),
            T("Gold", C(14, 10, 2), C(255, 240, 190), "stars", C(255, 200, 60), C(255, 236, 150), C(190, 130, 20)),
            T("Aurora", C(2, 8, 20), C(210, 255, 240), "frost", C(40, 255, 170), C(0, 200, 255), C(150, 90, 255)),
            T("Lava", C(20, 2, 0), C(255, 200, 120), "embers", C(255, 60, 0), C(255, 140, 0), C(150, 10, 0)),
            T("Cyberpunk", C(10, 6, 20), w, "code", C(252, 238, 10), C(0, 240, 255), C(255, 0, 110)),
            T("Void", C(0, 0, 0), w, "stars", C(255, 255, 255), C(150, 150, 150), C(90, 90, 90)),
            T("Candy", C(30, 10, 30), w, "hearts", C(255, 120, 200), C(120, 220, 255), C(255, 240, 120)),
            T("Halloween", C(12, 4, 16), C(255, 200, 120), "embers", C(255, 120, 0), C(140, 40, 200), C(80, 220, 40)),
            T("Christmas", C(2, 14, 6), w, "snow", C(230, 30, 40), C(40, 200, 80), C(255, 215, 90)),
            T("Neon", C(4, 4, 10), w, "stars", C(57, 255, 20), C(255, 16, 240), C(0, 255, 255)),
            T("Love", C(26, 2, 12), C(255, 200, 220), "hearts", C(255, 60, 120), C(255, 150, 190), C(200, 20, 80)),
            T("Ice", C(2, 10, 22), w, "snow", C(150, 220, 255), C(255, 255, 255), C(80, 150, 255)),
        };
        list[0].NebulaPng = true;
        return list;
    }
}

class Config
{
    public string ThemeName = "Galaxy";
    public string ParticlesOverride = "";
    public int Fps = 30, BatteryFps = 15, AuraHeight = 18, EffectsHeight = 110, CycleMinutes = 10;
    public double StarDensity = 1.0, Speed = 1.0, GlowStrength = 1.0, LightningRate = 1.0, FireHeight = 1.0;
    public double ChaosLevel = 0.5, AudioSensitivity = 1.5;
    public bool Glow = true, Wire = true, Lightning = true, FireCurtain = false, Flames = false, Plasma = false, Sparks = false, Equalizer = false;
    public bool ShootingStars = true, Chaos = false, Meme = false, MusicReactive = false, AutoCycle = false, MemeImages = true;
    public HashSet<string> DisabledEvents = new HashSet<string>();
    public List<Theme> Custom = new List<Theme>();

    public List<Theme> Themes
    {
        get
        {
            var all = Theme.BuiltIn();
            foreach (var c in Custom) { all.RemoveAll(t => t.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase)); all.Add(c); }
            return all;
        }
    }

    public Theme Theme { get { return Find(ThemeName) ?? Themes[0]; } }

    public Theme Find(string name)
    {
        return Themes.FirstOrDefault(t => t.Name.Equals(name ?? "", StringComparison.OrdinalIgnoreCase));
    }

    public string ParticleType(Theme t)
    {
        return string.IsNullOrEmpty(ParticlesOverride) ? t.Particles : ParticlesOverride;
    }

    public bool CrazyOn { get { return Chaos || Meme; } }

    // ---------- presets ----------

    public static readonly string[] Presets = { "Chill", "Aura Max", "Inferno", "Rave", "Meme Lord", "Total Chaos", "Default" };

    public void ApplyPreset(string name)
    {
        var d = new Config();
        // effects only; theme choice and custom themes are kept
        Glow = d.Glow; Wire = d.Wire; Lightning = d.Lightning; FireCurtain = d.FireCurtain; Flames = d.Flames;
        Plasma = d.Plasma; Sparks = d.Sparks; Equalizer = d.Equalizer; Chaos = d.Chaos; Meme = d.Meme;
        MusicReactive = d.MusicReactive; GlowStrength = d.GlowStrength; AuraHeight = d.AuraHeight; LightningRate = d.LightningRate;
        FireHeight = d.FireHeight; ChaosLevel = d.ChaosLevel; StarDensity = d.StarDensity; Speed = d.Speed;
        switch (name)
        {
            case "Chill":
                Lightning = false; Wire = false; GlowStrength = 0.7; StarDensity = 0.6; Speed = 0.5; ShootingStars = true;
                break;
            case "Aura Max":
                GlowStrength = 2.0; AuraHeight = 30; LightningRate = 2.5; Plasma = true; Sparks = true; FireCurtain = true; StarDensity = 1.6;
                break;
            case "Inferno":
                FireCurtain = true; Flames = true; Sparks = true; FireHeight = 2.2; GlowStrength = 1.4; Lightning = false;
                if (!Theme.Name.Equals("Lava")) ThemeName = "Fire";
                break;
            case "Rave":
                MusicReactive = true; Equalizer = true; Plasma = true; LightningRate = 2; GlowStrength = 1.6; Chaos = true; ChaosLevel = 0.4;
                DisabledEvents.Clear();
                break;
            case "Meme Lord":
                Meme = true; ChaosLevel = 0.7; Sparks = true; GlowStrength = 1.3;
                break;
            case "Total Chaos":
                Chaos = true; Meme = true; ChaosLevel = 1.0; FireCurtain = true; Plasma = true; Sparks = true; Lightning = true;
                LightningRate = 3; GlowStrength = 1.8; StarDensity = 2.0; Speed = 1.6; MusicReactive = true; DisabledEvents.Clear();
                break;
        }
    }

    // ---------- load / save ----------

    public static Config Load(string path)
    {
        var cfg = new Config();
        if (!File.Exists(path)) { cfg.Save(path); return cfg; }
        try
        {
            var r = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
            if (r == null) return cfg;
            cfg.ThemeName = Str(r, "theme", cfg.ThemeName);
            cfg.ParticlesOverride = Str(r, "particles", cfg.ParticlesOverride);
            cfg.Fps = Int(r, "fps", cfg.Fps, 5, 60);
            cfg.BatteryFps = Int(r, "batteryFps", cfg.BatteryFps, 1, 60);
            cfg.AuraHeight = Int(r, "auraHeight", cfg.AuraHeight, 0, 60);
            cfg.EffectsHeight = Int(r, "effectsHeight", cfg.EffectsHeight, 30, 400);
            cfg.CycleMinutes = Int(r, "cycleMinutes", cfg.CycleMinutes, 1, 240);
            cfg.StarDensity = Dbl(r, "starDensity", cfg.StarDensity, 0, 4);
            cfg.Speed = Dbl(r, "speed", cfg.Speed, 0.1, 4);
            cfg.GlowStrength = Dbl(r, "glowStrength", cfg.GlowStrength, 0, 2);
            cfg.LightningRate = Dbl(r, "lightningRate", cfg.LightningRate, 0.1, 5);
            cfg.FireHeight = Dbl(r, "fireHeight", cfg.FireHeight, 0.2, 4);
            cfg.ChaosLevel = Dbl(r, "chaosLevel", cfg.ChaosLevel, 0, 1);
            cfg.AudioSensitivity = Dbl(r, "musicSensitivity", cfg.AudioSensitivity, 0.1, 6);
            cfg.Glow = Bool(r, "glow", cfg.Glow);
            cfg.Wire = Bool(r, "liveWire", cfg.Wire);
            cfg.Lightning = Bool(r, "lightning", cfg.Lightning);
            cfg.FireCurtain = Bool(r, "fireCurtain", cfg.FireCurtain);
            cfg.Flames = Bool(r, "flames", cfg.Flames);
            cfg.Plasma = Bool(r, "plasma", cfg.Plasma);
            cfg.Sparks = Bool(r, "sparks", cfg.Sparks);
            cfg.Equalizer = Bool(r, "equalizer", cfg.Equalizer);
            cfg.ShootingStars = Bool(r, "shootingStars", cfg.ShootingStars);
            cfg.Chaos = Bool(r, "chaosMode", cfg.Chaos);
            cfg.Meme = Bool(r, "memeMode", cfg.Meme);
            cfg.MusicReactive = Bool(r, "musicReactive", cfg.MusicReactive);
            cfg.AutoCycle = Bool(r, "autoCycleThemes", cfg.AutoCycle);
            cfg.MemeImages = Bool(r, "memeImages", cfg.MemeImages);
            object v;
            if (r.TryGetValue("disabledEvents", out v) && v is object[])
                foreach (var o in (object[])v) if (o is string) cfg.DisabledEvents.Add((string)o);
            if (r.TryGetValue("customThemes", out v) && v is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)v)
                {
                    var d = kv.Value as Dictionary<string, object>;
                    if (d == null) continue;
                    var t = new Theme { Name = kv.Key, Custom = true, Bg = Hex(d, "background", Color.Black), Star = Hex(d, "star", Color.White), Particles = Str(d, "particles", "stars").ToLowerInvariant() };
                    var cols = new List<Color>();
                    object arr;
                    if (d.TryGetValue("colors", out arr) && arr is object[])
                        foreach (var c in (object[])arr) { Color col; if (TryHex(c as string, out col)) cols.Add(col); }
                    while (cols.Count < 3) cols.Add(cols.Count > 0 ? cols[0] : Color.White);
                    t.Colors = cols.ToArray();
                    cfg.Custom.Add(t);
                }
        }
        catch { }
        return cfg;
    }

    public void Save(string path)
    {
        var custom = new List<KeyValuePair<string, object>>();
        foreach (var t in Custom)
            custom.Add(new KeyValuePair<string, object>(t.Name, new List<KeyValuePair<string, object>>
            {
                P("background", ToHex(t.Bg)), P("colors", t.Colors.Select(ToHex).ToList()), P("star", ToHex(t.Star)), P("particles", t.Particles)
            }));
        var root = new List<KeyValuePair<string, object>>
        {
            P("theme", ThemeName), P("particles", ParticlesOverride), P("speed", Speed), P("starDensity", StarDensity),
            P("shootingStars", ShootingStars), P("autoCycleThemes", AutoCycle), P("cycleMinutes", CycleMinutes),
            P("glow", Glow), P("glowStrength", GlowStrength), P("auraHeight", AuraHeight), P("liveWire", Wire),
            P("lightning", Lightning), P("lightningRate", LightningRate), P("fireCurtain", FireCurtain), P("flames", Flames),
            P("fireHeight", FireHeight), P("plasma", Plasma), P("sparks", Sparks), P("equalizer", Equalizer),
            P("chaosMode", Chaos), P("memeMode", Meme), P("memeImages", MemeImages), P("chaosLevel", ChaosLevel), P("effectsHeight", EffectsHeight),
            P("musicReactive", MusicReactive), P("musicSensitivity", AudioSensitivity), P("disabledEvents", DisabledEvents.ToList()),
            P("fps", Fps), P("batteryFps", BatteryFps), P("customThemes", custom)
        };
        var sb = new StringBuilder();
        Write(sb, root, 0);
        sb.AppendLine();
        File.WriteAllText(path, sb.ToString());
    }

    static KeyValuePair<string, object> P(string k, object v) { return new KeyValuePair<string, object>(k, v); }

    static void Write(StringBuilder sb, object v, int ind)
    {
        string pad = new string(' ', ind * 2), pad2 = new string(' ', ind * 2 + 2);
        if (v is List<KeyValuePair<string, object>>)
        {
            var items = (List<KeyValuePair<string, object>>)v;
            if (items.Count == 0) { sb.Append("{}"); return; }
            sb.Append("{\n");
            for (int i = 0; i < items.Count; i++)
            {
                sb.Append(pad2).Append(Quote(items[i].Key)).Append(": ");
                Write(sb, items[i].Value, ind + 1);
                sb.Append(i < items.Count - 1 ? ",\n" : "\n");
            }
            sb.Append(pad).Append("}");
        }
        else if (v is string) sb.Append(Quote((string)v));
        else if (v is bool) sb.Append((bool)v ? "true" : "false");
        else if (v is int) sb.Append(((int)v).ToString(CultureInfo.InvariantCulture));
        else if (v is double) sb.Append(Math.Round((double)v, 3).ToString("0.0##", CultureInfo.InvariantCulture));
        else if (v is IEnumerable)
        {
            var parts = new List<string>();
            foreach (var o in (IEnumerable)v) { var s = new StringBuilder(); Write(s, o, ind + 1); parts.Add(s.ToString()); }
            sb.Append("[").Append(string.Join(", ", parts)).Append("]");
        }
        else sb.Append("null");
    }

    static string Quote(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }

    static string Str(Dictionary<string, object> d, string k, string def) { object v; return d.TryGetValue(k, out v) && v is string ? (string)v : def; }
    static bool Bool(Dictionary<string, object> d, string k, bool def) { object v; return d.TryGetValue(k, out v) && v is bool ? (bool)v : def; }
    static int Int(Dictionary<string, object> d, string k, int def, int lo, int hi)
    {
        object v;
        try { return d.TryGetValue(k, out v) ? Math.Max(lo, Math.Min(hi, Convert.ToInt32(v, CultureInfo.InvariantCulture))) : def; } catch { return def; }
    }
    static double Dbl(Dictionary<string, object> d, string k, double def, double lo, double hi)
    {
        object v;
        try { return d.TryGetValue(k, out v) ? Math.Max(lo, Math.Min(hi, Convert.ToDouble(v, CultureInfo.InvariantCulture))) : def; } catch { return def; }
    }

    static Color Hex(Dictionary<string, object> d, string key, Color def)
    {
        object v; Color c;
        return d.TryGetValue(key, out v) && TryHex(v as string, out c) ? c : def;
    }

    public static string ToHex(Color c)
    {
        return c.A == 255 ? string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B) : string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
    }

    public static bool TryHex(string s, out Color c)
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
