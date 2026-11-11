<h1 align="center">🕰️ DriftClock</h1>

<p align="center">
  <b>A huge, thin clock that slowly drifts around your screen.</b><br>
  A lightweight Windows 11 screensaver built to keep OLED panels safe from burn-in.
</p>

<p align="center">
  <a href="../../releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/Giovix92/DriftClock?style=for-the-badge&color=7aa2ff"></a>
  <a href="../../releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/Giovix92/DriftClock/total?style=for-the-badge&color=57c7a5"></a>
  <img alt="Windows" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=for-the-badge&logo=windows11&logoColor=white">
  <img alt=".NET Framework 4.8" src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/github/license/Giovix92/DriftClock?style=for-the-badge&color=f5a97f"></a>
</p>

---

## 🌙 What it does

Black screen, big thin digits, a date, and a few small dim extras tucked in the corners. The whole picture slowly **drifts**, so no pixel stays lit in the same spot.

```
┌──────────────────────────────────────────────┐
│  ☀ 18°  12° / 19°  Rome            🔋 82% ⚡  │
│                                              │
│                                              │
│                   21:47                      │
│            Tuesday, 10 November              │
│                                              │
│                                              │
│  Dentist · 09:30                  ▦ Nov 2026 │
│  Dinner · 20:00                              │
└──────────────────────────────────────────────┘
```

## ✨ Features

| | |
|---|---|
| 🔢 **Big thin clock** | Readable from across the desk. 24-hour by default, 12-hour optional, no seconds. |
| 🌀 **Pixel shifting** | The *entire* rendered image drifts, which is what actually protects the panel. |
| ⛅ **Weather** | Icon, temperature, high/low and place name from Open-Meteo, with a location you choose. |
| 🔋 **Battery** | Percentage and charging state, hidden on desktops. |
| 📅 **Calendar** | Optional month grid, plus upcoming events from a private iCal URL (Google or Outlook), recurrences and time zones included. |
| 🌗 **Night dimming** | Dims from sunset to sunrise at your location, with manual hours as a fallback. |
| 🖥️ **Multi-monitor** | Content on the primary display, every other screen stays pure black. |

## 🪶 Light by design

- 📦 One small `.scr`. No installer, no runtime to install: it uses the .NET Framework 4.8 already in Windows.
- ⚙️ Near-zero CPU and a small memory footprint.
- 😴 Never keeps the display awake. Your power plan stays in charge.
- 📴 Offline? It degrades silently, with no error dialogs.

## 📥 Install

1. Download `DriftClock.scr` from the [latest release](../../releases/latest).
2. Right-click it and choose **Install** (or copy it to `C:\Windows\System32`).
3. Open **Settings → Personalization → Lock screen → Screen saver** and pick **DriftClock**.

## 🛠️ Build from source

Requires the .NET SDK.

```powershell
dotnet build src/DriftClock -c Release
```

The result is `src/DriftClock/bin/Release/net48/DriftClock.scr`.

## 📄 License

[GPL-3.0](LICENSE)
