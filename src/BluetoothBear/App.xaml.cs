using System.ComponentModel;
using System.Threading;
using System.Windows;
using BluetoothBear.ViewModels;
using WinForms = System.Windows.Forms;

namespace BluetoothBear;

/// <summary>
/// Application host. Owns the WinForms tray icon and the WPF window, and keeps the
/// app alive in the tray after the window is closed (ShutdownMode = OnExplicitShutdown).
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "BluetoothBear_SingleInstance_{4C7D1B6E-2C3A-4F0E-9A1B-9D2F3E4A5B6C}";

    private Mutex? _mutex;
    private WinForms.NotifyIcon? _tray;
    private MainWindow? _window;
    private MainViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _vm = new MainViewModel();
        _vm.PropertyChanged += OnViewModelChanged;

        _window = new MainWindow { DataContext = _vm };
        _window.Closing += OnWindowClosing;
        _window.Deactivated += OnFlyoutDeactivated;

        SetupTray();

        ShowFlyout();          // pop up at the tray on first launch
        _ = _vm.StartAsync();
    }

    private void SetupTray()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Open BluetoothBear", null, (_, _) => ShowFlyout());
        menu.Items.Add("Refresh now", null, async (_, _) => await (_vm?.RefreshAsync() ?? Task.CompletedTask));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new WinForms.NotifyIcon
        {
            Icon = TrayIcons.Idle,
            Text = "BluetoothBear",
            Visible = true,
            ContextMenuStrip = menu,
        };
        // Left-click toggles the flyout; right-click shows the context menu automatically.
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                ToggleFlyout();
            }
        };
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_tray is null || _vm is null) return;

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.AnyConnected):
                _tray.Icon = _vm.AnyConnected ? TrayIcons.Connected : TrayIcons.Idle;
                break;
            case nameof(MainViewModel.TrayText):
                _tray.Text = _vm.TrayText;
                break;
        }
    }

    // Flyout show/hide bookkeeping.
    private DateTime _lastHidden = DateTime.MinValue;
    private DateTime _suppressHideUntil = DateTime.MinValue;
    private bool _isExiting;

    private void ShowFlyout()
    {
        if (_window is null) return;
        // Briefly ignore deactivation right after showing, so the flyout doesn't
        // dismiss itself before it has settled into the foreground.
        _suppressHideUntil = DateTime.UtcNow.AddMilliseconds(500);
        _window.ShowAtTray();
        _ = _vm?.RefreshAsync();
    }

    private void HideFlyout()
    {
        if (_window is null || !_window.IsVisible) return;
        _window.Hide();
        _lastHidden = DateTime.UtcNow;
    }

    private void ToggleFlyout()
    {
        if (_window is null) return;
        if (_window.IsVisible)
        {
            HideFlyout();
            return;
        }

        // If a tray click just dismissed the flyout (via Deactivated), don't immediately reopen it.
        if (DateTime.UtcNow - _lastHidden < TimeSpan.FromMilliseconds(300)) return;
        ShowFlyout();
    }

    // Clicking away from the flyout dismisses it (standard tray-flyout behavior).
    private void OnFlyoutDeactivated(object? sender, EventArgs e)
    {
        if (DateTime.UtcNow < _suppressHideUntil) return;
        HideFlyout();
    }

    // Alt+F4 etc. hides to the tray instead of exiting.
    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting) return;
        e.Cancel = true;
        HideFlyout();
    }

    private void ExitApp()
    {
        _isExiting = true;
        _vm?.Stop();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
