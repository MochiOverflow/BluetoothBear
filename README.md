# BluetoothBear 🐻

A lightweight Windows app to quickly connect/disconnect Bluetooth devices (built for
wireless headphones/earbuds) and glance at their **battery level**.

It lives in the **system tray** and opens as a **flyout** — a compact, rounded popup
anchored in the bottom-right corner above the taskbar (like the volume/network panels),
not a full window. **Left-click** the tray icon to toggle the flyout; click away (or press
**Esc**) to dismiss it. **Right-click** the tray icon for quick actions (Open / Refresh / Exit).

## Features

- A flyout with a card per paired Bluetooth device (classic + BLE) and a connected (green) /
  disconnected (gray) status dot.
- **Connect / Disconnect** button per device (classic audio via `BluetoothSetServiceState`).
- **Add a device** button that opens Windows' pairing wizard (`DevicePairingWizard.exe`,
  falling back to the Bluetooth settings page) to pair new devices.
- Shows **battery %** with a colored bar when the device reports it (Windows 11 PnP
  property for classic headsets; GATT Battery Service for BLE). Best-effort — not every
  device reports battery.
- Auto-refreshes every 30s, each time the flyout opens, and on demand. "Open Bluetooth settings" shortcut.
- **Start with Windows** checkbox (per-user HKCU Run entry; no admin needed).
- Single instance; always in the tray; tray icon turns blue when something is connected, gray otherwise.

## Architecture

- **Host** (`App.xaml.cs`): WPF `Application` that owns a WinForms `NotifyIcon` (tray) and
  the flyout; `ShutdownMode=OnExplicitShutdown` keeps it alive in the tray. Handles
  show/toggle/auto-hide (with a short suppress window + debounce to avoid flicker on click).
- **UI** (`MainWindow.xaml`): a borderless, transparent, top-most flyout
  (`WindowStyle=None`, `ShowInTaskbar=False`, `SizeToContent=Height`) anchored to the
  work-area corner via `ShowAtTray()`; card list bound to view models; styling/converters inline.
- **MVVM** (`ViewModels/`): `MainViewModel` (refresh, in-place merge, 30s timer, tray state)
  and `DeviceViewModel` (status/battery/toggle command).
- **Bluetooth** (`Bluetooth/`): UI-agnostic services — `DeviceEnumerator` (WinRT),
  `ConnectionController` (Win32 P/Invoke), `PnpBatteryReader` (PnP tree scan for classic
  headset battery), `BatteryReader` (BLE GATT fallback).

### How battery reading works

Classic headset battery is not exposed on the association-endpoint node that WinRT's
paired-device enumeration returns. It lives on a child function node (e.g. the
*Hands-Free AG*). `PnpBatteryReader` scans the PnP device tree via `PnpObject.FindAllAsync`,
reads `DEVPKEY_Bluetooth_Battery` + `DEVPKEY_Bluetooth_DeviceAddress` on every node, and
maps battery → address. `DeviceEnumerator` then merges that into each device by address.
Still best-effort: devices that don't report battery to Windows show none.

## Requirements

- Windows 10 1903+ / Windows 11
- .NET 9 SDK (to build) — the published self-contained build needs no runtime installed.

## Build & run

```powershell
dotnet run --project src/BluetoothBear
```

## Publish a single .exe

```powershell
dotnet publish src/BluetoothBear -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The result lands in `src/BluetoothBear/bin/Release/net9.0-windows10.0.19041.0/win-x64/publish/`.

## Notes & limitations

- **Connect/disconnect** of classic audio devices uses the Win32
  `BluetoothSetServiceState` API (there is no clean public WinRT equivalent). It enables
  /disables the A2DP Audio Sink and Hands-Free service drivers.
- **Battery** for classic headsets relies on the device/driver populating the Windows
  `DEVPKEY_Bluetooth_Battery` property — many do on Windows 11, some don't.

## Roadmap

- [x] "Start with Windows" toggle (HKCU Run key)
- [ ] Per-device connect/disconnect notifications polish
- [ ] Distinct per-device status submenu (signal, address, services)
