using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using DesktopWeather.Models;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DesktopWeather.Services;

internal sealed class WeatherController : IDisposable
{
    private readonly WeatherSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly List<OverlayWindow> _overlays = new();
    private bool _disposed;
    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }
    public int ScreenCount => _overlays.Count;
    internal IReadOnlyList<OverlayWindow> Overlays => _overlays;
    public event EventHandler? StateChanged;

    public WeatherController(WeatherSettings settings, Dispatcher dispatcher)
    {
        _settings = settings;
        _dispatcher = dispatcher;
        _settings.PropertyChanged += SettingsChanged;
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
    }

    public void Start()
    {
        if (_disposed || IsRunning) return;
        IsRunning = true;
        IsPaused = false;
        try { RebuildOverlays(); }
        catch { Stop(); throw; }
    }
    public void Stop()
    {
        IsRunning = false;
        IsPaused = false;
        CloseOverlays();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    public void TogglePause()
    {
        if (!IsRunning) return;
        IsPaused = !IsPaused;
        foreach (OverlayWindow overlay in _overlays) overlay.Surface.AnimationEnabled = !IsPaused;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WeatherSettings.AllMonitors) && IsRunning) RebuildOverlays();
        if (e.PropertyName == nameof(WeatherSettings.Mode)) StateChanged?.Invoke(this, EventArgs.Empty);
    }
    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        _dispatcher.BeginInvoke(new Action(() => { if (!_disposed && IsRunning) RebuildOverlays(); }));
    }
    private void RebuildOverlays()
    {
        CloseOverlays();
        Forms.Screen[] screens = _settings.AllMonitors ? Forms.Screen.AllScreens : new[] { Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0] };
        double area = screens.Sum(s => (double)s.Bounds.Width * s.Bounds.Height);
        foreach (Forms.Screen screen in screens)
        {
            var overlay = new OverlayWindow(screen.Bounds, screen.DeviceName, _settings, screen.Bounds.Width * (double)screen.Bounds.Height / area);
            _overlays.Add(overlay);
            overlay.Surface.AnimationEnabled = !IsPaused;
            overlay.Show();
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    private void CloseOverlays()
    {
        foreach (OverlayWindow overlay in _overlays) overlay.Close();
        _overlays.Clear();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings.PropertyChanged -= SettingsChanged;
        SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        Stop();
    }
}
