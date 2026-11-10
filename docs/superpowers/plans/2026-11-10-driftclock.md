# DriftClock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A lightweight Windows 11 `.scr` screensaver showing a huge pixel-shifting clock with dim corner extras (weather, battery, iCal events, month grid) and night dimming.

**Architecture:** One WinForms executable targeting .NET Framework 4.8. Pure logic (arg parsing, settings, drift path, night schedule, Open-Meteo parsing, iCal parsing/recurrence, event selection, exit detection) lives in `Core/` and `Data/` with no WinForms dependency and is unit-tested. `UI/` holds a GDI+ renderer and thin forms (fullscreen saver, black secondary screens, preview child window, settings dialog). Background refresh runs on `System.Threading.Timer`s and swaps an immutable `ScreenState` snapshot read by the UI timer.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms + GDI+, `System.Web.Extensions` (JavaScriptSerializer) for JSON, `System.Security` (DPAPI `ProtectedData`), xUnit 2 for tests, built via installed .NET 8 SDK with `Microsoft.NETFramework.ReferenceAssemblies` (build-time only).

**Spec:** `docs/superpowers/specs/2026-11-10-clock-screensaver-design.md`

## Global Constraints

- Target `net48`; no NuGet **runtime** dependencies (build/test-only packages allowed). Single output file renamed `DriftClock.scr`, ~100–300 KB.
- Well under 1% CPU, < 50 MB RAM. Repaint only when displayed content/offset/brightness changed; check once per second.
- Never call `SetThreadExecutionState` or otherwise hold the display/system awake.
- Offline/errors degrade silently — no dialogs, no crashes in `/s` mode.
- Background `#000000`; clock ≈ `#C8C8C8`; extras ≈ `#707070`. Font Segoe UI Variable Display Light if installed, else Segoe UI Light. Clock height ≈ 28–30% of screen height.
- Drift: every 60 s, whole frame (corners included) to a new offset within ±16 px (DPI-scaled); corner margins ≥ drift range so nothing clips.
- Defaults: 24h, no seconds, °C, night brightness 35% (range 10–100), manual night 23:00–07:00.
- Refresh: weather 30 min, iCal 15 min, battery 60 s. Weather stale > 3 h → hidden; calendar cache > 24 h → hidden.
- Paths: settings `%APPDATA%\DriftClock\settings.ini`; cache `%LOCALAPPDATA%\DriftClock\`. iCal URL stored DPAPI (CurrentUser) encrypted, base64.
- Secondary monitors pure black. Per-monitor DPI aware.
- Date text and month grid first weekday follow `CultureInfo.CurrentCulture`.

## Review Focus

1. **Startup mouse jitter** — Windows delivers WM_MOUSEMOVE right after the window shows; the saver must not exit until the cursor moves > 8 px from its first observed position. (Task 6 tests)
2. **Argument variants** — `/c:1234`, `/C`, `/p 1234`, `/P:1234`, `/s`, `-s`, no args all parse correctly. (Task 1 tests)
3. **Manual night window crossing midnight, and polar day/night** — 23:00–07:00 is night at 02:00 and 23:30, not at 12:00; missing sunrise/sunset falls back to manual. (Task 2 tests)
4. **iCal edge cases** — all-day DTEND is exclusive; TZID with IANA name; event spanning midnight; EXDATE removes an occurrence; unsupported RRULE is skipped without throwing. (Tasks 4–5 tests)
5. **First run offline / malformed responses** — no cache + network failure or garbage JSON/ICS yields null/empty, never throws. (Tasks 3–4 tests)

---

## File Structure

```
DriftClock.sln
src/DriftClock/DriftClock.csproj
src/DriftClock/Program.cs                 entry, mode dispatch, DPI awareness
src/DriftClock/Core/ScreensaverArgs.cs    arg parsing
src/DriftClock/Core/Settings.cs           ini load/save + DPAPI
src/DriftClock/Core/DriftPath.cs          pixel-shift offsets
src/DriftClock/Core/NightSchedule.cs      is-night decision
src/DriftClock/Core/ExitDetector.cs       input → should-exit
src/DriftClock/Data/Weather.cs            Open-Meteo forecast + geocoding parse/fetch
src/DriftClock/Data/ICal.cs               ICS unfolding/parsing → CalEvent
src/DriftClock/Data/IanaZones.cs          IANA → Windows zone table
src/DriftClock/Data/Recurrence.cs         RRULE/EXDATE expansion
src/DriftClock/Data/EventSelector.cs      which events to show
src/DriftClock/Data/DataService.cs        timers, fetch, cache, ScreenState snapshot
src/DriftClock/UI/ClockRenderer.cs        GDI+ drawing
src/DriftClock/UI/SaverForm.cs            fullscreen primary / black secondary
src/DriftClock/UI/PreviewWindow.cs        /p child window
src/DriftClock/UI/SettingsForm.cs         /c dialog
tests/DriftClock.Tests/DriftClock.Tests.csproj
tests/DriftClock.Tests/*Tests.cs
```

---

### Task 1: Project scaffold + argument parsing

**Files:**
- Create: `DriftClock.sln`, `src/DriftClock/DriftClock.csproj`, `src/DriftClock/Program.cs`, `src/DriftClock/Core/ScreensaverArgs.cs`, `tests/DriftClock.Tests/DriftClock.Tests.csproj`, `tests/DriftClock.Tests/ScreensaverArgsTests.cs`, `.gitignore` (bin/, obj/, .superpowers/)

**Interfaces:**
- Produces: `enum SaverMode { Settings, Fullscreen, Preview }`; `sealed class ScreensaverArgs { SaverMode Mode; IntPtr Handle; static ScreensaverArgs Parse(string[] args); }`

- [ ] **Step 1:** `git init` in project root; create `.gitignore`.
- [ ] **Step 2:** Create csproj: `<TargetFramework>net48</TargetFramework>`, `<OutputType>WinExe</OutputType>`, `<UseWindowsForms>true</UseWindowsForms>`, `<LangVersion>7.3</LangVersion>`, `AssemblyName=DriftClock`, references `System.Web.Extensions`, `System.Security`; PackageReference `Microsoft.NETFramework.ReferenceAssemblies` (PrivateAssets=all). `app.manifest` with `<dpiAwareness>PerMonitorV2</dpiAwareness>`. AfterBuild target copies `$(TargetPath)` to `$(TargetDir)DriftClock.scr`. `InternalsVisibleTo DriftClock.Tests`. Test csproj: net48, xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, ProjectReference to app.
- [ ] **Step 3: Write failing tests** in `ScreensaverArgsTests`:

```csharp
[Theory]
[InlineData(new string[0], SaverMode.Settings, 0)]
[InlineData(new[]{"/c"}, SaverMode.Settings, 0)]
[InlineData(new[]{"/C:1234"}, SaverMode.Settings, 1234)]
[InlineData(new[]{"/s"}, SaverMode.Fullscreen, 0)]
[InlineData(new[]{"-S"}, SaverMode.Fullscreen, 0)]
[InlineData(new[]{"/p","5678"}, SaverMode.Preview, 5678)]
[InlineData(new[]{"/P:5678"}, SaverMode.Preview, 5678)]
[InlineData(new[]{"/x"}, SaverMode.Settings, 0)]
public void Parse_handles_variants(string[] a, SaverMode mode, long h)
{ var r = ScreensaverArgs.Parse(a); Assert.Equal(mode, r.Mode); Assert.Equal((IntPtr)h, r.Handle); }

[Fact] public void Preview_without_handle_falls_back_to_settings()
{ Assert.Equal(SaverMode.Settings, ScreensaverArgs.Parse(new[]{"/p"}).Mode); }
```

- [ ] **Step 4:** `dotnet test` → FAIL (type missing).
- [ ] **Step 5:** Implement `ScreensaverArgs.Parse`. `Program.Main` dispatches on mode with placeholder empty methods (filled in Task 9).
- [ ] **Step 6:** `dotnet test` → PASS; `dotnet build -c Release` → `DriftClock.scr` exists in `src/DriftClock/bin/Release/net48/`.
- [ ] **Step 7:** Commit `feat: scaffold project and screensaver arg parsing` (include the existing `docs/`).

### Task 2: Settings, drift path, night schedule

**Files:**
- Create: `Core/Settings.cs`, `Core/DriftPath.cs`, `Core/NightSchedule.cs`; tests `SettingsTests.cs`, `DriftPathTests.cs`, `NightScheduleTests.cs`

**Interfaces:**
- Produces:
  - `sealed class Settings` with properties: `string LocationName; double? Latitude; double? Longitude; bool Use12Hour; bool UseFahrenheit; bool ShowWeather=true; bool ShowBattery=true; bool ShowMonthGrid=false; bool ShowEvents=false; string ICalUrl; int NightBrightnessPercent=35; TimeSpan ManualNightStart=23:00; TimeSpan ManualNightEnd=07:00`. `static Settings Load(string path)`, `void Save(string path)`, `static string DefaultPath`.
  - `static class DriftPath { Point OffsetFor(long step, int range); }` — `step` = minutes since epoch.
  - `static class NightSchedule { bool IsNight(DateTime nowLocal, DateTime? sunriseLocal, DateTime? sunsetLocal, TimeSpan manualStart, TimeSpan manualEnd); }`

- [ ] **Step 1: Write failing tests:**

```csharp
// Settings
[Fact] public void Roundtrip_preserves_values_and_encrypts_url() {
  var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".ini");
  var s = new Settings{ LocationName="Milan", Latitude=45.46, Longitude=9.19, Use12Hour=true, ShowEvents=true, ICalUrl="https://x/secret.ics", NightBrightnessPercent=20 };
  s.Save(p);
  Assert.DoesNotContain("secret", File.ReadAllText(p));
  var r = Settings.Load(p);
  Assert.Equal("Milan", r.LocationName); Assert.Equal(45.46, r.Latitude); Assert.True(r.Use12Hour);
  Assert.Equal("https://x/secret.ics", r.ICalUrl); Assert.Equal(20, r.NightBrightnessPercent);
}
[Fact] public void Missing_or_corrupt_file_yields_defaults() {
  Assert.Equal(35, Settings.Load(@"C:\nope\none.ini").NightBrightnessPercent);
  var p = Path.GetTempFileName(); File.WriteAllText(p, "garbage\n=\nNightBrightnessPercent=abc");
  Assert.Equal(35, Settings.Load(p).NightBrightnessPercent);
}
[Fact] public void Brightness_is_clamped() { /* write 500 → loads 100; write 1 → loads 10 */ }

// DriftPath
[Fact] public void Offsets_stay_in_range() { for (long i=0;i<10000;i++){ var o=DriftPath.OffsetFor(i,16); Assert.InRange(o.X,-16,16); Assert.InRange(o.Y,-16,16);} }
[Fact] public void Consecutive_offsets_differ_by_at_least_2px() { for (long i=0;i<1000;i++){ var a=DriftPath.OffsetFor(i,16); var b=DriftPath.OffsetFor(i+1,16); Assert.True(Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)>=2);} }
[Fact] public void Path_covers_box() { /* over 2000 steps, distinct points >= 200 and min/max X,Y reach within 2px of ±16 */ }

// NightSchedule
[Fact] public void Uses_sun_times_when_present() {
  var d=new DateTime(2026,11,10); var rise=d.AddHours(7); var set=d.AddHours(17);
  Assert.True(NightSchedule.IsNight(d.AddHours(18), rise, set, T(23), T(7)));
  Assert.False(NightSchedule.IsNight(d.AddHours(12), rise, set, T(23), T(7)));
  Assert.True(NightSchedule.IsNight(d.AddHours(5), rise, set, T(23), T(7)));
}
[Theory] [InlineData(2,true)] [InlineData(23.5,true)] [InlineData(12,false)] [InlineData(7,false)]
public void Manual_window_crosses_midnight(double h, bool night) {
  Assert.Equal(night, NightSchedule.IsNight(new DateTime(2026,11,10).AddHours(h), null, null, T(23), T(7)));
}
[Fact] public void Manual_window_same_day() { /* 01:00–06:00: 03:00 true, 08:00 false */ }
```

- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Implement. Settings: `key=value` lines, invariant culture for numbers, unknown keys ignored, `ICalUrl` stored as `ICalUrlProtected=<base64 DPAPI CurrentUser>`. Save creates directory. DriftPath: Lissajous with incommensurate periods, quantised to ints, e.g. `x = round(range*sin(step*2π/37))`, `y = round(range*sin(step*2π/53 + π/3))`; if result equals previous step's, nudge — must satisfy the tests.
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: settings, drift path, night schedule`.

### Task 3: Weather (Open-Meteo)

**Files:**
- Create: `Data/Weather.cs`; tests `WeatherTests.cs` with fixture strings inline.

**Interfaces:**
- Produces:
  - `sealed class WeatherInfo { double CurrentTemp; int WeatherCode; double High; double Low; DateTime? SunriseLocal; DateTime? SunsetLocal; DateTime FetchedUtc; }`
  - `sealed class GeoResult { string Name; string Admin; string Country; double Latitude; double Longitude; }`
  - `static class Weather { string ForecastUrl(double lat, double lon, bool fahrenheit); WeatherInfo ParseForecast(string json, DateTime fetchedUtc); List<GeoResult> ParseGeocode(string json); WeatherInfo Fetch(double lat,double lon,bool f); List<GeoResult> Search(string q); string GlyphFor(int code); }`
- `ForecastUrl` = `https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min,sunrise,sunset&timezone=auto&forecast_days=1` + `&temperature_unit=fahrenheit` when f. Invariant-culture numbers. Search URL `https://geocoding-api.open-meteo.com/v1/search?name={escaped}&count=8&language=en&format=json`.
- Sunrise/sunset come back in the location's local time (`timezone=auto`); convert to machine local using `utc_offset_seconds` from the response.

- [ ] **Step 1: Write failing tests:** `ParseForecast` on a realistic sample → temp 12.3, code 3, high 14, low 8, sunrise parsed; `ParseForecast` with `"sunrise":[null]` → `SunriseLocal == null`; `ParseForecast("not json")` and `("")` → returns null (no throw); `ParseGeocode` sample with 2 results → 2 items with names; `ParseGeocode("{}")` → empty list; `ForecastUrl(45.46, 9.19, true)` contains `latitude=45.46` (invariant even under `it-IT` culture — set `CultureInfo.CurrentCulture` in the test) and `temperature_unit=fahrenheit`; `GlyphFor` returns non-empty for codes 0,3,61,71,95 and for unknown 999.
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Implement with `JavaScriptSerializer.DeserializeObject` → dictionaries; catch all and return null/empty. `Fetch`/`Search` use `HttpWebRequest` with 10 s timeout, TLS 1.2 enabled (`ServicePointManager.SecurityProtocol |= Tls12`), return null/empty on any exception. Glyphs from Segoe UI Symbol (☀ ⛅ ☁ 🌫 🌧 ❄ ⛈).
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: open-meteo weather and geocoding`.

### Task 4: iCal parsing + time zones

**Files:**
- Create: `Data/ICal.cs`, `Data/IanaZones.cs`; tests `ICalTests.cs`

**Interfaces:**
- Produces:
  - `sealed class CalEvent { string Summary; DateTime StartLocal; DateTime EndLocal; bool AllDay; string RRule; List<DateTime> ExDatesLocal; }` (pre-expansion master; `RRule` null when not recurring)
  - `static class ICal { List<CalEvent> Parse(string ics, TimeZoneInfo local); string NormalizeUrl(string url); }`
  - `static class IanaZones { TimeZoneInfo Find(string tzid); }` — tries `FindSystemTimeZoneById(tzid)` first, then the embedded table (cover at least the ~60 most common IANA zones: Europe/*, America/*, Asia/*, Australia/*, Africa/Johannesburg, UTC/Etc/UTC), else null (treat as floating/local).

- [ ] **Step 1: Write failing tests:**
  - Folded lines (`SUMMARY:Long\r\n  title`) unfold to `"Long title"`; escaped `\,` `\n` unescaped.
  - `DTSTART:20261110T150000Z` → local = UTC→`local` conversion (test passes an explicit `TimeZoneInfo` for Europe/Rome = "W. Europe Standard Time" → 16:00).
  - `DTSTART;TZID=America/New_York:20261110T090000` with local Rome → 15:00.
  - `DTSTART;VALUE=DATE:20261110` / `DTEND;VALUE=DATE:20261111` → `AllDay`, start 10 Nov 00:00, end 11 Nov 00:00 (exclusive).
  - Floating `DTSTART:20261110T090000` → 09:00 unchanged.
  - Missing DTEND → end = start (+1 day if all-day).
  - `EXDATE;TZID=...:a,b` → 2 entries; RRULE string captured verbatim.
  - `ICal.Parse("garbage")` and VEVENT without DTSTART → skipped, no throw.
  - `NormalizeUrl("webcal://x/y.ics") == "https://x/y.ics"`.
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Implement line-based parser: unfold, split `NAME;PARAMS:VALUE`, handle BEGIN/END:VEVENT, ignore VTIMEZONE blocks (rely on IanaZones/Windows IDs). Per-event try/catch.
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: ical parsing with timezone mapping`.

### Task 5: Recurrence expansion + event selection

**Files:**
- Create: `Data/Recurrence.cs`, `Data/EventSelector.cs`; tests `RecurrenceTests.cs`, `EventSelectorTests.cs`

**Interfaces:**
- Consumes: `CalEvent` (Task 4).
- Produces:
  - `static class Recurrence { IEnumerable<CalEvent> Expand(CalEvent master, DateTime windowStart, DateTime windowEnd); }` — yields concrete occurrences (RRule=null) overlapping the window; non-recurring master yields itself if it overlaps.
  - `sealed class EventLine { string Text; }` and `static class EventSelector { List<EventLine> Select(IEnumerable<CalEvent> masters, DateTime nowLocal, bool use12h, CultureInfo culture); }`

- [ ] **Step 1: Write failing tests:**
  - DAILY INTERVAL=2 from 1 Nov 09:00, window 10–12 Nov → occurrences 11 Nov (and not 10, 12).
  - WEEKLY BYDAY=MO,WE window = week of 9 Nov → 9 and 11 Nov.
  - MONTHLY (start 10 Jan) → 10 Nov in window; YEARLY → once.
  - COUNT=3 stops after 3; UNTIL=20261105T000000Z excludes 10 Nov.
  - EXDATE matching 11 Nov removes it.
  - `FREQ=HOURLY` or `BYSETPOS` → yields nothing, no throw.
  - Occurrences keep duration (end − start).
  - Selector, now = 10 Nov 14:00: events 09:00 (ended), 16:00 "Design review", 18:30 "Gym", all-day today "Holiday" → lines `["All day Holiday", "16:00 Design review", "18:30 Gym", <first tomorrow event prefixed with culture short day name e.g. "Wed 09:00 Dentist">]` because fewer than 3 today-remaining timed+all-day… rule: today's remaining (in-progress counts) first, all-day first; if count < 3, append tomorrow's; cap 4.
  - Event spanning midnight (yesterday 22:00 → today 01:00) at now 00:30 is shown.
  - `use12h` → `"4:00 PM Design review"`.
  - Summary longer than 40 chars truncated with `…`.
- [ ] **Step 2:** `dotnet test` → FAIL.
- [ ] **Step 3:** Implement. Expansion iterates candidate dates from DTSTART forward up to windowEnd, cap 5000 iterations as safety.
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat: recurrence expansion and event selection`.

### Task 6: Exit detector

**Files:**
- Create: `Core/ExitDetector.cs`; test `ExitDetectorTests.cs`

**Interfaces:**
- Produces: `sealed class ExitDetector { ExitDetector(int thresholdPx = 8); bool OnMouseMove(Point screenPos); bool OnKeyOrClick(); }` — returns true when the saver should close.

- [ ] **Step 1: Tests:** first move at any position → false; subsequent moves within 8 px of first → false; move 9 px away → true; `OnKeyOrClick()` → true.
- [ ] **Step 2–4:** fail → implement → pass.
- [ ] **Step 5:** Commit `feat: exit detection with jitter threshold`.

### Task 7: Data service (refresh, cache, battery, snapshot)

**Files:**
- Create: `Data/DataService.cs`; test `DataServiceTests.cs`

**Interfaces:**
- Consumes: `Settings`, `Weather`, `ICal`, `Recurrence`, `EventSelector`, `NightSchedule`.
- Produces:
  - `sealed class ScreenState { WeatherInfo Weather /*null = hide*/; int? BatteryPercent /*null = no battery*/; bool Charging; List<EventLine> Events /*null = hide*/; bool IsNight; }`
  - `sealed class DataService : IDisposable { DataService(Settings s, string cacheDir, Func<double,double,bool,WeatherInfo> fetchWeather, Func<string,string> fetchText, Func<DateTime> clock); void Start(); ScreenState Current { get; } /* volatile snapshot */ ScreenState Compose(DateTime nowLocal); }`
  - Production overload `DataService(Settings s)` wires `Weather.Fetch`, an `HttpWebRequest` text getter, `DateTime.Now`, cache dir `%LOCALAPPDATA%\DriftClock\`.
- Battery from `SystemInformation.PowerStatus`: `BatteryChargeStatus.NoSystemBattery` or `Unknown` → null; `PowerLineStatus.Online` → Charging.
- Cache files: `weather.json` (raw response + fetched UTC in a sidecar line), `calendar.ics` (raw) with file mtime as fetched time.

- [ ] **Step 1: Tests** (inject fakes, temp cache dir):
  - Start with failing fetchers and empty cache → `Compose` returns Weather null, Events null, IsNight per manual hours; no throw.
  - Successful weather fetch → cache file written; new DataService with failing fetcher reads cache → Weather non-null.
  - Weather fetched 4 h ago (clock fake) → Weather null in `Compose`; IsNight still uses its sunrise/sunset (cached sun times valid for the same calendar day only, else manual).
  - `ShowWeather=false` but lat/lon set → Weather null in state, IsNight uses sun times.
  - Calendar cache older than 24 h → Events null; `ShowEvents=false` → Events null and fetchText never called.
- [ ] **Step 2–4:** fail → implement (timers: weather 30 min, calendar 15 min, first fetch immediately on `Start` in background; battery read inside `Compose`) → pass.
- [ ] **Step 5:** Commit `feat: data service with caching and staleness`.

### Task 8: Renderer

**Files:**
- Create: `UI/ClockRenderer.cs`; test `ClockRendererTests.cs` (smoke tests on a `Bitmap`)

**Interfaces:**
- Consumes: `ScreenState`, `Settings`, `DriftPath`.
- Produces: `sealed class ClockRenderer : IDisposable { ClockRenderer(float dpiScale); void Draw(Graphics g, Rectangle bounds, DateTime nowLocal, ScreenState state, Settings s, Point driftOffset); string RenderKey(DateTime now, ScreenState st, Point offset) /* changes only when output would change */ }`
- Layout C: time centered (height ≈ 29% of `bounds.Height`), date below, weather top-left (`glyph temp°  H/L  Location`), battery top-right (`🔋 78%`, `⚡` when charging), events bottom-left, month grid bottom-right (current month, today highlighted with a `#303030` filled rounded rect, culture first-day-of-week). Corner margin = max(3% of width, 2×drift range). Night: multiply all text colours by `NightBrightnessPercent/100`. AntiAlias text (`TextRenderingHint.AntiAliasGridFit`). Fonts created once per bounds size and cached.

- [ ] **Step 1: Tests:** Draw into 1920×1080 and 160×120 bitmaps with all extras on and all off → no throw; with all off, pixels in the four 200×150 corners are all black; at night with 35% brightness, max channel value across the bitmap ≤ 0.35×200+2; `RenderKey` equal for two times within the same minute, different across minutes.
- [ ] **Step 2–4:** fail → implement → pass.
- [ ] **Step 5:** Commit `feat: gdi+ clock renderer (layout C)`.

### Task 9: Saver forms, preview, settings dialog, wiring

**Files:**
- Create: `UI/SaverForm.cs`, `UI/PreviewWindow.cs`, `UI/SettingsForm.cs`; Modify: `Program.cs`

**Interfaces:**
- Consumes: everything above.
- `SaverForm(Screen screen, bool primary, DataService data, Settings s)`: borderless, TopMost, bounds = screen bounds, black, double-buffered, `Cursor.Hide()`. Primary: 1 s `System.Windows.Forms.Timer` → compute offset `DriftPath.OffsetFor(minutesSinceEpoch, (int)(16*dpi))`, `Invalidate()` only if `RenderKey` changed. Secondary: paints black, no timer. All forms share one `ExitDetector`; any exit → `Application.Exit()`.
- `PreviewWindow`: `NativeWindow`/Form re-parented via `SetParent` + `WS_CHILD` into the handle, sized to parent client rect (`GetClientRect`), renders with renderer at dpiScale 0.15 using cached data only (no network), closes when parent is destroyed (check `IsWindow` on the 1 s tick).
- `SettingsForm`: controls per spec — location search box + Search button → `Weather.Search` on background thread → ListBox of `Name, Admin, Country` → selection stores name/lat/lon; checkboxes 12-hour, °F, weather, battery, month grid, events; iCal URL textbox (enabled when events checked); night brightness TrackBar 10–100; manual night start/end `DateTimePicker` (time only). OK saves `Settings.DefaultPath`, Cancel discards. If `/c:<hwnd>` given, show as owned by that window.
- `Program.Main`: `[STAThread]`, `Application.EnableVisualStyles()`, dispatch by mode; global `ThreadException`/`UnhandledException` handlers in Fullscreen mode just exit silently.

- [ ] **Step 1:** Implement forms and wiring.
- [ ] **Step 2:** `dotnet test` → all PASS; `dotnet build -c Release` → `DriftClock.scr` < 300 KB (`ls -l`).
- [ ] **Step 3: Manual verification:** run `DriftClock.scr /c` → dialog, search "Milan", pick, enable all, OK → `settings.ini` exists, URL not in plain text. Run `DriftClock.scr /s` → clock on primary, others black, small mouse nudge (<8 px) doesn't exit, real move exits. Watch Task Manager for 2 min: CPU ~0%, RAM < 50 MB. Right-click → Install → Windows Screen Saver Settings preview thumbnail shows the clock.
- [ ] **Step 4:** Commit `feat: saver, preview and settings windows`.
