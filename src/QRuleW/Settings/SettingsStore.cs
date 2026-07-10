using System.IO;
using System.Text.Json;
using QRuleW.Core;

namespace QRuleW.Settings;

/// <summary>Persisted app settings. Kept intentionally tiny — just the hotkey.</summary>
public sealed class AppSettings
{
    public HotkeyModifiers HotkeyModifiers { get; set; } = HotkeyGesture.Default.Modifiers;
    public uint HotkeyVirtualKey { get; set; } = HotkeyGesture.Default.VirtualKey;

    public HotkeyGesture ToGesture()
    {
        var g = new HotkeyGesture(HotkeyModifiers, HotkeyVirtualKey);
        return g.IsValid ? g : HotkeyGesture.Default;
    }

    public static AppSettings FromGesture(HotkeyGesture g) =>
        new() { HotkeyModifiers = g.Modifiers, HotkeyVirtualKey = g.VirtualKey };
}

/// <summary>
/// Reads/writes <see cref="AppSettings"/> as JSON under %APPDATA%\QRuleW\settings.json.
/// Written only when the user changes the shortcut — never during a scan.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QRuleW");
        _path = Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null) return settings;
            }
        }
        catch
        {
            // Corrupt or unreadable settings fall back to defaults rather than crashing at startup.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // Non-fatal: a failed settings write just means the shortcut resets next launch.
        }
    }
}
