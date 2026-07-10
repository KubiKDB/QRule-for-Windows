using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using WinRT;
using QRuleW.Core;
using Loc = QRuleW.Localization.Strings;

namespace QRuleW.Share;

/// <summary>
/// Shows the native Windows share sheet for a scan result.
///
/// The usual <c>DataTransferManager.As&lt;IDataTransferManagerInterop&gt;()</c> pattern throws
/// PlatformNotSupportedException in a self-contained/CsWinRT app because it needs built-in COM
/// interop marshalling of a [ComImport] interface. Instead we fetch the DataTransferManager
/// activation factory already QI'd to IDataTransferManagerInterop via RoGetActivationFactory and
/// invoke its two methods (GetForWindow / ShowShareUIForWindow) directly through the COM vtable —
/// which works without built-in COM support.
///
/// The caller must first close the overlays, drop the card out of the topmost band, and make the
/// card the foreground window, or the sheet renders behind / refuses to appear.
/// </summary>
public sealed class SharePresenter
{
    // IID of Windows.ApplicationModel.DataTransfer.IDataTransferManager (the default interface).
    private static readonly Guid DataTransferManagerIid =
        new(0xA5CAEE9B, 0x8708, 0x49D1, 0x8D, 0x36, 0x67, 0xD2, 0x5A, 0x8D, 0xA0, 0x0C);

    // IID of IDataTransferManagerInterop.
    private static readonly Guid DataTransferManagerInteropIid =
        new(0x3A3DCD6C, 0x3EAB, 0x43DC, 0xBC, 0xDE, 0x45, 0x67, 0x1C, 0xE8, 0x00, 0xC8);

    private const string DataTransferManagerClassName =
        "Windows.ApplicationModel.DataTransfer.DataTransferManager";

    // IDataTransferManagerInterop is IInspectable-based: 3 IUnknown + 3 IInspectable slots precede it.
    private const int VtblGetForWindow = 6;
    private const int VtblShowShareUIForWindow = 7;

    /// <summary>Presents the share UI anchored to <paramref name="hwnd"/>.</summary>
    public unsafe void Show(IntPtr hwnd, ScanResult result)
    {
        IntPtr hstring = IntPtr.Zero;
        IntPtr interop = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(
                WindowsCreateString(DataTransferManagerClassName, DataTransferManagerClassName.Length, out hstring));

            var interopIid = DataTransferManagerInteropIid;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, ref interopIid, out interop));
            Diagnostics.Log("Share.Show: got interop factory");

            var vtbl = (IntPtr*)*(IntPtr*)interop;

            // GetForWindow(HWND, REFIID, void** result)
            var getForWindow =
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)vtbl[VtblGetForWindow];
            var dtmIid = DataTransferManagerIid;
            IntPtr managerAbi;
            Marshal.ThrowExceptionForHR(getForWindow(interop, hwnd, &dtmIid, &managerAbi));

            // DataTransferManager is a WinRT runtime class (IInspectable), so it must be wrapped with
            // MarshalInspectable, not MarshalInterface — the latter yields a broken object.
            var manager = MarshalInspectable<DataTransferManager>.FromAbi(managerAbi);
            if (managerAbi != IntPtr.Zero) Marshal.Release(managerAbi); // FromAbi took its own ref
            Diagnostics.Log($"Share.Show: got DataTransferManager (null={manager is null})");
            if (manager is null) return;

            TypedEventHandler<DataTransferManager, DataRequestedEventArgs> handler = null!;
            handler = (_, args) =>
            {
                try
                {
                    var data = args.Request.Data;
                    data.Properties.Title = Loc.AppName;

                    if (result.OpenableUrl is not null)
                    {
                        data.Properties.Title = result.OpenableUrl.ToString();
                        data.SetWebLink(result.OpenableUrl);   // share URLs as a web link
                    }
                    else
                    {
                        data.SetText(result.Payload);          // everything else as plain text
                    }
                    Diagnostics.Log("Share.Show: DataRequested populated");
                }
                catch (Exception ex)
                {
                    Diagnostics.Log("Share.Show: DataRequested handler failed", ex);
                }
                finally
                {
                    manager.DataRequested -= handler;
                }
            };
            manager.DataRequested += handler;

            // ShowShareUIForWindow(HWND)
            var showShareUi =
                (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int>)vtbl[VtblShowShareUIForWindow];
            Marshal.ThrowExceptionForHR(showShareUi(interop, hwnd));
            Diagnostics.Log("Share.Show: ShowShareUIForWindow called");
        }
        finally
        {
            if (interop != IntPtr.Zero) Marshal.Release(interop);
            if (hstring != IntPtr.Zero) WindowsDeleteString(hstring);
        }
    }

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);
}
