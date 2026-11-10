using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DriftClock.Tests
{
    /// <summary>Behaviors real Google/Outlook feeds hit: alarms, moved/cancelled instances, cross-zone series.</summary>
    public class ICalFeedTests
    {
        // Rome: UTC+2 until 25 Oct 2026, UTC+1 after.
        private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

        private static string Cal(params string[] events)
        {
            return "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" + string.Concat(events) + "END:VCALENDAR\r\n";
        }

        private static string Ev(string body)
        {
            return "BEGIN:VEVENT\r\n" + body + "END:VEVENT\r\n";
        }

        private static List<CalEvent> Occurrences(string ics, DateTime from, DateTime to)
        {
            return ICal.Parse(ics, Rome).SelectMany(m => Recurrence.Expand(m, from, to)).OrderBy(o => o.StartLocal).ToList();
        }

        [Fact]
        public void Valarm_summary_does_not_replace_the_event_title()
        {
            string ics = Cal(Ev("UID:1\r\nDTSTART:20261110T150000Z\r\nSUMMARY:Real title\r\n" +
                                "BEGIN:VALARM\r\nACTION:EMAIL\r\nSUMMARY:Alarm notification\r\nTRIGGER:-PT10M\r\nEND:VALARM\r\n"));
            Assert.Equal("Real title", ICal.Parse(ics, Rome).Single().Summary);
        }

        [Fact]
        public void Valarm_dtstart_like_properties_do_not_leak_into_the_event()
        {
            string ics = Cal(Ev("UID:1\r\nDTSTART:20261110T150000Z\r\nSUMMARY:X\r\n" +
                                "BEGIN:VALARM\r\nTRIGGER;VALUE=DATE-TIME:20261110T140000Z\r\nDTSTART:20200101T000000Z\r\nEND:VALARM\r\n"));
            Assert.Equal(new DateTime(2026, 11, 10, 16, 0, 0), ICal.Parse(ics, Rome).Single().StartLocal);
        }

        [Fact]
        public void Moved_instance_replaces_the_original_occurrence()
        {
            string ics = Cal(
                Ev("UID:a\r\nDTSTART:20261109T090000\r\nDTEND:20261109T100000\r\nRRULE:FREQ=DAILY\r\nSUMMARY:Standup\r\n"),
                Ev("UID:a\r\nRECURRENCE-ID:20261110T090000\r\nDTSTART:20261110T110000\r\nDTEND:20261110T120000\r\nSUMMARY:Standup (moved)\r\n"));
            var day = Occurrences(ics, new DateTime(2026, 11, 10), new DateTime(2026, 11, 11));
            Assert.Single(day);
            Assert.Equal(new DateTime(2026, 11, 10, 11, 0, 0), day[0].StartLocal);
            Assert.Equal("Standup (moved)", day[0].Summary);
            // the neighbouring days are untouched
            Assert.Single(Occurrences(ics, new DateTime(2026, 11, 11), new DateTime(2026, 11, 12)));
        }

        [Fact]
        public void Cancelled_instance_removes_just_that_occurrence()
        {
            string ics = Cal(
                Ev("UID:a\r\nDTSTART:20261109T090000\r\nDTEND:20261109T100000\r\nRRULE:FREQ=DAILY\r\nSUMMARY:Standup\r\n"),
                Ev("UID:a\r\nRECURRENCE-ID:20261110T090000\r\nDTSTART:20261110T090000\r\nSTATUS:CANCELLED\r\nSUMMARY:Standup\r\n"));
            Assert.Empty(Occurrences(ics, new DateTime(2026, 11, 10), new DateTime(2026, 11, 11)));
            Assert.Single(Occurrences(ics, new DateTime(2026, 11, 11), new DateTime(2026, 11, 12)));
        }

        [Fact]
        public void Override_without_a_matching_master_is_kept()
        {
            string ics = Cal(Ev("UID:zzz\r\nRECURRENCE-ID:20261110T090000\r\nDTSTART:20261110T110000\r\nSUMMARY:Orphan\r\n"));
            Assert.Single(Occurrences(ics, new DateTime(2026, 11, 10), new DateTime(2026, 11, 11)));
        }

        [Fact]
        public void Weekly_utc_series_keeps_its_utc_time_across_a_dst_change()
        {
            // 19 Oct 07:00Z = 09:00 CEST; after the 25 Oct change the same UTC time is 08:00 CET.
            string ics = Cal(Ev("UID:u\r\nDTSTART:20261019T070000Z\r\nDTEND:20261019T073000Z\r\nRRULE:FREQ=WEEKLY\r\nSUMMARY:Sync\r\n"));
            var before = Occurrences(ics, new DateTime(2026, 10, 19), new DateTime(2026, 10, 20));
            var after = Occurrences(ics, new DateTime(2026, 11, 2), new DateTime(2026, 11, 3));
            Assert.Equal(new DateTime(2026, 10, 19, 9, 0, 0), before.Single().StartLocal);
            Assert.Equal(new DateTime(2026, 11, 2, 8, 0, 0), after.Single().StartLocal);
        }

        [Fact]
        public void Weekly_tokyo_series_is_correct_in_november_when_started_in_july()
        {
            // Tokyo has no DST: 09:00 JST = 00:00Z = 02:00 CEST in July, 01:00 CET in November.
            string ics = Cal(Ev("UID:t\r\nDTSTART;TZID=Asia/Tokyo:20260706T090000\r\nDTEND;TZID=Asia/Tokyo:20260706T100000\r\n" +
                                "RRULE:FREQ=WEEKLY\r\nEXDATE;TZID=Asia/Tokyo:20261109T090000\r\nSUMMARY:Call\r\n"));
            var july = Occurrences(ics, new DateTime(2026, 7, 6), new DateTime(2026, 7, 7));
            var nov2 = Occurrences(ics, new DateTime(2026, 11, 2), new DateTime(2026, 11, 3));
            var nov9 = Occurrences(ics, new DateTime(2026, 11, 9), new DateTime(2026, 11, 10));
            Assert.Equal(new DateTime(2026, 7, 6, 2, 0, 0), july.Single().StartLocal);
            Assert.Equal(new DateTime(2026, 11, 2, 1, 0, 0), nov2.Single().StartLocal);
            Assert.Empty(nov9); // the EXDATE still matches once expansion happens in the series' own zone
        }

        [Fact]
        public void Floating_recurring_events_still_expand_in_local_time()
        {
            string ics = Cal(Ev("UID:f\r\nDTSTART:20261019T090000\r\nRRULE:FREQ=WEEKLY\r\nSUMMARY:Local\r\n"));
            Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0),
                         Occurrences(ics, new DateTime(2026, 11, 2), new DateTime(2026, 11, 3)).Single().StartLocal);
        }
    }
}
