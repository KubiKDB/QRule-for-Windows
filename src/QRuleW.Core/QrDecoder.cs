using ZXing;
using ZXing.Common;

namespace QRuleW.Core;

/// <summary>
/// Decodes a QR code from a raw pixel buffer using ZXing.Net. Operates on plain byte arrays
/// (no System.Drawing / WPF), so it runs and unit-tests on any platform.
/// </summary>
public sealed class QrDecoder
{
    private readonly BarcodeReaderGeneric _primary;
    private readonly BarcodeReaderGeneric _pureBarcode;

    public QrDecoder()
    {
        _primary = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new[] { BarcodeFormat.QR_CODE },
            },
        };

        // Second pass for screen-perfect codes whose quiet zone / locator ZXing rejects on the first pass.
        _pureBarcode = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PureBarcode = true,
                PossibleFormats = new[] { BarcodeFormat.QR_CODE },
            },
        };
    }

    /// <summary>
    /// Attempts to decode a QR code from a 32-bits-per-pixel BGRA/BGR buffer. Returns null if none found.
    /// </summary>
    /// <param name="bgra">Row-major pixel bytes, 4 bytes per pixel (B, G, R, X).</param>
    public ScanResult? Decode(byte[] bgra, int width, int height)
    {
        if (bgra is null || width <= 0 || height <= 0 || bgra.Length < width * height * 4)
            return null;

        var source = new RGBLuminanceSource(bgra, width, height, RGBLuminanceSource.BitmapFormat.BGR32);

        // First pass: normal, then inverted (light-on-dark codes).
        var result = _primary.Decode(source) ?? _primary.Decode(source.invert());
        // Second pass: pure-barcode heuristic.
        result ??= _pureBarcode.Decode(source) ?? _pureBarcode.Decode(source.invert());

        var text = result?.Text;
        return string.IsNullOrEmpty(text) ? null : new ScanResult(text);
    }
}
