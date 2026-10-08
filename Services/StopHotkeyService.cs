using System;
using System.Windows;
using System.Windows.Interop;
using DesktopWeather.Interop;

namespace DesktopWeather.Services;

internal sealed class StopHotkeyService : IDisposable
{
    internal const int HotkeyId = 0x41E0;
    internal static readonly uint ShowMessageId = NativeMethods.RegisterWindowMessage("DesktopWeather.ShowSettings.1");
    private readonly IntPtr _handle;
    private readonly HwndSource _source;
    private readonly Action _stop, _show;
    internal bool Registered { get; }

    internal StopHotkeyService(Window window, Action stop, Action show)
    {
        _stop = stop;
        _show = show;
        _handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(WindowProc);
        Registered = NativeMethods.RegisterHotKey(_handle, HotkeyId, NativeMethods.ModControl | NativeMethods.ModAlt | NativeMethods.ModNoRepeat, 0x7B);
    }
    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId) { _stop(); handled = true; }
        else if ((uint)message == ShowMessageId) { _show(); handled = true; }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (Registered) NativeMethods.UnregisterHotKey(_handle, HotkeyId);
        if (!_source.IsDisposed) _source.RemoveHook(WindowProc);
    }
}
