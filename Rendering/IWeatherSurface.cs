using System;
using System.Windows.Media.Imaging;

namespace DesktopWeather.Rendering;

internal interface IWeatherSurface : IDisposable
{
    int ParticleCount { get; }
    bool IsAnimating { get; }
    bool AnimationEnabled { get; set; }
    AnimationMetrics Metrics { get; }
    BitmapSource Capture();
}
