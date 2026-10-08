using System;

namespace DesktopWeather.Rendering;

// A zero target follows composition frames; capped mode retains its deadline remainder.
public sealed class FramePacer
{
    private double _composition = double.NegativeInfinity;
    private double _lastUpdate = double.NaN, _nextDeadline;
    public bool TryAdvance(double compositionSeconds, double wallSeconds, int targetFps, out double seconds)
    {
        seconds = 0;
        if (!double.IsFinite(compositionSeconds) || !double.IsFinite(wallSeconds)) return false;
        if (compositionSeconds <= _composition) return false;
        _composition = compositionSeconds;
        if (double.IsNaN(_lastUpdate)) { _lastUpdate = wallSeconds; _nextDeadline = wallSeconds; }
        if (targetFps > 0)
        {
            if (wallSeconds + 0.0002 < _nextDeadline) return false;
            double interval = 1.0 / targetFps;
            _nextDeadline += interval;
            if (_nextDeadline < wallSeconds - interval) _nextDeadline = wallSeconds + interval;
        }
        seconds = Math.Clamp(wallSeconds - _lastUpdate, 0, 0.1);
        _lastUpdate = wallSeconds;
        return true;
    }
    public void Reset() { _composition = double.NegativeInfinity; _lastUpdate = double.NaN; _nextDeadline = 0; }
}
