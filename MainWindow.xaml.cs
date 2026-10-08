using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopWeather.Models;
using DesktopWeather.Services;

namespace DesktopWeather;

public partial class MainWindow : Window
{
    private readonly WeatherSettings _settings;
    private readonly WeatherController _controller;
    private readonly Action _exit;
    private bool _allowClose;
    private bool _closed;
    private bool _hotkeyAvailable = true;
    private readonly DispatcherTimer _fpsTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    internal MainWindow(WeatherSettings settings, WeatherController controller, Action exit)
    {
        _settings = settings;
        _controller = controller;
        _exit = exit;
        InitializeComponent();
        DataContext = settings;
        PreviewSurface.Configure(settings, preview: true);
        using (var icon = IconFactory.Create())
        {
            Icon = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            Icon.Freeze();
        }
        _controller.StateChanged += ControllerStateChanged;
        _settings.PropertyChanged += SettingsChanged;
        StateChanged += (_, _) => UpdatePreviewPlayback();
        IsVisibleChanged += (_, _) => { if (IsVisible) _fpsTimer.Start(); else _fpsTimer.Stop(); };
        _fpsTimer.Tick += (_, _) => UpdateFrameRate();
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _closed = true;
            _fpsTimer.Stop();
            PreviewSurface.Dispose();
            _controller.StateChanged -= ControllerStateChanged;
            _settings.PropertyChanged -= SettingsChanged;
        };
        Loaded += (_, _) =>
        {
            Rect work = SystemParameters.WorkArea;
            if (Height > work.Height) Height = Math.Max(MinHeight, work.Height - 16);
            if (Width > work.Width) Width = Math.Max(MinWidth, work.Width - 16);
            _fpsTimer.Start();
        };
        RefreshUi();
    }

    internal void SetHotkeyAvailable(bool available) { _hotkeyAvailable = available; RefreshUi(); }
    internal void ShowPanel()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }
    internal void CloseForExit() { _allowClose = true; if (!_closed) Close(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        if (_settings.MinimizeToTray) { e.Cancel = true; Hide(); }
        else { _allowClose = true; Dispatcher.BeginInvoke(_exit); }
    }
    private void PrimaryButtonClick(object sender, RoutedEventArgs e)
    {
        if (_controller.IsRunning) _controller.Stop(); else _controller.Start();
    }
    private void PauseButtonClick(object sender, RoutedEventArgs e) => _controller.TogglePause();
    private void ExitButtonClick(object sender, RoutedEventArgs e) => _exit();
    private void MinimizeButtonClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseButtonClick(object sender, RoutedEventArgs e) => Close();
    private void PreviewFrameSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement frame) frame.Clip = new RectangleGeometry(new Rect(0, 0, frame.ActualWidth, frame.ActualHeight), 15, 15);
    }
    private void ControllerStateChanged(object? sender, EventArgs e) => RefreshUi();
    private void SettingsChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(WeatherSettings.Mode)) RefreshUi(); }
    private void RefreshUi()
    {
        PrimaryButton.Content = _controller.IsRunning ? "停止特效" : WeatherModeInfo.StartLabel(_settings.Mode);
        PauseButton.IsEnabled = _controller.IsRunning;
        PauseButton.Content = _controller.IsPaused ? "继续" : "暂停";
        StateTitle.Text = !_controller.IsRunning ? "特效已停止" : _controller.IsPaused ? "特效已暂停" : WeatherModeInfo.RunningLabel(_settings.Mode) + " · " + _controller.ScreenCount + " 个屏幕";
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_controller.IsRunning ? (_controller.IsPaused ? "#DEAA59" : "#7DE1ED") : "#62748D"));
        StateDetail.Text = _hotkeyAvailable ? "Ctrl + Alt + F12 可随时停止特效" : "停止快捷键被占用，请使用面板或托盘停止特效";
        PreviewTitle.Text = WeatherModeInfo.Title(_settings.Mode);
        PreviewSubtitle.Text = WeatherModeInfo.Subtitle(_settings.Mode);
        UpdatePreviewPlayback();
        UpdateFrameRate();
    }
    private void UpdatePreviewPlayback()
    {
        PreviewSurface.AnimationEnabled = !_controller.IsRunning && WindowState != WindowState.Minimized;
        PreviewSurface.Visibility = _controller.IsRunning ? Visibility.Hidden : Visibility.Visible;
    }
    private void UpdateFrameRate()
    {
        if (_controller.IsPaused) { FpsText.Text = "已暂停"; return; }
        var source = _controller.IsRunning && _controller.Overlays.Count > 0 ? _controller.Overlays[0].Surface : PreviewSurface;
        FpsText.Text = source.Metrics.FramesPerSecond > 0 ? source.Metrics.FramesPerSecond.ToString("0") + " FPS" : "测量中";
    }
    private void PresetButtonClick(object sender, RoutedEventArgs e)
        => WeatherPresets.Apply(_settings, (sender as Button)?.Tag?.ToString());
}
