using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BluetoothBear.Bluetooth;

/// <summary>
/// Best-effort battery read for BLE devices via the standard GATT Battery Service.
/// Classic-device battery comes from the enumeration property instead (see DeviceEnumerator).
/// </summary>
public static class BatteryReader
{
    public static async Task<int?> TryReadLeBatteryAsync(BtDevice device)
    {
        if (device.Kind != BtKind.LowEnergy)
        {
            return null;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            return await ReadAsync(device.AepId).WaitAsync(cts.Token);
        }
        catch
        {
            return null; // unreachable device, no battery service, or timeout
        }
    }

    private static async Task<int?> ReadAsync(string aepId)
    {
        using var le = await BluetoothLEDevice.FromIdAsync(aepId);
        if (le is null)
        {
            return null;
        }

        var services = await le.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached);
        var service = services.Services.FirstOrDefault();
        if (service is null)
        {
            return null;
        }

        var chars = await service.GetCharacteristicsForUuidAsync(
            GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached);
        var characteristic = chars.Characteristics.FirstOrDefault();
        if (characteristic is null)
        {
            return null;
        }

        var read = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
        if (read.Status != GattCommunicationStatus.Success || read.Value is null || read.Value.Length == 0)
        {
            return null;
        }

        var reader = DataReader.FromBuffer(read.Value);
        int pct = reader.ReadByte();
        return pct is >= 0 and <= 100 ? pct : null;
    }
}
