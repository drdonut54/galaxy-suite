// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
// Galaxy Trainer: a memory scanner / trainer for SINGLE-PLAYER games (desktop or browser).
// Finds values like health and money by scanning the game's memory, then lets you set or freeze them.
// Deliberately refuses online / anti-cheat protected games.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

static class Native
{
    public const int PROCESS_ACCESS = 0x0010 | 0x0020 | 0x0008 | 0x0400; // VM_READ | VM_WRITE | VM_OPERATION | QUERY_INFORMATION

    [StructLayout(LayoutKind.Sequential)]
    public struct MBI
    {
        public IntPtr BaseAddress, AllocationBase;
        public int AllocationProtect, Align1;
        public IntPtr RegionSize;
        public int State, Protect, Type, Align2;
    }

    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);
    [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr written);
    [DllImport("kernel32.dll")] public static extern IntPtr VirtualQueryEx(IntPtr h, IntPtr addr, out MBI info, IntPtr len);
    [DllImport("kernel32.dll")] public static extern bool GetExitCodeProcess(IntPtr h, out int code);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, int mods, int vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
}

enum Cmp { Exact, Unknown, Increased, Decreased, Changed, Unchanged, IncBy, DecBy, Plausible }

static class Values
{
    public static readonly string[] TypeNames = { "Int32", "Float", "Double", "JS Int" };

    public static bool IsInt(int k) { return k == 0 || k == 3; }
    public static int Width(int k) { return k == 2 ? 8 : 4; }

    // Decode a value; ok=false filters out garbage (NaN, absurd floats, odd JS ints).
    public static double Get(byte[] b, int off, int k, out bool ok)
    {
        ok = true;
        if (off + Width(k) > b.Length) { ok = false; return 0; }
        switch (k)
        {
            case 0: return BitConverter.ToInt32(b, off);
            case 1:
                {
                    float f = BitConverter.ToSingle(b, off);
                    float a = Math.Abs(f);
                    if (float.IsNaN(f) || float.IsInfinity(f) || (a != 0 && (a < 1e-4f || a > 1e9f))) { ok = false; return 0; }
                    return f;
                }
            case 2:
                {
                    double d = BitConverter.ToDouble(b, off);
                    double a = Math.Abs(d);
                    if (double.IsNaN(d) || double.IsInfinity(d) || (a != 0 && (a < 1e-4 || a > 1e12))) { ok = false; return 0; }
                    return d;
                }
            default:
                {
                    // V8 small integer: stored shifted left by one, low bit clear
                    int r = BitConverter.ToInt32(b, off);
                    if ((r & 1) != 0) { ok = false; return 0; }
                    return r >> 1;
                }
        }
    }

    public static byte[] Encode(int k, double v)
    {
        switch (k)
        {
            case 0: return BitConverter.GetBytes((int)Math.Round(v));
            case 1: return BitConverter.GetBytes((float)v);
            case 2: return BitConverter.GetBytes(v);
            default: return BitConverter.GetBytes((int)Math.Round(v) * 2);
        }
    }

    public static bool Eq(double x, double v, int k)
    {
        bool whole = v == Math.Floor(v);
        if (IsInt(k)) return whole && x == v;
        return Math.Abs(x - v) < (whole ? 0.5 : 0.01);
    }

    public static bool Test(Cmp c, double x, double p, bool okp, double v, int k)
    {
        switch (c)
        {
            case Cmp.Exact: return Eq(x, v, k);
            case Cmp.Unknown: return true;
            case Cmp.Plausible: return InPool(x, k);
            case Cmp.Increased: return okp && x > p;
            case Cmp.Decreased: return okp && x < p;
            case Cmp.Changed: return okp && x != p;
            case Cmp.Unchanged: return okp && x == p;
            case Cmp.IncBy: return okp && Eq(x - p, v, k);
            case Cmp.DecBy: return okp && Eq(p - x, v, k);
        }
        return false;
    }

    // Range a fresh stat (health, money, ammo) plausibly starts in. Drops zeros, flags and pointers.
    public static bool InPool(double x, int k)
    {
        return IsInt(k) ? x >= 2 && x <= 10000000 : x >= 0.01 && x <= 10000000;
    }

    // Range a tracked stat may move within (health can hit 0).
    public static bool InTracked(double x, int k)
    {
        return x >= 0 && x <= 100000000;
    }

    // A real stat change, not float jitter from physics/animation.
    public static bool SensibleChange(double p, double x, int k)
    {
        if (IsInt(k)) return true;
        return Math.Abs(x - p) >= 0.001 * Math.Max(1.0, Math.Abs(p));
    }

    public static bool TryRead(IntPtr h, long addr, int k, out double v)
    {
        var buf = new byte[8];
        IntPtr rd;
        v = 0;
        if (!Native.ReadProcessMemory(h, (IntPtr)addr, buf, (IntPtr)Width(k), out rd) || (long)rd != Width(k)) return false;
        bool ok;
        v = Get(buf, 0, k, out ok);
        if (!ok && k != 3) { v = k == 1 ? BitConverter.ToSingle(buf, 0) : BitConverter.ToDouble(buf, 0); }
        return true;
    }

    public static bool Write(IntPtr h, long addr, int k, double v)
    {
        var b = Encode(k, v);
        IntPtr wr;
        return Native.WriteProcessMemory(h, (IntPtr)addr, b, (IntPtr)b.Length, out wr);
    }

    public static string Fmt(double v, int k)
    {
        return IsInt(k) ? ((long)v).ToString("N0") : v.ToString("0.###");
    }
}

class Chunk
{
    public IntPtr H;
    public long Base;
    public int Size;
    public long FileOff;
    public ulong[][] Mask = new ulong[4][];
    public bool[] All = new bool[4];
    public long[] Count = new long[4];
    public byte[] Prev;          // previous values when the snapshot fits in RAM
}

class Change { public IntPtr H; public long Addr; public int Type; public double Prev, Cur; }

class Hit { public IntPtr H; public long Addr; public int Type; }

// Scans memory in 16 MB chunks. Candidates are kept as per-chunk bitmasks (one bit per 4-byte slot).
// Previous values stay in RAM when they fit, otherwise in a temp snapshot file.
class Scanner
{
    const int CHUNK = 16 << 20;
    const long RAM_LIMIT = 1536L << 20;
    public List<Chunk> Chunks = new List<Chunk>();
    public bool[] Types = new bool[4];
    public bool Started;
    bool useRam;
    string snapPath;

    public long Total
    {
        get { long t = 0; foreach (var c in Chunks) for (int k = 0; k < 4; k++) t += c.Count[k]; return t; }
    }

    public void Reset()
    {
        Chunks = new List<Chunk>();
        Started = false;
        if (snapPath != null) { try { File.Delete(snapPath); } catch { } }
        snapPath = null;
    }

    static List<Chunk> Enumerate(List<IntPtr> handles)
    {
        var list = new List<Chunk>();
        int sz = Marshal.SizeOf(typeof(Native.MBI));
        foreach (var h in handles)
        {
            long addr = 0;
            Native.MBI m;
            while (addr < 0x7FFFFFFF0000L && Native.VirtualQueryEx(h, (IntPtr)addr, out m, (IntPtr)sz) != IntPtr.Zero)
            {
                long b = (long)m.BaseAddress, s = (long)m.RegionSize;
                if (s <= 0) break;
                bool writable = (m.Protect & (0x04 | 0x08 | 0x40 | 0x80)) != 0 && (m.Protect & 0x100) == 0;
                // committed, writable, private or image memory (skip mapped files)
                if (m.State == 0x1000 && writable && m.Type != 0x40000)
                    for (long o = 0; o < s; o += CHUNK)
                        list.Add(new Chunk { H = h, Base = b + o, Size = (int)Math.Min(CHUNK, s - o) });
                addr = b + s;
            }
        }
        return list;
    }

    FileStream OpenSnap()
    {
        return useRam ? null : new FileStream(snapPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
    }

    void ForEachChunk(Action<Chunk, FileStream> body, Action<int> progress)
    {
        int done = 0, n = Math.Max(1, Chunks.Count);
        Parallel.ForEach(Chunks, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            () => OpenSnap(),
            (c, st, fs) =>
            {
                body(c, fs);
                int d = Interlocked.Increment(ref done);
                if (progress != null && d % 32 == 0) progress(d * 100 / n);
                return fs;
            },
            fs => { if (fs != null) fs.Dispose(); });
        if (progress != null) progress(100);
    }

    byte[] GetPrev(Chunk c, FileStream fs)
    {
        if (useRam) return c.Prev;
        var prev = new byte[c.Size];
        fs.Seek(c.FileOff, SeekOrigin.Begin);
        int got = 0;
        while (got < prev.Length) { int r = fs.Read(prev, got, prev.Length - got); if (r <= 0) break; got += r; }
        return prev;
    }

    void PutPrev(Chunk c, FileStream fs, byte[] cur, bool keep)
    {
        if (useRam) { c.Prev = keep ? cur : null; return; }
        if (!keep) return;
        fs.Seek(c.FileOff, SeekOrigin.Begin);
        fs.Write(cur, 0, c.Size);
    }

    static byte[] ReadChunk(Chunk c)
    {
        var cur = new byte[c.Size];
        IntPtr rd;
        if (!Native.ReadProcessMemory(c.H, (IntPtr)c.Base, cur, (IntPtr)c.Size, out rd) || (long)rd != c.Size) return null;
        return cur;
    }

    static void Clear(Chunk c)
    {
        for (int k = 0; k < 4; k++) { c.Count[k] = 0; c.Mask[k] = null; c.All[k] = false; }
        c.Prev = null;
    }

    public void Run(List<IntPtr> handles, Cmp cmp, double v, bool[] types, Action<int> progress)
    {
        bool first = !Started;
        if (first)
        {
            Types = (bool[])types.Clone();
            Chunks = Enumerate(handles);
            long off = 0;
            foreach (var c in Chunks) { c.FileOff = off; off += c.Size; }
            useRam = off <= RAM_LIMIT;
            if (!useRam)
            {
                snapPath = Path.Combine(Path.GetTempPath(), "GalaxyTrainer_" + Guid.NewGuid().ToString("N") + ".snap");
                using (var fs = new FileStream(snapPath, FileMode.Create)) fs.SetLength(off);
            }
        }
        ForEachChunk((c, fs) => ScanChunk(c, fs, first, cmp, v), progress);
        Started = true;
    }

    void ScanChunk(Chunk c, FileStream fs, bool first, Cmp cmp, double v)
    {
        if (!first && c.Count.All(x => x == 0)) return;
        var cur = ReadChunk(c);
        if (cur == null) { Clear(c); return; }
        byte[] prev = first ? null : GetPrev(c, fs);

        int slots = c.Size / 4;
        int words = (slots + 63) / 64;
        for (int k = 0; k < 4; k++)
        {
            if (!Types[k]) continue;
            if (first)
            {
                if (cmp == Cmp.Unknown && k == 0) { c.All[0] = true; c.Count[0] = slots; continue; }
                var m = new ulong[words];
                long cnt = 0;
                for (int i = 0; i < slots; i++)
                {
                    bool ok;
                    double x = Values.Get(cur, i * 4, k, out ok);
                    if (!ok) continue;
                    if (Values.Test(cmp, x, 0, false, v, k)) { m[i >> 6] |= 1UL << (i & 63); cnt++; }
                }
                c.Mask[k] = cnt > 0 ? m : null;
                c.Count[k] = cnt;
            }
            else
            {
                if (c.Count[k] == 0) continue;
                var old = c.Mask[k];
                bool all = c.All[k];
                var m = new ulong[words];
                long cnt = 0;
                for (int w = 0; w < words; w++)
                {
                    ulong bits = all ? ulong.MaxValue : old[w];
                    if (bits == 0) continue;
                    for (int b = 0; b < 64; b++)
                    {
                        if ((bits & (1UL << b)) == 0) continue;
                        int i = w * 64 + b;
                        if (i >= slots) break;
                        bool ok1, ok2;
                        double x = Values.Get(cur, i * 4, k, out ok1);
                        if (!ok1) continue;
                        double p = Values.Get(prev, i * 4, k, out ok2);
                        if (Values.Test(cmp, x, p, ok2, v, k)) { m[w] |= 1UL << b; cnt++; }
                    }
                }
                c.Mask[k] = cnt > 0 ? m : null;
                c.All[k] = false;
                c.Count[k] = cnt;
            }
        }
        PutPrev(c, fs, cur, c.Count.Any(x => x > 0));
    }

    // Smart-detector pass over the pool of never-changed values. Anything that leaves the plausible
    // range is dropped. Anything that changed leaves the pool; real-looking changes are reported so
    // the detector can follow them individually. Remaining candidates are unchanged, so the snapshot
    // stays valid without rewriting it.
    public void AutoPass(ConcurrentBag<Change> changes, bool calibrating)
    {
        ForEachChunk((c, fs) =>
        {
            if (c.Count.All(x => x == 0)) return;
            var cur = ReadChunk(c);
            if (cur == null) { Clear(c); return; }
            var prev = GetPrev(c, fs);
            int slots = c.Size / 4;
            for (int k = 0; k < 4; k++)
            {
                if (c.Count[k] == 0) continue;
                var m = c.Mask[k];
                long cnt = c.Count[k];
                for (int w = 0; w < m.Length; w++)
                {
                    ulong bits = m[w];
                    if (bits == 0) continue;
                    for (int b = 0; b < 64; b++)
                    {
                        if ((bits & (1UL << b)) == 0) continue;
                        int i = w * 64 + b;
                        if (i >= slots) break;
                        bool ok1, ok2;
                        double x = Values.Get(cur, i * 4, k, out ok1);
                        double p = Values.Get(prev, i * 4, k, out ok2);
                        bool drop;
                        if (!ok1 || !ok2) drop = true;
                        else if (x == p) drop = false;
                        else
                        {
                            drop = true;
                            if (!calibrating && Values.InTracked(x, k) && Values.SensibleChange(p, x, k))
                                changes.Add(new Change { H = c.H, Addr = c.Base + (long)i * 4, Type = k, Prev = p, Cur = x });
                        }
                        if (drop) { m[w] &= ~(1UL << b); cnt--; }
                    }
                }
                c.Count[k] = cnt;
                if (cnt == 0) c.Mask[k] = null;
            }
            if (useRam && c.Count.All(x => x == 0)) c.Prev = null;
        }, null);
    }

    public List<Hit> Hits(int max)
    {
        var r = new List<Hit>();
        foreach (var c in Chunks)
        {
            int slots = c.Size / 4;
            for (int k = 0; k < 4; k++)
            {
                if (c.Count[k] == 0) continue;
                for (int i = 0; i < slots; i++)
                {
                    bool set = c.All[k] || (c.Mask[k][i >> 6] & (1UL << (i & 63))) != 0;
                    if (!set) continue;
                    r.Add(new Hit { H = c.H, Addr = c.Base + (long)i * 4, Type = k });
                    if (r.Count >= max) return r;
                }
            }
        }
        return r;
    }
}

// ---------- Smart auto-detector ----------
// Process of elimination: values that change constantly, jitter, or leave sane ranges are thrown out.
// Values that stay quiet and then change in a stat-like way are followed and scored.

class Track
{
    public IntPtr H;
    public long Addr;
    public int Type;
    public double First, Cur, Max, Min;
    public int Changes, Drops, Rises, MinusOnes;
    public bool Refilled, Exceeded, Dead, WipedOut;
    public bool? Twin;
    public int TwinStreak, TwinOff = -1;
    public bool TwinBroken;
    public int BigDrops;
    public double FirstSeen = -1, LastSeen;
    public readonly HashSet<double> DropSizes = new HashSet<double>();
    public double TwinCheckedAt = -99;
    public readonly List<double> Recent = new List<double>();
    public string Guess = "";
    public int Score;

    public void Update(double x, double t)
    {
        if (FirstSeen < 0) FirstSeen = t;
        LastSeen = t;
        if (x == Cur) return;
        if (!Values.InTracked(x, Type) || !Values.SensibleChange(Cur, x, Type)) { Dead = true; return; }
        double d = x - Cur;
        Changes++;
        Recent.Add(t);
        Recent.RemoveAll(r => r < t - 4);
        if (Recent.Count > 3) { Dead = true; return; }      // changes constantly: timer, position, animation
        if (d < 0)
        {
            Drops++;
            if (Values.IsInt(Type) && d == -1) MinusOnes++;
            DropSizes.Add(Math.Round(-d, 2));
            if (-d > Max * 0.6) BigDrops++;               // one hit taking most of the bar is rare; a rewrite isn't
            if (x == 0 && Cur >= Max) WipedOut = true;      // full -> 0 in one step: a reset/flag, not damage
        }
        else
        {
            Rises++;
            if (Max > 0 && x > Max * 1.01) Exceeded = true;
            if (Drops > 0 && x >= Max * 0.99) Refilled = true;
        }
        Cur = x;
        Max = Math.Max(Max, x);
        Min = Math.Min(Min, x);
    }

    public void Classify()
    {
        bool isInt = Values.IsInt(Type);
        // a stable neighbour holding the starting/max value, seen on at least two separate checks
        bool twin = TwinStreak >= 3 && !TwinBroken && Max >= 1;
        bool startedFull = First >= Max * 0.999;

        // ---- health: needs a max-health partner, several hits, and a sane range
        int health = 0;
        bool healthRange = isInt ? Max >= 10 && Max <= 100000 : (Max >= 1 && Max <= 100000);
        if (twin && healthRange && !Exceeded && !WipedOut && BigDrops <= 1)
        {
            health = 30;                                   // twin
            if (Drops >= 2) health += 25;
            if (Drops >= 3) health += 10;
            if (DropSizes.Count >= 2) health += 10;          // hits of different sizes
            if (Rises > 0 && Rises <= Drops) health += 10;   // healed / regenerated
            if (startedFull) health += 5;
            if (!isInt) health += 5;
            if (isInt && MinusOnes == Drops) health -= 40;   // only ever -1: that's a counter / ammo
            if (Drops < 2) health = Math.Min(health, 55);
            // designers pick round max values (100, 150, 250.0); background noise doesn't
            bool round = isInt ? (Max % 5 == 0 || Max <= 20) : (Max == Math.Floor(Max) || Max == 1);
            if (!round) health = Math.Min(health, 45);
            // needs a few hits over a real stretch of play before we're sure
            if (Drops < 3 || LastSeen - FirstSeen < 20) health = Math.Min(health, 75);
        }
        else if (Drops >= 1 && healthRange && !WipedOut) health = 25;   // "maybe", never auto-added

        // ---- ammo: drops one at a time, refills to its max
        int ammo = 0;
        if (isInt && Max <= 9999 && MinusOnes >= 3 && MinusOnes * 2 >= Drops && !WipedOut)
            ammo = 40 + (MinusOnes >= 6 ? 10 : 0) + (Refilled ? 20 : 0) + (twin ? 20 : 0);

        // ---- money: moves both ways in chunks, has no max, goes above its starting value
        int money = 0;
        if (Max >= 10 && Max <= 1000000000 && !twin && !WipedOut)
        {
            money = 15 + 5 * Math.Min(Changes, 5);
            if (Rises > 0 && Drops > 0) money += 15;
            if (Exceeded) money += 15;
            if (MinusOnes > Changes / 2) money -= 30;
            if (isInt || Type == 2) money += 5;
            if (Changes < 3) money = Math.Min(money, 50);
        }

        Score = health; Guess = "Health";
        if (ammo > Score) { Score = ammo; Guess = "Ammo"; }
        if (money > Score) { Score = money; Guess = "Money"; }
        Score = Math.Max(0, Math.Min(100, Score));
    }

    // Games usually store "max health" / "max ammo" right next to the current value.
    // Once a partner is found it must stay at the same spot and keep holding the max value.
    // A real max-health never changes; a coincidental neighbour eventually does.
    public void CheckTwin(double t)
    {
        if (TwinBroken || t - TwinCheckedAt < 2) return;
        TwinCheckedAt = t;
        const int R = 128;                                   // max value lives in the same struct
        var buf = new byte[R * 2];
        IntPtr rd;
        if (!Native.ReadProcessMemory(H, (IntPtr)(Addr - R), buf, (IntPtr)buf.Length, out rd) || (long)rd != buf.Length) { TwinBroken = true; return; }
        bool ok;
        if (TwinOff >= 0)
        {
            double v = Values.Get(buf, TwinOff, Type, out ok);
            if (ok && v == Max) TwinStreak++;
            else { TwinBroken = true; TwinStreak = 0; }
        }
        else
        {
            for (int off = 0; off + Values.Width(Type) <= buf.Length; off += 4)
            {
                if (off == R) continue;
                double v = Values.Get(buf, off, Type, out ok);
                if (ok && v == Max && v >= 1) { TwinOff = off; TwinStreak = 1; break; }
            }
        }
        Twin = TwinStreak > 0 && !TwinBroken;
    }
}

class AutoDetector
{
    const int MAX_TRACKED = 50000;
    readonly Scanner scanner;
    readonly List<IntPtr> handles;
    readonly List<Track> tracked = new List<Track>();
    readonly HashSet<long> seen = new HashSet<long>();
    readonly HashSet<long> reported = new HashSet<long>();
    readonly Dictionary<string, int> bestScore = new Dictionary<string, int>();
    readonly Dictionary<string, int> addedPerType = new Dictionary<string, int>();
    volatile bool running;
    public Action<string, List<Track>> OnUpdate;
    public Action<List<Track>> OnFound;

    public AutoDetector(Scanner s, List<IntPtr> h) { scanner = s; handles = h; }

    public bool Running { get { return running; } }
    public void Stop() { running = false; }

    public void Start(bool[] types)
    {
        running = true;
        Task.Factory.StartNew(() =>
        {
            try { Loop(types); }
            catch (Exception ex) { running = false; Report("Auto-detect stopped: " + ex.Message, null); }
        }, TaskCreationOptions.LongRunning);
    }

    void Loop(bool[] types)
    {
        var clock = Stopwatch.StartNew();
        Report("Reading game memory...", null);
        scanner.Run(handles, Cmp.Plausible, 0, types, null);
        double calibrateUntil = clock.Elapsed.TotalSeconds + 5;

        while (running)
        {
            double t0 = clock.Elapsed.TotalSeconds;
            bool calibrating = t0 < calibrateUntil;
            var bag = new ConcurrentBag<Change>();
            scanner.AutoPass(bag, calibrating);
            double t = clock.Elapsed.TotalSeconds;

            // hundreds of values changing at once = loading / memory being moved, not a stat
            bool burst = bag.Count > 300;
            foreach (var ch in burst ? new ConcurrentBag<Change>() : bag)
            {
                if (tracked.Count >= MAX_TRACKED) break;
                if (!seen.Add(ch.Addr * 4 + ch.Type)) continue;
                var tr = new Track { H = ch.H, Addr = ch.Addr, Type = ch.Type, First = ch.Prev, Cur = ch.Prev, Max = ch.Prev, Min = ch.Prev };
                tr.Update(ch.Cur, t);
                tracked.Add(tr);
            }

            foreach (var tr in tracked)
            {
                if (tr.Dead) continue;
                double v;
                if (!Values.TryRead(tr.H, tr.Addr, tr.Type, out v)) { tr.Dead = true; continue; }
                if (burst && v != tr.Cur) { tr.Dead = true; continue; }
                tr.Update(v, t);
                if (tr.Dead) continue;
                if (tr.Drops > 0 || tr.MinusOnes > 0) tr.CheckTwin(t);
                tr.Classify();
            }
            tracked.RemoveAll(x => x.Dead);

            var top = tracked.Where(x => x.Changes > 0).OrderByDescending(x => x.Score).ThenBy(x => x.Changes).Take(300).ToList();
            string msg = calibrating
                ? "Calibrating: removing values that change on their own... (" + scanner.Total.ToString("N0") + " left)"
                : "Watching " + scanner.Total.ToString("N0") + " quiet values, following " + tracked.Count.ToString("N0") + " that changed. Just play!";
            Report(msg, top);

            var found = new List<Track>();
            foreach (var g in top.Where(x => x.Score >= 80 && x.Changes >= 3).GroupBy(x => x.Guess))
            {
                var best = g.First();
                int prev;
                bool have = bestScore.TryGetValue(g.Key, out prev);
                if (have && best.Score < prev + 10) continue;          // only replace with something clearly better
                if (reported.Contains(best.Addr * 4 + best.Type)) continue;
                int added;
                addedPerType.TryGetValue(g.Key, out added);
                if (added >= 3) continue;
                bestScore[g.Key] = best.Score;
                addedPerType[g.Key] = added + 1;
                reported.Add(best.Addr * 4 + best.Type);
                found.Add(best);
            }
            if (found.Count > 0 && OnFound != null) OnFound(found);

            int wait = 350 - (int)((clock.Elapsed.TotalSeconds - t0) * 1000);
            if (wait > 0) Thread.Sleep(wait);
        }
        scanner.Reset();
    }

    void Report(string msg, List<Track> top)
    {
        if (OnUpdate != null) OnUpdate(msg, top ?? new List<Track>());
    }
}

class Target
{
    public string Exe, Title, Dir;
    public List<int> Pids = new List<int>();
    public bool Browser;
    public override string ToString()
    {
        return (Browser ? "[Browser] " : "") + Title + "  -  " + Exe + ".exe" + (Browser ? "  (" + Pids.Count + " tabs/processes)" : "  (pid " + Pids[0] + ")");
    }
}

static class Guard
{
    // Online games / anti-cheat: editing these hurts other players and gets accounts banned.
    static readonly string[] Blocked = {
        "robloxplayerbeta", "windows10universal", "fortniteclient-win64-shipping", "valorant-win64-shipping", "cs2", "csgo",
        "r5apex", "r5apex_dx12", "gta5", "gta5_enhanced", "playgtav", "rainbowsix", "rainbowsix_be", "destiny2", "overwatch",
        "league of legends", "leagueclient", "dota2", "rocketleague", "tslgame", "pubg", "cod", "modernwarfare", "blackopscoldwar",
        "fivem", "rustclient", "escapefromtarkov", "deadbydaylight-win64-shipping", "fallguys_client", "warframe.x64", "wow",
        "ffxiv_dx11", "pathofexile", "pathofexile_x64", "lostark", "genshinimpact", "starrail", "zenlesszonezero",
        "marvel-win64-shipping", "discovery", "bf2042", "bfv", "bf1", "haloinfinite", "xdefiant", "eldenring", "nightreign",
        "minecraft.windows", "brawlhalla", "paladins", "smite", "splitgate", "naraka", "thefinals", "deltaforce", "arc raiders",
        "pioneergame", "battlebit", "hunt", "huntgame", "seaofthieves", "newworld", "throne and liberty", "tl", "palworld-win64-shipping-cmd"
    };

    static readonly string[] AntiCheatMarkers = {
        "EasyAntiCheat", "BattlEye", "EasyAntiCheat_EOS_Setup.exe", "start_protected_game.exe", "BEService.exe", "vgc.exe", "mhyprot2.sys", "ACE-Base.sys"
    };

    public static string WhyBlocked(Target t)
    {
        if (t.Browser) return null;
        if (Blocked.Contains(t.Exe.ToLowerInvariant())) return t.Exe + " is an online game.";
        if (string.IsNullOrEmpty(t.Dir)) return null;
        var dirs = new List<string> { t.Dir };
        var parent = Directory.GetParent(t.Dir);
        if (parent != null) { dirs.Add(parent.FullName); if (parent.Parent != null) dirs.Add(parent.Parent.FullName); }
        foreach (var d in dirs)
            foreach (var mk in AntiCheatMarkers)
                try { if (Directory.Exists(Path.Combine(d, mk)) || File.Exists(Path.Combine(d, mk))) return "This game ships with anti-cheat (" + mk + ")."; }
                catch { }
        return null;
    }
}

class Hud : Form
{
    readonly Label lbl = new Label();
    public Hud()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(12, 12);
        Size = new Size(420, 34);
        BackColor = Color.FromArgb(24, 8, 52);
        Opacity = 0.88;
        lbl.Dock = DockStyle.Fill;
        lbl.ForeColor = Color.FromArgb(200, 235, 255);
        lbl.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        lbl.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(lbl);
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x8000000 | 0x20 | 0x8; return cp; }
    }
    public void Say(string s) { lbl.Text = s; if (!Visible) Show(); }
}

class Entry
{
    public string Desc;
    public IntPtr H;
    public long Addr;
    public int Type;
    public bool Frozen;
    public double FrozenVal;
}

class MainForm : Form
{
    static readonly Color Bg = Color.FromArgb(14, 8, 30), Panel2 = Color.FromArgb(26, 14, 52), Fg = Color.FromArgb(236, 228, 255),
        Accent = Color.FromArgb(166, 108, 245), Cyan = Color.FromArgb(79, 195, 255), Pink = Color.FromArgb(232, 63, 191);
    static readonly string[] BrowserExes = { "chrome", "msedge", "brave", "opera", "vivaldi", "firefox" };

    readonly ComboBox procBox = new ComboBox();
    readonly Label status = new Label();
    readonly TextBox valueBox = new TextBox();
    readonly CheckBox[] typeChecks = new CheckBox[4];
    readonly Label countLbl = new Label();
    readonly Label guide = new Label();
    readonly ProgressBar bar = new ProgressBar();
    readonly ListView results = new ListView();
    readonly ListView table = new ListView();
    readonly List<Button> scanButtons = new List<Button>();
    readonly System.Windows.Forms.Timer freezeTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer refreshTimer = new System.Windows.Forms.Timer();
    readonly Hud hud = new Hud();

    Target target;
    readonly List<IntPtr> handles = new List<IntPtr>();
    readonly Scanner scanner = new Scanner();
    readonly List<Entry> entries = new List<Entry>();
    List<Hit> shownHits = new List<Hit>();
    bool busy, suppressCheck, hotkeysOn;
    AutoDetector auto;
    Button autoBtn;
    int candidateNo;

    public MainForm()
    {
        Text = "Galaxy Trainer - single-player memory editor";
        Size = new Size(1180, 760);
        MinimumSize = new Size(900, 560);
        BackColor = Bg;
        ForeColor = Fg;
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;

        BuildTop();
        BuildRight();
        BuildLeft();

        freezeTimer.Interval = 100;
        freezeTimer.Tick += (s, e) => FreezeTick();
        freezeTimer.Start();
        refreshTimer.Interval = 500;
        refreshTimer.Tick += (s, e) => RefreshTick();
        refreshTimer.Start();

        FormClosing += (s, e) => { Detach(); scanner.Reset(); };
        Shown += (s, e) => LoadProcesses();
    }

    // ---------- UI construction ----------

    Button Btn(string text, EventHandler click, Color? color = null, int width = 130)
    {
        var b = new Button
        {
            Text = text, Width = width, Height = 30, FlatStyle = FlatStyle.Flat,
            BackColor = Panel2, ForeColor = Fg, Margin = new Padding(3), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderColor = color ?? Accent;
        b.Click += click;
        return b;
    }

    Label Head(string text)
    {
        return new Label { Text = text, AutoSize = true, ForeColor = Cyan, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Margin = new Padding(3, 12, 3, 4) };
    }

    void BuildTop()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8, 8, 8, 0), BackColor = Panel2, WrapContents = false };
        procBox.Width = 520;
        procBox.DropDownStyle = ComboBoxStyle.DropDownList;
        procBox.BackColor = Bg;
        procBox.ForeColor = Fg;
        procBox.FlatStyle = FlatStyle.Flat;
        top.Controls.Add(procBox);
        top.Controls.Add(Btn("Refresh list", (s, e) => LoadProcesses(), null, 100));
        top.Controls.Add(Btn("Attach", (s, e) => Attach(), Pink, 90));
        status.AutoSize = true;
        status.Margin = new Padding(10, 7, 3, 3);
        status.ForeColor = Color.FromArgb(185, 169, 217);
        status.Text = "Pick a single-player game and click Attach.";
        top.Controls.Add(status);
        Controls.Add(top);
    }

    void BuildLeft()
    {
        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = 300, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true, Padding = new Padding(8), BackColor = Bg
        };

        left.Controls.Add(Head("Smart auto-detect"));
        autoBtn = Btn("Start smart auto-detect (just play)", (s, e) => ToggleAuto(), Cyan, 270);
        autoBtn.Height = 40;
        autoBtn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        left.Controls.Add(autoBtn);

        left.Controls.Add(Head("Auto-find wizards"));
        left.Controls.Add(Btn("Find money / ammo / count", (s, e) => WizardMoney(), Color.Gold, 270));
        left.Controls.Add(Btn("Find health / bars", (s, e) => WizardHealth(), Pink, 270));
        guide.MaximumSize = new Size(270, 0);
        guide.AutoSize = true;
        guide.ForeColor = Color.FromArgb(200, 235, 255);
        guide.Margin = new Padding(3, 6, 3, 6);
        guide.Text = "Attach to a game, then hit Smart auto-detect and just play - it finds health, ammo and money by itself.\n\nOr use a wizard.\n\nHotkeys work while you're in the game:\n  Ctrl+Alt+D  value went down\n  Ctrl+Alt+I  value went up\n  Ctrl+Alt+U  value didn't change\n  Ctrl+Alt+C  value changed";
        left.Controls.Add(guide);

        left.Controls.Add(Head("Manual scan"));
        var vl = new Label { Text = "Value:", AutoSize = true, Margin = new Padding(3, 4, 3, 0) };
        left.Controls.Add(vl);
        valueBox.Width = 270;
        valueBox.BackColor = Panel2;
        valueBox.ForeColor = Fg;
        valueBox.BorderStyle = BorderStyle.FixedSingle;
        left.Controls.Add(valueBox);

        var types = new FlowLayoutPanel { Width = 280, Height = 54, Margin = new Padding(0) };
        for (int k = 0; k < 4; k++)
        {
            typeChecks[k] = new CheckBox { Text = Values.TypeNames[k], AutoSize = true, Checked = k < 2, ForeColor = Fg };
            types.Controls.Add(typeChecks[k]);
        }
        left.Controls.Add(types);

        var grid = new FlowLayoutPanel { Width = 285, Height = 190, Margin = new Padding(0) };
        Action<string, Cmp, bool> add = (t, c, needsValue) =>
        {
            var b = Btn(t, (s, e) => RunScan(c, needsValue), null, 132);
            scanButtons.Add(b);
            grid.Controls.Add(b);
        };
        add("First scan: exact", Cmp.Exact, true);
        add("First scan: unknown", Cmp.Unknown, false);
        add("Next: exact value", Cmp.Exact, true);
        add("Next: changed", Cmp.Changed, false);
        add("Next: increased", Cmp.Increased, false);
        add("Next: decreased", Cmp.Decreased, false);
        add("Next: unchanged", Cmp.Unchanged, false);
        add("Next: up by value", Cmp.IncBy, true);
        add("Next: down by value", Cmp.DecBy, true);
        grid.Controls.Add(Btn("New scan (reset)", (s, e) => ResetScan(), Pink, 132));
        left.Controls.Add(grid);

        bar.Width = 270;
        bar.Height = 10;
        left.Controls.Add(bar);
        countLbl.AutoSize = true;
        countLbl.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        countLbl.Text = "No scan yet";
        left.Controls.Add(countLbl);

        Controls.Add(left);
        left.BringToFront();
        UpdateButtons();
    }

    void BuildRight()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Bg, SplitterWidth = 6 };

        StyleList(results);
        results.Columns.Add("Address", 160);
        results.Columns.Add("Type", 80);
        results.Columns.Add("Value", 140);
        results.Columns.Add("Looks like", 110);
        results.Columns.Add("Confidence", 100);
        results.DoubleClick += (s, e) => AddSelectedHits();
        var rTop = new Label { Text = "  Results  (double-click to add to cheat table)", Dock = DockStyle.Top, Height = 24, ForeColor = Cyan, TextAlign = ContentAlignment.MiddleLeft };
        split.Panel1.Controls.Add(results);
        split.Panel1.Controls.Add(rTop);

        StyleList(table);
        table.CheckBoxes = true;
        table.Columns.Add("Freeze / Name", 220);
        table.Columns.Add("Address", 150);
        table.Columns.Add("Type", 80);
        table.Columns.Add("Value", 140);
        table.ItemChecked += (s, e) =>
        {
            if (suppressCheck) return;
            var en = (Entry)e.Item.Tag;
            en.Frozen = e.Item.Checked;
            double v;
            if (en.Frozen && Values.TryRead(en.H, en.Addr, en.Type, out v)) en.FrozenVal = v;
        };
        table.DoubleClick += (s, e) => SetSelected();

        var tb = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, BackColor = Panel2, Padding = new Padding(4) };
        tb.Controls.Add(Btn("Set value...", (s, e) => SetSelected(), Pink, 110));
        tb.Controls.Add(Btn("Max it (999999)", (s, e) => SetSelected(999999), Color.Gold, 130));
        tb.Controls.Add(Btn("Freeze all", (s, e) => FreezeAll(true), null, 100));
        tb.Controls.Add(Btn("Unfreeze all", (s, e) => FreezeAll(false), null, 100));
        tb.Controls.Add(Btn("Rename", (s, e) => RenameSelected(), null, 80));
        tb.Controls.Add(Btn("Remove", (s, e) => RemoveSelected(), null, 80));
        tb.Controls.Add(Btn("Save table", (s, e) => SaveTable(), null, 95));
        tb.Controls.Add(Btn("Load table", (s, e) => LoadTable(), null, 95));
        var tTop = new Label { Text = "  Cheat table  (tick = freeze, double-click = set value)", Dock = DockStyle.Top, Height = 24, ForeColor = Cyan, TextAlign = ContentAlignment.MiddleLeft };
        split.Panel2.Controls.Add(table);
        split.Panel2.Controls.Add(tb);
        split.Panel2.Controls.Add(tTop);

        Controls.Add(split);
        split.SplitterDistance = 300;
    }

    static void StyleList(ListView lv)
    {
        lv.Dock = DockStyle.Fill;
        lv.View = View.Details;
        lv.FullRowSelect = true;
        lv.HideSelection = false;
        lv.BackColor = Color.FromArgb(10, 5, 22);
        lv.ForeColor = Color.FromArgb(236, 228, 255);
        lv.BorderStyle = BorderStyle.None;
        lv.Font = new Font("Consolas", 10f);
    }

    // ---------- processes ----------

    void LoadProcesses()
    {
        procBox.Items.Clear();
        int self = Process.GetCurrentProcess().Id;
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "explorer", "ApplicationFrameHost", "TextInputHost", "SystemSettings", "ShellExperienceHost", "SearchHost",
            "StartMenuExperienceHost", "GalaxyTrainer", "GalaxyTaskbar", "windhawk", "Taskmgr", "claude", "WindowsTerminal" };
        var seenBrowsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<Target>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == self || p.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(p.MainWindowTitle) || skip.Contains(p.ProcessName)) continue;
                bool browser = BrowserExes.Contains(p.ProcessName.ToLowerInvariant());
                if (browser)
                {
                    if (!seenBrowsers.Add(p.ProcessName)) continue;
                    var t = new Target { Exe = p.ProcessName, Title = p.MainWindowTitle, Browser = true };
                    t.Pids = BrowserContentPids(p.ProcessName);
                    if (t.Pids.Count > 0) list.Add(t);
                    continue;
                }
                string dir = null;
                try { dir = Path.GetDirectoryName(p.MainModule.FileName); } catch { }
                var tg = new Target { Exe = p.ProcessName, Title = p.MainWindowTitle, Dir = dir };
                tg.Pids.Add(p.Id);
                list.Add(tg);
            }
            catch { }
        }
        foreach (var t in list.OrderBy(t => t.Browser).ThenBy(t => t.Title)) procBox.Items.Add(t);
        if (procBox.Items.Count > 0) procBox.SelectedIndex = 0;
    }

    static List<int> BrowserContentPids(string exe)
    {
        var pids = new List<int>();
        try
        {
            using (var q = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='" + exe + ".exe'"))
                foreach (ManagementObject o in q.Get())
                {
                    string cmd = (o["CommandLine"] as string) ?? "";
                    bool content = exe == "firefox"
                        ? cmd.Contains("-contentproc") && cmd.Contains(" tab")
                        : cmd.Contains("--type=renderer") && !cmd.Contains("--extension-process");
                    if (content) pids.Add(Convert.ToInt32(o["ProcessId"]));
                }
        }
        catch { }
        return pids;
    }

    void Attach()
    {
        var t = procBox.SelectedItem as Target;
        if (t == null) return;
        string why = Guard.WhyBlocked(t);
        if (why != null)
        {
            MessageBox.Show(this, why + "\n\nGalaxy Trainer only works on single-player games. Editing online or anti-cheat protected games " +
                "ruins it for other players and gets accounts banned.", "Not allowed", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }
        Detach();
        foreach (int pid in t.Pids)
        {
            var h = Native.OpenProcess(Native.PROCESS_ACCESS, false, pid);
            if (h != IntPtr.Zero) handles.Add(h);
        }
        if (handles.Count == 0)
        {
            MessageBox.Show(this, "Couldn't open the game's memory. Try running Galaxy Trainer as administrator.", "Galaxy Trainer");
            return;
        }
        target = t;
        for (int k = 0; k < 4; k++) typeChecks[k].Checked = t.Browser ? k >= 2 : k < 2;
        status.Text = "Attached: " + t.Title + (t.Browser ? "  (only use on single-player browser games)" : "");
        RegisterHotkeys(true);
        ResetScan();
        guide.Text = "Attached! Pick a wizard above.\n\n" + HotkeyHelp();
    }

    void Detach()
    {
        StopAuto();
        RegisterHotkeys(false);
        foreach (var h in handles) Native.CloseHandle(h);
        handles.Clear();
        entries.Clear();
        table.Items.Clear();
        target = null;
    }

    bool Alive()
    {
        foreach (var h in handles) { int code; if (Native.GetExitCodeProcess(h, out code) && code == 259) return true; }
        return false;
    }

    // ---------- hotkeys ----------

    const int MOD_ALT = 1, MOD_CTRL = 2;
    static readonly int[] HotVk = { 'D', 'I', 'U', 'C' };
    static readonly Cmp[] HotCmp = { Cmp.Decreased, Cmp.Increased, Cmp.Unchanged, Cmp.Changed };

    static string HotkeyHelp()
    {
        return "Hotkeys (work in-game):\n  Ctrl+Alt+D  value went down\n  Ctrl+Alt+I  value went up\n  Ctrl+Alt+U  value didn't change\n  Ctrl+Alt+C  value changed";
    }

    void RegisterHotkeys(bool on)
    {
        if (on == hotkeysOn) return;
        for (int i = 0; i < HotVk.Length; i++)
        {
            if (on) Native.RegisterHotKey(Handle, 100 + i, MOD_ALT | MOD_CTRL, HotVk[i]);
            else Native.UnregisterHotKey(Handle, 100 + i);
        }
        hotkeysOn = on;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312)
        {
            int i = m.WParam.ToInt32() - 100;
            if (i >= 0 && i < HotCmp.Length)
            {
                if (!scanner.Started) hud.Say("Start a scan first (use a wizard)");
                else RunScan(HotCmp[i], false);
            }
        }
        base.WndProc(ref m);
    }

    // ---------- scanning ----------

    bool ParseValue(out double v)
    {
        string s = valueBox.Text.Trim().Replace(",", "");
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
            || double.TryParse(valueBox.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out v);
    }

    void UpdateButtons()
    {
        for (int i = 0; i < scanButtons.Count; i++)
        {
            bool firstBtn = i < 2;
            scanButtons[i].Enabled = !busy && auto == null && target != null && (firstBtn ? !scanner.Started : scanner.Started);
        }
    }

    void ResetScan()
    {
        StopAuto();
        if (busy) return;
        scanner.Reset();
        results.Items.Clear();
        shownHits = new List<Hit>();
        countLbl.Text = "No scan yet";
        bar.Value = 0;
        UpdateButtons();
    }

    void RunScan(Cmp cmp, bool needsValue)
    {
        if (busy || target == null) return;
        if (!Alive()) { status.Text = "The game has closed."; Detach(); UpdateButtons(); return; }
        double v = 0;
        if (needsValue && !ParseValue(out v)) { MessageBox.Show(this, "Type a number in the Value box first.", "Galaxy Trainer"); return; }
        var types = typeChecks.Select(c => c.Checked).ToArray();
        if (!scanner.Started && !types.Any(x => x)) { MessageBox.Show(this, "Tick at least one value type.", "Galaxy Trainer"); return; }

        busy = true;
        UpdateButtons();
        countLbl.Text = "Scanning...";
        hud.Say("Scanning...");
        var hs = new List<IntPtr>(handles);
        Task.Factory.StartNew(() =>
        {
            Exception err = null;
            try { scanner.Run(hs, cmp, v, types, p => BeginInvoke((Action)(() => bar.Value = Math.Min(100, p)))); }
            catch (Exception ex) { err = ex; }
            BeginInvoke((Action)(() => ScanDone(err)));
        });
    }

    void ScanDone(Exception err)
    {
        busy = false;
        UpdateButtons();
        if (err != null) { countLbl.Text = "Scan failed"; MessageBox.Show(this, err.Message, "Scan failed"); return; }
        long total = scanner.Total;
        countLbl.Text = total.ToString("N0") + " results";
        SystemSounds.Asterisk.Play();
        ShowHits();

        if (total == 0)
        {
            hud.Say("0 results - start a new scan");
            guide.Text = "No matches left. The value may be stored differently (try ticking more types) or the game " +
                "changed it in a way that didn't match. Click 'New scan' and try again.";
        }
        else if (total <= 12)
        {
            int added = 0;
            foreach (var h in shownHits)
                if (!entries.Any(e => e.H == h.H && e.Addr == h.Addr && e.Type == h.Type)) { AddEntry("Candidate " + (++candidateNo), h); added++; }
            hud.Say("Found it! " + total + " result(s) added to cheat table");
            guide.Text = "Found " + total + " match(es) and added them to the cheat table.\n\n" +
                "Select one and click 'Set value' or 'Max it', or tick it to freeze. If the game doesn't change, try the next one. " +
                "Rename the one that works (e.g. 'Health') and Save the table.";
        }
        else hud.Say(total.ToString("N0") + " results - keep going");
    }

    void ShowHits()
    {
        results.BeginUpdate();
        results.Items.Clear();
        shownHits = scanner.Total <= 2000 ? scanner.Hits(2000) : scanner.Hits(300);
        foreach (var h in shownHits)
        {
            var it = new ListViewItem(h.Addr.ToString("X"));
            it.SubItems.Add(Values.TypeNames[h.Type]);
            it.SubItems.Add("");
            it.Tag = h;
            results.Items.Add(it);
        }
        results.EndUpdate();
        RefreshTick();
    }

    // ---------- smart auto-detect ----------

    void ToggleAuto()
    {
        if (auto != null) { StopAuto(); return; }
        if (target == null) { MessageBox.Show(this, "Attach to a game first.", "Galaxy Trainer"); return; }
        if (busy) return;
        ResetScan();
        var types = new bool[4];
        if (target.Browser) { types[2] = true; types[3] = true; }
        else { types[0] = true; types[1] = true; }
        auto = new AutoDetector(new Scanner(), new List<IntPtr>(handles));
        auto.OnUpdate = (msg, top) => { try { BeginInvoke((Action)(() => AutoUpdate(msg, top))); } catch { } };
        auto.OnFound = found => { try { BeginInvoke((Action)(() => AutoFound(found))); } catch { } };
        autoBtn.Text = "Stop auto-detect";
        autoBtn.FlatAppearance.BorderColor = Pink;
        guide.Text = "Smart auto-detect is running.\n\n" +
            "1. Stand still for 5 seconds while it calibrates (it throws out everything that changes on its own).\n" +
            "2. Then just play: get hurt, heal, shoot, reload, buy or pick up money.\n\n" +
            "It follows anything that changes like a stat, checks for a matching 'max' value next to it, and scores it. " +
            "Anything it's sure about gets added to the cheat table by itself.";
        UpdateButtons();
        auto.Start(types);
    }

    void StopAuto()
    {
        if (auto == null) return;
        auto.Stop();
        auto = null;
        autoBtn.Text = "Start smart auto-detect (just play)";
        autoBtn.FlatAppearance.BorderColor = Cyan;
        UpdateButtons();
    }

    void AutoUpdate(string msg, List<Track> top)
    {
        if (auto == null) return;
        countLbl.Text = msg;
        results.BeginUpdate();
        results.Items.Clear();
        foreach (var t in top.Take(200))
        {
            var it = new ListViewItem(t.Addr.ToString("X"));
            it.SubItems.Add(Values.TypeNames[t.Type]);
            it.SubItems.Add(Values.Fmt(t.Cur, t.Type));
            it.SubItems.Add(t.Score >= 30 ? t.Guess : "?");
            it.SubItems.Add(t.Score + "%");
            if (t.Score >= 70) it.ForeColor = Color.FromArgb(120, 255, 170);
            else if (t.Score >= 45) it.ForeColor = Color.Gold;
            it.Tag = new Hit { H = t.H, Addr = t.Addr, Type = t.Type };
            results.Items.Add(it);
        }
        results.EndUpdate();
    }

    void AutoFound(List<Track> found)
    {
        foreach (var t in found)
        {
            if (entries.Any(e => e.H == t.H && e.Addr == t.Addr && e.Type == t.Type)) continue;
            AddEntry(t.Guess + " (auto, " + t.Score + "%)", new Hit { H = t.H, Addr = t.Addr, Type = t.Type });
        }
        string names = string.Join(", ", found.Select(f => f.Guess).Distinct());
        hud.Say("Detected: " + names + " - added to cheat table");
        SystemSounds.Asterisk.Play();
        guide.Text = "Detected " + names + "! It's in the cheat table.\n\n" +
            "Select it and click 'Max it' or tick the box to freeze it. Games often keep a display copy too, " +
            "so if one entry doesn't stick, try the others or freeze them all. Auto-detect keeps running to find more.";
    }

    // ---------- wizards ----------

    void WizardMoney()
    {
        if (target == null) { MessageBox.Show(this, "Attach to a game first.", "Galaxy Trainer"); return; }
        string s = Prompt("Find money / ammo / any number", "How much do you have right now?\n(Type exactly the number shown in the game.)", "");
        if (s == null) return;
        ResetScan();
        valueBox.Text = s;
        for (int k = 0; k < 4; k++) typeChecks[k].Checked = target.Browser ? k >= 2 : k != 2;
        guide.Text = "Step 2: in the game, spend or earn some (buy something, pick up coins, fire a shot).\n\n" +
            "Then type the NEW number in the Value box and click 'Next: exact value'. Repeat until a few results remain; they'll be added to the cheat table automatically.";
        RunScan(Cmp.Exact, true);
    }

    void WizardHealth()
    {
        if (target == null) { MessageBox.Show(this, "Attach to a game first.", "Galaxy Trainer"); return; }
        ResetScan();
        for (int k = 0; k < 4; k++) typeChecks[k].Checked = target.Browser ? k >= 2 : k < 2;
        guide.Text = "Scanning everything... Then go back into the game:\n\n" +
            "1. Take damage, then press Ctrl+Alt+D\n" +
            "2. Walk around without getting hurt, press Ctrl+Alt+U (do this 2-3 times)\n" +
            "3. Heal, then press Ctrl+Alt+I\n\n" +
            "You'll hear a sound after each step. Repeat until it says 'Found it!' - the matches go into the cheat table on their own.\n\n" +
            "Tip: if health is shown as a number, the money wizard is faster.";
        RunScan(Cmp.Unknown, false);
    }

    // ---------- cheat table ----------

    void AddEntry(string desc, Hit h)
    {
        var en = new Entry { Desc = desc, H = h.H, Addr = h.Addr, Type = h.Type };
        entries.Add(en);
        var it = new ListViewItem(desc);
        it.SubItems.Add(h.Addr.ToString("X"));
        it.SubItems.Add(Values.TypeNames[h.Type]);
        it.SubItems.Add("");
        it.Tag = en;
        suppressCheck = true;
        table.Items.Add(it);
        suppressCheck = false;
    }

    void AddSelectedHits()
    {
        foreach (ListViewItem it in results.SelectedItems) AddEntry("Value " + (++candidateNo), (Hit)it.Tag);
    }

    IEnumerable<Entry> Selected()
    {
        var sel = table.SelectedItems.Cast<ListViewItem>().Select(i => (Entry)i.Tag).ToList();
        return sel;
    }

    void SetSelected(double? fixedValue = null)
    {
        var sel = Selected().ToList();
        if (sel.Count == 0) { MessageBox.Show(this, "Select a row in the cheat table first.", "Galaxy Trainer"); return; }
        double v;
        if (fixedValue.HasValue) v = fixedValue.Value;
        else
        {
            string s = Prompt("Set value", "New value:", "999");
            if (s == null || !double.TryParse(s.Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return;
        }
        foreach (var en in sel)
        {
            double val = en.Type == 1 && fixedValue.HasValue ? 9999 : v;
            Values.Write(en.H, en.Addr, en.Type, val);
            if (en.Frozen) en.FrozenVal = val;
        }
    }

    void FreezeAll(bool on)
    {
        suppressCheck = true;
        foreach (ListViewItem it in table.Items)
        {
            var en = (Entry)it.Tag;
            double v;
            if (on && Values.TryRead(en.H, en.Addr, en.Type, out v)) en.FrozenVal = v;
            en.Frozen = on;
            it.Checked = on;
        }
        suppressCheck = false;
    }

    void RenameSelected()
    {
        if (table.SelectedItems.Count != 1) return;
        var it = table.SelectedItems[0];
        string s = Prompt("Rename", "Name:", it.Text);
        if (string.IsNullOrWhiteSpace(s)) return;
        ((Entry)it.Tag).Desc = s;
        it.Text = s;
    }

    void RemoveSelected()
    {
        foreach (ListViewItem it in table.SelectedItems.Cast<ListViewItem>().ToList())
        {
            entries.Remove((Entry)it.Tag);
            table.Items.Remove(it);
        }
    }

    static string TablePath(string exe)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GalaxyTrainer");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, exe + ".table.txt");
    }

    void SaveTable()
    {
        if (target == null || entries.Count == 0) return;
        var lines = entries.Select(e => string.Join("\t", e.Desc.Replace("\t", " "), e.Addr.ToString("X"), e.Type.ToString(),
            e.Frozen ? "1" : "0", e.FrozenVal.ToString(CultureInfo.InvariantCulture)));
        File.WriteAllLines(TablePath(target.Exe), lines);
        status.Text = "Saved to " + TablePath(target.Exe);
    }

    void LoadTable()
    {
        if (target == null) return;
        string p = TablePath(target.Exe);
        if (!File.Exists(p)) { MessageBox.Show(this, "No saved table for this game yet.", "Galaxy Trainer"); return; }
        foreach (var line in File.ReadAllLines(p))
        {
            var f = line.Split('\t');
            if (f.Length < 5) continue;
            long addr = long.Parse(f[1], NumberStyles.HexNumber);
            int type = int.Parse(f[2]);
            IntPtr h = handles.FirstOrDefault(x => { double v; return Values.TryRead(x, addr, type, out v); });
            if (h == IntPtr.Zero) continue;
            AddEntry(f[0], new Hit { H = h, Addr = addr, Type = type });
        }
        MessageBox.Show(this, "Loaded. Note: most games move their values around each time they start, " +
            "so saved addresses often stop working after a restart - just re-run the wizard.", "Galaxy Trainer");
    }

    // ---------- timers ----------

    void FreezeTick()
    {
        foreach (var en in entries) if (en.Frozen) Values.Write(en.H, en.Addr, en.Type, en.FrozenVal);
    }

    void RefreshTick()
    {
        if (target == null || busy) return;
        if (auto == null && results.Items.Count <= 500)
            foreach (ListViewItem it in results.Items)
            {
                var h = (Hit)it.Tag;
                double v;
                string s = Values.TryRead(h.H, h.Addr, h.Type, out v) ? Values.Fmt(v, h.Type) : "??";
                if (it.SubItems[2].Text != s) it.SubItems[2].Text = s;
            }
        foreach (ListViewItem it in table.Items)
        {
            var en = (Entry)it.Tag;
            double v;
            string s = Values.TryRead(en.H, en.Addr, en.Type, out v) ? Values.Fmt(v, en.Type) : "??";
            if (it.SubItems[3].Text != s) it.SubItems[3].Text = s;
        }
    }

    // ---------- helpers ----------

    string Prompt(string title, string text, string def)
    {
        using (var f = new Form())
        {
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = f.MaximizeBox = false;
            f.ClientSize = new Size(360, 140);
            f.BackColor = Bg;
            f.ForeColor = Fg;
            f.Font = Font;
            var l = new Label { Text = text, Left = 12, Top = 10, Width = 336, Height = 44 };
            var t = new TextBox { Text = def, Left = 12, Top = 58, Width = 336, BackColor = Panel2, ForeColor = Fg };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 186, Top = 96, Width = 76, FlatStyle = FlatStyle.Flat };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 272, Top = 96, Width = 76, FlatStyle = FlatStyle.Flat };
            f.Controls.AddRange(new Control[] { l, t, ok, cancel });
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            return f.ShowDialog(this) == DialogResult.OK ? t.Text : null;
        }
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
