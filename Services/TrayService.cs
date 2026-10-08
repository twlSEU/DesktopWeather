using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using DesktopWeather.Models;
using Forms = System.Windows.Forms;

namespace DesktopWeather.Services;

internal sealed class TrayService : IDisposable
{
    private readonly WeatherController _controller;
    private readonly WeatherSettings _settings;
    private readonly Forms.NotifyIcon _tray;
    private readonly Icon _icon;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _toggle = new("开始下雪");
    private readonly Forms.ToolStripMenuItem _pause = new("暂停特效");
    private readonly Dictionary<WeatherMode, Forms.ToolStripMenuItem> _modes = new();
    private readonly Action _showSettings;
    private readonly Action _exit;

    internal TrayService(WeatherController controller, WeatherSettings settings, Action showSettings, Action exit)
    {
        _controller = controller;
        _settings = settings;
        _showSettings = showSettings;
        _exit = exit;
        _icon = IconFactory.Create();
        var show = new Forms.ToolStripMenuItem("打开设置");
        show.Click += (_, _) => OnUi(_showSettings);
        _toggle.Click += (_, _) => OnUi(() => { if (_controller.IsRunning) _controller.Stop(); else _controller.Start(); });
        _pause.Click += (_, _) => OnUi(_controller.TogglePause);
        var exitItem = new Forms.ToolStripMenuItem("退出软件");
        exitItem.Click += (_, _) => OnUi(_exit);
        _menu.Items.AddRange(new Forms.ToolStripItem[] { show, new Forms.ToolStripSeparator(), _toggle, _pause, new Forms.ToolStripSeparator() });
        foreach (WeatherMode mode in new[] { WeatherMode.Petals, WeatherMode.Fireflies, WeatherMode.Rain, WeatherMode.Leaves, WeatherMode.Snow })
        {
            var item = new Forms.ToolStripMenuItem(WeatherModeInfo.Name(mode));
            item.Click += (_, _) => OnUi(() => _settings.Mode = mode);
            _modes.Add(mode, item);
            _menu.Items.Add(item);
        }
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(exitItem);
        _tray = new Forms.NotifyIcon { Icon = _icon, Text = "桌面天气 · 点击打开设置", ContextMenuStrip = _menu, Visible = true };
        _tray.DoubleClick += (_, _) => OnUi(_showSettings);
        _tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) OnUi(_showSettings); };
        _controller.StateChanged += StateChanged;
        Update();
    }
    private static void OnUi(Action action) => Application.Current.Dispatcher.BeginInvoke(action);
    private void StateChanged(object? sender, EventArgs e) => Update();
    private void Update()
    {
        _toggle.Text = _controller.IsRunning ? "停止特效" : WeatherModeInfo.StartLabel(_settings.Mode);
        _pause.Enabled = _controller.IsRunning;
        _pause.Text = _controller.IsPaused ? "继续特效" : "暂停特效";
        foreach (var item in _modes) item.Value.Checked = _settings.Mode == item.Key;
        _tray.Text = "桌面天气 · " + (_controller.IsRunning ? (_controller.IsPaused ? "已暂停" : WeatherModeInfo.RunningLabel(_settings.Mode)) : "已停止");
    }
    public void Dispose()
    {
        _controller.StateChanged -= StateChanged;
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}
