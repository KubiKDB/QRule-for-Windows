using System.Globalization;
using System.Resources;

namespace QRuleW.Localization;

/// <summary>
/// Strongly-typed access to the localized resources. The active culture follows the Windows
/// display language automatically (CurrentUICulture), so Ukrainian systems get uk strings.
/// Hand-authored (instead of the VS designer) so it builds cleanly from the CLI / macOS.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Rm =
        new("QRuleW.Localization.Strings", typeof(Strings).Assembly);

    private static string Get(string key) => Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string AppName => Get(nameof(AppName));
    public static string TrayScan => Get(nameof(TrayScan));
    public static string TrayChangeShortcut => Get(nameof(TrayChangeShortcut));
    public static string TrayLaunchAtStartup => Get(nameof(TrayLaunchAtStartup));
    public static string TrayAbout => Get(nameof(TrayAbout));
    public static string TrayQuit => Get(nameof(TrayQuit));
    public static string Open => Get(nameof(Open));
    public static string Copy => Get(nameof(Copy));
    public static string Copied => Get(nameof(Copied));
    public static string Share => Get(nameof(Share));
    public static string Close => Get(nameof(Close));
    public static string NoQrFound => Get(nameof(NoQrFound));
    public static string HotkeyHint => Get(nameof(HotkeyHint));
    public static string RecorderWindowTitle => Get(nameof(RecorderWindowTitle));
    public static string RecorderCurrent => Get(nameof(RecorderCurrent));
    public static string RecorderPrompt => Get(nameof(RecorderPrompt));
    public static string RecorderRecord => Get(nameof(RecorderRecord));
    public static string RecorderSave => Get(nameof(RecorderSave));
    public static string RecorderCancel => Get(nameof(RecorderCancel));
    public static string ShortcutInUse => Get(nameof(ShortcutInUse));
    public static string HotkeyFailedTitle => Get(nameof(HotkeyFailedTitle));
    public static string HotkeyFailedBody => Get(nameof(HotkeyFailedBody));
    public static string AboutBody => Get(nameof(AboutBody));
}
