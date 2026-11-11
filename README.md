# DriftClock

A lightweight **Windows 11 screensaver** with a huge, thin clock that slowly *drifts* around the screen. Built for **OLED burn-in prevention** through whole-image pixel shifting, while staying pleasant and useful to glance at.

![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue) ![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4) ![license](https://img.shields.io/badge/license-GPL--3.0-green)

## Features

- **Big thin clock** readable from across the desk, with the date underneath.
- **Pixel shifting** of the entire image, so no pixel stays lit in the same place.
- **Weather** (Open-Meteo): icon, temperature, high/low and location, with a selectable location.
- **Battery** percentage and charging state (hidden on desktops).
- **Month grid** with today highlighted (opt-in).
- **Upcoming events** from a private iCal URL, Google or Outlook (opt-in), including recurrences and time zones.
- **Night dimming** from sunset to sunrise at your location, with manual hours as a fallback.
- **Multi-monitor aware**: content on the primary display, the others stay pure black.
- 24-hour time by default, with 12-hour and °F options.

## Lightweight by design

- Single small `.scr`, no installer, no runtime to install (uses .NET Framework 4.8, already in Windows).
- Near-zero CPU, small memory footprint.
- Never keeps the display awake: your power plan stays in charge.
- Fails silently when offline, no error dialogs.

## Install

1. Download `DriftClock.scr` from the [latest release](../../releases/latest).
2. Right-click it and choose **Install**, or copy it to `C:\Windows\System32`.
3. Open *Settings > Personalization > Lock screen > Screen saver* and pick **DriftClock**.

## Build from source

Requires the .NET SDK.

```powershell
dotnet build src/DriftClock -c Release
```

The output is `src/DriftClock/bin/Release/net48/DriftClock.scr`.

## License

GPL-3.0, see [LICENSE](LICENSE).
