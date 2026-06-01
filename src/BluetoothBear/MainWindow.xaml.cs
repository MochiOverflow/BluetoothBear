using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace BluetoothBear;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Reuse the runtime-drawn tray icon as the window icon.
        Icon = Imaging.CreateBitmapSourceFromHIcon(
            TrayIcons.Connected.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        // Content height changes (device list / battery loading) — stay anchored to the tray corner.
        SizeChanged += (_, _) => { if (IsVisible) AnchorToTray(); };
    }

    /// <summary>Show the flyout in the bottom-right work-area corner, just above the taskbar.</summary>
    public void ShowAtTray()
    {
        Show();
        UpdateLayout();   // ensure ActualWidth/ActualHeight reflect SizeToContent
        AnchorToTray();
        Activate();
        Focus();
    }

    private void AnchorToTray()
    {
        var area = SystemParameters.WorkArea; // excludes the taskbar
        Left = area.Right - ActualWidth;
        Top = area.Bottom - ActualHeight;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
        => TryStart("ms-settings:bluetooth");

    // Opens Windows' "Add a device" pairing wizard (direct to device discovery),
    // falling back to the Bluetooth settings page if the wizard isn't present.
    private void AddDevice_Click(object sender, RoutedEventArgs e)
    {
        var wizard = Path.Combine(Environment.SystemDirectory, "DevicePairingWizard.exe");
        if (!File.Exists(wizard) || !TryStart(wizard))
        {
            TryStart("ms-settings:bluetooth");
        }
    }

    private static bool TryStart(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
