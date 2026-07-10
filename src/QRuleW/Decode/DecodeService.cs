using QRuleW.Capture;
using QRuleW.Core;

namespace QRuleW.Decode;

/// <summary>
/// Bridges a monitor capture + a pixel-space selection rectangle to the cross-platform
/// <see cref="QrDecoder"/>. Crops the region out of the frozen pixel buffer (a pure array copy —
/// no second GDI round-trip) and decodes it.
/// </summary>
public sealed class DecodeService
{
    private readonly QrDecoder _decoder = new();

    /// <summary>Crops <paramref name="rect"/> from <paramref name="capture"/> and attempts a QR decode.</summary>
    public ScanResult? Decode(MonitorCapture capture, PixelRect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return null;

        var crop = new byte[rect.Width * rect.Height * 4];
        var srcStride = capture.Stride;
        var srcPixels = capture.Pixels;

        for (var row = 0; row < rect.Height; row++)
        {
            var srcOffset = (rect.Y + row) * srcStride + rect.X * 4;
            var dstOffset = row * rect.Width * 4;
            Array.Copy(srcPixels, srcOffset, crop, dstOffset, rect.Width * 4);
        }

        return _decoder.Decode(crop, rect.Width, rect.Height);
    }
}
