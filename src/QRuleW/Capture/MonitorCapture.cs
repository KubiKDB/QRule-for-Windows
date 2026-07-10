using System.Windows.Media.Imaging;

namespace QRuleW.Capture;

/// <summary>
/// A single monitor's frozen screenshot plus the geometry needed to place an overlay over it and
/// crop selections out of it. All bounds are in physical pixels (virtual-desktop coordinates).
/// </summary>
public sealed class MonitorCapture
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int PixelWidth { get; init; }
    public required int PixelHeight { get; init; }

    /// <summary>Work area (excludes taskbar) in physical pixels, for clamping the result card.</summary>
    public required int WorkX { get; init; }
    public required int WorkY { get; init; }
    public required int WorkWidth { get; init; }
    public required int WorkHeight { get; init; }

    /// <summary>Effective DPI scale of this monitor (1.0 = 96 DPI, 1.5 = 150%).</summary>
    public required double DpiScale { get; init; }

    /// <summary>Frozen screenshot as a WPF image source for the overlay.</summary>
    public required BitmapSource Image { get; init; }

    /// <summary>Raw BGRA pixel bytes (row-major, <see cref="Stride"/> bytes per row) for cropping/decoding.</summary>
    public required byte[] Pixels { get; init; }

    public required int Stride { get; init; }
}
