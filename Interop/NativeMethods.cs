using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DesktopWeather.Interop;

internal static class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const long WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExLayered = 0x80000, WsExNoActivate = 0x08000000, WsExTopmost = 0x8;
    internal const int WmNcHitTest = 0x84, WmMouseActivate = 0x21, WmHotkey = 0x312;
    internal const uint SwpNoActivate = 0x10, SwpNoOwnerZOrder = 0x200, SwpNoMove = 0x2, SwpNoSize = 0x1;
    internal const uint ModAlt = 0x1, ModControl = 0x2, ModNoRepeat = 0x4000;
    internal static readonly IntPtr HwndTopmost = new(-1);
    internal static readonly IntPtr HwndBroadcast = new(0xffff);

    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong32(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyIcon(IntPtr icon);

    internal static long GetExtendedStyle(IntPtr hwnd) => IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64() : GetWindowLong32(hwnd, GwlExStyle);
    internal static void MakeOverlay(IntPtr hwnd)
    {
        long style = GetExtendedStyle(hwnd) | WsExTransparent | WsExLayered | WsExToolWindow | WsExNoActivate;
        Marshal.SetLastPInvokeError(0);
        IntPtr old = IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, GwlExStyle, new IntPtr(style)) : new IntPtr(SetWindowLong32(hwnd, GwlExStyle, unchecked((int)style)));
        if (old == IntPtr.Zero && Marshal.GetLastWin32Error() != 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
