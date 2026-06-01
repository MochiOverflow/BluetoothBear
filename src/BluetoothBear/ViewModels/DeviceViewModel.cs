using BluetoothBear.Bluetooth;

namespace BluetoothBear.ViewModels;

/// <summary>Presents one <see cref="BtDevice"/> for the card UI and exposes a toggle command.</summary>
public sealed class DeviceViewModel : ObservableObject
{
    private readonly Func<DeviceViewModel, Task> _toggle;
    private BtDevice _model;
    private bool _isBusy;

    public DeviceViewModel(BtDevice model, Func<DeviceViewModel, Task> toggle)
    {
        _model = model;
        _toggle = toggle;
        Key = KeyOf(model);
        ToggleCommand = new AsyncRelayCommand(() => _toggle(this), () => !IsBusy && _model.HasAddress);
    }

    public AsyncRelayCommand ToggleCommand { get; }

    /// <summary>Stable identity across refreshes (MAC address if known, else the AEP id).</summary>
    public string Key { get; private set; }

    public BtDevice Model => _model;

    public string Name => _model.Name;

    public bool IsConnected => _model.IsConnected;

    public bool HasAddress => _model.HasAddress;

    public bool HasBattery => _model.BatteryPercent.HasValue;

    public int BatteryPercent => _model.BatteryPercent ?? 0;

    public string BatteryText => _model.BatteryPercent is int p ? $"{p}%" : string.Empty;

    public string KindText => _model.Kind == BtKind.LowEnergy ? "Bluetooth LE" : "Bluetooth";

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (Set(ref _isBusy, value))
            {
                Raise(nameof(StatusText));
                ToggleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText => _isBusy
        ? (_model.IsConnected ? "Disconnecting…" : "Connecting…")
        : (_model.IsConnected ? "Connected" : "Disconnected");

    /// <summary>Re-point this view model at a fresh device snapshot, keeping the same instance.</summary>
    public void Apply(BtDevice model)
    {
        _model = model;
        Key = KeyOf(model);
        Raise(nameof(Name));
        Raise(nameof(IsConnected));
        Raise(nameof(HasAddress));
        Raise(nameof(HasBattery));
        Raise(nameof(BatteryPercent));
        Raise(nameof(BatteryText));
        Raise(nameof(KindText));
        Raise(nameof(StatusText));
        ToggleCommand.RaiseCanExecuteChanged();
    }

    public static string KeyOf(BtDevice d) => d.HasAddress ? $"A:{d.Address:X}" : $"I:{d.AepId}";
}
