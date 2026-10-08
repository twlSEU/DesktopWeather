using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DesktopWeather.Models;

[JsonConverter(typeof(JsonStringEnumConverter<WeatherMode>))]
public enum WeatherMode { Snow, Leaves, Petals, Fireflies, Rain }

public sealed class WeatherSettings : INotifyPropertyChanged
{
    private WeatherMode _mode = WeatherMode.Snow;
    private int _density = 260;
    private double _size = 5;
    private double _speed = 65;
    private double _wind = 10;
    private double _opacity = 80;
    private bool _allMonitors = true;
    private bool _energySaving;
    private bool _minimizeToTray = true;

    public WeatherMode Mode { get => _mode; set => Set(ref _mode, Enum.IsDefined(value) ? value : WeatherMode.Snow); }
    public int Density { get => _density; set => Set(ref _density, Math.Clamp(value, 50, 1600)); }
    public double Size { get => _size; set => Set(ref _size, Limit(value, 2, 16, 5)); }
    public double Speed { get => _speed; set => Set(ref _speed, Limit(value, 15, 180, 65)); }
    public double Wind { get => _wind; set => Set(ref _wind, Limit(value, -100, 100, 10)); }
    public double Opacity { get => _opacity; set => Set(ref _opacity, Limit(value, 20, 100, 80)); }
    public bool AllMonitors { get => _allMonitors; set => Set(ref _allMonitors, value); }
    public bool EnergySaving { get => _energySaving; set => Set(ref _energySaving, value); }
    public bool MinimizeToTray { get => _minimizeToTray; set => Set(ref _minimizeToTray, value); }
    [JsonIgnore] public int FrameRate => EnergySaving ? 30 : 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    private static double Limit(double value, double min, double max, double fallback) =>
        Math.Clamp(double.IsFinite(value) ? value : fallback, min, max);
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name == nameof(EnergySaving)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FrameRate)));
    }
}
