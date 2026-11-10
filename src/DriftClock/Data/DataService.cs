using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DriftClock
{
    /// <summary>Immutable snapshot of everything the renderer needs besides the clock itself.</summary>
    public sealed class ScreenState
    {
        public WeatherInfo Weather;          // null = hide
        public int? BatteryPercent;          // null = no battery / hidden
        public bool Charging;
        public List<EventLine> Events;       // null = hide
        public bool IsNight;
    }

    public sealed class DataService : IDisposable
    {
        private static readonly TimeSpan WeatherMaxAge = TimeSpan.FromHours(3);
        private static readonly TimeSpan CalendarMaxAge = TimeSpan.FromHours(24);
        private static readonly TimeSpan BatteryInterval = TimeSpan.FromSeconds(60);
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly Settings _s;
        private readonly string _dir;
        private readonly Func<double, double, bool, WeatherInfo> _fetchWeather;
        private readonly Func<string, string> _fetchText;
        private readonly Func<DateTime> _clock;
        private readonly object _gate = new object();

        private WeatherInfo _weather;
        private List<CalEvent> _calendar;
        private DateTime _calendarFetched;
        private int _version;
        private ScreenState _composed;
        private DateTime _composedMinute;
        private int _composedVersion = -1;
        private int? _battery;
        private bool _charging;
        private DateTime _batteryRead = DateTime.MinValue;
        private System.Threading.Timer _weatherTimer, _calendarTimer;

        public DataService(Settings s)
            : this(s, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriftClock"),
                   Weather.Fetch, Weather.HttpGet, () => DateTime.Now)
        {
        }

        public DataService(Settings s, string cacheDir, Func<double, double, bool, WeatherInfo> fetchWeather,
                           Func<string, string> fetchText, Func<DateTime> clock)
        {
            _s = s;
            _dir = cacheDir;
            _fetchWeather = fetchWeather;
            _fetchText = fetchText;
            _clock = clock;
            LoadWeatherCache();
            LoadCalendarCache();
        }

        public void Start()
        {
            _weatherTimer = new System.Threading.Timer(_ => RefreshWeather(), null, 0, (int)TimeSpan.FromMinutes(30).TotalMilliseconds);
            _calendarTimer = new System.Threading.Timer(_ => RefreshCalendar(), null, 0, (int)TimeSpan.FromMinutes(15).TotalMilliseconds);
        }

        public void Dispose()
        {
            if (_weatherTimer != null) _weatherTimer.Dispose();
            if (_calendarTimer != null) _calendarTimer.Dispose();
        }

        /// <summary>Cheap to call every second: recomputed at most once per minute or after a refresh.</summary>
        public ScreenState Current
        {
            get
            {
                DateTime now = _clock();
                DateTime minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
                lock (_gate)
                {
                    if (_composed == null || _composedMinute != minute || _composedVersion != _version)
                    {
                        _composed = Compose(now);
                        _composedMinute = minute;
                        _composedVersion = _version;
                    }
                    return _composed;
                }
            }
        }

        public void RefreshWeather()
        {
            try
            {
                if (!_s.Latitude.HasValue || !_s.Longitude.HasValue) return;
                WeatherInfo w = _fetchWeather(_s.Latitude.Value, _s.Longitude.Value, _s.UseFahrenheit);
                if (w == null) return;
                lock (_gate) { _weather = w; _version++; }
                SaveWeatherCache(w);
            }
            catch { /* offline or malformed: keep whatever we have */ }
        }

        public void RefreshCalendar()
        {
            try
            {
                if (!_s.ShowEvents || string.IsNullOrWhiteSpace(_s.ICalUrl)) return;
                string text = _fetchText(ICal.NormalizeUrl(_s.ICalUrl));
                if (string.IsNullOrEmpty(text)) return;
                List<CalEvent> events = ICal.Parse(text, TimeZoneInfo.Local);
                DateTime now = _clock();
                lock (_gate) { _calendar = events; _calendarFetched = now; _version++; }
                try
                {
                    Directory.CreateDirectory(_dir);
                    string path = Path.Combine(_dir, "calendar.ics");
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                    File.SetLastWriteTime(path, now);
                }
                catch { }
            }
            catch { }
        }

        public ScreenState Compose(DateTime nowLocal)
        {
            WeatherInfo w;
            List<CalEvent> cal;
            DateTime calFetched;
            lock (_gate) { w = _weather; cal = _calendar; calFetched = _calendarFetched; }

            DateTime? rise = null, set = null;
            if (w != null && w.SunriseLocal.HasValue && w.SunsetLocal.HasValue &&
                w.SunriseLocal.Value.Date == nowLocal.Date && w.SunsetLocal.Value.Date == nowLocal.Date)
            {
                rise = w.SunriseLocal;
                set = w.SunsetLocal;
            }

            var st = new ScreenState
            {
                IsNight = NightSchedule.IsNight(nowLocal, rise, set, _s.ManualNightStart, _s.ManualNightEnd)
            };

            if (_s.ShowWeather && w != null && nowLocal.ToUniversalTime() - w.FetchedUtc <= WeatherMaxAge)
                st.Weather = w;

            if (_s.ShowEvents && cal != null && nowLocal - calFetched <= CalendarMaxAge)
                st.Events = EventSelector.Select(cal, nowLocal, _s.Use12Hour, CultureInfo.CurrentCulture);

            if (_s.ShowBattery)
            {
                lock (_gate)
                {
                    if (nowLocal - _batteryRead >= BatteryInterval || nowLocal < _batteryRead)
                    {
                        ReadBattery(out _battery, out _charging);
                        _batteryRead = nowLocal;
                    }
                    st.BatteryPercent = _battery;
                    st.Charging = _charging;
                }
            }
            return st;
        }

        private static void ReadBattery(out int? percent, out bool charging)
        {
            percent = null;
            charging = false;
            try
            {
                PowerStatus ps = SystemInformation.PowerStatus;
                if ((ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0 ||
                    ps.BatteryChargeStatus == BatteryChargeStatus.Unknown) return;
                float life = ps.BatteryLifePercent;
                if (life < 0f || life > 1f) return;
                percent = (int)Math.Round(life * 100);
                charging = ps.PowerLineStatus == PowerLineStatus.Online;
            }
            catch { }
        }

        // ---- cache -------------------------------------------------------------------------------------------

        private string WeatherCachePath { get { return Path.Combine(_dir, "weather.cache"); } }

        private void SaveWeatherCache(WeatherInfo w)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                var sb = new StringBuilder();
                sb.AppendLine("Lat=" + _s.Latitude.Value.ToString("R", Inv));
                sb.AppendLine("Lon=" + _s.Longitude.Value.ToString("R", Inv));
                sb.AppendLine("F=" + _s.UseFahrenheit);
                sb.AppendLine("Temp=" + w.CurrentTemp.ToString("R", Inv));
                sb.AppendLine("Code=" + w.WeatherCode.ToString(Inv));
                sb.AppendLine("High=" + w.High.ToString("R", Inv));
                sb.AppendLine("Low=" + w.Low.ToString("R", Inv));
                if (w.SunriseLocal.HasValue) sb.AppendLine("Sunrise=" + w.SunriseLocal.Value.ToString("o", Inv));
                if (w.SunsetLocal.HasValue) sb.AppendLine("Sunset=" + w.SunsetLocal.Value.ToString("o", Inv));
                sb.AppendLine("Fetched=" + w.FetchedUtc.ToUniversalTime().ToString("o", Inv));
                File.WriteAllText(WeatherCachePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        private void LoadWeatherCache()
        {
            try
            {
                if (!File.Exists(WeatherCachePath) || !_s.Latitude.HasValue || !_s.Longitude.HasValue) return;
                var kv = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(WeatherCachePath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) kv[line.Substring(0, i)] = line.Substring(i + 1);
                }
                // A cache for another location or temperature unit is useless: ignore it.
                if (Math.Abs(double.Parse(kv["Lat"], Inv) - _s.Latitude.Value) > 0.001 ||
                    Math.Abs(double.Parse(kv["Lon"], Inv) - _s.Longitude.Value) > 0.001 ||
                    bool.Parse(kv["F"]) != _s.UseFahrenheit) return;

                var w = new WeatherInfo
                {
                    CurrentTemp = double.Parse(kv["Temp"], Inv),
                    WeatherCode = int.Parse(kv["Code"], Inv),
                    High = double.Parse(kv["High"], Inv),
                    Low = double.Parse(kv["Low"], Inv),
                    FetchedUtc = DateTime.Parse(kv["Fetched"], Inv, DateTimeStyles.RoundtripKind).ToUniversalTime()
                };
                string v;
                if (kv.TryGetValue("Sunrise", out v)) w.SunriseLocal = DateTime.Parse(v, Inv, DateTimeStyles.RoundtripKind).ToLocalTime();
                if (kv.TryGetValue("Sunset", out v)) w.SunsetLocal = DateTime.Parse(v, Inv, DateTimeStyles.RoundtripKind).ToLocalTime();
                _weather = w;
            }
            catch { _weather = null; }
        }

        private void LoadCalendarCache()
        {
            try
            {
                string path = Path.Combine(_dir, "calendar.ics");
                if (!File.Exists(path)) return;
                _calendar = ICal.Parse(File.ReadAllText(path), TimeZoneInfo.Local);
                _calendarFetched = File.GetLastWriteTime(path);
            }
            catch { _calendar = null; }
        }
    }
}
