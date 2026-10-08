using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using DesktopWeather.Models;
using DesktopWeather.Services;

namespace DesktopWeather;

internal static class PerformanceProbe
{
    internal static async Task<int> RunAsync(string directory, bool smooth, WeatherMode mode = WeatherMode.Snow)
    {
        Directory.CreateDirectory(directory);
        var settings = new WeatherSettings { Density = 700, Size = 7, Speed = 100, Wind = 25, Opacity = 90,
            EnergySaving = !smooth, Mode = mode };
        using var controller = new WeatherController(settings, Application.Current.Dispatcher);
        MainWindow? panel = null;
        try
        {
            panel = new MainWindow(settings, controller, () => { });
            Application.Current.MainWindow = panel;
            panel.Show();
            controller.Start();
            await Task.Delay(2000);
            foreach (var overlay in controller.Overlays) overlay.Surface.Metrics.StartCapture();
            using var process = Process.GetCurrentProcess();
            TimeSpan cpuStart = process.TotalProcessorTime;
            long allocatedStart = GC.GetTotalAllocatedBytes(true);
            int[] collections = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            var clock = Stopwatch.StartNew();
            await Task.Delay(5000);
            clock.Stop();
            process.Refresh();
            var samples = controller.Overlays.Select(o =>
            {
                double[] sorted = o.Surface.Metrics.IntervalsMilliseconds.OrderBy(v => v).ToArray();
                return new {
                    Bounds = o.ScreenBounds.ToString(),
                    Particles = o.Surface.ParticleCount,
                    Frames = o.Surface.Metrics.CapturedFrames,
                    Fps = o.Surface.Metrics.CapturedFrames / clock.Elapsed.TotalSeconds,
                    MeanFrameMs = sorted.Length > 0 ? sorted.Average() : 0,
                    P95FrameMs = sorted.Length > 0 ? sorted[(int)((sorted.Length - 1) * 0.95)] : 0,
                    MaxFrameMs = sorted.Length > 0 ? sorted[^1] : 0
                };
            }).ToArray();
            File.WriteAllText(Path.Combine(directory, "performance.json"), JsonSerializer.Serialize(new {
                Mode = settings.Mode.ToString(), Smooth = smooth, RenderTier = RenderCapability.Tier >> 16,
                Seconds = clock.Elapsed.TotalSeconds,
                CpuCorePercent = (process.TotalProcessorTime - cpuStart).TotalSeconds / clock.Elapsed.TotalSeconds * 100,
                ManagedAllocationBytes = GC.GetTotalAllocatedBytes(true) - allocatedStart,
                Gen0Collections = GC.CollectionCount(0) - collections[0],
                Gen1Collections = GC.CollectionCount(1) - collections[1],
                Gen2Collections = GC.CollectionCount(2) - collections[2], Screens = samples
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "error.txt"), ex.ToString()); return 1; }
        finally { controller.Stop(); panel?.CloseForExit(); }
    }
}
