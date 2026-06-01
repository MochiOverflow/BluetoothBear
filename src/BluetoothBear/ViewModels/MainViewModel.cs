using System.Collections.ObjectModel;
using System.Windows.Threading;
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
    private bool _isRefreshing;
    private bool _anyConnected;
    private bool _startWithWindows;
    private string _statusSummary = "Loading…";
    private string _trayText = "BluetoothBear";

    public MainViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsRefreshing);
        _startWithWindows = StartupManager.IsEnabled();
        _timer = new DispatcherTimer { Interval = RefreshInterval };
        _timer.Tick += async (_, _) => await RefreshAsync();
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

    public void Stop() => _timer.Stop();

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
