using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopWeather.Interop;
using DesktopWeather.Models;
using DesktopWeather.Rendering;
using DesktopWeather.Services;

namespace DesktopWeather;

// Runs against this application's own windows, without interacting with other applications.
internal static class SelfTestRunner
{
    internal static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var settings = new WeatherSettings();
        using var controller = new WeatherController(settings, Application.Current.Dispatcher);
        MainWindow? panel = null;
        bool exitRequested = false;
        StopHotkeyService? hotkey = null;
        TrayService? tray = null;
        try
        {
            panel = new MainWindow(settings, controller, () => { exitRequested = true; panel?.CloseForExit(); });
            Application.Current.MainWindow = panel;
            panel.Show();
            await Task.Delay(350);
            Check(!controller.IsRunning, "启动时只有预览，未自动开启桌面特效", checks);
            Check(panel.PreviewSurface.ParticleCount > 0 && panel.PreviewSurface.IsAnimating, "设置面板预览正在绘制", checks);
            CheckSnowFocus(directory, checks);
            Capture(panel, Path.Combine(directory, "snow-settings.png"));
            using (var icon = IconFactory.Create())
            using (var stream = File.Create(Path.Combine(directory, "app.ico"))) icon.Save(stream);
            tray = new TrayService(controller, settings, panel.ShowPanel, () => { exitRequested = true; });
            checks.Add("托盘图标和控制菜单初始化成功");
            double originalWidth = panel.Width, originalHeight = panel.Height;
            panel.Width = 900;
            panel.Height = 620;
            await Task.Delay(100);
            Capture(panel, Path.Combine(directory, "compact-settings.png"));
            panel.Width = originalWidth;
            panel.Height = originalHeight;
            await Task.Delay(100);

            IntPtr foreground = NativeMethods.GetForegroundWindow();
            panel.PrimaryButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(250);
            Check(controller.IsRunning && controller.ScreenCount > 0, "开始按钮创建屏幕特效窗口", checks);
            Check(!panel.PreviewSurface.IsAnimating, "桌面特效开启时不重复播放面板预览", checks);
            Check(NativeMethods.GetForegroundWindow() == foreground, "特效窗口没有抢走前台焦点", checks);
            foreach (OverlayWindow overlay in controller.Overlays)
            {
                long style = NativeMethods.GetExtendedStyle(overlay.Handle);
                long required = NativeMethods.WsExTransparent | NativeMethods.WsExLayered | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow | NativeMethods.WsExTopmost;
                Check((style & required) == required, "特效窗口具有透明、穿透、置顶和不激活属性", checks);
                Check(NativeMethods.SendMessage(overlay.Handle, NativeMethods.WmNcHitTest, IntPtr.Zero, IntPtr.Zero).ToInt32() == -1, "特效窗口命中测试返回鼠标穿透", checks);
                Check(NativeMethods.GetWindowRect(overlay.Handle, out var bounds) &&
                    bounds.Left == overlay.ScreenBounds.Left && bounds.Top == overlay.ScreenBounds.Top &&
                    bounds.Right == overlay.ScreenBounds.Right && bounds.Bottom == overlay.ScreenBounds.Bottom,
                    "特效窗口覆盖对应显示器的物理边界", checks);
            }
            CheckTransparentSurface(controller.Overlays[0].Surface, Path.Combine(directory, "snow-overlay.png"), checks);
            panel.DensitySlider.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, 550.0);
            await Task.Delay(80);
            Check(settings.Density == 550 && Math.Abs(controller.Overlays.Sum(o => o.Surface.ParticleCount) - 550) <= controller.ScreenCount,
                "数量滑块实时更新屏幕特效", checks);
            panel.PauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(controller.IsPaused && controller.Overlays.All(o => !o.Surface.IsAnimating), "暂停后停止动画回调", checks);
            panel.PauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!controller.IsPaused && controller.Overlays.All(o => o.Surface.IsAnimating), "继续按钮恢复动画", checks);

            panel.LeavesChoice.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            await Task.Delay(150);
            Check(settings.Mode == WeatherMode.Leaves, "天气选择按钮切换为落叶模式", checks);
            Capture(panel, Path.Combine(directory, "leaves-settings.png"));
            CheckTransparentSurface(controller.Overlays[0].Surface, Path.Combine(directory, "leaves-overlay.png"), checks);
            panel.SnowChoice.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            await Task.Delay(150);
            BitmapSource switchedSnow = controller.Overlays[0].Surface.Capture();
            var snowPixels = new uint[switchedSnow.PixelWidth * switchedSnow.PixelHeight];
            switchedSnow.CopyPixels(snowPixels, switchedSnow.PixelWidth * 4, 0);
            Check(snowPixels.Any(pixel => pixel != 0) && snowPixels.All(pixel => pixel == (pixel >> 24) * 0x01010101),
                "从彩色落叶切回雪花时，白色粒子混合正确且无残留色彩", checks);
            foreach (var effect in new[] {
                (Choice: panel.PetalsChoice, Mode: WeatherMode.Petals, Preset: panel.SpringPreset, Count: 160),
                (Choice: panel.FirefliesChoice, Mode: WeatherMode.Fireflies, Preset: panel.SummerNightPreset, Count: 80),
                (Choice: panel.RainChoice, Mode: WeatherMode.Rain, Preset: panel.SummerRainPreset, Count: 500) })
            {
                IntPtr handle = controller.Overlays[0].Handle;
                effect.Choice.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                await Task.Delay(250);
                Check(settings.Mode == effect.Mode && controller.Overlays[0].Handle == handle &&
                    panel.PreviewTitle.Text == WeatherModeInfo.Title(effect.Mode), WeatherModeInfo.Name(effect.Mode) + "实时切换，无需重建特效窗口", checks);
                CheckTransparentSurface(controller.Overlays[0].Surface, Path.Combine(directory, effect.Mode + "-overlay.png"), checks, effect.Mode);
                controller.Stop();
                effect.Preset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(250);
                Check(settings.Mode == effect.Mode && settings.Density == effect.Count &&
                    panel.PrimaryButton.Content.ToString() == WeatherModeInfo.StartLabel(effect.Mode), WeatherModeInfo.Name(effect.Mode) + "预设和开始按钮生效", checks);
                Check(panel.PreviewSurface.IsAnimating, WeatherModeInfo.Name(effect.Mode) + "面板预览恢复播放", checks);
                Capture(panel, Path.Combine(directory, effect.Mode + "-settings.png"));
                CheckTransparentSurface(panel.PreviewSurface, Path.Combine(directory, effect.Mode + "-preview.png"), checks, effect.Mode);
                panel.PrimaryButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(100);
            }
            settings.AllMonitors = false;
            await Task.Delay(100);
            Check(controller.ScreenCount == 1, "主屏幕设置生效", checks);
            settings.AllMonitors = true;
            await Task.Delay(100);
            Check(controller.ScreenCount == System.Windows.Forms.Screen.AllScreens.Length, "所有屏幕设置生效", checks);

            panel.Close();
            await Task.Delay(60);
            Check(!panel.IsVisible && !panel.PreviewSurface.IsAnimating && controller.IsRunning, "关闭面板后保留特效，预览停止消耗", checks);
            panel.ShowPanel();
            await Task.Delay(60);
            Check(!panel.PreviewSurface.IsAnimating && controller.IsRunning, "重新打开面板后仍只有桌面特效在运行", checks);
            hotkey = new StopHotkeyService(panel, controller.Stop, panel.ShowPanel);
            NativeMethods.SendMessage(new WindowInteropHelper(panel).Handle, NativeMethods.WmHotkey, new IntPtr(StopHotkeyService.HotkeyId), IntPtr.Zero);
            Check(!controller.IsRunning && controller.ScreenCount == 0, "停止快捷键消息关闭全部特效窗口", checks);
            Check(panel.PreviewSurface.IsAnimating, "停止桌面特效后恢复面板预览", checks);
            checks.Add("全局停止快捷键注册：" + (hotkey.Registered ? "成功" : "已被其他程序占用，面板会提示"));

            var store = new SettingsStore(Path.Combine(directory, "config-test"));
            Check(store.Save(settings), "设置成功写入文件", checks);
            WeatherSettings loaded = store.Load();
            Check(loaded.Density == settings.Density && loaded.Mode == settings.Mode && loaded.AllMonitors == settings.AllMonitors,
                "再次读取保留所选天气和参数", checks);
            File.WriteAllText(Path.Combine(directory, "config-test", "settings.json"), "{broken");
            Check(store.Load().Density == 260 && store.LastError != null, "损坏的设置文件安全恢复为默认设置", checks);
            hotkey.Dispose();
            hotkey = null;
            settings.MinimizeToTray = false;
            panel.Close();
            await Task.Delay(60);
            Check(exitRequested && !panel.IsVisible, "关闭托盘驻留后，关闭面板会请求退出软件", checks);
            File.WriteAllText(Path.Combine(directory, "self-test.json"), JsonSerializer.Serialize(new { passed = true, checks }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(directory, "self-test.json"), JsonSerializer.Serialize(new { passed = false, checks, error = ex.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
        finally { hotkey?.Dispose(); tray?.Dispose(); controller.Stop(); panel?.CloseForExit(); }
    }

    private static void Check(bool result, string description, List<string> checks)
    {
        if (!result) throw new InvalidOperationException(description);
        checks.Add(description);
    }
    private static void CheckSnowFocus(string directory, List<string> checks)
    {
        double SoftEdgeFraction(BitmapSource bitmap)
        {
            var pixels = new uint[bitmap.PixelWidth * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            int visible = pixels.Count(pixel => (pixel >> 24) >= 8);
            return pixels.Count(pixel => (pixel >> 24) >= 8 && (pixel >> 24) < 240) / (double)visible;
        }
        Check(SoftEdgeFraction(ParticleSprites.GetSnow(4)) > SoftEdgeFraction(ParticleSprites.GetSnow(0)) + 0.25,
            "近景雪花纹理的柔化边缘明显多于清晰远景", checks);
        bool premultiplied = true;
        bool white = true;
        for (int i = 0; i < SnowAppearance.SpriteCount; i++)
        {
            BitmapSource texture = ParticleSprites.GetSnow(i);
            var pixels = new uint[texture.PixelWidth * texture.PixelHeight];
            texture.CopyPixels(pixels, texture.PixelWidth * 4, 0);
            foreach (uint pixel in pixels)
            {
                uint alpha = pixel >> 24;
                premultiplied &= (pixel & 255) <= alpha && ((pixel >> 8) & 255) <= alpha && ((pixel >> 16) & 255) <= alpha;
                white &= pixel == alpha * 0x01010101;
            }
        }
        Check(premultiplied, "所有景深雪花纹理保持正确的透明度预乘", checks);
        Check(white, "雪花纹理满足白色像素混合的颜色条件", checks);
        var atlas = new NativeSpriteAtlas();
        var far = atlas.Get(WeatherMode.Snow, 0, 7, 0, 0.2);
        var near = atlas.Get(WeatherMode.Snow, 0, 7, 0, 0.95);
        Check(near.Width > far.Width && near.Height > far.Height,
            "原生桌面绘制按距离选择不同纹理与虚焦范围", checks);

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 33, 53)), null, new Rect(0, 0, 540, 170));
            string[] titles = { "远景 · 边缘清晰", "中景 · 轻微柔化", "近景 · 虚焦散开" };
            int[] textures = { 0, 2, 4 };
            for (int i = 0; i < 3; i++)
            {
                dc.DrawImage(ParticleSprites.GetSnow(textures[i]), new Rect(i * 180 + 58, 45, 64, 64));
                var text = new FormattedText(titles[i], System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 13, Brushes.White, 1);
                dc.DrawText(text, new Point(i * 180 + (180 - text.Width) / 2, 125));
            }
        }
        var image = new RenderTargetBitmap(540, 170, 96, 96, PixelFormats.Pbgra32);
        image.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(Path.Combine(directory, "snow-focus-textures.png")); encoder.Save(stream);
    }
    private static RenderTargetBitmap Render(FrameworkElement element)
    {
        element.UpdateLayout();
        var image = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        image.Render(element);
        return image;
    }
    private static void Capture(FrameworkElement element, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Render(element)));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
    private static void CheckTransparentSurface(IWeatherSurface surface, string path, List<string> checks, WeatherMode? mode = null)
    {
        BitmapSource image = surface.Capture();
        byte[] pixels = new byte[image.PixelWidth * image.PixelHeight * 4];
        image.CopyPixels(pixels, image.PixelWidth * 4, 0);
        int drawn = 0;
        int colored = 0;
        bool premultiplied = true;
        for (int i = 3; i < pixels.Length; i += 4)
        {
            byte alpha = pixels[i];
            if (alpha > 0) drawn++;
            if (pixels[i - 3] > alpha || pixels[i - 2] > alpha || pixels[i - 1] > alpha) premultiplied = false;
            int blue = pixels[i - 3], green = pixels[i - 2], red = pixels[i - 1];
            if (mode == WeatherMode.Petals && red > green + 10 ||
                mode == WeatherMode.Fireflies && green > blue + 10 && red > blue + 10 ||
                mode == WeatherMode.Rain && blue > red + 10) colored++;
        }
        Check(premultiplied, "透明粒子的颜色正确预乘，不出现不透明背景", checks);
        Check(drawn > 0 && drawn < image.PixelWidth * image.PixelHeight / 3, "特效绘制包含粒子，背景保持透明", checks);
        if (mode.HasValue) Check(colored > 0, WeatherModeInfo.Name(mode.Value) + "绘制了对应颜色的专用粒子", checks);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
