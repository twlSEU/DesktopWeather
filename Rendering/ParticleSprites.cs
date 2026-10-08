using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopWeather.Models;

namespace DesktopWeather.Rendering;

// Shared small textures. Paths are rasterised once, never inside an animation frame.
internal static class ParticleSprites
{
    private static readonly BitmapSource[] Snow = {
        CreateSnow(false), CreateSnow(true),
        SoftenSnow(CreateSnow(false), 1.1), SoftenSnow(CreateSnow(true), 0.8),
        SoftenSnow(CreateSnow(false), 4.4), SoftenSnow(CreateSnowCloud(), 5)
    };
    private static readonly BitmapSource[] Leaves = {
        CreateLeaf(Color.FromRgb(232, 172, 67)), CreateLeaf(Color.FromRgb(222, 121, 54)),
        CreateLeaf(Color.FromRgb(196, 88, 51)), CreateLeaf(Color.FromRgb(176, 160, 63)),
        CreateLeaf(Color.FromRgb(231, 191, 103))
    };
    private static readonly BitmapSource[] Petals = {
        CreatePetal(Color.FromRgb(255, 207, 223)), CreatePetal(Color.FromRgb(255, 226, 236)),
        CreatePetal(Color.FromRgb(249, 166, 200)), CreatePetal(Color.FromRgb(255, 241, 245)),
        CreatePetal(Color.FromRgb(244, 187, 215))
    };
    private static readonly BitmapSource[] Fireflies = {
        CreateFirefly(Color.FromRgb(219, 255, 126)), CreateFirefly(Color.FromRgb(255, 238, 140)),
        CreateFirefly(Color.FromRgb(181, 241, 133))
    };
    private static readonly BitmapSource Rain = CreateRain();
    internal static BitmapSource Get(WeatherMode mode, int variant, double radius, double depth = 0.5) => mode switch
    {
        WeatherMode.Leaves => Leaves[variant % Leaves.Length],
        WeatherMode.Petals => Petals[variant % Petals.Length],
        WeatherMode.Fireflies => Fireflies[variant % Fireflies.Length],
        WeatherMode.Rain => Rain,
        _ => Snow[SnowAppearance.SpriteIndex(variant, radius, depth)]
    };
    internal static BitmapSource GetSnow(int index) => Snow[index];

    private static BitmapSource CreateSnow(bool detailed)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new MatrixTransform(29, 0, 0, 29, 32, 32));
            if (!detailed) dc.DrawEllipse(Brushes.White, null, new Point(0, 0), 0.58, 0.58);
            else
            {
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    for (int i = 0; i < 6; i++)
                    {
                        double angle = i * Math.PI / 3;
                        Point tip = new(Math.Cos(angle), Math.Sin(angle));
                        context.BeginFigure(new Point(0, 0), false, false);
                        context.LineTo(tip, true, false);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Point branch = new(tip.X * 0.58, tip.Y * 0.58);
                            context.BeginFigure(branch, false, false);
                            context.LineTo(new Point(branch.X + Math.Cos(angle + side * Math.PI / 3) * 0.27,
                                branch.Y + Math.Sin(angle + side * Math.PI / 3) * 0.27), true, false);
                        }
                    }
                }
                geometry.Freeze();
                var pen = new Pen(Brushes.White, 0.1); pen.Freeze();
                dc.DrawGeometry(null, pen, geometry);
            }
            dc.Pop();
        }
        return Render(visual, 64, 64);
    }
    private static BitmapSource CreateSnowCloud()
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawEllipse(Brushes.White, null, new Point(32, 32), 12, 16);
            dc.DrawEllipse(Brushes.White, null, new Point(27, 36), 9, 10);
        }
        return Render(visual, 64, 64);
    }
    private static BitmapSource SoftenSnow(BitmapSource source, double sigma)
    {
        int width = source.PixelWidth, height = source.PixelHeight, reach = (int)Math.Ceiling(sigma * 3);
        var input = new uint[width * height];
        source.CopyPixels(input, width * 4, 0);
        var kernel = new double[reach * 2 + 1];
        double total = 0;
        for (int offset = -reach; offset <= reach; offset++)
        {
            double weight = Math.Exp(-offset * offset / (2 * sigma * sigma));
            kernel[offset + reach] = weight;
            total += weight;
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= total;
        var horizontal = new double[input.Length];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double alpha = 0;
            for (int offset = -reach; offset <= reach; offset++)
            {
                int sampleX = x + offset;
                if (sampleX >= 0 && sampleX < width) alpha += (input[y * width + sampleX] >> 24) * kernel[offset + reach];
            }
            horizontal[y * width + x] = alpha;
        }
        var pixels = new uint[input.Length];
        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
        {
            double alpha = 0;
            for (int offset = -reach; offset <= reach; offset++)
            {
                int sampleY = y + offset;
                if (sampleY >= 0 && sampleY < height) alpha += horizontal[sampleY * width + x] * kernel[offset + reach];
            }
            uint value = (uint)Math.Clamp((int)Math.Round(alpha), 0, 255);
            // These are white flakes: RGB equals alpha in premultiplied BGRA.
            pixels[y * width + x] = value * 0x01010101;
        }
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }
    private static BitmapSource CreateLeaf(Color color)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new MatrixTransform(29, 0, 0, 47, 32, 54));
            Geometry leaf = Geometry.Parse("M 0,-1 C 0.85,-0.68 1.05,0.35 0,1 C -1.05,0.35 -0.85,-0.68 0,-1 Z");
            leaf.Freeze();
            var brush = new SolidColorBrush(color); brush.Freeze();
            var vein = new Pen(new SolidColorBrush(Color.FromArgb(125, 98, 61, 31)), 0.055); vein.Freeze();
            dc.DrawGeometry(brush, null, leaf);
            dc.DrawLine(vein, new Point(0, -0.8), new Point(0, 1.1));
            dc.DrawLine(vein, new Point(0, 0.1), new Point(0.4, -0.2));
            dc.DrawLine(vein, new Point(0, 0.45), new Point(-0.4, 0.05));
            dc.Pop();
        }
        return Render(visual, 64, 112);
    }
    private static BitmapSource CreatePetal(Color color)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new MatrixTransform(28, 0, 0, 39, 32, 44));
            Geometry petal = Geometry.Parse("M 0,-0.66 C 0.38,-1.2 0.95,-0.9 0.88,-0.32 C 0.83,0.3 0.3,0.83 0,1 C -0.28,0.78 -0.81,0.2 -0.88,-0.32 C -0.96,-0.91 -0.4,-1.19 0,-0.66 Z");
            petal.Freeze();
            var fill = new LinearGradientBrush(Color.FromRgb(255, 245, 249), color, new Point(0, 0), new Point(0.8, 1));
            fill.Freeze();
            var fold = new Pen(new SolidColorBrush(Color.FromArgb(95, 225, 120, 166)), 0.04); fold.Freeze();
            dc.DrawGeometry(fill, null, petal);
            dc.DrawLine(fold, new Point(0, -0.4), new Point(0, 0.8));
            dc.Pop();
        }
        return Render(visual, 64, 88);
    }
    private static BitmapSource CreateFirefly(Color color)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(180, color.R, color.G, color.B), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(65, color.R, color.G, color.B), 0.28));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
            glow.Freeze();
            dc.DrawEllipse(glow, null, new Point(32, 32), 30, 30);
            var core = new SolidColorBrush(Color.FromRgb(255, 255, 209)); core.Freeze();
            dc.DrawEllipse(core, null, new Point(32, 32), 3.7, 3.7);
        }
        return Render(visual, 64, 64);
    }
    private static BitmapSource CreateRain()
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            var rain = new LinearGradientBrush();
            rain.StartPoint = new Point(0, 0); rain.EndPoint = new Point(0, 1);
            rain.GradientStops.Add(new GradientStop(Color.FromArgb(0, 183, 222, 250), 0));
            rain.GradientStops.Add(new GradientStop(Color.FromArgb(185, 192, 231, 255), 0.72));
            rain.GradientStops.Add(new GradientStop(Color.FromArgb(90, 217, 242, 255), 1));
            rain.Freeze();
            dc.DrawRoundedRectangle(rain, null, new Rect(4, 3, 8, 106), 4, 4);
        }
        return Render(visual, 16, 112);
    }
    private static BitmapSource Render(Visual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
