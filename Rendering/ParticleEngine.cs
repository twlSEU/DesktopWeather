using System;
using System.Collections.Generic;
using DesktopWeather.Models;

namespace DesktopWeather.Rendering;

public sealed class WeatherParticle
{
    public double X, Y, SizeFactor, Depth, FallFactor, Phase, Rotation, RotationSpeed;
    public double Brightness = 1;
    public int Variant;
}

// No UI objects per particle: one surface draws the entire particle set.
public sealed class ParticleEngine
{
    private readonly Random _random;
    private readonly List<WeatherParticle> _particles = new();
    private double _width = 1, _height = 1, _elapsed;
    private WeatherMode _mode;
    public IReadOnlyList<WeatherParticle> Particles => _particles;

    public ParticleEngine(int? seed = null) => _random = seed.HasValue ? new Random(seed.Value) : new Random();
    public void Resize(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
        foreach (WeatherParticle p in _particles) { p.X *= width / _width; p.Y *= height / _height; }
        _width = width;
        _height = height;
    }

    public void Configure(WeatherMode mode, int count)
    {
        count = Math.Clamp(count, 0, 1600);
        if (_mode != mode) { _mode = mode; _particles.Clear(); _elapsed = 0; }
        if (_particles.Count > count) _particles.RemoveRange(count, _particles.Count - count);
        while (_particles.Count < count) _particles.Add(CreateParticle(true));
    }

    public void Step(double seconds, WeatherSettings settings, double unitsScale = 1)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        // Resume after sleep without allowing a large simulation jump.
        double dt = Math.Min(seconds, 0.1);
        _elapsed += dt;
        foreach (WeatherParticle p in _particles)
        {
            if (settings.Mode == WeatherMode.Fireflies)
            {
                p.X += (settings.Wind * 0.2 + Math.Cos(_elapsed * 0.65 + p.Phase) * settings.Speed * 0.28 * p.Depth) * dt * unitsScale;
                p.Y += Math.Sin(_elapsed * 0.48 + p.Phase * 1.7) * settings.Speed * 0.2 * p.Depth * dt * unitsScale;
                double pulse = (1 + Math.Sin(_elapsed * (0.8 + p.FallFactor * 0.25) + p.Phase)) / 2;
                p.Brightness = 0.22 + 0.78 * pulse * pulse;
                p.Rotation = 0;
            }
            else if (settings.Mode == WeatherMode.Rain)
            {
                double fall = settings.Speed * 4.4 * p.FallFactor;
                p.X += settings.Wind * dt * unitsScale;
                p.Y += fall * dt * unitsScale;
                p.Rotation = -Math.Atan2(settings.Wind, fall) * 180 / Math.PI;
            }
            else if (settings.Mode == WeatherMode.Snow)
            {
                double sway = Math.Sin(_elapsed * (0.55 + p.Depth * 0.45) + p.Phase) * (6 + 20 * p.Depth)
                    + Math.Sin(_elapsed * 0.27 + p.Phase * 1.4) * 3 * p.Depth;
                p.X += (settings.Wind * (0.25 + 0.8 * p.Depth) + sway) * dt * unitsScale;
                p.Y += settings.Speed * p.FallFactor * (1 + 0.04 * Math.Sin(_elapsed * 0.6 + p.Phase)) * dt * unitsScale;
                p.Rotation = (p.Rotation + p.RotationSpeed * dt * 0.32) % 360;
            }
            else
            {
                bool leaves = settings.Mode == WeatherMode.Leaves, petals = settings.Mode == WeatherMode.Petals;
                double sway = leaves ? 38 : petals ? 26 : 14;
                p.X += (settings.Wind + Math.Sin(_elapsed * (leaves ? 1.5 : petals ? 1.1 : 0.85) + p.Phase) * sway * p.Depth) * dt * unitsScale;
                p.Y += settings.Speed * p.FallFactor * (leaves ? 0.8 : petals ? 0.65 : 1) * dt * unitsScale;
                p.Rotation = (p.Rotation + p.RotationSpeed * dt * (leaves ? 1 : petals ? 0.65 : 0.24)) % 360;
            }
            double margin = (settings.Size * 5 + 8) * unitsScale;
            if (p.X < -margin) p.X = _width + margin;
            if (p.X > _width + margin) p.X = -margin;
            if (settings.Mode == WeatherMode.Fireflies)
            {
                if (p.Y < -margin) p.Y = _height + margin;
                if (p.Y > _height + margin) p.Y = -margin;
            }
            else if (p.Y > _height + margin)
            {
                p.X = _random.NextDouble() * _width;
                p.Y = -margin;
            }
        }
    }

    private WeatherParticle CreateParticle(bool distribute)
    {
        var particle = new WeatherParticle
        {
            X = _random.NextDouble() * _width,
            Y = distribute ? _random.NextDouble() * _height : -20,
            SizeFactor = 0.45 + _random.NextDouble() * 0.85,
            Depth = 0.35 + _random.NextDouble() * 0.65,
            FallFactor = 0.5 + _random.NextDouble() * 0.9,
            Phase = _random.NextDouble() * Math.PI * 2,
            Rotation = _random.NextDouble() * 360,
            RotationSpeed = -60 + _random.NextDouble() * 120,
            Variant = _random.Next(_mode == WeatherMode.Snow ? SnowAppearance.VariantCount : 5)
        };
        if (_mode == WeatherMode.Snow)
        {
            // Mostly distant flakes; a few large, soft foreground flakes give depth.
            particle.Depth = 0.1 + 0.9 * Math.Pow(_random.NextDouble(), 1.7);
            particle.SizeFactor = SnowAppearance.SizeFactor(particle.Depth, 0.85 + _random.NextDouble() * 0.25);
            particle.FallFactor = SnowAppearance.FallFactor(particle.Depth, 0.8 + _random.NextDouble() * 0.35);
            particle.RotationSpeed = -18 + _random.NextDouble() * 36;
        }
        return particle;
    }
}
