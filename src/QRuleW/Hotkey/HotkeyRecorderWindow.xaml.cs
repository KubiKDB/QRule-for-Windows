using System.Windows;
using System.Windows.Input;
using QRuleW.Core;
using Loc = QRuleW.Localization.Strings;

namespace QRuleW.Hotkey;

/// <summary>
/// Modal dialog for rebinding the global hotkey. Captures a chord via keyboard input and validates it
/// against currently-registered hotkeys (system + other apps) by trial registration before allowing Save.
/// </summary>
public partial class HotkeyRecorderWindow : Window
{
    private readonly HotkeyService _hotkeys;
    private readonly HotkeyGesture _current;
    private HotkeyGesture _pending;
    private bool _recording;

    /// <summary>The gesture the user accepted (valid only when ShowDialog returned true).</summary>
    public HotkeyGesture Result => _pending;

    public HotkeyRecorderWindow(HotkeyService hotkeys, HotkeyGesture current)
    {
        InitializeComponent();
        _hotkeys = hotkeys;
        _current = current;
        _pending = current;

        Title = Loc.RecorderWindowTitle;
        HintText.Text = Loc.HotkeyHint;
        CurrentLabel.Text = Loc.RecorderCurrent;
        RecordButton.Content = Loc.RecorderRecord;
        SaveButton.Content = Loc.RecorderSave;
        CancelButton.Content = Loc.RecorderCancel;

        ShortcutDisplay.Text = current.Format();
        UpdateSaveState();
    }

    private void OnRecord(object sender, RoutedEventArgs e)
    {
        _recording = true;
        ErrorText.Visibility = Visibility.Collapsed;
        ShortcutDisplay.Text = Loc.RecorderPrompt;
        SaveButton.IsEnabled = false;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!_recording) return;

        e.Handled = true;

        // Alt combos arrive as Key.System with the real key in SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (HotkeyGesture.IsModifierKey(vk))
            return; // waiting for the non-modifier key

        if (key == Key.Escape)
        {
            // Esc while recording just cancels the capture, keeping the previous shortcut.
            _recording = false;
            ShortcutDisplay.Text = _pending.Format();
            UpdateSaveState();
            return;
        }

        var mods = FromKeyboard(Keyboard.Modifiers);
        var candidate = new HotkeyGesture(mods, vk);

        if (!candidate.IsValid)
            return; // needs at least one modifier

        _recording = false;
        ShortcutDisplay.Text = candidate.Format();

        if (_hotkeys.IsAvailable(candidate, _current))
        {
            _pending = candidate;
            ErrorText.Visibility = Visibility.Collapsed;
            SaveButton.IsEnabled = true;
        }
        else
        {
            ErrorText.Text = Loc.ShortcutInUse;
            ErrorText.Visibility = Visibility.Visible;
            SaveButton.IsEnabled = false;
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdateSaveState() => SaveButton.IsEnabled = _pending.IsValid && !_recording;

    private static HotkeyModifiers FromKeyboard(ModifierKeys m)
    {
        var result = HotkeyModifiers.None;
        if (m.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (m.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Win;
        return result;
    }
}
