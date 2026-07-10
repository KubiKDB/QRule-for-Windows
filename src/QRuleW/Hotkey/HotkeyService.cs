using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using QRuleW.Core;
using QRuleW.Interop;

namespace QRuleW.Hotkey;

/// <summary>
/// Owns the global hotkey. Creates a hidden message window, registers the chord with Win32
/// RegisterHotKey, and raises <see cref="Pressed"/> when it fires. Zero polling — the OS delivers
/// WM_HOTKEY only on the keypress, so the app stays at 0% idle CPU.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xB00B;

    private readonly HwndSource _sink;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyService()
    {
        // A tiny hidden top-level window purely to receive WM_HOTKEY.
        var parameters = new HwndSourceParameters("QRuleW.HotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,          // not WS_VISIBLE
            ExtendedWindowStyle = 0,
        };
        _sink = new HwndSource(parameters);
        _sink.AddHook(WndProc);
    }

    public IntPtr Handle => _sink.Handle;

    /// <summary>Registers <paramref name="gesture"/> as the active hotkey, replacing any previous one.</summary>
    public bool Register(HotkeyGesture gesture)
    {
        Unregister();
        if (!gesture.IsValid) return false;

        var mods = ToWin32Modifiers(gesture.Modifiers) | NativeMethods.MOD_NOREPEAT;
        _registered = NativeMethods.RegisterHotKey(_sink.Handle, HotkeyId, mods, gesture.VirtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        NativeMethods.UnregisterHotKey(_sink.Handle, HotkeyId);
        _registered = false;
    }

    /// <summary>
    /// True if <paramref name="gesture"/> can be registered right now (nobody else owns it).
    /// Temporarily drops the current registration, trial-registers on a throwaway id, then restores.
    /// </summary>
    public bool IsAvailable(HotkeyGesture gesture, HotkeyGesture? current)
    {
        if (!gesture.IsValid) return false;

        const int trialId = HotkeyId + 1;
        var wasRegistered = _registered;
        if (wasRegistered) Unregister();

        var mods = ToWin32Modifiers(gesture.Modifiers) | NativeMethods.MOD_NOREPEAT;
        var ok = NativeMethods.RegisterHotKey(_sink.Handle, trialId, mods, gesture.VirtualKey);
        if (ok) NativeMethods.UnregisterHotKey(_sink.Handle, trialId);

        // Restore whatever was active before the probe.
        if (wasRegistered && current is not null) Register(current);
        return ok;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    private static uint ToWin32Modifiers(HotkeyModifiers m)
    {
        uint result = 0;
        if (m.HasFlag(HotkeyModifiers.Alt)) result |= NativeMethods.MOD_ALT;
        if (m.HasFlag(HotkeyModifiers.Control)) result |= NativeMethods.MOD_CONTROL;
        if (m.HasFlag(HotkeyModifiers.Shift)) result |= NativeMethods.MOD_SHIFT;
        if (m.HasFlag(HotkeyModifiers.Win)) result |= NativeMethods.MOD_WIN;
        return result;
    }

    public void Dispose()
    {
        Unregister();
        _sink.RemoveHook(WndProc);
        _sink.Dispose();
    }
}
