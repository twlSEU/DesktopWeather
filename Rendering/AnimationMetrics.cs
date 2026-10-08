using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DesktopWeather.Rendering;

public sealed class AnimationMetrics
{
    private long _previous, _fpsStart;
    private int _fpsFrames;
    private bool _capture;
    private readonly object _sync = new();
    private readonly List<double> _intervals = new(4096);
    public long TotalFrames { get; private set; }
    public double FramesPerSecond { get; private set; }
    public double[] IntervalsMilliseconds { get { lock (_sync) return _intervals.ToArray(); } }
    public int CapturedFrames { get; private set; }
    public void RecordFrame()
    {
        lock (_sync)
        {
            long now = Stopwatch.GetTimestamp();
            TotalFrames++;
            if (_capture)
            {
                CapturedFrames++;
                if (_previous != 0) _intervals.Add((now - _previous) * 1000.0 / Stopwatch.Frequency);
            }
            _previous = now;
            if (_fpsStart == 0) _fpsStart = now;
            _fpsFrames++;
            double seconds = (now - _fpsStart) / (double)Stopwatch.Frequency;
            if (seconds >= 0.75)
            {
                FramesPerSecond = _fpsFrames / seconds;
                _fpsStart = now;
                _fpsFrames = 0;
            }
        }
    }
    public void StartCapture()
    {
        lock (_sync)
        {
            _intervals.Clear();
            CapturedFrames = 0;
            _previous = 0;
            _capture = true;
        }
    }
    public void ResetClock() { lock (_sync) { _previous = 0; _fpsStart = 0; _fpsFrames = 0; FramesPerSecond = 0; } }
}
