using System.Diagnostics;
using Microsoft.Win32;

namespace BluetoothBear;

/// <summary>
/// Controls whether BluetoothBear launches at sign-in by adding/removing a value under
/// the per-user "Run" registry key (HKCU). Per-user, so it needs no admin rights, and
/// the user can also disable it from Task Manager → Startup.
/// </summary>
public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BluetoothBear";

    private static string ExePath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? string.Empty;

    /// <summary>True if a Run entry for BluetoothBear currently exists.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Add (enabled) or remove (disabled) the Run entry. Best-effort; never throws.</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null) return;

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{ExePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry unavailable / locked down; ignore and let the UI re-read actual state.
        }
    }
}
