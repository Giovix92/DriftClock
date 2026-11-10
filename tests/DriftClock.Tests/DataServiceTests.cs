using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DriftClock.Tests
{
    public class DataServiceTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "dc-" + Guid.NewGuid().ToString("N"));
        private DateTime _now = new DateTime(2026, 11, 10, 18, 0, 0);

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private Settings Located()
        {
            return new Settings { Latitude = 45.46, Longitude = 9.19, LocationName = "Milan" };
        }

        private WeatherInfo Info(TimeSpan age, DateTime sunriseDay)
        {
            return new WeatherInfo
            {
                CurrentTemp = 12, WeatherCode = 3, High = 14, Low = 8,
                SunriseLocal = sunriseDay.Date.AddHours(7), SunsetLocal = sunriseDay.Date.AddHours(17),
                FetchedUtc = _now.ToUniversalTime() - age
            };
        }

        private DataService Make(Settings s, Func<double, double, bool, WeatherInfo> w = null, Func<string, string> t = null)
        {
            return new DataService(s, _dir, w ?? ((a, b, c) => null), t ?? (u => null), () => _now);
        }

        [Fact]
        public void No_data_and_failing_fetchers_degrade_silently()
        {
            var s = Located();
            s.ShowEvents = true;
            s.ICalUrl = "https://x/y.ics";
            var svc = Make(s, (a, b, c) => { throw new Exception("boom"); }, u => { throw new Exception("boom"); });
            svc.RefreshWeather();
            svc.RefreshCalendar();
            var st = svc.Compose(new DateTime(2026, 11, 10, 2, 0, 0));
            Assert.Null(st.Weather);
            Assert.Null(st.Events);
            Assert.True(st.IsNight); // manual 23:00-07:00
        }

        [Fact]
        public void Successful_fetch_is_cached_and_used_offline()
        {
            var svc = Make(Located(), (a, b, c) => Info(TimeSpan.FromMinutes(1), _now));
            svc.RefreshWeather();
            Assert.True(Directory.GetFiles(_dir).Length > 0);

            var offline = Make(Located());
            var w = offline.Compose(_now).Weather;
            Assert.NotNull(w);
            Assert.Equal(12, w.CurrentTemp, 3);
            Assert.Equal(3, w.WeatherCode);
        }

        [Fact]
        public void Weather_older_than_three_hours_is_hidden_but_sun_times_still_drive_night()
        {
            var svc = Make(Located(), (a, b, c) => Info(TimeSpan.FromHours(4), _now));
            svc.RefreshWeather();
            var st = svc.Compose(_now); // 18:00, sunset 17:00
            Assert.Null(st.Weather);
            Assert.True(st.IsNight);    // manual hours would say day
        }

        [Fact]
        public void Sun_times_from_another_day_fall_back_to_manual_hours()
        {
            var svc = Make(Located(), (a, b, c) => Info(TimeSpan.FromMinutes(5), _now.AddDays(-1)));
            svc.RefreshWeather();
            Assert.False(svc.Compose(_now).IsNight);
        }

        [Fact]
        public void Weather_hidden_by_setting_still_fetches_and_uses_sun_times()
        {
            var s = Located();
            s.ShowWeather = false;
            var svc = Make(s, (a, b, c) => Info(TimeSpan.FromMinutes(1), _now));
            svc.RefreshWeather();
            var st = svc.Compose(_now);
            Assert.Null(st.Weather);
            Assert.True(st.IsNight);
        }

        [Fact]
        public void No_location_means_no_weather_fetch()
        {
            int calls = 0;
            var svc = Make(new Settings(), (a, b, c) => { calls++; return null; });
            svc.RefreshWeather();
            Assert.Equal(0, calls);
        }

        [Fact]
        public void Events_come_from_fetched_calendar()
        {
            var s = new Settings { ShowEvents = true, ICalUrl = "webcal://x/y.ics" };
            string requested = null;
            string ics = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART:20261110T200000\r\nSUMMARY:Dinner\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
            var svc = Make(s, null, u => { requested = u; return ics; });
            svc.RefreshCalendar();
            Assert.Equal("https://x/y.ics", requested);
            var lines = svc.Compose(_now).Events;
            Assert.Single(lines);
            Assert.EndsWith("Dinner", lines[0].Text);
        }

        [Fact]
        public void Events_off_means_no_calendar_fetch()
        {
            int calls = 0;
            var s = new Settings { ShowEvents = false, ICalUrl = "https://x/y.ics" };
            var svc = Make(s, null, u => { calls++; return "x"; });
            svc.RefreshCalendar();
            Assert.Equal(0, calls);
            Assert.Null(svc.Compose(_now).Events);
        }

        [Fact]
        public void Calendar_cache_older_than_a_day_is_hidden()
        {
            Directory.CreateDirectory(_dir);
            string path = Path.Combine(_dir, "calendar.ics");
            File.WriteAllText(path, "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART:20261110T200000\r\nSUMMARY:Dinner\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n");
            var s = new Settings { ShowEvents = true, ICalUrl = "https://x/y.ics" };
            File.WriteAllText(Path.Combine(_dir, "calendar.key"), DataService.CacheKey(s.ICalUrl));

            File.SetLastWriteTime(path, _now.AddHours(-25));
            Assert.Null(Make(s).Compose(_now).Events);

            File.SetLastWriteTime(path, _now.AddHours(-1));
            Assert.Single(Make(s).Compose(_now).Events);
        }

        private const string OneEventIcs =
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nDTSTART:20261110T200000\r\nSUMMARY:Dinner\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

        private string SeedCache(string url)
        {
            Directory.CreateDirectory(_dir);
            string ics = Path.Combine(_dir, "calendar.ics");
            File.WriteAllText(ics, OneEventIcs);
            File.WriteAllText(Path.Combine(_dir, "calendar.key"), DataService.CacheKey(url));
            File.SetLastWriteTime(ics, _now.AddMinutes(-5));
            return ics;
        }

        [Fact]
        public void Http_calendar_url_is_never_fetched()
        {
            int calls = 0;
            var s = new Settings { ShowEvents = true, ICalUrl = "http://x/y.ics" };
            var svc = Make(s, null, u => { calls++; return OneEventIcs; });
            svc.RefreshCalendar();
            Assert.Equal(0, calls);
            Assert.Null(svc.Compose(_now).Events);
        }

        [Fact]
        public void Fetched_calendar_survives_a_restart_for_the_same_url()
        {
            var s = new Settings { ShowEvents = true, ICalUrl = "https://x/y.ics" };
            Make(s, null, u => OneEventIcs).RefreshCalendar();
            Assert.Single(Make(s).Compose(_now).Events); // offline restart uses the cache
        }

        [Fact]
        public void Cache_for_a_different_url_is_deleted_and_not_shown()
        {
            string ics = SeedCache("https://old/secret.ics");
            var s = new Settings { ShowEvents = true, ICalUrl = "https://new/other.ics" };
            Assert.Null(Make(s).Compose(_now).Events);
            Assert.False(File.Exists(ics));
            Assert.False(File.Exists(Path.Combine(_dir, "calendar.key")));
        }

        [Theory]
        [InlineData(false, "https://x/y.ics")]
        [InlineData(true, null)]
        [InlineData(true, "  ")]
        public void Cache_is_deleted_when_events_are_off_or_the_link_is_removed(bool showEvents, string url)
        {
            string ics = SeedCache("https://x/y.ics");
            var s = new Settings { ShowEvents = showEvents, ICalUrl = url };
            Make(s);
            Assert.False(File.Exists(ics));
        }

        [Fact]
        public void Cache_key_does_not_contain_the_url()
        {
            string key = DataService.CacheKey("https://calendar.example/private/secret-token.ics");
            Assert.DoesNotContain("secret", key);
            Assert.Equal(key, DataService.CacheKey(" webcal://calendar.example/private/secret-token.ics "));
            Assert.NotEqual(key, DataService.CacheKey("https://calendar.example/private/other.ics"));
        }

        [Fact]
        public void DeleteCalendarCache_removes_both_files()
        {
            string ics = SeedCache("https://x/y.ics");
            DataService.DeleteCalendarCache(_dir);
            Assert.False(File.Exists(ics));
            Assert.False(File.Exists(Path.Combine(_dir, "calendar.key")));
            DataService.DeleteCalendarCache(_dir); // already gone: no throw
        }

        [Fact]
        public void Battery_is_null_when_disabled()
        {
            var s = new Settings { ShowBattery = false };
            Assert.Null(Make(s).Compose(_now).BatteryPercent);
        }
    }
}
