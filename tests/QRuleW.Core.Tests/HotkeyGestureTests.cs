using System.Text.Json;
using QRuleW.Core;
using Xunit;

namespace QRuleW.Core.Tests;

public class HotkeyGestureTests
{
    [Fact]
    public void DefaultIsCtrlShift7()
    {
        var d = HotkeyGesture.Default;
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift, d.Modifiers);
        Assert.Equal((uint)0x37, d.VirtualKey);
        Assert.Equal("Ctrl+Shift+7", d.Format());
        Assert.True(d.IsValid);
    }

    [Theory]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x53u, "Ctrl+Alt+S")]
    [InlineData(HotkeyModifiers.Win, 0x71u, "Win+F2")]
    [InlineData(HotkeyModifiers.Shift, 0x20u, "Shift+Space")]
    public void FormatsCombos(HotkeyModifiers mods, uint vk, string expected)
    {
        Assert.Equal(expected, new HotkeyGesture(mods, vk).Format());
    }

    [Fact]
    public void ModifierOnlyOrNoModifierIsInvalid()
    {
        Assert.False(new HotkeyGesture(HotkeyModifiers.None, 0x37).IsValid);      // no modifier
        Assert.False(new HotkeyGesture(HotkeyModifiers.Control, 0x11).IsValid);   // VK is Control itself
        Assert.False(new HotkeyGesture(HotkeyModifiers.Control, 0).IsValid);      // no key
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var original = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x37);
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<HotkeyGesture>(json)!;
        Assert.Equal(original, restored);
    }

    [Fact]
    public void EqualityByValue()
    {
        Assert.Equal(new HotkeyGesture(HotkeyModifiers.Shift, 0x41),
                     new HotkeyGesture(HotkeyModifiers.Shift, 0x41));
        Assert.NotEqual(new HotkeyGesture(HotkeyModifiers.Shift, 0x41),
                        new HotkeyGesture(HotkeyModifiers.Control, 0x41));
    }
}
