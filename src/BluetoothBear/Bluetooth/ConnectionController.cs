using System.Runtime.InteropServices;
using static BluetoothBear.Bluetooth.NativeMethods;

namespace BluetoothBear.Bluetooth;

/// <summary>
/// Connects/disconnects classic Bluetooth devices by enabling/disabling their audio
/// service drivers via BluetoothSetServiceState. Blocking; call from a background thread.
/// </summary>
public static class ConnectionController
{
    public static Task<bool> ConnectAsync(BtDevice device) => Task.Run(() => SetState(device, connect: true));

    public static Task<bool> DisconnectAsync(BtDevice device) => Task.Run(() => SetState(device, connect: false));

    private static bool SetState(BtDevice device, bool connect)
    {
        if (!device.HasAddress)
        {
            return false;
        }

        var findParams = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = (uint)Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>() };
        IntPtr findHandle = BluetoothFindFirstRadio(ref findParams, out IntPtr radio);
        if (findHandle == IntPtr.Zero || radio == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var info = new BLUETOOTH_DEVICE_INFO
            {
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(),
                Address = device.Address,
            };

            // Populate the rest of the struct from the paired record. Non-fatal if it fails.
            _ = BluetoothGetDeviceInfo(radio, ref info);

            uint flag = connect ? BLUETOOTH_SERVICE_ENABLE : BLUETOOTH_SERVICE_DISABLE;

            // Toggle both A2DP and HFP so headsets fully (dis)connect. Success if either takes.
            bool any = false;
            Guid audio = AudioSinkServiceClass;
            Guid hands = HandsfreeServiceClass;
            any |= BluetoothSetServiceState(radio, ref info, ref audio, flag) == ERROR_SUCCESS;
            any |= BluetoothSetServiceState(radio, ref info, ref hands, flag) == ERROR_SUCCESS;
            return any;
        }
        finally
        {
            CloseHandle(radio);
            BluetoothFindRadioClose(findHandle);
        }
    }
}
