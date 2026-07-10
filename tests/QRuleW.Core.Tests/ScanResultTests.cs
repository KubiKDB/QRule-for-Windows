using QRuleW.Core;
using Xunit;

namespace QRuleW.Core.Tests;

public class ScanResultTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("http://example.com/path?q=1", "http://example.com/path?q=1")]
    [InlineData("  https://spaced.com  ", "https://spaced.com/")]
    [InlineData("mailto:a@b.com", "mailto:a@b.com")]
    [InlineData("tel:+15551234567", "tel:+15551234567")]
    [InlineData("sms:+15551234567", "sms:+15551234567")]
    [InlineData("www.example.com", "https://www.example.com/")]
    [InlineData("WWW.Example.com/path", "https://www.example.com/path")]
    public void RecognizesOpenableUrls(string payload, string expected)
    {
        var result = new ScanResult(payload);
        Assert.True(result.CanOpen);
        Assert.Equal(expected, result.OpenableUrl!.ToString());
    }

    [Theory]
    [InlineData("just some text")]
    [InlineData("WIFI:S:net;T:WPA;P:pw;;")]
    [InlineData("ftp://example.com")]           // scheme not allow-listed
    [InlineData("javascript:alert(1)")]         // scheme not allow-listed
    [InlineData("www.has space.com")]           // www but contains a space
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsNonOpenablePayloads(string payload)
    {
        var result = new ScanResult(payload);
        Assert.False(result.CanOpen);
        Assert.Null(result.OpenableUrl);
    }

    [Fact]
    public void PreservesRawPayload()
    {
        const string raw = "  Hello, QRule  ";
        Assert.Equal(raw, new ScanResult(raw).Payload);
    }
}
