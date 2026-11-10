using System;
using System.Linq;
using Xunit;

namespace DriftClock.Tests
{
    public class RecurrenceTests
    {
        private static CalEvent Master(DateTime start, string rrule, double hours = 1)
        {
            return new CalEvent { Summary = "E", StartLocal = start, EndLocal = start.AddHours(hours), RRule = rrule };
        }

        private static DateTime D(int day, int hour = 0) { return new DateTime(2026, 11, day, hour, 0, 0); }

        [Fact]
        public void Daily_with_interval()
        {
            var m = Master(D(1, 9), "FREQ=DAILY;INTERVAL=2");
            var occ = Recurrence.Expand(m, D(10), D(12)).ToList();
            Assert.Single(occ);
            Assert.Equal(D(11, 9), occ[0].StartLocal);
            Assert.Null(occ[0].RRule);
        }

        [Fact]
        public void Weekly_byday()
        {
            var m = Master(D(2, 9), "FREQ=WEEKLY;BYDAY=MO,WE"); // 2 Nov 2026 is a Monday
            var occ = Recurrence.Expand(m, D(9), D(16)).Select(o => o.StartLocal).ToList();
            Assert.Equal(new[] { D(9, 9), D(11, 9) }, occ);
        }

        [Fact]
        public void Monthly_and_yearly()
        {
            var monthly = Master(new DateTime(2026, 1, 10, 9, 0, 0), "FREQ=MONTHLY");
            Assert.Single(Recurrence.Expand(monthly, D(10), D(11)));
            var yearly = Master(new DateTime(2025, 11, 10, 9, 0, 0), "FREQ=YEARLY");
            Assert.Single(Recurrence.Expand(yearly, D(1), new DateTime(2026, 12, 1)));
        }

        [Fact]
        public void Monthly_skips_months_without_that_day()
        {
            var m = Master(new DateTime(2026, 1, 31, 9, 0, 0), "FREQ=MONTHLY");
            var occ = Recurrence.Expand(m, new DateTime(2026, 2, 1), new DateTime(2026, 4, 1)).ToList();
            Assert.Single(occ); // only 31 March exists in Feb..Mar
            Assert.Equal(new DateTime(2026, 3, 31, 9, 0, 0), occ[0].StartLocal);
        }

        [Fact]
        public void Count_limits_occurrences()
        {
            var m = Master(D(8, 9), "FREQ=DAILY;COUNT=3");
            Assert.Equal(3, Recurrence.Expand(m, D(1), new DateTime(2026, 12, 1)).Count());
        }

        [Fact]
        public void Until_excludes_later_occurrences()
        {
            var m = Master(D(1, 9), "FREQ=DAILY;UNTIL=20261105T000000Z");
            Assert.Equal(4, Recurrence.Expand(m, D(1), new DateTime(2026, 12, 1)).Count());
            Assert.Empty(Recurrence.Expand(m, D(10), D(11)));
        }

        [Fact]
        public void Exdate_removes_an_occurrence()
        {
            var m = Master(D(9, 9), "FREQ=DAILY");
            m.ExDatesLocal.Add(D(11, 9));
            var occ = Recurrence.Expand(m, D(10), D(13)).Select(o => o.StartLocal).ToList();
            Assert.Equal(new[] { D(10, 9), D(12, 9) }, occ);
        }

        [Theory]
        [InlineData("FREQ=HOURLY")]
        [InlineData("FREQ=MONTHLY;BYDAY=1MO;BYSETPOS=1")]
        [InlineData("garbage")]
        public void Unsupported_rules_yield_nothing_and_do_not_throw(string rule)
        {
            var m = Master(D(1, 9), rule);
            Assert.Empty(Recurrence.Expand(m, D(1), new DateTime(2026, 12, 1)));
        }

        [Fact]
        public void Occurrences_keep_duration()
        {
            var m = Master(D(9, 9), "FREQ=DAILY", 1.5);
            foreach (var o in Recurrence.Expand(m, D(10), D(13)))
                Assert.Equal(TimeSpan.FromHours(1.5), o.EndLocal - o.StartLocal);
        }

        [Fact]
        public void Non_recurring_yields_itself_only_when_overlapping()
        {
            var m = Master(D(10, 9), null);
            Assert.Single(Recurrence.Expand(m, D(10), D(11)));
            Assert.Empty(Recurrence.Expand(m, D(11), D(12)));
        }

        [Fact]
        public void Old_daily_series_still_reaches_the_window()
        {
            var m = Master(new DateTime(2015, 1, 1, 9, 0, 0), "FREQ=DAILY");
            Assert.Single(Recurrence.Expand(m, D(10), D(11)));
        }
    }
}
