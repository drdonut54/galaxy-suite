// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
// Real meme images for meme/crazy mode. Downloaded at runtime from Imgflip's public meme list and cached
// locally (%LOCALAPPDATA%\GalaxyTaskbar\memes). No images are shipped with the program.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;

static class MemeLibrary
{
    const string ListUrl = "https://api.imgflip.com/get_memes";
    const int Want = 40, MaxHeight = 110, MaxWidth = 180;

    static readonly object gate = new object();
    static List<Bitmap> images = new List<Bitmap>();
    static volatile bool busy;
    static bool started;

    public static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GalaxyTaskbar", "memes");
    public static volatile string Status = "Not loaded yet";

    public static int Count { get { lock (gate) return images.Count; } }

    public static Bitmap Pick(Random r)
    {
        lock (gate) return images.Count == 0 ? null : images[r.Next(images.Count)];
    }

    // Distinct picks, as many as are available.
    public static List<Bitmap> PickMany(Random r, int n)
    {
        lock (gate) return images.OrderBy(x => r.Next()).Take(n).ToList();
    }

    public static void EnsureLoaded()
    {
        if (started) return;
        started = true;
        Start(false);
    }

    public static void Refresh() { Start(true); }

    static void Start(bool force)
    {
        if (busy) return;
        busy = true;
        var th = new Thread(() => { try { Load(force); } catch (Exception ex) { Status = "Couldn't load memes: " + ex.Message; } finally { busy = false; } });
        th.IsBackground = true;
        th.Start();
    }

    static void Load(bool force)
    {
        Directory.CreateDirectory(CacheDir);
        string stamp = Path.Combine(CacheDir, "updated.txt");
        var cached = Directory.GetFiles(CacheDir, "*.png");

        if (!force && cached.Length > 0)
        {
            Publish(cached.Select(LoadFile).Where(b => b != null).ToList());
            Status = Count + " memes ready (cached)";
            bool fresh = File.Exists(stamp) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(stamp)).TotalDays < 7;
            if (fresh && cached.Length >= Want / 2) return;
        }

        Status = "Downloading memes from Imgflip...";
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        string json;
        using (var wc = new WebClient()) { wc.Headers["User-Agent"] = "GalaxyTaskbar"; json = wc.DownloadString(ListUrl); }
        var root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
        var data = root != null && root.ContainsKey("data") ? root["data"] as Dictionary<string, object> : null;
        var memes = data != null && data.ContainsKey("memes") ? data["memes"] as object[] : null;
        if (memes == null) throw new Exception("unexpected reply");

        var rng = new Random();
        var picks = memes.OfType<Dictionary<string, object>>().OrderBy(m => rng.Next()).Take(Want).ToList();
        var fresh2 = new List<Bitmap>();
        foreach (var m in picks)
        {
            try
            {
                string id = Convert.ToString(m["id"]), url = Convert.ToString(m["url"]);
                if (!url.StartsWith("https://i.imgflip.com/")) continue;     // only Imgflip's own image host
                string file = Path.Combine(CacheDir, Safe(id) + ".png");
                Bitmap thumb;
                if (File.Exists(file)) thumb = LoadFile(file);
                else
                {
                    byte[] bytes;
                    using (var wc = new WebClient()) { wc.Headers["User-Agent"] = "GalaxyTaskbar"; bytes = wc.DownloadData(url); }
                    using (var ms = new MemoryStream(bytes))
                    using (var img = Image.FromStream(ms))
                        thumb = Thumb(img);
                    thumb.Save(file, ImageFormat.Png);
                }
                if (thumb != null) fresh2.Add(thumb);
                Status = "Downloading memes... " + fresh2.Count + "/" + picks.Count;
            }
            catch { }
        }
        if (fresh2.Count > 0) Publish(fresh2);
        File.WriteAllText(stamp, DateTime.UtcNow.ToString("o"));
        Status = Count + " memes ready (from Imgflip)";
    }

    static void Publish(List<Bitmap> list)
    {
        lock (gate) images = list;     // old bitmaps stay alive for any frame still drawing them; GC collects them
    }

    static string Safe(string s) { return new string(s.Where(char.IsLetterOrDigit).ToArray()); }

    static Bitmap LoadFile(string f)
    {
        try { using (var img = Image.FromFile(f)) return Thumb(img); } catch { return null; }
    }

    // Small premultiplied copy, sized for the taskbar.
    static Bitmap Thumb(Image img)
    {
        float s = Math.Min(1f, Math.Min(MaxHeight / (float)img.Height, MaxWidth / (float)img.Width));
        int w = Math.Max(1, (int)(img.Width * s)), h = Math.Max(1, (int)(img.Height * s));
        var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(b))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, 0, 0, w, h);
        }
        return b;
    }
}
