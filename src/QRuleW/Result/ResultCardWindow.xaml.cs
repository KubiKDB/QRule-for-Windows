using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using QRuleW.Capture;
using QRuleW.Core;
using QRuleW.Interop;
using Loc = QRuleW.Localization.Strings;

namespace QRuleW.Result;

public partial class ResultCardWindow : Window
{
    private static readonly TimeSpan CopyAutoCloseDelay = TimeSpan.FromSeconds(0.7);
    private const string GlyphCheck = "\uE73E"; // Segoe MDL2 checkmark

    private readonly ScanResult _result;
    private readonly PixelRect _selection;
    private readonly MonitorCapture _capture;
    private bool _copied;

    /// <summary>Raised when the card should be dismissed and the whole scan torn down.</summary>
    public event Action? CloseRequested;

    /// <summary>Raised when the user taps Share (coordinator handles z-order + the share sheet).</summary>
    public event Action? ShareRequested;

    public ResultCardWindow(ScanResult result, PixelRect selection, MonitorCapture capture)
    {
        InitializeComponent();
        _result = result;
        _selection = selection;
        _capture = capture;

        OpenLabel.Text = Loc.Open;
        CopyLabel.Text = Loc.Copy;
        ShareLabel.Text = Loc.Share;
        CloseLabel.Text = Loc.Close;
        OpenButton.IsEnabled = result.CanOpen;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Payload text with middle-ellipsis once the final width is known.
        PayloadText.Text = MiddleTruncate(_result.Payload, PayloadText.MaxWidth, maxLines: 2);
        PlaceNearSelection();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                CloseRequested?.Invoke();
                break;
            case Key.Enter when _result.CanOpen:
                e.Handled = true;
                OnOpen(this, e);
                break;
            case Key.C when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                e.Handled = true;
                OnCopy(this, e);
                break;
        }
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (_result.OpenableUrl is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(_result.OpenableUrl.ToString()) { UseShellExecute = true });
        }
        catch
        {
            // A missing/failing handler shouldn't crash the app; just dismiss.
        }
        CloseRequested?.Invoke();
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_copied) return;
        TrySetClipboard(_result.Payload);

        _copied = true;
        CopyLabel.Text = Loc.Copied;
        CopyGlyph.Text = GlyphCheck;

        var timer = new DispatcherTimer { Interval = CopyAutoCloseDelay };
        timer.Tick += (_, _) => { timer.Stop(); CloseRequested?.Invoke(); };
        timer.Start();
    }

    private void OnShare(object sender, RoutedEventArgs e) => ShareRequested?.Invoke();

    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private static void TrySetClipboard(string text)
    {
        // The clipboard can be briefly locked by another process; retry a few times.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetText(text); return; }
            catch { System.Threading.Thread.Sleep(30); }
        }
    }

    /// <summary>Positions the card just below the selection (flipping above / clamping to the work area),
    /// using physical pixels so it lands correctly on mixed-DPI monitors.</summary>
    private void PlaceNearSelection()
    {
        var scale = _capture.DpiScale <= 0 ? 1.0 : _capture.DpiScale;
        var cardW = (int)Math.Round(ActualWidth * scale);
        var cardH = (int)Math.Round(ActualHeight * scale);
        var gap = (int)Math.Round(12 * scale);
        var margin = (int)Math.Round(8 * scale);

        var selLeft = _capture.X + _selection.X;
        var selTop = _capture.Y + _selection.Y;
        var selBottom = selTop + _selection.Height;
        var centerX = selLeft + _selection.Width / 2;

        var left = centerX - cardW / 2;
        var top = selBottom + gap;

        var workLeft = _capture.WorkX;
        var workTop = _capture.WorkY;
        var workRight = _capture.WorkX + _capture.WorkWidth;
        var workBottom = _capture.WorkY + _capture.WorkHeight;

        if (top + cardH > workBottom)
            top = selTop - gap - cardH; // flip above

        left = Clamp(left, workLeft + margin, workRight - margin - cardW);
        top = Clamp(top, workTop + margin, workBottom - margin - cardH);

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, left, top, cardW, cardH,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        Activate();
        Focus();
    }

    /// <summary>Drops the card out of the always-on-top band so the share sheet can appear in front.</summary>
    public void DropTopmost() => Topmost = false;

    private static int Clamp(int v, int min, int max) => max < min ? min : (v < min ? min : (v > max ? max : v));

    /// <summary>Truncates text in the middle with an ellipsis until it fits within <paramref name="maxLines"/>.</summary>
    private string MiddleTruncate(string text, double maxWidth, int maxLines)
    {
        if (string.IsNullOrEmpty(text) || FitsWithin(text, maxWidth, maxLines))
            return text;

        const string ellipsis = "…";
        // Binary-search the number of characters to keep (split across head + tail).
        int lo = 1, hi = text.Length;
        var best = ellipsis;
        while (lo <= hi)
        {
            var keep = (lo + hi) / 2;
            var head = keep - keep / 2;
            var tail = keep / 2;
            var candidate = text[..head] + ellipsis + text[^tail..];
            if (FitsWithin(candidate, maxWidth, maxLines))
            {
                best = candidate;
                lo = keep + 1;
            }
            else
            {
                hi = keep - 1;
            }
        }
        return best;
    }

    private bool FitsWithin(string text, double maxWidth, int maxLines)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(PayloadText.FontFamily, PayloadText.FontStyle,
            PayloadText.FontWeight, PayloadText.FontStretch);
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            typeface, PayloadText.FontSize, Brushes.White, dpi)
        {
            MaxTextWidth = maxWidth,
        };
        var lineHeight = PayloadText.FontSize * 1.35;
        return ft.Height <= lineHeight * maxLines + 0.5;
    }
}
