using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BluetoothBear.Bluetooth;

/// <summary>A nearby, pairable device surfaced during discovery.</summary>
public sealed class DiscoveredDevice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int? SignalDbm { get; init; }
}

/// <summary>
/// Live discovery of nearby unpaired Bluetooth devices (classic + LE) via WinRT
/// <see cref="DeviceWatcher"/>, plus in-app pairing. Watcher callbacks arrive on a
/// background thread — subscribers must marshal to the UI thread themselves.
/// </summary>
public sealed class DeviceDiscovery
{
    private const string CanPairKey = "System.Devices.Aep.CanPair";
    private const string IsPairedKey = "System.Devices.Aep.IsPaired";
    private const string SignalKey = "System.Devices.Aep.SignalStrength";

    private static readonly string[] RequestedProperties =
    [
        "System.Devices.Aep.DeviceAddress",
        "System.Devices.Aep.IsPresent",
        SignalKey,
        CanPairKey,
        IsPairedKey,
    ];

    private readonly List<DeviceWatcher> _watchers = [];
    private readonly Dictionary<string, DeviceInformation> _found = new();
    private readonly object _gate = new();

    public event Action<DiscoveredDevice>? DeviceAdded;
    public event Action<DiscoveredDevice>? DeviceUpdated;
    public event Action<string>? DeviceRemoved;

    public void Start()
    {
        Stop();
        AddWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(false));
        AddWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(false));
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.Added -= OnAdded;
                watcher.Updated -= OnUpdated;
                watcher.Removed -= OnRemoved;
                if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                {
                    watcher.Stop();
                }
            }
            catch
            {
                // Watcher already stopping/disposed.
            }
        }

        _watchers.Clear();
        lock (_gate)
        {
            _found.Clear();
        }
    }

    public async Task<DevicePairingResultStatus> PairAsync(string id)
    {
        DeviceInformation? device;
        lock (_gate)
        {
            _found.TryGetValue(id, out device);
        }
        if (device is null)
        {
            return DevicePairingResultStatus.Failed;
        }

        var custom = device.Pairing.Custom;

        // Accept the "just works" / confirm ceremonies headphones and most accessories use.
        void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            switch (args.PairingKind)
            {
                case DevicePairingKinds.ConfirmOnly:
                case DevicePairingKinds.DisplayPin:
                case DevicePairingKinds.ConfirmPinMatch:
                    args.Accept();
                    break;
            }
        }

        custom.PairingRequested += OnPairingRequested;
        try
        {
            var result = await custom.PairAsync(
                DevicePairingKinds.ConfirmOnly | DevicePairingKinds.DisplayPin | DevicePairingKinds.ConfirmPinMatch);
            return result.Status;
        }
        catch
        {
            return DevicePairingResultStatus.Failed;
        }
        finally
        {
            custom.PairingRequested -= OnPairingRequested;
        }
    }

    private void AddWatcher(string selector)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
        watcher.Added += OnAdded;
        watcher.Updated += OnUpdated;
        watcher.Removed += OnRemoved;
        _watchers.Add(watcher);
        watcher.Start();
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation info)
    {
        if (!ShouldShow(info))
        {
            return;
        }

        lock (_gate)
        {
            _found[info.Id] = info;
        }
        DeviceAdded?.Invoke(ToModel(info));
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        DeviceInformation? info;
        lock (_gate)
        {
            _found.TryGetValue(update.Id, out info);
        }
        if (info is null)
        {
            return;
        }

        info.Update(update);
        DeviceUpdated?.Invoke(ToModel(info));
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        bool removed;
        lock (_gate)
        {
            removed = _found.Remove(update.Id);
        }
        if (removed)
        {
            DeviceRemoved?.Invoke(update.Id);
        }
    }

    private static bool ShouldShow(DeviceInformation info)
    {
        if (string.IsNullOrWhiteSpace(info.Name))
        {
            return false; // hide nameless beacons/peripherals
        }
        if (info.Properties.TryGetValue(CanPairKey, out var canPair) && canPair is false)
        {
            return false;
        }
        if (info.Properties.TryGetValue(IsPairedKey, out var isPaired) && isPaired is true)
        {
            return false;
        }
        return true;
    }

    private static DiscoveredDevice ToModel(DeviceInformation info)
    {
        int? signal = null;
        if (info.Properties.TryGetValue(SignalKey, out var value) && value is not null)
        {
            try { signal = Convert.ToInt32(value); }
            catch { /* leave null */ }
        }

        return new DiscoveredDevice
        {
            Id = info.Id,
            Name = info.Name,
            SignalDbm = signal,
        };
    }
}
