using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopWeather.Models;

namespace DesktopWeather.Rendering;

public sealed class ParticleSurface : FrameworkElement, IWeatherSurface
{
    private sealed class ParticleVisual
    {
        internal readonly DrawingVisual Visual = new();
        internal RotateTransform? Spin;
    }
    private readonly ParticleEngine _engine = new();
    private readonly FramePacer _pacer = new();
    private readonly List<ParticleVisual> _particles = new();
    private readonly VisualCollection _children;
    private WeatherSettings? _settings;
    private WeatherMode? _paintedMode;
    private bool _preview, _enabled = true, _subscribed, _disposed;
    private double _countScale = 1;
    public int ParticleCount => _engine.Particles.Count;
    public bool IsAnimating => _subscribed;
    public AnimationMetrics Metrics { get; } = new();

    public ParticleSurface()
    {
        _children = new VisualCollection(this);
        IsHitTestVisible = false;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
        Loaded += (_, _) => UpdateSubscription();
        Unloaded += (_, _) => Unsubscribe();
        IsVisibleChanged += (_, _) => UpdateSubscription();
        SizeChanged += (_, _) => { _engine.Resize(ActualWidth, ActualHeight); SyncVisuals(false); };
    }
    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public bool AnimationEnabled
    {
        get => _enabled;
        set { _enabled = value; UpdateSubscription(); }
    }
    public void Configure(WeatherSettings settings, bool preview = false, double countScale = 1)
    {
        if (_settings != null) _settings.PropertyChanged -= SettingsChanged;
        _settings = settings;
        _preview = preview;
        _countScale = countScale;
        settings.PropertyChanged += SettingsChanged;
        _engine.Resize(ActualWidth, ActualHeight);
        SyncVisuals(true);
    }
    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_settings == null || _disposed) return;
        if (e.PropertyName is nameof(WeatherSettings.Mode) or nameof(WeatherSettings.Size)) SyncVisuals(true);
        else if (e.PropertyName == nameof(WeatherSettings.Density)) SyncVisuals(false);
        else if (e.PropertyName == nameof(WeatherSettings.Opacity)) UpdateOpacity();
        else if (e.PropertyName == nameof(WeatherSettings.EnergySaving)) _pacer.Reset();
    }
    private void SyncVisuals(bool repaint)
    {
        if (_settings == null || _disposed) return;
        int count = _preview ? Math.Clamp((int)(_settings.Density * 0.2), 12, 100) : Math.Max(1, (int)Math.Round(_settings.Density * _countScale));
        bool modeChanged = _paintedMode != _settings.Mode;
        _engine.Configure(_settings.Mode, count);
        while (_particles.Count > count)
        {
            _children.RemoveAt(_children.Count - 1);
            _particles.RemoveAt(_particles.Count - 1);
        }
        int original = _particles.Count;
        while (_particles.Count < count)
        {
            var particle = new ParticleVisual();
            _particles.Add(particle);
            _children.Add(particle.Visual);
        }
        for (int i = repaint || modeChanged ? 0 : original; i < count; i++) PaintParticle(i);
        _paintedMode = _settings.Mode;
        UpdateOpacity();
        UpdatePositions();
    }
    private void PaintParticle(int index)
    {
        if (_settings == null) return;
        WeatherParticle particle = _engine.Particles[index];
        ParticleVisual node = _particles[index];
        double radius = _settings.Size * particle.SizeFactor * (_preview ? 0.85 : 1);
        using (DrawingContext dc = node.Visual.RenderOpen())
            dc.DrawImage(ParticleSprites.Get(_settings.Mode, particle.Variant, radius, particle.Depth), new Rect(-1, -1, 2, 2));
        var extent = ParticleAppearance.Extents(_settings.Mode, particle.Depth);
        bool rotates = _settings.Mode != WeatherMode.Fireflies && (_settings.Mode != WeatherMode.Snow ||
            SnowAppearance.Rotates(SnowAppearance.SpriteIndex(particle.Variant, radius, particle.Depth)));
        if (rotates)
        {
            var scale = new ScaleTransform(radius * extent.Width, radius * extent.Height); scale.Freeze();
            node.Spin = new RotateTransform(particle.Rotation);
            var transform = new TransformGroup();
            transform.Children.Add(scale);
            transform.Children.Add(node.Spin);
            node.Visual.Transform = transform;
        }
        else
        {
            node.Spin = null;
            Matrix matrix = Matrix.Identity;
            matrix.Scale(radius * extent.Width, radius * extent.Height);
            var transform = new MatrixTransform(matrix); transform.Freeze();
            node.Visual.Transform = transform;
        }
    }
    private void UpdateOpacity()
    {
        if (_settings == null) return;
        for (int i = 0; i < _particles.Count; i++)
            _particles[i].Visual.Opacity = _settings.Opacity / 100 * ParticleAppearance.Opacity(_settings.Mode, _engine.Particles[i]);
    }
    private void UpdatePositions()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            WeatherParticle particle = _engine.Particles[i];
            ParticleVisual node = _particles[i];
            node.Visual.Offset = new Vector(particle.X, particle.Y);
            if (node.Spin != null) node.Spin.Angle = particle.Rotation;
        }
        if (_settings?.Mode == WeatherMode.Fireflies) UpdateOpacity();
    }
    private void UpdateSubscription()
    {
        if (_enabled && IsLoaded && IsVisible && !_disposed)
        {
            if (_subscribed) return;
            _pacer.Reset();
            Metrics.ResetClock();
            CompositionTarget.Rendering += RenderFrame;
            _subscribed = true;
        }
        else Unsubscribe();
    }
    private void Unsubscribe()
    {
        if (!_subscribed) return;
        CompositionTarget.Rendering -= RenderFrame;
        _subscribed = false;
        Metrics.ResetClock();
    }
    private void RenderFrame(object? sender, EventArgs e)
    {
        if (_settings == null || e is not RenderingEventArgs rendering) return;
        double wall = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        if (!_pacer.TryAdvance(rendering.RenderingTime.TotalSeconds, wall, _settings.FrameRate, out double dt)) return;
        _engine.Step(dt, _settings);
        UpdatePositions();
        Metrics.RecordFrame();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unsubscribe();
        if (_settings != null) _settings.PropertyChanged -= SettingsChanged;
        _children.Clear();
        _particles.Clear();
    }
    public BitmapSource Capture()
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(ActualWidth)), Math.Max(1, (int)Math.Ceiling(ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        return bitmap;
    }
}
