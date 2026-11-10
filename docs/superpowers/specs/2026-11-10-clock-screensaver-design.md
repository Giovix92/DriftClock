# Clock Screensaver — Design

Date: 2026-11-10
Working name: **DriftClock** (Claude's call; rename freely)

## Intent

A Windows 11 screensaver for a desk monitor or laptop whose primary job is to
prevent OLED burn-in and image retention via pixel shifting, while being
pleasant and useful to glance at: a big clock plus a few small extras
(weather, battery, date, calendar). It must be lightweight.

## Goals

- Huge, thin clock digits that read from across a desk.
- Pixel shifting of the *entire* rendered image so no pixel stays lit in one place.
- Small, dim extras: date, weather for a user-chosen location, battery (if present),
  month grid (opt-in), upcoming iCal events (opt-in).
- Dim at night (sunset → sunrise at the weather location; manual hours fallback).
- Lightweight: single small `.scr`, no runtime to install, near-zero CPU.

## Non-goals

- No content on secondary monitors (they are pure black).
- No account sign-in / OAuth for calendars — iCal URL only.
- No installer; distribution is the `.scr` file.
- No seconds display, no decorative animation beyond the drift.

## Anti-goals (would be a failure even if it "works")

- Noticeable CPU/GPU use or memory bloat (target: well under 1% CPU, < 50 MB RAM).
- Requiring the user to install a runtime or anything else.
- Any static bright element that never moves (defeats the purpose).
- Preventing the system from sleeping / turning off the display per power plan
  (never call `SetThreadExecutionState` or otherwise hold the display on).
- Crashing or showing error dialogs when offline; failures degrade silently.

## Decisions made with the user

- **Layout C ("clock alone, extras in corners")** on a pure black background:
  - Center: time (very large, thin weight), date on one line below it
    (e.g. "Tuesday, 10 November").
  - Top-left: weather — icon, current temperature, today's high/low, location name. Small, dim.
  - Top-right: battery percentage (+ charging indicator). Hidden entirely when the
    machine has no battery.
  - Bottom-left: upcoming events from iCal (opt-in).
  - Bottom-right: small month grid with today highlighted (opt-in). Toggle independent
    from events.
  - When an extra is off/unavailable its corner is simply empty; the clock does not move to fill it.
  - Mockup reference: `.superpowers/brainstorm/*/content/layout-v2.html`, option C.
- **Calendar:** both a month grid and real events, each opt-in in settings. Events come
  from a pasted private iCal URL (Google/Outlook "secret address in iCal format").
- **Weather:** location is selectable in settings.
- **Night dimming:** dim between sunset and sunrise at the weather location; if that is
  unavailable (no location set, offline with no cache), use manual night hours set in settings.
- **Multi-monitor:** everything on the primary monitor; others pure black.
- **Defaults:** 24-hour time, no seconds, °C. Settings toggles for 12-hour and °F.
- **Tech:** Claude's call, accepted by the user — C# targeting **.NET Framework 4.8**
  (preinstalled on Windows 11), WinForms + GDI+, built with the installed .NET 8 SDK
  (`net48` target with `Microsoft.NETFramework.ReferenceAssemblies`). Output renamed to `.scr`.

## Claude's calls (user did not specify; reasonable defaults)

- **Text colour:** greys, not pure white. Clock ≈ #C8C8C8, extras ≈ #707070. Configurable
  is not required.
- **Font:** Segoe UI Light (or Segoe UI Variable Display Light if present) — ships with Windows.
  Clock height ≈ 28–30% of screen height.
- **Pixel shift:** every 60 s, move the whole frame (corners included) to a new offset within a
  ±16 px (DPI-scaled) box, chosen so consecutive positions differ by at least a few pixels and
  cycle through the box over time (e.g. a slow Lissajous or shuffled grid walk). Corner
  margins must account for the shift so nothing clips.
- **Night dim:** night brightness default 35% (slider in settings, 10–100%). Transition can be instant
  or a short fade; builder's choice.
- **Events shown:** today's remaining events plus the next day's if fewer than 3, max 4 lines,
  format "16:00 Title" (all-day events show "All day"). Times displayed in local time.
- **iCal parsing:** hand-rolled minimal parser (no NuGet dependency): VEVENT with DTSTART/DTEND
  (UTC, floating, TZID via Windows time zone mapping, all-day DATE), SUMMARY, line unfolding,
  basic RRULE (FREQ DAILY/WEEKLY/MONTHLY/YEARLY, INTERVAL, COUNT, UNTIL, BYDAY) and EXDATE.
  Unsupported recurrences are skipped, not crash. `webcal://` is rewritten to `https://`.
- **Weather provider:** Open-Meteo (free, no API key) — forecast endpoint for current temp,
  weather code, daily max/min, sunrise/sunset; geocoding endpoint for the location search.
- **Refresh:** weather every 30 min, iCal every 15 min, battery every 60 s, repaint once per
  second only if the displayed minute/state changed (otherwise once per minute). Network calls
  run off the UI thread with short timeouts.
- **Cache:** last weather and calendar responses cached to `%LOCALAPPDATA%\DriftClock\` so the
  screen fills immediately on start and works offline. Stale data (> 3 h weather) is shown
  dimmer or hidden — builder's choice.
- **Settings storage:** simple key=value file at `%APPDATA%\DriftClock\settings.ini`
  (no JSON library needed on net48).
- **Settings dialog (`/c`):** small WinForms dialog: location search box → results list → pick
  (stores name, lat, lon); 12h toggle; °F toggle; show weather; show battery; show month grid;
  show events + iCal URL field; night dim brightness; manual night hours (from/to). OK/Cancel.
- **Exit behaviour:** close on any key, mouse click, or mouse movement beyond a few pixels from
  the start position (ignore jitter).
- **Screensaver modes:** `/s` fullscreen; `/c` (and no args) settings; `/p <hwnd>` renders a
  scaled-down live preview inside the Windows screensaver settings thumbnail. Argument forms
  `/c:1234`, `/p 1234`, case-insensitive, must all be handled.
- **DPI:** per-monitor DPI aware so text is crisp on scaled laptop displays.
- **Weather icons:** drawn simply (Segoe UI Symbol / Segoe Fluent Icons glyphs or small vector
  shapes) — no image assets required.

- **IANA time zones in iCal:** embed a small IANA→Windows zone mapping table (net48 has no
  built-in conversion).
- **Locale:** date text and the month grid's first weekday follow the system regional settings.
- **iCal URL secrecy:** stored encrypted with DPAPI (current user) in settings.ini, not plain text.
- **Calendar staleness:** cached events older than 24 h are hidden.
- **Night dim source:** sunrise/sunset come from the configured location even if the weather
  corner is turned off.

## Left to the builder

- Internal code structure, class names, rendering technique details (e.g. double buffering).
- Exact drift path algorithm, within the constraints above.
- Exact typography sizes/spacing of the extras, within "small and dim".
- Fade vs instant night-dim transition.
- How stale data is visually indicated.

## Constraints

- Windows 11, .NET Framework 4.8, no extra runtime, no NuGet runtime dependencies.
- Single `.scr` output file (small; ~100–300 KB target).
- Must install via right-click → Install, or copying into `C:\Windows\System32`.
