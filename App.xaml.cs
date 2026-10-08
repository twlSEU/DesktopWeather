using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DesktopWeather.Interop;
using DesktopWeather.Models;
using DesktopWeather.Services;

namespace DesktopWeather;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private bool _ownsMutex, _exiting;
    private WeatherSettings? _settings;
    private SettingsStore? _store;
    private WeatherController? _controller;
    private MainWindow? _panel;
    private TrayService? _tray;
    private StopHotkeyService? _hotkey;
    private DispatcherTimer? _saveTimer;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length >= 2 && e.Args[0] == "--benchmark")
        {
            WeatherMode mode = e.Args.Length >= 4 && Enum.TryParse(e.Args[3], true, out WeatherMode requested) && Enum.IsDefined(requested) ? requested : WeatherMode.Snow;
            Shutdown(await PerformanceProbe.RunAsync(e.Args[1], e.Args.Length >= 3 && e.Args[2] == "smooth", mode));
            return;
        }
        if (e.Args.Length >= 2 && e.Args[0] == "--self-test")
        {
            int code = await SelfTestRunner.RunAsync(e.Args[1]);
            Shutdown(code);
            return;
        }
        DispatcherUnhandledException += OnUnhandledException;
        try
        {
            _singleInstance = new Mutex(true, "Local\\DesktopWeather.SingleInstance.1", out _ownsMutex);
            if (!_ownsMutex)
            {
                NativeMethods.PostMessage(NativeMethods.HwndBroadcast, StopHotkeyService.ShowMessageId, IntPtr.Zero, IntPtr.Zero);
                Shutdown();
                return;
            }
            _store = new SettingsStore(AppContext.BaseDirectory);
            _settings = _store.Load();
            _controller = new WeatherController(_settings, Dispatcher);
            _panel = new MainWindow(_settings, _controller, ExitApp);
            MainWindow = _panel;
            _panel.Show();
            _hotkey = new StopHotkeyService(_panel, _controller.Stop, _panel.ShowPanel);
            _panel.SetHotkeyAvailable(_hotkey.Registered);
            _tray = new TrayService(_controller, _settings, _panel.ShowPanel, ExitApp);
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _store.Save(_settings); };
            _settings.PropertyChanged += SettingsChanged;
        }
        catch (Exception ex) { ReportError(ex); ExitApp(); }
    }

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_saveTimer == null) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _controller?.Stop();
        ReportError(e.Exception);
        ExitApp();
    }
    private static void ReportError(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "error.log"), DateTimeOffset.Now + Environment.NewLine + ex + Environment.NewLine); }
        catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException) { }
        MessageBox.Show("软件遇到了问题。请重新打开；详细信息保存在软件文件夹的 error.log。\n\n" + ex.Message, "桌面天气", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    internal void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        _saveTimer?.Stop();
        if (_settings != null) _store?.Save(_settings);
        _hotkey?.Dispose();
        _hotkey = null;
        _tray?.Dispose();
        _tray = null;
        _controller?.Dispose();
        _panel?.CloseForExit();
        Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _saveTimer?.Stop();
        if (_settings != null)
        {
            _settings.PropertyChanged -= SettingsChanged;
            _store?.Save(_settings);
        }
        _hotkey?.Dispose();
        _tray?.Dispose();
        _controller?.Dispose();
        if (_ownsMutex) _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
