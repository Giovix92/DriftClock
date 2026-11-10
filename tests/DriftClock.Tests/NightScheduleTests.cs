using System;
using Xunit;

namespace DriftClock.Tests
{
    public class NightScheduleTests
    {
        private static TimeSpan T(double h) { return TimeSpan.FromHours(h); }

        [Fact]
        public void Uses_sun_times_when_present()
        {
            var d = new DateTime(2026, 11, 10);
            var rise = d.AddHours(7);
            var set = d.AddHours(17);
            Assert.True(NightSchedule.IsNight(d.AddHours(18), rise, set, T(23), T(7)));
            Assert.False(NightSchedule.IsNight(d.AddHours(12), rise, set, T(23), T(7)));
            Assert.True(NightSchedule.IsNight(d.AddHours(5), rise, set, T(23), T(7)));
        }

        [Theory]
        [InlineData(2, true)]
        [InlineData(23.5, true)]
        [InlineData(12, false)]
        [InlineData(7, false)]
        public void Manual_window_crosses_midnight(double h, bool night)
        {
            Assert.Equal(night, NightSchedule.IsNight(new DateTime(2026, 11, 10).AddHours(h), null, null, T(23), T(7)));
        }

        [Fact]
        public void Manual_window_same_day()
        {
            var d = new DateTime(2026, 11, 10);
            Assert.True(NightSchedule.IsNight(d.AddHours(3), null, null, T(1), T(6)));
            Assert.False(NightSchedule.IsNight(d.AddHours(8), null, null, T(1), T(6)));
        }

        [Fact]
        public void Only_one_sun_time_falls_back_to_manual()
        {
            var d = new DateTime(2026, 11, 10);
            Assert.True(NightSchedule.IsNight(d.AddHours(2), d.AddHours(7), null, T(23), T(7)));
            Assert.False(NightSchedule.IsNight(d.AddHours(12), null, d.AddHours(17), T(23), T(7)));
        }
    }
}
