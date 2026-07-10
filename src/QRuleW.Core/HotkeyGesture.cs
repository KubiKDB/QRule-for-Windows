using System.Text;
using System.Text.Json.Serialization;

namespace QRuleW.Core;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,       // MOD_ALT
    Control = 2,   // MOD_CONTROL
    Shift = 4,     // MOD_SHIFT
    Win = 8,       // MOD_WIN
}

/// <summary>
/// A platform-neutral description of a global hotkey: a set of modifiers plus a Win32 virtual-key code.
/// Serializes to/from settings.json and formats a human-readable label ("Ctrl+Shift+7").
/// </summary>
public sealed class HotkeyGesture : IEquatable<HotkeyGesture>
{
    public HotkeyGesture(HotkeyModifiers modifiers, uint virtualKey)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
    }

    public HotkeyModifiers Modifiers { get; }

    /// <summary>Win32 virtual-key code (e.g. 0x37 for the '7' key).</summary>
    public uint VirtualKey { get; }

    /// <summary>The macOS ⇧⌘7 equivalent: Ctrl+Shift+7.</summary>
    [JsonIgnore]
    public static HotkeyGesture Default { get; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x37);

    public bool IsValid =>
        Modifiers != HotkeyModifiers.None && VirtualKey != 0 && !IsModifierKey(VirtualKey);

    /// <summary>A modifier-only VK cannot stand alone as the trigger key.</summary>
    public static bool IsModifierKey(uint vk) => vk is
        0x10 or 0x11 or 0x12 or            // Shift, Control, Alt
        0xA0 or 0xA1 or 0xA2 or 0xA3 or    // L/R Shift, L/R Control
        0xA4 or 0xA5 or                    // L/R Alt (Menu)
        0x5B or 0x5C;                      // L/R Win

    public string Format()
    {
        var sb = new StringBuilder();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) sb.Append("Win+");
        sb.Append(KeyName(VirtualKey));
        return sb.ToString();
    }

    /// <summary>Best-effort friendly name for common virtual-key codes.</summary>
    public static string KeyName(uint vk) => vk switch
    {
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),               // 0-9
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),               // A-Z
        >= 0x60 and <= 0x69 => "Num" + (char)('0' + (vk - 0x60)),   // Numpad 0-9
        >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),                   // F1-F24
        0x20 => "Space",
        0x0D => "Enter",
        0x1B => "Esc",
        0x09 => "Tab",
        0x2E => "Delete",
        0xBB => "=",
        0xBD => "-",
        0xC0 => "`",
        _ => "0x" + vk.ToString("X2"),
    };

    public override string ToString() => Format();

    public bool Equals(HotkeyGesture? other) =>
        other is not null && Modifiers == other.Modifiers && VirtualKey == other.VirtualKey;

    public override bool Equals(object? obj) => Equals(obj as HotkeyGesture);

    public override int GetHashCode() => HashCode.Combine(Modifiers, VirtualKey);
}
