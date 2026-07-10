using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using WinRT;
using QRuleW.Core;
using QRuleW.Interop;
using Loc = QRuleW.Localization.Strings;

namespace QRuleW.Share;

/// <summary>
/// Shows the native Windows share sheet for a scan result, using the DataTransferManager COM interop
/// that works from a Win32/WPF window. The caller must first drop the result card out of the topmost
/// band (see <see cref="Result.ResultCardWindow.DropTopmost"/>) and close the overlays, or the sheet
/// renders behind them — the exact z-order pitfall hit on macOS.
/// </summary>
public sealed class SharePresenter
{
    // IID of Windows.ApplicationModel.DataTransfer.IDataTransferManager.
    private static readonly Guid DataTransferManagerIid =
        new(0xA5CAEE9B, 0x8708, 0x49D1, 0x8D, 0x36, 0x67, 0xD2, 0x5A, 0x8D, 0xA0, 0x0C);

    /// <summary>Presents the share UI anchored to <paramref name="hwnd"/>.</summary>
    public void Show(IntPtr hwnd, ScanResult result)
    {
        var interop = DataTransferManager.As<NativeMethods.IDataTransferManagerInterop>();
        var iid = DataTransferManagerIid;
        var abi = interop.GetForWindow(hwnd, ref iid);
        var manager = MarshalInterface<DataTransferManager>.FromAbi(abi);

        TypedEventHandler<DataTransferManager, DataRequestedEventArgs> handler = null!;
        handler = (_, args) =>
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

            manager.DataRequested -= handler;
        };

        manager.DataRequested += handler;
        interop.ShowShareUIForWindow(hwnd);
    }
}
