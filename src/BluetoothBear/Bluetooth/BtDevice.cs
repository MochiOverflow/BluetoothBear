namespace BluetoothBear.Bluetooth;

/// <summary>Which Bluetooth stack a device belongs to. Affects how we read battery.</summary>
public enum BtKind
{
    Classic,
    LowEnergy,
}

/// <summary>A paired Bluetooth device as surfaced in the tray menu.</summary>
public sealed class BtDevice
{
    /// <summary>Friendly name, e.g. "WH-1000XM4".</summary>
    public required string Name { get; init; }

    /// <summary>WinRT association-endpoint id (used for BLE GATT lookups).</summary>
    public required string AepId { get; init; }

    /// <summary>48-bit Bluetooth MAC address as a single integer (0 if unknown).</summary>
    public required ulong Address { get; init; }

    public required BtKind Kind { get; init; }

    public bool IsConnected { get; init; }

    /// <summary>Battery percentage 0-100, or null when the device does not report it.</summary>
    public int? BatteryPercent { get; init; }

    public bool HasAddress => Address != 0;

    /// <summary>Parses a MAC string ("ab:cd:ef:12:34:56" or "ABCDEF123456") into a 48-bit address.</summary>
    public static ulong ParseAddress(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return 0;

        ulong result = 0;
        foreach (var c in mac)
        {
            int nibble = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => -1,
            };
            if (nibble < 0) continue; // skip ':' and other separators
            result = (result << 4) | (uint)nibble;
        }

        return result;
    }
}
