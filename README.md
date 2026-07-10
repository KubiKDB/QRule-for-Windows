# QRule W

A single-purpose Windows system-tray utility that reads QR codes displayed **anywhere on the
screen** — browsers, PDFs, paused videos, remote-desktop sessions, video calls — anything visible.
Windows port of the macOS QRule menu-bar app.

Press the hotkey (default **Ctrl+Shift+7**), the screen freezes, drag a box around the QR code,
and QRule decodes it locally and shows a small card with **Open / Copy / Share / Close**. No idle
CPU (nothing is monitored until you press the shortcut), no network, no telemetry, and the
screenshot is never written to disk or transmitted.

## How it works

1. Global hotkey → one-time screenshot of **every** monitor (GDI `CopyFromScreen`).
2. Full-screen borderless topmost overlays (one per monitor) show the frozen shot with a 35% dim
   and a crosshair cursor.
3. Drag a rectangle — it's punched out of the dim with a 1px border and a live `W × H` badge
   (physical pixels). Selections under 8×8 px are ignored.
4. On release the region is cropped from the frozen shot and decoded with **ZXing.Net**
   (QR, TryHarder + inverted + pure-barcode passes).
5. Success → a dark Windows-11-style card next to the selection. **Open** (URLs only), **Copy**
   (shows "Copied", auto-closes), **Share** (native Windows share sheet), **Close**.
   `Enter` = Open, `Ctrl+C` = Copy, `Esc` = Close.
6. Failure → a "No QR code found" toast; drag again. `Esc` or a click outside cancels.

Tray menu: **Scan QR Code · Change Shortcut… · Launch at Startup · About · Quit**. UI is localized
in **English** and **Ukrainian**, following the Windows display language.

## Project layout

| Path | What |
|---|---|
| `src/QRuleW.Core` | Cross-platform decode/URL/DPI/hotkey logic (`net8.0`) — unit-tested on any OS |
| `src/QRuleW` | The WPF tray app (`net8.0-windows10.0.19041.0`) |
| `src/QRuleW.Package` | MSIX manifest + Store logos |
| `tests/QRuleW.Core.Tests` | xunit suite for the Core library |

## Building

### macOS / Linux (compile + test + publish)

Requires the **official** .NET 8 SDK. The Homebrew `dotnet@8` is missing the WindowsDesktop MSBuild
targets that WPF needs; install the real SDK side-by-side if needed:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir "$HOME/.dotnet"
```

Then:

```bash
./build.sh                 # build + Core tests + self-contained win-x64 → dist/win-x64/QRuleW.exe
./build.sh win-arm64       # ARM64 target
```

`build.sh` compiles the WPF app cross-OS (`EnableWindowsTargeting=true`), runs the Core tests, and
publishes a single self-contained exe you copy to a Windows machine. It **cannot** run the app or
build the MSIX — both are Windows-only.

### Windows (run + MSIX)

```powershell
.\build.ps1                                   # publish + pack unsigned MSIX → dist\win-x64\QRuleW.msix
.\build.ps1 -Sign -CertThumbprint <thumb>     # sign for local sideload install
```

Needs the Windows 10/11 SDK for `makeappx.exe` / `signtool.exe`. The unsigned `.msix` is for
Microsoft Store submission.

## Verification

**Automated (runs on macOS):** `./build.sh` — full solution compile, 41 Core unit tests
(URL-detection matrix, QR generate→decode round-trip incl. inverted / noise / blank, SelectionMath
at 100% / 150% / 225% and the 7px↔8px gate boundary, hotkey serialization), and a self-contained
win-x64 publish.

**Manual (Windows machine — the runtime checklist):**

1. Tray icon + menu appear; switch the Windows display language to Ukrainian → all strings localize.
2. `Ctrl+Shift+7` freezes **all** monitors with dim + crosshair; the badge shows physical pixels with `×`.
3. Show a QR with a URL payload: drag → card appears adjacent; **Open** enabled; `Enter` opens the
   browser; **Copy** shows "Copied" and auto-closes ~0.7 s; `Ctrl+C` copies.
4. Non-URL payload (plain text / WIFI): **Open** greyed; **Share** shares as text.
5. Empty-area drag → "No QR code found" toast, then re-drag works; a <8×8 drag is silently ignored.
6. `Esc` cancels; a click on the dim outside the card cancels.
7. **Mixed DPI** (e.g. laptop 150% + external 100%): overlay pixel-exact on both; a selection on each
   monitor decodes correctly; badge numbers are physical; the card sits correctly near the selection.
8. **Share**: sheet appears **in front** (overlays closed, card dropped from topmost); URL shared as a
   web link; dismissing the sheet keeps the card.
9. **Change Shortcut…**: rebind (e.g. `Ctrl+Alt+S`); old chord dead, new works, survives restart;
   trying a shortcut another app owns shows the "already in use" message.
10. **Launch at Startup** toggle → verify the `HKCU\...\Run` value; toggle off removes it.
11. Task Manager: 0% CPU when idle; memory flat after 20 scans (no screenshot leak).
12. MSIX install: hotkey, share, and startup-task all work packaged.

## Notes

- **Micro-QR** is not supported by ZXing.Net (macOS Vision was); standard QR only.
- The default hotkey **Ctrl+Shift+7** matches the macOS ⇧⌘7 and has no global Windows conflict. If
  another global-hotkey app already owns it, QRule shows a tray notification and opens the recorder
  instead of running without a shortcut.
- Everything is decoded on-device; the app requests no network capability.
