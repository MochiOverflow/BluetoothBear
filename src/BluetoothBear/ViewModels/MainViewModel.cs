using System.Collections.ObjectModel;
using System.Windows.Threading;
using Windows.Devices.Enumeration;
using BluetoothBear.Bluetooth;

namespace BluetoothBear.ViewModels;

/// <summary>
/// Drives the window: keeps the device list fresh (timer + on demand), maps connection
/// toggles to the Bluetooth services, and exposes tray-icon state.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly DispatcherTimer _timer;
    private readonly Dispatcher _dispatcher;
    private readonly AppSettings _settings;
    private readonly DeviceDiscovery _discovery = new();
    private bool _isRefreshing;
    private bool _anyConnected;
    private bool _startWithWindows;
    private bool _isDiscovering;
    private string _statusSummary = "Loading…";
    private string _trayText = "BluetoothBear";

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _settings = AppSettings.Load();
        _startWithWindows = StartupManager.IsEnabled();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsRefreshing);
        BeginAddDeviceCommand = new RelayCommand(BeginAddDevice);
        CloseDiscoveryCommand = new RelayCommand(ExitDiscovery);

        _discovery.DeviceAdded += OnDiscoveryAdded;
        _discovery.DeviceUpdated += OnDiscoveryUpdated;
        _discovery.DeviceRemoved += OnDiscoveryRemoved;

        _timer = new DispatcherTimer { Interval = RefreshInterval };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public ObservableCollection<DiscoveredDeviceViewModel> Discovered { get; } = [];

    public RelayCommand BeginAddDeviceCommand { get; }

    public RelayCommand CloseDiscoveryCommand { get; }

    /// <summary>True while the flyout is showing the in-app "Add a device" discovery view.</summary>
    public bool IsDiscovering
    {
        get => _isDiscovering;
        private set
        {
            if (Set(ref _isDiscovering, value))
            {
                Raise(nameof(ShowDeviceList));
            }
        }
    }

    /// <summary>Inverse of <see cref="IsDiscovering"/>, for binding the normal list's visibility.</summary>
    public bool ShowDeviceList => !_isDiscovering;

    public bool HasNoDiscovered => Discovered.Count == 0;

    /// <summary>Which "Add a device" experience the button triggers; persisted on change.</summary>
    public PairingMethod PairingMethod
    {
        get => _settings.PairingMethod;
        set
        {
            if (_settings.PairingMethod == value) return;
            _settings.PairingMethod = value;
            _settings.Save();
            Raise();
        }
    }

    /// <summary>Launch BluetoothBear at sign-in (HKCU Run key). Bound to the footer checkbox.</summary>
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (!Set(ref _startWithWindows, value)) return;

            StartupManager.SetEnabled(value);

            // Reflect what actually stuck, in case the registry write was blocked.
            bool actual = StartupManager.IsEnabled();
            if (actual != _startWithWindows)
            {
                _startWithWindows = actual;
                Raise();
            }
        }
    }

    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (Set(ref _isRefreshing, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasNoDevices => Devices.Count == 0;

    public bool AnyConnected
    {
        get => _anyConnected;
        private set => Set(ref _anyConnected, value);
    }

    public string StatusSummary
    {
        get => _statusSummary;
        private set => Set(ref _statusSummary, value);
    }

    public string TrayText
    {
        get => _trayText;
        private set => Set(ref _trayText, value);
    }

    public async Task StartAsync()
    {
        _timer.Start();
        await RefreshAsync();
    }

    public void Stop()
    {
        _timer.Stop();
        _discovery.Stop();
    }

    private void BeginAddDevice()
    {
        switch (PairingMethod)
        {
            case PairingMethod.WindowsSettings:
                PairingLauncher.OpenWindowsSettings();
                break;
            case PairingMethod.ClassicWizard:
                PairingLauncher.OpenClassicWizard();
                break;
            default:
                StartDiscovery();
                break;
        }
    }

    private void StartDiscovery()
    {
        Discovered.Clear();
        Raise(nameof(HasNoDiscovered));
        IsDiscovering = true;
        _discovery.Start();
    }

    /// <summary>Leave the discovery view and stop the radio scan. Safe to call when not discovering.</summary>
    public void ExitDiscovery()
    {
        if (!_isDiscovering) return;
        _discovery.Stop();
        IsDiscovering = false;
        Discovered.Clear();
        Raise(nameof(HasNoDiscovered));
    }

    private async Task PairAsync(DiscoveredDeviceViewModel device)
    {
        if (device.IsPairing) return;

        device.IsPairing = true;
        try
        {
            var status = await _discovery.PairAsync(device.Id);
            if (status is DevicePairingResultStatus.Paired or DevicePairingResultStatus.AlreadyPaired)
            {
                ExitDiscovery();
                await RefreshAsync(); // the newly paired device shows up in the main list
            }
            else
            {
                device.SetError(Describe(status));
                device.IsPairing = false;
            }
        }
        catch
        {
            device.SetError("Pairing failed");
            device.IsPairing = false;
        }
    }

    private static string Describe(DevicePairingResultStatus status) => status switch
    {
        DevicePairingResultStatus.PairingCanceled => "Pairing canceled",
        DevicePairingResultStatus.AuthenticationFailure => "Authentication failed",
        DevicePairingResultStatus.ConnectionRejected => "Connection rejected",
        DevicePairingResultStatus.AccessDenied => "Access denied",
        DevicePairingResultStatus.NotReadyToPair => "Device not ready",
        _ => "Couldn't pair",
    };

    // Watcher callbacks arrive off the UI thread — marshal before touching the collection.
    private void OnDiscoveryAdded(DiscoveredDevice device) => _dispatcher.BeginInvoke(() =>
    {
        if (Discovered.Any(x => x.Id == device.Id)) return;
        Discovered.Add(new DiscoveredDeviceViewModel(device, PairAsync));
        Raise(nameof(HasNoDiscovered));
    });

    private void OnDiscoveryUpdated(DiscoveredDevice device) => _dispatcher.BeginInvoke(() =>
        Discovered.FirstOrDefault(x => x.Id == device.Id)?.Apply(device));

    private void OnDiscoveryRemoved(string id) => _dispatcher.BeginInvoke(() =>
    {
        var existing = Discovered.FirstOrDefault(x => x.Id == id);
        if (existing is not null)
        {
            Discovered.Remove(existing);
            Raise(nameof(HasNoDiscovered));
        }
    });

    public async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            var devices = await DeviceEnumerator.GetPairedDevicesAsync();
            devices = await EnrichLeBatteryAsync(devices);
            MergeDevices(devices);
            UpdateSummary();
        }
        catch
        {
            // Keep the previous snapshot on transient failures.
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private static async Task<IReadOnlyList<BtDevice>> EnrichLeBatteryAsync(IReadOnlyList<BtDevice> devices)
    {
        var needsBattery = devices
            .Where(d => d is { IsConnected: true, BatteryPercent: null, Kind: BtKind.LowEnergy })
            .ToList();
        if (needsBattery.Count == 0)
        {
            return devices;
        }

        var reads = await Task.WhenAll(needsBattery.Select(async d => (d.Address, Pct: await BatteryReader.TryReadLeBatteryAsync(d))));
        var found = reads.Where(r => r.Pct.HasValue).ToDictionary(r => r.Address, r => r.Pct);
        if (found.Count == 0)
        {
            return devices;
        }

        return devices
            .Select(d => found.TryGetValue(d.Address, out var pct)
                ? new BtDevice { Name = d.Name, AepId = d.AepId, Address = d.Address, Kind = d.Kind, IsConnected = d.IsConnected, BatteryPercent = pct }
                : d)
            .ToList();
    }

    private async Task ToggleDeviceAsync(DeviceViewModel device)
    {
        if (device.IsBusy) return;

        device.IsBusy = true;
        bool wasConnected = device.IsConnected;
        try
        {
            _ = wasConnected
                ? await ConnectionController.DisconnectAsync(device.Model)
                : await ConnectionController.ConnectAsync(device.Model);

            // Let the stack settle, then reflect the real state.
            await Task.Delay(1500);
            await RefreshAsync();
        }
        finally
        {
            device.IsBusy = false;
        }
    }

    /// <summary>Merge in place by key so existing cards (and in-flight toggles) survive a refresh.</summary>
    private void MergeDevices(IReadOnlyList<BtDevice> latest)
    {
        var existing = Devices.ToDictionary(d => d.Key);

        var ordered = new List<DeviceViewModel>(latest.Count);
        foreach (var model in latest)
        {
            string key = DeviceViewModel.KeyOf(model);
            if (existing.TryGetValue(key, out var vm))
            {
                vm.Apply(model);
            }
            else
            {
                vm = new DeviceViewModel(model, ToggleDeviceAsync);
            }
            ordered.Add(vm);
        }

        // Remove devices that are gone.
        for (int i = Devices.Count - 1; i >= 0; i--)
        {
            if (!ordered.Contains(Devices[i]))
            {
                Devices.RemoveAt(i);
            }
        }

        // Insert/move to match the desired order.
        for (int i = 0; i < ordered.Count; i++)
        {
            int current = Devices.IndexOf(ordered[i]);
            if (current < 0)
            {
                Devices.Insert(i, ordered[i]);
            }
            else if (current != i)
            {
                Devices.Move(current, i);
            }
        }

        Raise(nameof(HasNoDevices));
    }

    private void UpdateSummary()
    {
        int total = Devices.Count;
        int connected = Devices.Count(d => d.IsConnected);
        AnyConnected = connected > 0;

        StatusSummary = total == 0
            ? "No paired devices"
            : $"{total} device{(total == 1 ? "" : "s")} • {connected} connected";

        string trayText = connected switch
        {
            0 => "BluetoothBear — nothing connected",
            1 => $"BluetoothBear — {Devices.First(d => d.IsConnected).Name}",
            _ => $"BluetoothBear — {connected} devices connected",
        };
        TrayText = trayText.Length <= 63 ? trayText : trayText[..62] + "…";
    }
}
