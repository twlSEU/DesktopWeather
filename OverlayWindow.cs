using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using DesktopWeather.Interop;
using DesktopWeather.Models;
using DesktopWeather.Rendering;

namespace DesktopWeather;

internal sealed class OverlayWindow
{
    private const string ClassName = "DesktopWeather.NativeOverlay.1";
    private static readonly LayeredWindowNative.WindowProc Procedure = WindowProc;
    private static bool _registered;
    private bool _closed;
    internal Rectangle ScreenBounds { get; }
    internal IWeatherSurface Surface { get; }
    internal IntPtr Handle { get; }

    internal OverlayWindow(Rectangle bounds, string device, WeatherSettings settings, double countScale)
    {
        ScreenBounds = bounds;
        IntPtr instance = LayeredWindowNative.GetModuleHandle(null);
        if (!_registered)
        {
            var registration = new LayeredWindowNative.WindowClass { Size = (uint)Marshal.SizeOf<LayeredWindowNative.WindowClass>(),
                Proc = Procedure, Instance = instance, Name = ClassName };
            if (LayeredWindowNative.RegisterClassEx(ref registration) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            _registered = true;
        }
        uint ex = (uint)(NativeMethods.WsExTransparent | NativeMethods.WsExLayered | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow | NativeMethods.WsExTopmost);
        Handle = LayeredWindowNative.Require(LayeredWindowNative.CreateWindowEx(ex, ClassName, "DesktopWeather Effect", 0x80000000,
            bounds.X, bounds.Y, bounds.Width, bounds.Height, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero));
        try
        {
            Surface = new NativeParticleRenderer(Handle, bounds.X, bounds.Y, bounds.Width, bounds.Height, device, settings, countScale,
                error => Application.Current.Dispatcher.BeginInvoke(new Action(() => throw new InvalidOperationException("特效绘制失败", error))));
        }
        catch { LayeredWindowNative.DestroyWindow(Handle); throw; }
    }
    internal void Show()
    {
        ((NativeParticleRenderer)Surface).Start();
        LayeredWindowNative.ShowWindow(Handle, 4);
        if (!NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopmost, ScreenBounds.X, ScreenBounds.Y,
            ScreenBounds.Width, ScreenBounds.Height, NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    internal void Close()
    {
        if (_closed) return;
        _closed = true;
        Surface.Dispose();
        LayeredWindowNative.DestroyWindow(Handle);
    }
    private static IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == NativeMethods.WmNcHitTest) return new IntPtr(-1);
        if (message == NativeMethods.WmMouseActivate) return new IntPtr(3);
        return LayeredWindowNative.DefWindowProc(hwnd, message, wParam, lParam);
    }
}
