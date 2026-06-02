using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BluetoothBear.Bluetooth;

/// <summary>
/// Enumerates paired Bluetooth devices (classic + BLE) via WinRT, reading connection
/// state and — best effort — the battery percentage exposed by the device's PnP node.
/// </summary>
public static class DeviceEnumerator
{
    // DEVPKEY_Bluetooth_Battery. Present (when supported) on a connected device's AEP node.
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string IsConnectedKey = "System.Devices.Aep.IsConnected";
    private const string AddressKey = "System.Devices.Aep.DeviceAddress";

    private static readonly string[] RequestedProperties =
    [
        IsConnectedKey,
        AddressKey,
        BatteryKey,
    ];

    public static async Task<IReadOnlyList<BtDevice>> GetPairedDevicesAsync()
    {
        var classic = QueryAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true), BtKind.Classic);
        var le = QueryAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), BtKind.LowEnergy);
        var batteryTask = PnpBatteryReader.GetBatteryByAddressAsync();

        var results = await Task.WhenAll(classic, le);
        var pnpBattery = await batteryTask;

        // A device can appear under both stacks (dual-mode). Keep the connected/richer entry.
        var byAddress = new Dictionary<ulong, BtDevice>();
        var noAddress = new List<BtDevice>();
        foreach (var device in results.SelectMany(r => r).Select(d => WithBattery(d, pnpBattery)))
        {
            if (!device.HasAddress)
            {
                noAddress.Add(device);
                continue;
            }

            if (byAddress.TryGetValue(device.Address, out var existing) && Score(existing) >= Score(device))
            {
                continue;
            }

            byAddress[device.Address] = device;
        }

        return byAddress.Values
            .Concat(noAddress)
            .OrderByDescending(d => d.IsConnected)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Remove the pairing for a device ("forget"). Best effort; returns true if it's gone.</summary>
    public static async Task<bool> UnpairAsync(string aepId)
    {
        if (string.IsNullOrEmpty(aepId)) return false;
        try
        {
            var di = await DeviceInformation.CreateFromIdAsync(aepId);
            if (!di.Pairing.IsPaired) return true;

            var result = await di.Pairing.UnpairAsync();
            return result.Status is DeviceUnpairingResultStatus.Unpaired
                or DeviceUnpairingResultStatus.AlreadyUnpaired;
        }
        catch
        {
            return false;
        }
    }

    // Prefer connected entries, then ones that actually report battery.
    private static int Score(BtDevice d) => (d.IsConnected ? 2 : 0) + (d.BatteryPercent.HasValue ? 1 : 0);

    private static async Task<List<BtDevice>> QueryAsync(string selector, BtKind kind)
    {
        var list = new List<BtDevice>();
        try
        {
            var found = await DeviceInformation.FindAllAsync(selector, RequestedProperties);
            foreach (var di in found)
            {
                list.Add(new BtDevice
                {
                    Name = string.IsNullOrWhiteSpace(di.Name) ? "(unnamed device)" : di.Name,
                    AepId = di.Id,
                    Address = BtDevice.ParseAddress(GetProp(di, AddressKey) as string),
                    Kind = kind,
                    IsConnected = GetProp(di, IsConnectedKey) as bool? ?? false,
                    BatteryPercent = ParseBattery(GetProp(di, BatteryKey)),
                });
            }
        }
        catch
        {
            // A failing selector (e.g. no radio present) should not break the whole refresh.
        }

        return list;
    }

    private static object? GetProp(DeviceInformation di, string key)
        => di.Properties.TryGetValue(key, out var value) ? value : null;

    // Fill in battery from the PnP tree scan when the enumeration itself didn't carry one.
    private static BtDevice WithBattery(BtDevice device, IReadOnlyDictionary<ulong, int> battery)
    {
        if (device.BatteryPercent.HasValue || !device.HasAddress || !battery.TryGetValue(device.Address, out int pct))
        {
            return device;
        }

        return new BtDevice
        {
            Name = device.Name,
            AepId = device.AepId,
            Address = device.Address,
            Kind = device.Kind,
            IsConnected = device.IsConnected,
            BatteryPercent = pct,
        };
    }

    private static int? ParseBattery(object? value)
    {
        if (value is null) return null;
        try
        {
            var pct = Convert.ToInt32(value);
            return pct is >= 0 and <= 100 ? pct : null;
        }
        catch
        {
            return null;
        }
    }
}
