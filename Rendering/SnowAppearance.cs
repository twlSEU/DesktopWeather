using System;

namespace DesktopWeather.Rendering;

// Depth is stable for the particle's lifetime: 0 is distant, 1 is near the viewer.
internal static class SnowAppearance
{
    internal const int SpriteCount = 6;
    internal const int VariantCount = 10;
    private const int CrystalVariants = 3;
    internal static int Layer(double depth) => depth < 0.38 ? 0 : depth < 0.78 ? 1 : 2;
    internal static int SpriteIndex(int variant, double radius, double depth)
    {
        int layer = Layer(depth);
        if (layer == 2) return 4 + variant % 2;
        bool crystal = variant >= VariantCount - CrystalVariants && radius >= 3;
        return layer * 2 + (crystal ? 1 : 0);
    }
    internal static bool Rotates(int sprite) => sprite is 1 or 3;
    internal static double Extent(double depth) => Layer(depth) switch { 2 => 1.45, 1 => 1.08, _ => 1 };
    internal static double SizeFactor(double depth, double variation) => (0.26 + 1.65 * Math.Pow(depth, 1.35)) * variation;
    internal static double FallFactor(double depth, double variation) => (0.35 + 1.05 * depth) * variation;
    internal static double Opacity(double depth) => depth <= 0.72 ? 0.45 + depth / 0.72 * 0.43 : 0.88 - (depth - 0.72) / 0.28 * 0.38;
}
