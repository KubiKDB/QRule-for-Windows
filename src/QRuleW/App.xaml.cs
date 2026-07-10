using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using QRuleW.Coordination;
using QRuleW.Core;
using QRuleW.Hotkey;
using QRuleW.Settings;
using QRuleW.Startup;
using Loc = QRuleW.Localization.Strings;

namespace QRuleW;

public partial class App : Application
{
    private const string MutexName = "QRuleW.SingleInstance.5F3A";

    private Mutex? _singleInstance;
    private TaskbarIcon? _tray;
    private HotkeyService? _hotkeys;
    private ScanCoordinator? _coordinator;
    private SettingsStore? _settings;
    private StartupService? _startup;
    private HotkeyGesture _gesture = HotkeyGesture.Default;
    private MenuItem? _startupMenuItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Any unhandled exception (startup or later) is logged rather than silently killing the app.
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("Dispatcher", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain", args.ExceptionObject as Exception);

        try
        {
            // Single instance — a second launch just exits.
            _singleInstance = new Mutex(initiallyOwned: true, MutexName, out var isNew);
            if (!isNew)
            {
                Shutdown();
                return;
            }

            _settings = new SettingsStore();
            _startup = new StartupService();
            _coordinator = new ScanCoordinator();
            _hotkeys = new HotkeyService();
            _hotkeys.Pressed += () => _coordinator.StartScan();

            _gesture = _settings.Load().ToGesture();

            BuildTray();
            RegisterHotkeyOrPrompt(_gesture);
        }
        catch (Exception ex)
        {
            LogCrash("Startup", ex);
            MessageBox.Show(
                $"QRule W failed to start:\n\n{ex.Message}\n\nDetails saved to:\n{CrashLogPath}",
                Loc.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void BuildTray()
    {
        _tray = new TaskbarIcon { ToolTipText = Loc.AppName };
        var icon = LoadTrayIcon();
        if (icon is not null) _tray.IconSource = icon;
        _tray.TrayLeftMouseUp += (_, _) => _coordinator?.StartScan();
        _tray.ContextMenu = BuildMenu();
        _tray.ForceCreate();
    }

    /// <summary>
    /// Loads the tray icon defensively. WPF's ICO decoder can reject some .ico layouts, so a failure
    /// here must not take the whole app down — fall back to the exe's own embedded icon, then to none.
    /// </summary>
    private static BitmapSource? LoadTrayIcon()
    {
        try
        {
            return new BitmapImage(new Uri("pack://application:,,,/Assets/qrule.ico"));
        }
        catch (Exception ex)
        {
            LogCrash("TrayIcon(pack)", ex);
        }

        try
        {
            var exe = Environment.ProcessPath;
            using var extracted = exe is null ? null : System.Drawing.Icon.ExtractAssociatedIcon(exe);
            if (extracted is not null)
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    extracted.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
        }
        catch (Exception ex)
        {
            LogCrash("TrayIcon(exe)", ex);
        }

        return null; // tray still works, just without a custom glyph
    }

    private static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QRuleW", "crash.log");

    private static void LogCrash(string stage, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath,
                $"[{DateTime.Now:o}] {stage}: {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging is best-effort; never let it throw.
        }
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var scan = new MenuItem { Header = Loc.TrayScan };
        scan.Click += (_, _) => _coordinator?.StartScan();

        var changeShortcut = new MenuItem { Header = Loc.TrayChangeShortcut };
        changeShortcut.Click += (_, _) => ChangeShortcut();

        _startupMenuItem = new MenuItem { Header = Loc.TrayLaunchAtStartup, IsCheckable = true };
        _startupMenuItem.Click += (_, _) => ToggleStartup();

        var about = new MenuItem { Header = Loc.TrayAbout };
        about.Click += (_, _) => ShowAbout();

        var quit = new MenuItem { Header = Loc.TrayQuit };
        quit.Click += (_, _) => QuitApp();

        menu.Items.Add(scan);
        menu.Items.Add(new Separator());
        menu.Items.Add(changeShortcut);
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(about);
        menu.Items.Add(quit);

        // Refresh the startup checkmark each time the menu opens.
        menu.Opened += (_, _) =>
        {
            if (_startupMenuItem is not null && _startup is not null)
                _startupMenuItem.IsChecked = _startup.IsEnabled;
        };

        return menu;
    }

    private void RegisterHotkeyOrPrompt(HotkeyGesture gesture)
    {
        if (_hotkeys!.Register(gesture))
            return;

        // Another global-hotkey app owns the chord: tell the user and open the recorder.
        _tray?.ShowNotification(Loc.HotkeyFailedTitle, string.Format(Loc.HotkeyFailedBody, gesture.Format()));
        ChangeShortcut();
    }

    private void ChangeShortcut()
    {
        if (_hotkeys is null) return;

        var dialog = new HotkeyRecorderWindow(_hotkeys, _gesture);
        if (dialog.ShowDialog() == true && dialog.Result.IsValid)
        {
            _gesture = dialog.Result;
            _settings?.Save(AppSettings.FromGesture(_gesture));
            _hotkeys.Register(_gesture);
        }
        else
        {
            // Restore the prior binding in case the probe left it unregistered.
            _hotkeys.Register(_gesture);
        }
    }

    private void ToggleStartup()
    {
        if (_startup is null) return;
        _startup.SetEnabled(!_startup.IsEnabled);
        if (_startupMenuItem is not null) _startupMenuItem.IsChecked = _startup.IsEnabled;
    }

    private void ShowAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        var body = string.Format(Loc.AboutBody, version, _gesture.Format());
        MessageBox.Show(body, Loc.TrayAbout, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void QuitApp()
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
