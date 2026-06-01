using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BluetoothBear;

/// <summary>How the "Add a device" button behaves.</summary>
public enum PairingMethod
{
    /// <summary>Discover and pair inside the BluetoothBear flyout (modern, default).</summary>
    InApp,

    /// <summary>Open the Windows 11 Settings Bluetooth page.</summary>
    WindowsSettings,

    /// <summary>Open the classic Win32 "Add a device" wizard.</summary>
    ClassicWizard,
}

/// <summary>Persisted user preferences, stored as JSON in %APPDATA%\BluetoothBear.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public PairingMethod PairingMethod { get; set; } = PairingMethod.InApp;

    private static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BluetoothBear");

    private static string FilePath => Path.Combine(Directory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // Missing or corrupt settings — fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Best-effort; a failed save just means the preference won't persist.
        }
    }
}
