using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using DesktopWeather.Models;

namespace DesktopWeather.Rendering;

internal sealed class NativeSpriteAtlas
{
    internal sealed record Sprite(int Width, int Height, uint[] Pixels);
    private readonly record struct Key(WeatherMode Mode, int Variant, int Radius, int Angle);
    private readonly Dictionary<Key, Sprite> _cache = new();
    private readonly Queue<Key> _order = new();
    private readonly Dictionary<WeatherMode, Sprite[]> _sources = new();
    private long _bytes;

    internal NativeSpriteAtlas()
    {
        foreach (WeatherMode mode in Enum.GetValues<WeatherMode>())
        {
            int count = mode == WeatherMode.Snow ? SnowAppearance.SpriteCount : mode == WeatherMode.Rain ? 1 : mode == WeatherMode.Fireflies ? 3 : 5;
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++) sprites[i] = Read(mode == WeatherMode.Snow ? ParticleSprites.GetSnow(i) : ParticleSprites.Get(mode, i, 5));
            _sources.Add(mode, sprites);
        }
    }
    private static Sprite Read(BitmapSource bitmap)
    {
        var pixels = new uint[bitmap.PixelWidth * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return new Sprite(bitmap.PixelWidth, bitmap.PixelHeight, pixels);
    }
    internal Sprite Get(WeatherMode mode, int variant, double radius, double rotation, double depth = 0.5)
    {
        int shape = mode == WeatherMode.Snow ? SnowAppearance.SpriteIndex(variant, radius, depth) : variant % _sources[mode].Length;
        int angle = mode == WeatherMode.Fireflies || mode == WeatherMode.Snow && !SnowAppearance.Rotates(shape) ? 0 : (int)Math.Round(((rotation % 360 + 360) % 360) / 5) % 72;
        var key = new Key(mode, shape, Math.Max(1, (int)Math.Round(radius)), angle);
        if (_cache.TryGetValue(key, out Sprite? cached)) return cached;
        Sprite raw = _sources[mode][shape];
        var extent = ParticleAppearance.Extents(mode, depth);
        double halfWidth = key.Radius * extent.Width;
        double halfHeight = key.Radius * extent.Height;
        double radians = angle * Math.PI / 36;
        double cosine = Math.Cos(radians), sine = Math.Sin(radians);
        int width = (int)Math.Ceiling(2 * (Math.Abs(cosine) * halfWidth + Math.Abs(sine) * halfHeight)) + 2;
        int height = (int)Math.Ceiling(2 * (Math.Abs(sine) * halfWidth + Math.Abs(cosine) * halfHeight)) + 2;
        var pixels = new uint[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double dx = x + 0.5 - width / 2.0, dy = y + 0.5 - height / 2.0;
            double sx = ((cosine * dx + sine * dy) / halfWidth + 1) * raw.Width / 2 - 0.5;
            double sy = ((-sine * dx + cosine * dy) / halfHeight + 1) * raw.Height / 2 - 0.5;
            if (sx < -0.5 || sy < -0.5 || sx >= raw.Width - 0.5 || sy >= raw.Height - 0.5) continue;
            int floorX = (int)Math.Floor(sx), floorY = (int)Math.Floor(sy);
            int x0 = Math.Clamp(floorX, 0, raw.Width - 1), x1 = Math.Clamp(floorX + 1, 0, raw.Width - 1);
            int y0 = Math.Clamp(floorY, 0, raw.Height - 1), y1 = Math.Clamp(floorY + 1, 0, raw.Height - 1);
            int fx = (int)((sx - floorX) * 256), fy = (int)((sy - floorY) * 256);
            uint top = Lerp(raw.Pixels[y0 * raw.Width + x0], raw.Pixels[y0 * raw.Width + x1], fx);
            uint bottom = Lerp(raw.Pixels[y1 * raw.Width + x0], raw.Pixels[y1 * raw.Width + x1], fx);
            pixels[y * width + x] = Lerp(top, bottom, fy);
        }
        var result = new Sprite(width, height, pixels);
        while (_cache.Count >= 8192 || (_bytes + pixels.Length * 4L > 64 * 1024 * 1024 && _order.Count > 0))
        {
            Key expired = _order.Dequeue();
            _bytes -= _cache[expired].Pixels.Length * 4L;
            _cache.Remove(expired);
        }
        _cache.Add(key, result); _order.Enqueue(key); _bytes += pixels.Length * 4L;
        return result;
    }
    private static uint Lerp(uint first, uint second, int fraction)
    {
        uint weight = (uint)fraction, inverse = 256 - weight;
        uint rb = (((first & 0x00ff00ff) * inverse + (second & 0x00ff00ff) * weight) >> 8) & 0x00ff00ff;
        uint ag = ((((first >> 8) & 0x00ff00ff) * inverse + ((second >> 8) & 0x00ff00ff) * weight) >> 8) & 0x00ff00ff;
        return rb | (ag << 8);
    }
}
