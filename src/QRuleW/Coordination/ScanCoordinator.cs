using System.Windows;
using System.Windows.Interop;
using QRuleW.Capture;
using QRuleW.Core;
using QRuleW.Decode;
using QRuleW.Overlay;
using QRuleW.Result;
using QRuleW.Share;
using Loc = QRuleW.Localization.Strings;

namespace QRuleW.Coordination;

/// <summary>
/// Orchestrates a single scan: freeze → select → decode → result card, and owns all teardown.
/// Mirrors the macOS ScanCoordinator, including the share z-order sequencing.
/// </summary>
public sealed class ScanCoordinator
{
    private readonly CaptureService _capture = new();
    private readonly DecodeService _decode = new();
    private readonly SharePresenter _share = new();

    private readonly List<OverlayWindow> _overlays = new();
    private ResultCardWindow? _card;
    private ScanResult? _cardResult;
    private bool _scanning;

    /// <summary>Begins a scan. No-op if one is already in progress.</summary>
    public void StartScan()
    {
        if (_scanning) return;
        _scanning = true;

        IReadOnlyList<MonitorCapture> monitors;
        try
        {
            monitors = _capture.CaptureAllMonitors();
        }
        catch
        {
            _scanning = false;
            return;
        }

        if (monitors.Count == 0)
        {
            _scanning = false;
            return;
        }

        foreach (var monitor in monitors)
        {
            var overlay = new OverlayWindow(monitor);
            overlay.SelectionCompleted += OnSelectionCompleted;
            overlay.Cancelled += Cancel;
            _overlays.Add(overlay);

            overlay.Show();
            var hwnd = new WindowInteropHelper(overlay).Handle;
            NativeMethods_SetOverlayBounds(hwnd, monitor);
        }

        ActivateOverlayUnderCursor();
    }

    private static void NativeMethods_SetOverlayBounds(IntPtr hwnd, MonitorCapture m)
    {
        // Position/size the overlay in physical pixels — exact on mixed-DPI multi-monitor setups.
        Interop.NativeMethods.SetWindowPos(
            hwnd, Interop.NativeMethods.HWND_TOPMOST,
            m.X, m.Y, m.PixelWidth, m.PixelHeight,
            Interop.NativeMethods.SWP_NOACTIVATE | Interop.NativeMethods.SWP_SHOWWINDOW);
    }

    private void ActivateOverlayUnderCursor()
    {
        if (!Interop.NativeMethods.GetCursorPos(out var pt))
        {
            _overlays.FirstOrDefault()?.Activate();
            return;
        }

        var target = _overlays.FirstOrDefault(o =>
            pt.X >= o.Capture.X && pt.X < o.Capture.X + o.Capture.PixelWidth &&
            pt.Y >= o.Capture.Y && pt.Y < o.Capture.Y + o.Capture.PixelHeight)
            ?? _overlays.FirstOrDefault();

        target?.Activate();
    }

    private void OnSelectionCompleted(OverlayWindow overlay, PixelRect pixelRect)
    {
        if (!_scanning) return;
        var capture = overlay.Capture;

        // Decode off the UI thread; marshal the result back.
        Task.Run(() => _decode.Decode(capture, pixelRect))
            .ContinueWith(t =>
            {
                if (!_scanning) return; // cancelled meanwhile
                var result = t.IsCompletedSuccessfully ? t.Result : null;
                if (result is null)
                    overlay.ShowToast(Loc.NoQrFound);
                else
                    ShowResult(overlay, pixelRect, result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ShowResult(OverlayWindow overlay, PixelRect pixelRect, ScanResult result)
    {
        foreach (var o in _overlays) o.EnterResultMode();

        var card = new ResultCardWindow(result, pixelRect, overlay.Capture);
        card.CloseRequested += Cancel;
        card.ShareRequested += OnShareRequested;
        _card = card;
        _cardResult = result;
        card.Show();
    }

    private void OnShareRequested()
    {
        if (_card is null || _cardResult is null) return;

        // Pitfall #2: the share sheet renders below topmost overlays and needs its anchor window to be
        // the foreground window. Close the overlays, drop the card out of the topmost band, and force
        // the card to the foreground before invoking the sheet, so it is visible and clickable.
        CloseOverlays();
        _card.DropTopmost();
        _card.Activate();

        var hwnd = new WindowInteropHelper(_card).Handle;
        Interop.NativeMethods.SetForegroundWindow(hwnd);

        try
        {
            Diagnostics.Log($"Share: invoking for hwnd={hwnd}");
            _share.Show(hwnd, _cardResult);
        }
        catch (Exception ex)
        {
            // If the share sheet can't be shown, leave the card up so the user can still Copy/Open.
            Diagnostics.Log("Share: failed", ex);
        }
    }

    private void CloseOverlays()
    {
        foreach (var overlay in _overlays)
        {
            overlay.SelectionCompleted -= OnSelectionCompleted;
            overlay.Cancelled -= Cancel;
            overlay.Close();
        }
        _overlays.Clear();
    }

    /// <summary>Tears down every window and releases the frozen screenshots.</summary>
    public void Cancel()
    {
        CloseOverlays();

        if (_card is not null)
        {
            _card.CloseRequested -= Cancel;
            _card.ShareRequested -= OnShareRequested;
            _card.Close();
            _card = null;
        }

        _cardResult = null;
        _scanning = false;
    }
}
