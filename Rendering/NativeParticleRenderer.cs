using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopWeather.Interop;
using DesktopWeather.Models;

namespace DesktopWeather.Rendering;

internal sealed unsafe class NativeParticleRenderer : IWeatherSurface
{
    private readonly record struct DirtyRect(int X, int Y, int Width, int Height);
    private readonly IntPtr _window;
    private readonly int _width, _height, _x, _y, _displayFps;
    private readonly double _scale, _countScale;
    private readonly WeatherSettings _settings, _frame = new();
    private readonly ParticleEngine _engine = new();
    private readonly NativeSpriteAtlas _atlas = new();
    private readonly DirtyRect[] _dirty = new DirtyRect[1600];
    private readonly object _bufferLock = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly ManualResetEventSlim _active = new(true);
    private readonly Action<Exception> _error;
    private Thread? _thread;
    private IntPtr _dc, _bitmap, _previousBitmap, _bits;
    private int _dirtyCount;
    private bool _disposed;
    public int ParticleCount => _engine.Particles.Count;
    public bool IsAnimating => !_disposed && _active.IsSet && _thread?.IsAlive == true;
    public AnimationMetrics Metrics { get; } = new();
    public bool AnimationEnabled
    {
        get => _active.IsSet;
        set { if (value) _active.Set(); else { _active.Reset(); Metrics.ResetClock(); } }
    }
    internal NativeParticleRenderer(IntPtr window, int x, int y, int width, int height, string device,
        WeatherSettings settings, double countScale, Action<Exception> error)
    {
        _window = window; _x = x; _y = y; _width = width; _height = height;
        _settings = settings; _countScale = countScale; _error = error;
        uint dpi = LayeredWindowNative.GetDpiForWindow(window);
        _scale = (dpi == 0 ? 96 : dpi) / 96.0;
        int refresh = LayeredWindowNative.RefreshRate(device);
        _displayFps = refresh == 59 ? 60 : refresh == 119 ? 120 : refresh == 143 ? 144 : refresh;
        _engine.Resize(width, height);
        _engine.Configure(settings.Mode, Math.Max(1, (int)Math.Round(settings.Density * countScale)));
        try
        {
            _dc = LayeredWindowNative.Require(LayeredWindowNative.CreateCompatibleDC(IntPtr.Zero));
            var info = new LayeredWindowNative.BitmapInfo { Size = (uint)Marshal.SizeOf<LayeredWindowNative.BitmapInfo>(),
                Width = width, Height = -height, Planes = 1, BitCount = 32, SizeImage = checked((uint)(width * (long)height * 4)) };
            _bitmap = LayeredWindowNative.Require(LayeredWindowNative.CreateDIBSection(_dc, ref info, 0, out _bits, IntPtr.Zero, 0));
            _previousBitmap = LayeredWindowNative.SelectObject(_dc, _bitmap);
            new Span<uint>((void*)_bits, checked(width * height)).Clear();
        }
        catch { FreeBuffer(); throw; }
    }
    internal void Start()
    {
        if (_thread != null || _disposed) return;
        _thread = new Thread(RenderLoop) { IsBackground = true, Name = "DesktopWeather animation" };
        _thread.Start();
    }
    private void ReadSettings()
    {
        _frame.Mode = _settings.Mode; _frame.Density = _settings.Density; _frame.Size = _settings.Size;
        _frame.Speed = _settings.Speed; _frame.Wind = _settings.Wind; _frame.Opacity = _settings.Opacity;
        _frame.EnergySaving = _settings.EnergySaving;
    }
    private void RenderLoop()
    {
        double last = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, deadline = last;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                if (!_active.IsSet)
                {
                    _active.Wait(_stop.Token);
                    last = deadline = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                    Metrics.ResetClock();
                }
                ReadSettings();
                _engine.Configure(_frame.Mode, Math.Max(1, (int)Math.Round(_frame.Density * _countScale)));
                double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                _engine.Step(now - last, _frame, _scale);
                last = now;
                lock (_bufferLock) DrawAndPresent();
                if (_active.IsSet) Metrics.RecordFrame();
                // Present off the settings thread and synchronise with the desktop compositor.
                LayeredWindowNative.DwmFlush();
                double interval = 1.0 / (_frame.EnergySaving ? 30 : _displayFps);
                deadline += interval;
                now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                if (deadline < now - interval) deadline = now;
                double remaining = deadline - now;
                if (remaining > 0.001) _stop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(remaining));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_stop.IsCancellationRequested) _error(ex); }
        finally { lock (_bufferLock) FreeBuffer(); Metrics.ResetClock(); }
    }
    private void DrawAndPresent()
    {
        uint* pixels = (uint*)_bits;
        // Clear only the rectangles occupied on the preceding frame, rather than 4K pixels.
        for (int i = 0; i < _dirtyCount; i++)
        {
            DirtyRect rect = _dirty[i];
            for (int y = rect.Y; y < rect.Y + rect.Height; y++) new Span<uint>(pixels + y * _width + rect.X, rect.Width).Clear();
        }
        _dirtyCount = 0;
        foreach (WeatherParticle particle in _engine.Particles)
        {
            double radius = _frame.Size * particle.SizeFactor * _scale;
            NativeSpriteAtlas.Sprite sprite = _atlas.Get(_frame.Mode, particle.Variant, radius, particle.Rotation, particle.Depth);
            int left = (int)Math.Round(particle.X - sprite.Width / 2.0), top = (int)Math.Round(particle.Y - sprite.Height / 2.0);
            int x0 = Math.Max(0, left), y0 = Math.Max(0, top), x1 = Math.Min(_width, left + sprite.Width), y1 = Math.Min(_height, top + sprite.Height);
            if (x1 <= x0 || y1 <= y0) continue;
            _dirty[_dirtyCount++] = new DirtyRect(x0, y0, x1 - x0, y1 - y0);
            uint opacity = (uint)Math.Clamp((int)Math.Round(_frame.Opacity / 100 * ParticleAppearance.Opacity(_frame.Mode, particle) * 256), 0, 256);
            fixed (uint* source = sprite.Pixels)
            {
                for (int y = y0; y < y1; y++)
                {
                    uint* row = pixels + y * _width + x0;
                    uint* src = source + (y - top) * sprite.Width + (x0 - left);
                    if (_frame.Mode == WeatherMode.Snow)
                    {
                        // White premultiplied flakes have RGB == alpha, so blend one channel.
                        // The preceding dirty rectangles cleared every old coloured particle.
                        for (int x = x0; x < x1; x++, row++, src++)
                        {
                            uint alpha = (*src >> 24) * opacity >> 8;
                            if (alpha == 0) continue;
                            uint combined = alpha + ((*row >> 24) * (256 - alpha) >> 8);
                            *row = combined * 0x01010101;
                        }
                        continue;
                    }
                    for (int x = x0; x < x1; x++, row++, src++)
                    {
                        uint value = *src;
                        uint alpha = (value >> 24) * opacity >> 8;
                        if (alpha == 0) continue;
                        uint rb = ((value & 0x00ff00ff) * opacity >> 8) & 0x00ff00ff;
                        uint ag = (((value >> 8) & 0x00ff00ff) * opacity >> 8) & 0x00ff00ff;
                        uint destination = *row;
                        if (destination != 0)
                        {
                            uint inverse = 256 - alpha;
                            rb += ((destination & 0x00ff00ff) * inverse >> 8) & 0x00ff00ff;
                            ag += (((destination >> 8) & 0x00ff00ff) * inverse >> 8) & 0x00ff00ff;
                        }
                        *row = rb | (ag << 8);
                    }
                }
            }
        }
        var point = new LayeredWindowNative.Point(_x, _y);
        var sourcePoint = new LayeredWindowNative.Point(0, 0);
        var size = new LayeredWindowNative.Size(_width, _height);
        var blend = new LayeredWindowNative.Blend { Alpha = 255, Format = 1 };
        if (!LayeredWindowNative.UpdateLayeredWindow(_window, IntPtr.Zero, ref point, ref size, _dc, ref sourcePoint, 0, ref blend, 2))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public BitmapSource Capture()
    {
        var pixels = new uint[checked(_width * _height)];
        lock (_bufferLock)
        {
            if (_bits == IntPtr.Zero) throw new ObjectDisposedException(nameof(NativeParticleRenderer));
            new ReadOnlySpan<uint>((void*)_bits, pixels.Length).CopyTo(pixels);
        }
        BitmapSource bitmap = BitmapSource.Create(_width, _height, 96, 96, PixelFormats.Pbgra32, null, pixels, _width * 4);
        bitmap.Freeze();
        return bitmap;
    }
    private void FreeBuffer()
    {
        if (_previousBitmap != IntPtr.Zero && _dc != IntPtr.Zero) LayeredWindowNative.SelectObject(_dc, _previousBitmap);
        if (_bitmap != IntPtr.Zero) LayeredWindowNative.DeleteObject(_bitmap);
        if (_dc != IntPtr.Zero) LayeredWindowNative.DeleteDC(_dc);
        _bits = _bitmap = _dc = _previousBitmap = IntPtr.Zero;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop.Cancel(); _active.Set();
        if (_thread == null) { lock (_bufferLock) FreeBuffer(); }
        bool finished = _thread == null || !_thread.IsAlive || _thread.Join(3000);
        if (finished) { _active.Dispose(); _stop.Dispose(); }
    }
}
