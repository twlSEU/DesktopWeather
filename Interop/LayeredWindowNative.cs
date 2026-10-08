using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DesktopWeather.Interop;

internal static class LayeredWindowNative
{
    internal delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowClass
    {
        internal uint Size, Style;
        internal WindowProc Proc;
        internal int ClassExtra, WindowExtra;
        internal IntPtr Instance, Icon, Cursor, Background;
        internal string? Menu;
        internal string Name;
        internal IntPtr SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { internal int X, Y; internal Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Size { internal int Width, Height; internal Size(int w, int h) { Width = w; Height = h; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct Blend { internal byte Operation, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo
    {
        internal uint Size;
        internal int Width, Height;
        internal ushort Planes, BitCount;
        internal uint Compression, SizeImage;
        internal int XPels, YPels;
        internal uint ColorsUsed, ColorsImportant;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DisplayMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string DeviceName;
        internal ushort SpecVersion, DriverVersion, Size, DriverExtra;
        internal uint Fields;
        internal int X, Y;
        internal uint Orientation, FixedOutput;
        internal short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string FormName;
        internal ushort LogPixels;
        internal uint BitsPerPixel, Width, Height, Flags, Frequency, IcmMethod, IcmIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern ushort RegisterClassEx(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplaySettings(string device, int modeNumber, ref DisplayMode mode);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr bitmap);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(IntPtr handle);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destinationDc, ref Point destination, ref Size size, IntPtr sourceDc, ref Point source, uint colorKey, ref Blend blend, uint flags);
    [DllImport("dwmapi.dll")] internal static extern int DwmFlush();

    internal static int RefreshRate(string device)
    {
        var mode = new DisplayMode { DeviceName = "", FormName = "", Size = (ushort)Marshal.SizeOf<DisplayMode>() };
        return EnumDisplaySettings(device, -1, ref mode) && mode.Frequency >= 30 && mode.Frequency <= 360 ? (int)mode.Frequency : 60;
    }
    internal static IntPtr Require(IntPtr pointer) => pointer != IntPtr.Zero ? pointer : throw new Win32Exception(Marshal.GetLastWin32Error());
}
