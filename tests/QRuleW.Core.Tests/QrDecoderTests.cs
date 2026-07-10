using QRuleW.Core;
using Xunit;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace QRuleW.Core.Tests;

public class QrDecoderTests
{
    private static byte[] RenderQr(string text, int size, out int width, out int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions { Width = size, Height = size, Margin = 2 },
        };
        var pixels = writer.Write(text);
        width = pixels.Width;
        height = pixels.Height;
        return pixels.Pixels; // BGRA, 4 bytes per pixel — matches BGR32 decode
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("Just some plain text payload")]
    [InlineData("mailto:hello@qrule.app")]
    public void DecodesGeneratedQr(string payload)
    {
        var bytes = RenderQr(payload, 300, out var w, out var h);
        var result = new QrDecoder().Decode(bytes, w, h);

        Assert.NotNull(result);
        Assert.Equal(payload, result!.Payload);
    }

    [Fact]
    public void DecodesInvertedQr()
    {
        var bytes = RenderQr("https://inverted.test", 300, out var w, out var h);
        // Invert every colour channel (leave the padding byte).
        for (var i = 0; i < bytes.Length; i++)
            if (i % 4 != 3) bytes[i] = (byte)(255 - bytes[i]);

        var result = new QrDecoder().Decode(bytes, w, h);
        Assert.NotNull(result);
        Assert.Equal("https://inverted.test", result!.Payload);
    }

    [Fact]
    public void ReturnsNullForNoise()
    {
        var rng = new Random(42);
        var w = 120; var h = 120;
        var bytes = new byte[w * h * 4];
        rng.NextBytes(bytes);

        Assert.Null(new QrDecoder().Decode(bytes, w, h));
    }

    [Fact]
    public void ReturnsNullForBlankImage()
    {
        var w = 100; var h = 100;
        var bytes = new byte[w * h * 4];
        Array.Fill(bytes, (byte)255); // all white

        Assert.Null(new QrDecoder().Decode(bytes, w, h));
    }

    [Fact]
    public void ReturnsNullForUndersizedBuffer()
    {
        Assert.Null(new QrDecoder().Decode(new byte[10], 100, 100));
    }
}
