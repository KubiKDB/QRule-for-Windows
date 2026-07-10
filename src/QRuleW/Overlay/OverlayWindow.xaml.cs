using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QRuleW.Capture;
using QRuleW.Core;

namespace QRuleW.Overlay;

public partial class OverlayWindow : Window
{
    private enum Mode { Selecting, Result }

    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(1.6);

    private readonly MonitorCapture _capture;
    private Mode _mode = Mode.Selecting;
    private bool _dragging;
    private Point _dragStart;
    private DipRect _selection;
    private DispatcherTimer? _toastTimer;

    /// <summary>Raised on a completed drag; carries the selection mapped to bitmap pixels.</summary>
    public event Action<OverlayWindow, PixelRect>? SelectionCompleted;

    /// <summary>Raised on Esc or (in result mode) a click on the dimmed area.</summary>
    public event Action? Cancelled;

    public OverlayWindow(MonitorCapture capture)
    {
        InitializeComponent();
        _capture = capture;
        Screenshot.Source = capture.Image;
        Loaded += (_, _) => { RedrawDim(); Focus(); };
        SizeChanged += (_, _) => RedrawDim();
    }

    public MonitorCapture Capture => _capture;

    /// <summary>After a successful decode, freeze the overlay so any click cancels the whole scan.</summary>
    public void EnterResultMode() => _mode = Mode.Result;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_mode == Mode.Result)
        {
            Cancelled?.Invoke();
            return;
        }

        HideToast();
        _dragging = true;
        _dragStart = e.GetPosition(this);
        _selection = new DipRect(_dragStart.X, _dragStart.Y, 0, 0);
        CaptureMouse();
        UpdateSelectionVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;

        var p = e.GetPosition(this);
        _selection = DipRect.FromPoints(_dragStart.X, _dragStart.Y, p.X, p.Y);
        UpdateSelectionVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;

        _dragging = false;
        ReleaseMouseCapture();

        var pixelRect = MapToBitmap(_selection);

        // Ignore stray clicks / tiny drags (< 8x8 physical px).
        if (!SelectionMath.IsSelectionUsable(pixelRect.Width, pixelRect.Height))
        {
            ClearSelectionVisual();
            return;
        }

        SelectionCompleted?.Invoke(this, pixelRect);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancelled?.Invoke();
        }
    }

    /// <summary>Shows the transient "No QR code found" toast and clears the current selection.</summary>
    public void ShowToast(string text)
    {
        ClearSelectionVisual();
        ToastText.Text = text;
        Toast.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = ToastDuration };
        _toastTimer.Tick += (_, _) => HideToast();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer?.Stop();
        _toastTimer = null;
        Toast.Visibility = Visibility.Collapsed;
    }

    private PixelRect MapToBitmap(DipRect dip) =>
        SelectionMath.DipRectToBitmapRect(
            dip, Screenshot.ActualWidth, Screenshot.ActualHeight,
            _capture.PixelWidth, _capture.PixelHeight);

    private void UpdateSelectionVisual()
    {
        if (_selection.Width <= 0 || _selection.Height <= 0)
        {
            ClearSelectionVisual();
            return;
        }

        Canvas.SetLeft(SelectionBorder, _selection.X);
        Canvas.SetTop(SelectionBorder, _selection.Y);
        SelectionBorder.Width = _selection.Width;
        SelectionBorder.Height = _selection.Height;
        SelectionBorder.Visibility = Visibility.Visible;

        var px = MapToBitmap(_selection);
        BadgeText.Text = SelectionMath.BadgeText(px.Width, px.Height);
        BadgeBorder.Visibility = Visibility.Visible;
        // Badge sits just under the selection's top-left, nudged inside the screen.
        Canvas.SetLeft(BadgeBorder, _selection.X);
        Canvas.SetTop(BadgeBorder, _selection.Y + _selection.Height + 4);

        RedrawDim();
    }

    private void ClearSelectionVisual()
    {
        _selection = new DipRect(0, 0, 0, 0);
        SelectionBorder.Visibility = Visibility.Collapsed;
        BadgeBorder.Visibility = Visibility.Collapsed;
        RedrawDim();
    }

    /// <summary>Dims the whole window, punching a hole where the current selection is.</summary>
    private void RedrawDim()
    {
        var full = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        if (_selection.Width > 0 && _selection.Height > 0)
        {
            var hole = new RectangleGeometry(
                new Rect(_selection.X, _selection.Y, _selection.Width, _selection.Height));
            DimPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, hole);
        }
        else
        {
            DimPath.Data = full;
        }
    }
}
