using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BluetoothBear.Converters;

/// <summary>true → green, false → gray. Used for the connection status dot.</summary>
public sealed class ConnectedBrushConverter : IValueConverter
{
    private static readonly Brush Connected = new SolidColorBrush(Color.FromRgb(0x22, 0xA3, 0x4A));
    private static readonly Brush Disconnected = new SolidColorBrush(Color.FromRgb(0xB0, 0xB4, 0xBA));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Connected : Disconnected;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Battery percent → green / amber / red.</summary>
public sealed class BatteryBrushConverter : IValueConverter
{
    private static readonly Brush High = new SolidColorBrush(Color.FromRgb(0x22, 0xA3, 0x4A));
    private static readonly Brush Mid = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush Low = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int pct = value is int i ? i : 0;
        return pct <= 20 ? Low : pct <= 50 ? Mid : High;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true → Visible, false → Collapsed.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>true → Collapsed, false → Visible.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}
