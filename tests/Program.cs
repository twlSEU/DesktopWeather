using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DesktopWeather.Models;
using DesktopWeather.Rendering;
using DesktopWeather.Services;

int count = 0;
void Check(bool result, string message) { if (!result) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
var pacer = new FramePacer();
int updates = 0;
for (int i = 0; i < 288; i++) if (pacer.TryAdvance(i / 144.0, i / 144.0, 0, out _)) updates++;
Check(updates == 288, "Smooth mode follows every compositor frame on a high-refresh display");
Check(!pacer.TryAdvance(287 / 144.0, 3, 0, out _), "Duplicate rendering notifications do not produce duplicate updates");
pacer.Reset(); updates = 0;
for (int i = 0; i < 288; i++) if (pacer.TryAdvance(i / 144.0, i / 144.0, 30, out _)) updates++;
Check(updates == 60, "30 FPS mode preserves its deadline remainder at 144 Hz");
pacer.Reset(); updates = 0;
for (int i = 0; i < 590; i++) if (pacer.TryAdvance(i / 59.0, i / 59.0, 30, out _)) updates++;
Check(updates == 300, "30 FPS mode keeps its average rate at 59 Hz");
Check(pacer.TryAdvance(50, 50, 30, out double resumed) && resumed <= 0.1, "Long pauses do not cause an animation jump");
var settings = new WeatherSettings { Density = int.MaxValue, Size = double.NaN, Speed = double.PositiveInfinity, Wind = -999, Opacity = 999, Mode = (WeatherMode)999 };
Check(settings.Density == 1600 && settings.Size == 5 && settings.Speed == 65 && settings.Wind == -100 && settings.Opacity == 100 && settings.Mode == WeatherMode.Snow, "Invalid settings are bounded and finite");
var engine = new ParticleEngine(42);
engine.Resize(1920, 1080);
engine.Configure(WeatherMode.Snow, settings.Density);
double beforeY = engine.Particles[0].Y;
engine.Step(double.NaN, settings);
Check(engine.Particles[0].Y == beforeY, "Invalid elapsed time is ignored");
engine.Step(600, settings);
Check(engine.Particles[0].Y - beforeY < 30, "Resuming from a long pause does not jump across the screen");
var distant = engine.Particles.Where(p => p.Depth < 0.38).ToArray();
var foreground = engine.Particles.Where(p => p.Depth >= 0.78).ToArray();
Check(distant.Length > foreground.Length * 2 && foreground.Length > 100 && foreground.Length < 400,
    "Snow has mostly distant flakes and a minority of foreground flakes");
Check(foreground.Average(p => p.SizeFactor) > distant.Average(p => p.SizeFactor) * 2,
    "Snow size follows distance: foreground flakes are larger");
foreach (WeatherParticle p in engine.Particles) p.Y = 500;
engine.Step(0.05, settings);
Check(foreground.Average(p => p.Y - 500) > distant.Average(p => p.Y - 500) * 2,
    "Foreground snow travels faster on screen than distant snow");
double[] snowDepths = engine.Particles.Select(p => p.Depth).ToArray();
var timer = Stopwatch.StartNew();
for (int i = 0; i < 18000; i++) engine.Step(1.0 / 30, settings);
timer.Stop();
Check(engine.Particles.Count == 1600 && engine.Particles.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && p.X > -200 && p.X < 2120 && p.Y > -200 && p.Y < 1280), "Ten simulated minutes keep all 1600 particles bounded");
Check(engine.Particles.Select((p, i) => p.Depth == snowDepths[i]).All(v => v), "Snow depth remains stable through motion and screen wrapping");
Console.WriteLine("Simulation elapsed: " + timer.ElapsedMilliseconds + "ms");
engine.Resize(800, 600);
engine.Resize(0, double.NaN);
settings.Mode = WeatherMode.Leaves;
engine.Configure(settings.Mode, 140);
for (int i = 0; i < 3000; i++) engine.Step(1.0 / 60, settings);
Check(engine.Particles.Count == 140 && engine.Particles.All(p => double.IsFinite(p.X) && double.IsFinite(p.Rotation)), "Mode changes and screen resizes keep particles valid");
engine.Configure(settings.Mode, 0);
Check(engine.Particles.Count == 0, "Zero target removes all particles");
double Travel(WeatherMode mode)
{
    var motion = new ParticleEngine(42);
    var weather = new WeatherSettings { Mode = mode, Speed = 100, Wind = 30 };
    motion.Resize(1920, 1080); motion.Configure(mode, 1);
    WeatherParticle particle = motion.Particles[0];
    particle.Y = 500; particle.FallFactor = 1;
    motion.Step(0.1, weather);
    return particle.Y - 500;
}
Check(Travel(WeatherMode.Petals) < Travel(WeatherMode.Snow) && Travel(WeatherMode.Rain) > Travel(WeatherMode.Snow) * 4,
    "Petals drift more slowly than snow, while rain falls distinctly faster");
var fireflies = new ParticleEngine(12);
var fireflySettings = new WeatherSettings { Mode = WeatherMode.Fireflies };
fireflies.Resize(1920, 1080); fireflies.Configure(WeatherMode.Fireflies, 80);
fireflies.Step(0.1, fireflySettings);
double[] brightness = fireflies.Particles.Select(p => p.Brightness).ToArray();
double[] heights = fireflies.Particles.Select(p => p.Y).ToArray();
for (int i = 0; i < 60; i++) fireflies.Step(1.0 / 60, fireflySettings);
Check(fireflies.Particles.Where((p, i) => Math.Abs(p.Brightness - brightness[i]) > 0.05).Count() > 20 &&
    fireflies.Particles.All(p => p.Brightness >= 0.22 && p.Brightness <= 1), "Fireflies pulse smoothly with independent phases and bounded brightness");
Check(fireflies.Particles.Where((p, i) => p.Y > heights[i]).Any() && fireflies.Particles.Where((p, i) => p.Y < heights[i]).Any(),
    "Fireflies float both upward and downward rather than falling");
foreach (WeatherMode mode in new[] { WeatherMode.Petals, WeatherMode.Fireflies, WeatherMode.Rain })
{
    settings.Mode = mode;
    engine.Configure(mode, 1600);
    for (int i = 0; i < 3000; i++) engine.Step(1.0 / 30, settings, 1.5);
    Check(engine.Particles.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Rotation) &&
        p.X > -200 && p.X < 1000 && p.Y > -200 && p.Y < 800), mode + " keeps particles bounded during long runs");
}
foreach (var preset in new[] { ("Spring", WeatherMode.Petals, 160), ("SummerNight", WeatherMode.Fireflies, 80), ("SummerRain", WeatherMode.Rain, 500) })
{
    WeatherPresets.Apply(settings, preset.Item1);
    Check(settings.Mode == preset.Item2 && settings.Density == preset.Item3, preset.Item1 + " configures the intended seasonal effect");
}
string directory = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "test-data");
var store = new SettingsStore(directory);
Check(store.Save(settings), "Settings can be saved");
Check(store.Load().Mode == settings.Mode && store.Load().Density == settings.Density, "Settings round trip");
foreach (WeatherMode mode in Enum.GetValues<WeatherMode>())
{
    settings.Mode = mode;
    Check(store.Save(settings) && store.Load().Mode == mode, mode + " survives a settings round trip");
}
File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"Mode\":\"Unknown\"}");
Check(store.Load().Mode == WeatherMode.Snow && store.LastError != null, "Unknown weather in a config falls back safely");
Console.WriteLine("All " + count + " core checks passed.");
