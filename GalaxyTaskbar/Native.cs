// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
using System;
using System.Runtime.InteropServices;
using System.Text;

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

// ---------- system audio level (for music-reactive mode) ----------

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumeratorCo { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
}

[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioMeterInformation
{
    [PreserveSig] int GetPeakValue(out float peak);
}

// Reads the peak level of whatever is playing on the default speakers and detects beats.
class Audio
{
    IAudioMeterInformation meter;
    double retryAt, reacquireAt;
    float avg;
    double lastBeat = -9;
    public float Level;   // smoothed 0..~1.5
    public float Beat;    // 1 on a beat, decays to 0

    public void Update(double t, float dt, float sensitivity)
    {
        if ((meter == null && t >= retryAt) || t >= reacquireAt) Acquire(t);
        float p = 0;
        if (meter != null)
        {
            try { if (meter.GetPeakValue(out p) != 0) { meter = null; p = 0; } }
            catch { meter = null; retryAt = t + 5; }
        }
        p = Math.Min(1.5f, p * sensitivity);
        Level += (p - Level) * Math.Min(1, dt * (p > Level ? 30 : 5));
        avg += (p - avg) * Math.Min(1, dt * 1.5f);
        Beat = Math.Max(0, Beat - dt * 4);
        if (p > avg * 1.35f + 0.05f && t - lastBeat > 0.22) { Beat = 1; lastBeat = t; }
    }

    void Acquire(double t)
    {
        reacquireAt = t + 10;            // follow default-device changes
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCo();
            IMMDevice dev;
            if (en.GetDefaultAudioEndpoint(0, 1, out dev) != 0 || dev == null) { meter = null; retryAt = t + 5; return; }
            Guid iid = typeof(IAudioMeterInformation).GUID;
            object o;
            if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) { meter = null; retryAt = t + 5; return; }
            meter = (IAudioMeterInformation)o;
        }
        catch { meter = null; retryAt = t + 5; }
    }
}
