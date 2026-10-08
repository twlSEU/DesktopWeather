using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using DesktopWeather.Interop;

namespace DesktopWeather.Services;

internal static class IconFactory
{
    internal static Icon Create()
    {
        using var bitmap = new Bitmap(64, 64);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Color.FromArgb(15, 27, 45));
            graphics.FillEllipse(background, 1, 1, 62, 62);
            using var pen = new Pen(Color.FromArgb(125, 225, 237), 3.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 6; i++)
            {
                double angle = i * Math.PI / 3;
                var tip = new PointF(32 + (float)Math.Cos(angle) * 21, 32 + (float)Math.Sin(angle) * 21);
                graphics.DrawLine(pen, new PointF(32, 32), tip);
                var branch = new PointF(32 + (float)Math.Cos(angle) * 12, 32 + (float)Math.Sin(angle) * 12);
                for (int side = -1; side <= 1; side += 2)
                {
                    double branchAngle = angle + side * Math.PI / 3;
                    graphics.DrawLine(pen, branch, new PointF(branch.X + (float)Math.Cos(branchAngle) * 7, branch.Y + (float)Math.Sin(branchAngle) * 7));
                }
            }
        }
        IntPtr handle = bitmap.GetHicon();
        try { using Icon borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { NativeMethods.DestroyIcon(handle); }
    }
}
