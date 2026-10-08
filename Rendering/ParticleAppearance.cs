using DesktopWeather.Models;

namespace DesktopWeather.Rendering;

internal static class ParticleAppearance
{
    internal static (double Width, double Height) Extents(WeatherMode mode, double depth = 0.5) => mode switch
    {
        WeatherMode.Snow => (SnowAppearance.Extent(depth), SnowAppearance.Extent(depth)),
        WeatherMode.Leaves => (1.05, 1.8), WeatherMode.Petals => (1.05, 1.4),
        WeatherMode.Fireflies => (1.6, 1.6), WeatherMode.Rain => (0.18, 3.4), _ => (1, 1)
    };
    internal static double Opacity(WeatherMode mode, WeatherParticle particle) =>
        mode == WeatherMode.Snow ? SnowAppearance.Opacity(particle.Depth) :
            (0.3 + 0.7 * particle.Depth) * (mode == WeatherMode.Fireflies ? particle.Brightness : mode == WeatherMode.Rain ? 0.65 : 1);
}
