using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRuleW.Interop;

namespace QRuleW.Capture;

/// <summary>
/// Takes a one-time still of every monitor with GDI's <c>Graphics.CopyFromScreen</c>. No continuous
/// monitoring, so idle CPU stays at zero. Runs only under Per-Monitor-v2 DPI awareness, so the reported
/// monitor bounds are in true physical pixels.
/// </summary>
public sealed class CaptureService
{
    /// <summary>Captures all monitors. Must be called on a thread with the app's DPI awareness active.</summary>
    public IReadOnlyList<MonitorCapture> CaptureAllMonitors()
    {
        var captures = new List<MonitorCapture>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
                if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
                {
                    var capture = CaptureMonitor(hMonitor, info);
                    if (capture is not null) captures.Add(capture);
                }
                return true; // continue enumeration
            },
            IntPtr.Zero);

        return captures;
    }

    private static MonitorCapture? CaptureMonitor(IntPtr hMonitor, NativeMethods.MONITORINFO info)
    {
        var bounds = info.rcMonitor;
        var work = info.rcWork;
        var width = bounds.Width;
        var height = bounds.Height;
        if (width <= 0 || height <= 0) return null;

        var dpiScale = 1.0;
        if (NativeMethods.GetDpiForMonitor(hMonitor,
                NativeMethods.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0)
        {
            dpiScale = dpiX / 96.0;
        }

        using var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new System.Drawing.Size(width, height),
                CopyPixelOperation.SourceCopy);
        }

        var data = bmp.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        byte[] pixels;
        int stride = data.Stride;
        try
        {
            pixels = new byte[stride * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, stride);
        image.Freeze(); // cross-thread + immutable

        return new MonitorCapture
        {
            X = bounds.Left,
            Y = bounds.Top,
            PixelWidth = width,
            PixelHeight = height,
            WorkX = work.Left,
            WorkY = work.Top,
            WorkWidth = work.Width,
            WorkHeight = work.Height,
            DpiScale = dpiScale,
            Image = image,
            Pixels = pixels,
            Stride = stride,
        };
    }
}
