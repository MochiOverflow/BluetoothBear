using Windows.Devices.Enumeration.Pnp;

namespace BluetoothBear.Bluetooth;

/// <summary>
/// Reads battery from the PnP device tree, keyed by Bluetooth address. The battery
/// property often lives on a child function node (e.g. the Hands-Free AG) rather than
/// the association-endpoint node that <see cref="DeviceEnumerator"/> sees, so a tree
/// scan is the reliable source for classic headset battery.
/// </summary>
public static class PnpBatteryReader
{
    // DEVPKEY_Bluetooth_Battery and DEVPKEY_Bluetooth_DeviceAddress.
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string AddressKey = "{2BD67D8B-8BEB-48D5-87E0-6CDA3428040A} 1";

    private static readonly string[] RequestedProperties = [BatteryKey, AddressKey];

    /// <summary>Map of Bluetooth address → battery percent for every node that reports it.</summary>
    public static async Task<IReadOnlyDictionary<ulong, int>> GetBatteryByAddressAsync()
    {
        var map = new Dictionary<ulong, int>();
        try
        {
            var nodes = await PnpObject.FindAllAsync(PnpObjectType.Device, RequestedProperties);
            foreach (var node in nodes)
            {
                if (!node.Properties.TryGetValue(BatteryKey, out var batteryValue) || batteryValue is null)
                {
                    continue;
                }

                int pct;
                try { pct = Convert.ToInt32(batteryValue); }
                catch { continue; }
                if (pct is < 0 or > 100)
                {
                    continue;
                }

                if (!node.Properties.TryGetValue(AddressKey, out var addressValue))
                {
                    continue;
                }

                ulong address = BtDevice.ParseAddress(addressValue as string);
                if (address == 0)
                {
                    continue;
                }

                // Multiple nodes for one device can report; keep the highest reading.
                if (!map.TryGetValue(address, out int existing) || pct > existing)
                {
                    map[address] = pct;
                }
            }
        }
        catch
        {
            // Tree scan unavailable; callers fall back to no battery.
        }

        return map;
    }
}
