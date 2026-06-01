using System.Diagnostics;
using System.IO;

namespace BluetoothBear;

/// <summary>Launches Windows' external pairing UIs (used by the non-in-app pairing methods).</summary>
public static class PairingLauncher
{
    public static void OpenWindowsSettings() => TryStart("ms-settings:bluetooth");

    public static void OpenClassicWizard()
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
