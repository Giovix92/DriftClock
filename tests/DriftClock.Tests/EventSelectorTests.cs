using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace DriftClock.Tests
{
    public class EventSelectorTests
    {
        private static readonly CultureInfo En = new CultureInfo("en-US");
        private static readonly DateTime Now = new DateTime(2026, 11, 10, 14, 0, 0);

        private static CalEvent Timed(string title, DateTime start, double hours = 1)
        {
            return new CalEvent { Summary = title, StartLocal = start, EndLocal = start.AddHours(hours) };
        }

        private static CalEvent AllDay(string title, DateTime day)
        {
            return new CalEvent { Summary = title, StartLocal = day.Date, EndLocal = day.Date.AddDays(1), AllDay = true };
        }

        private static List<string> Lines(IEnumerable<CalEvent> ev, DateTime now, bool h12 = false)
        {
            return EventSelector.Select(ev, now, h12, En).Select(l => l.Text).ToList();
        }

        [Fact]
        public void Remaining_today_with_all_day_first_and_ended_events_dropped()
        {
            var ev = new[]
            {
                Timed("Standup", new DateTime(2026, 11, 10, 9, 0, 0)),
                Timed("Gym", new DateTime(2026, 11, 10, 18, 30, 0)),
                Timed("Design review", new DateTime(2026, 11, 10, 16, 0, 0)),
                AllDay("Holiday", new DateTime(2026, 11, 10)),
                Timed("Dentist", new DateTime(2026, 11, 11, 9, 0, 0)),
            };
            Assert.Equal(new[] { "All day Holiday", "16:00 Design review", "18:30 Gym" }, Lines(ev, Now));
        }

        [Fact]
        public void Tomorrow_fills_in_when_fewer_than_three_today()
        {
            var ev = new[]
            {
                Timed("Gym", new DateTime(2026, 11, 10, 18, 30, 0)),
                Timed("Dentist", new DateTime(2026, 11, 11, 9, 0, 0)),
                AllDay("Trip", new DateTime(2026, 11, 11)),
                Timed("Lunch", new DateTime(2026, 11, 11, 13, 0, 0)),
                Timed("Dinner", new DateTime(2026, 11, 11, 20, 0, 0)),
            };
            var lines = Lines(ev, Now);
            Assert.Equal(4, lines.Count);
            Assert.Equal("18:30 Gym", lines[0]);
            Assert.Equal("Wed All day Trip", lines[1]);
            Assert.Equal("Wed 09:00 Dentist", lines[2]);
            Assert.Equal("Wed 13:00 Lunch", lines[3]);
        }

        [Fact]
        public void Event_spanning_midnight_is_shown_while_in_progress()
        {
            var ev = new[] { Timed("Late show", new DateTime(2026, 11, 9, 22, 0, 0), 3) };
            var lines = Lines(ev, new DateTime(2026, 11, 10, 0, 30, 0));
            Assert.Single(lines);
            Assert.Contains("Late show", lines[0]);
        }

        [Fact]
        public void Twelve_hour_format()
        {
            var ev = new[] { Timed("Design review", new DateTime(2026, 11, 10, 16, 0, 0)) };
            Assert.Equal("4:00 PM Design review", Lines(ev, Now, true).Single());
        }

        [Fact]
        public void Long_summaries_are_truncated_with_ellipsis()
        {
            var ev = new[] { Timed(new string('x', 50), new DateTime(2026, 11, 10, 16, 0, 0)) };
            string line = Lines(ev, Now).Single();
            string summary = line.Substring("16:00 ".Length);
            Assert.Equal(40, summary.Length);
            Assert.EndsWith("…", summary);
        }

        [Fact]
        public void Recurring_events_are_expanded_and_empty_input_is_fine()
        {
            var daily = new CalEvent
            {
                Summary = "Standup", RRule = "FREQ=DAILY",
                StartLocal = new DateTime(2026, 1, 1, 15, 0, 0), EndLocal = new DateTime(2026, 1, 1, 15, 30, 0)
            };
            Assert.Equal("15:00 Standup", Lines(new[] { daily }, Now)[0]);
            Assert.Empty(Lines(new CalEvent[0], Now));
            Assert.Empty(Lines(null, Now));
        }
    }
}
