namespace DesktopWeather.Models;

internal static class WeatherPresets
{
    internal static void Apply(WeatherSettings settings, string? name)
    {
        var values = name switch
        {
            "SoftSnow" => (WeatherMode.Snow, 180, 4.0, 40.0, 5.0, 75.0),
            "HeavySnow" => (WeatherMode.Snow, 700, 7.0, 100.0, 25.0, 90.0),
            "Autumn" => (WeatherMode.Leaves, 140, 9.0, 55.0, 35.0, 85.0),
            "Spring" => (WeatherMode.Petals, 160, 7.0, 38.0, 18.0, 85.0),
            "SummerNight" => (WeatherMode.Fireflies, 80, 3.5, 35.0, 5.0, 90.0),
            "SummerRain" => (WeatherMode.Rain, 500, 5.0, 120.0, 12.0, 65.0),
            _ => (settings.Mode, settings.Density, settings.Size, settings.Speed, settings.Wind, settings.Opacity)
        };
        (settings.Mode, settings.Density, settings.Size, settings.Speed, settings.Wind, settings.Opacity) = values;
    }
}
