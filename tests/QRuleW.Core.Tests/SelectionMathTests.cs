using QRuleW.Core;
using Xunit;

namespace QRuleW.Core.Tests;

public class SelectionMathTests
{
    [Theory]
    [InlineData(7, 7, false)]
    [InlineData(7, 20, false)]
    [InlineData(8, 8, true)]
    [InlineData(8, 7, false)]
    [InlineData(100, 100, true)]
    public void EnforcesEightPixelGate(int w, int h, bool usable)
    {
        Assert.Equal(usable, SelectionMath.IsSelectionUsable(w, h));
    }

    [Fact]
    public void BadgeUsesMultiplicationSign()
    {
        Assert.Equal("240 × 180", SelectionMath.BadgeText(240, 180));
    }

    [Fact]
    public void MapsOneToOneAtHundredPercent()
    {
        // Overlay DIP size == bitmap px size (100% scale).
        var dip = new DipRect(10, 20, 100, 50);
        var px = SelectionMath.DipRectToBitmapRect(dip, 1920, 1080, 1920, 1080);
        Assert.Equal(new PixelRect(10, 20, 100, 50), px);
    }

    [Fact]
    public void ScalesUpAt150Percent()
    {
        // 150% monitor: 1280x720 DIP overlay backed by a 1920x1080 physical bitmap.
        var dip = new DipRect(100, 100, 200, 200);
        var px = SelectionMath.DipRectToBitmapRect(dip, 1280, 720, 1920, 1080);
        Assert.Equal(new PixelRect(150, 150, 300, 300), px);
    }

    [Fact]
    public void ScalesUpAt225Percent()
    {
        var dip = new DipRect(0, 0, 100, 100);
        var px = SelectionMath.DipRectToBitmapRect(dip, 1000, 1000, 2250, 2250);
        Assert.Equal(new PixelRect(0, 0, 225, 225), px);
    }

    [Fact]
    public void ClampsToBitmapBounds()
    {
        var dip = new DipRect(-50, -50, 5000, 5000);
        var px = SelectionMath.DipRectToBitmapRect(dip, 1920, 1080, 1920, 1080);
        Assert.Equal(new PixelRect(0, 0, 1920, 1080), px);
    }

    [Fact]
    public void FromPointsNormalizesReversedDrag()
    {
        var r = DipRect.FromPoints(300, 300, 100, 120);
        Assert.Equal(new DipRect(100, 120, 200, 180), r);
    }
}
