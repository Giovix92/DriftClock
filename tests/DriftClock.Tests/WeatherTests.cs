using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace DriftClock.Tests
{
    public class WeatherTests
    {
        private const string Sample =
            "{\"latitude\":45.46,\"longitude\":9.19,\"utc_offset_seconds\":3600,\"timezone\":\"Europe/Rome\"," +
            "\"current\":{\"time\":\"2026-11-10T14:30\",\"temperature_2m\":12.3,\"weather_code\":3}," +
            "\"daily\":{\"time\":[\"2026-11-10\"],\"temperature_2m_max\":[14.0],\"temperature_2m_min\":[8.0]," +
            "\"sunrise\":[\"2026-11-10T07:08\"],\"sunset\":[\"2026-11-10T16:53\"]}}";

        [Fact]
        public void ParseForecast_reads_current_and_daily()
        {
            var fetched = new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Utc);
            var w = Weather.ParseForecast(Sample, fetched);
            Assert.NotNull(w);
            Assert.Equal(12.3, w.CurrentTemp, 3);
            Assert.Equal(3, w.WeatherCode);
            Assert.Equal(14.0, w.High, 3);
            Assert.Equal(8.0, w.Low, 3);
            Assert.Equal(fetched, w.FetchedUtc);
            // 07:08 at UTC+1 is 06:08 UTC, converted to the machine's local time
            var expected = new DateTime(2026, 11, 10, 6, 8, 0, DateTimeKind.Utc).ToLocalTime();
            Assert.Equal(expected, w.SunriseLocal.Value);
            Assert.True(w.SunsetLocal.HasValue);
        }

        [Fact]
        public void ParseForecast_null_sun_times_are_null()
        {
            string json = Sample.Replace("\"sunrise\":[\"2026-11-10T07:08\"]", "\"sunrise\":[null]");
            var w = Weather.ParseForecast(json, DateTime.UtcNow);
            Assert.NotNull(w);
            Assert.Null(w.SunriseLocal);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("{}")]
        public void ParseForecast_garbage_returns_null(string json)
        {
            Assert.Null(Weather.ParseForecast(json, DateTime.UtcNow));
        }

        [Fact]
        public void ParseGeocode_reads_results()
        {
            string json = "{\"results\":[{\"name\":\"Milan\",\"latitude\":45.46427,\"longitude\":9.18951,\"country\":\"Italy\",\"admin1\":\"Lombardy\"}," +
                          "{\"name\":\"Milan\",\"latitude\":35.9,\"longitude\":-88.7,\"country\":\"United States\",\"admin1\":\"Tennessee\"}]}";
            var r = Weather.ParseGeocode(json);
            Assert.Equal(2, r.Count);
            Assert.Equal("Milan", r[0].Name);
            Assert.Equal("Lombardy", r[0].Admin);
            Assert.Equal(-88.7, r[1].Longitude, 3);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("garbage")]
        [InlineData("")]
        public void ParseGeocode_garbage_returns_empty(string json)
        {
            Assert.Empty(Weather.ParseGeocode(json));
        }

        [Fact]
        public void ForecastUrl_uses_invariant_numbers_and_unit()
        {
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("it-IT");
                string url = Weather.ForecastUrl(45.46, 9.19, true);
                Assert.Contains("latitude=45.46", url);
                Assert.Contains("longitude=9.19", url);
                Assert.Contains("temperature_unit=fahrenheit", url);
                Assert.DoesNotContain("fahrenheit", Weather.ForecastUrl(45.46, 9.19, false));
            }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(61)]
        [InlineData(71)]
        [InlineData(95)]
        [InlineData(999)]
        public void GlyphFor_always_returns_something(int code)
        {
            Assert.False(string.IsNullOrEmpty(Weather.GlyphFor(code)));
        }
    }
}
