using System.Diagnostics;
using System.Text;
using Microsoft.Win32;
using QRuleW.Interop;

namespace QRuleW.Startup;

/// <summary>
/// Toggles "launch at login" via the HKCU Run key (unpackaged), matching the macOS SMAppService toggle.
/// When running as an MSIX package the Run key is ignored by Windows, so we no-op there and rely on the
/// package's declared startupTask (managed by the OS Settings app).
/// </summary>
public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QRuleW";

    public bool IsPackaged { get; } = DetectPackaged();

    public bool IsEnabled
    {
        get
        {
            if (IsPackaged) return false; // startupTask state lives in the OS, not queried here
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (IsPackaged) return;

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null) return;

        if (enabled)
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
                key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static bool DetectPackaged()
    {
        int length = 0;
        var rc = NativeMethods.GetCurrentPackageFullName(ref length, null);
        return rc != NativeMethods.APPMODEL_ERROR_NO_PACKAGE;
    }
}
