using BluetoothBear.Bluetooth;

namespace BluetoothBear.ViewModels;

/// <summary>A nearby pairable device shown in the discovery view, with a Pair command.</summary>
public sealed class DiscoveredDeviceViewModel : ObservableObject
{
    private readonly Func<DiscoveredDeviceViewModel, Task> _pair;
    private bool _isPairing;
    private string? _error;

    public DiscoveredDeviceViewModel(DiscoveredDevice model, Func<DiscoveredDeviceViewModel, Task> pair)
    {
        Id = model.Id;
        Name = model.Name;
        SignalText = FormatSignal(model.SignalDbm);
        _pair = pair;
        PairCommand = new AsyncRelayCommand(() => _pair(this), () => !IsPairing);
    }

    public string Id { get; }

    public string Name { get; private set; }

    public string SignalText { get; private set; }

    public AsyncRelayCommand PairCommand { get; }

    public bool IsPairing
    {
        get => _isPairing;
        set
        {
            if (Set(ref _isPairing, value))
            {
                Raise(nameof(StatusText));
                Raise(nameof(HasStatus));
                PairCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText => _error ?? (_isPairing ? "Pairing…" : string.Empty);

    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    public bool IsError => _error is not null;

    public void SetError(string message)
    {
        _error = message;
        Raise(nameof(StatusText));
        Raise(nameof(HasStatus));
        Raise(nameof(IsError));
    }

    public void Apply(DiscoveredDevice model)
    {
        Name = model.Name;
        SignalText = FormatSignal(model.SignalDbm);
        Raise(nameof(Name));
        Raise(nameof(SignalText));
    }

    private static string FormatSignal(int? dbm) => dbm is int v ? $"{v} dBm" : string.Empty;
}
