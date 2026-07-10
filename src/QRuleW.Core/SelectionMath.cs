namespace QRuleW.Core;

/// <summary>A rectangle in floating-point device-independent-pixel (DIP) space.</summary>
public readonly record struct DipRect(double X, double Y, double Width, double Height)
{
    public static DipRect FromPoints(double x1, double y1, double x2, double y2)
    {
        var x = Math.Min(x1, x2);
        var y = Math.Min(y1, y2);
        return new DipRect(x, y, Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }
}

/// <summary>A rectangle in integer physical-pixel space (screenshot bitmap coordinates).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);

/// <summary>
/// Conversions between the three coordinate spaces used during a scan:
/// overlay DIPs (mouse/drawing), physical pixels (capture / badge), and bitmap pixels (crop).
/// Kept dependency-free so the tricky DPI math is unit-testable on any OS.
/// </summary>
public static class SelectionMath
{
    /// <summary>Selections smaller than this in physical pixels are treated as a stray click and ignored.</summary>
    public const int MinSelectionPixels = 8;

    /// <summary>The U+00D7 multiplication sign used in the size badge (e.g. "240 × 240").</summary>
    public const char Times = '×';

    /// <summary>
    /// Maps a selection expressed in overlay DIPs to integer bitmap-pixel coordinates,
    /// scaling by the per-axis ratio between the frozen bitmap size and the overlay's DIP size,
    /// then clamping to the bitmap bounds. Mirrors the macOS scaleX/scaleY crop.
    /// </summary>
    public static PixelRect DipRectToBitmapRect(
        DipRect dip, double windowDipWidth, double windowDipHeight, int bitmapPxWidth, int bitmapPxHeight)
    {
        if (windowDipWidth <= 0 || windowDipHeight <= 0)
            return new PixelRect(0, 0, 0, 0);

        var scaleX = bitmapPxWidth / windowDipWidth;
        var scaleY = bitmapPxHeight / windowDipHeight;

        var left = (int)Math.Round(dip.X * scaleX);
        var top = (int)Math.Round(dip.Y * scaleY);
        var right = (int)Math.Round((dip.X + dip.Width) * scaleX);
        var bottom = (int)Math.Round((dip.Y + dip.Height) * scaleY);

        left = Clamp(left, 0, bitmapPxWidth);
        top = Clamp(top, 0, bitmapPxHeight);
        right = Clamp(right, 0, bitmapPxWidth);
        bottom = Clamp(bottom, 0, bitmapPxHeight);

        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>True when the physical-pixel selection is large enough to attempt a decode.</summary>
    public static bool IsSelectionUsable(int pxWidth, int pxHeight) =>
        pxWidth >= MinSelectionPixels && pxHeight >= MinSelectionPixels;

    /// <summary>The size-badge label, in physical pixels, using the U+00D7 separator.</summary>
    public static string BadgeText(int pxWidth, int pxHeight) => $"{pxWidth} {Times} {pxHeight}";

    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
}
