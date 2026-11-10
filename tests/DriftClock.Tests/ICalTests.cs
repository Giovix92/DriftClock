using System;
using System.Linq;
using Xunit;

namespace DriftClock.Tests
{
    public class ICalTests
    {
        // Europe/Rome: UTC+1 on 10 Nov 2026
        private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

        private static string Wrap(string body)
        {
            return "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" + body + "END:VEVENT\r\nEND:VCALENDAR\r\n";
        }

        private static CalEvent One(string body)
        {
            return ICal.Parse(Wrap(body), Rome).Single();
        }

        [Fact]
        public void Folded_lines_unfold_and_escapes_are_unescaped()
        {
            var e = One("DTSTART:20261110T150000Z\r\nSUMMARY:Long\r\n  title\\, with comma\\nand break\r\n");
            Assert.Equal("Long title, with comma and break", e.Summary);
        }

        [Fact]
        public void Utc_start_converts_to_local()
        {
            var e = One("DTSTART:20261110T150000Z\r\nDTEND:20261110T160000Z\r\nSUMMARY:X\r\n");
            Assert.Equal(new DateTime(2026, 11, 10, 16, 0, 0), e.StartLocal);
            Assert.Equal(new DateTime(2026, 11, 10, 17, 0, 0), e.EndLocal);
            Assert.False(e.AllDay);
        }

        [Fact]
        public void Iana_tzid_converts_to_local()
        {
            var e = One("DTSTART;TZID=America/New_York:20261110T090000\r\nSUMMARY:X\r\n");
            Assert.Equal(new DateTime(2026, 11, 10, 15, 0, 0), e.StartLocal);
        }

        [Fact]
        public void All_day_end_is_exclusive_date()
        {
            var e = One("DTSTART;VALUE=DATE:20261110\r\nDTEND;VALUE=DATE:20261111\r\nSUMMARY:Holiday\r\n");
            Assert.True(e.AllDay);
            Assert.Equal(new DateTime(2026, 11, 10), e.StartLocal);
            Assert.Equal(new DateTime(2026, 11, 11), e.EndLocal);
        }

        [Fact]
        public void Floating_time_is_unchanged()
        {
            var e = One("DTSTART:20261110T090000\r\nSUMMARY:X\r\n");
            Assert.Equal(new DateTime(2026, 11, 10, 9, 0, 0), e.StartLocal);
        }

        [Fact]
        public void Missing_dtend_defaults()
        {
            var timed = One("DTSTART:20261110T090000\r\nSUMMARY:X\r\n");
            Assert.Equal(timed.StartLocal, timed.EndLocal);
            var day = One("DTSTART;VALUE=DATE:20261110\r\nSUMMARY:X\r\n");
            Assert.Equal(new DateTime(2026, 11, 11), day.EndLocal);
        }

        [Fact]
        public void Rrule_and_exdates_are_captured()
        {
            var e = One("DTSTART;TZID=Europe/Rome:20261101T090000\r\nRRULE:FREQ=DAILY;COUNT=5\r\n" +
                        "EXDATE;TZID=Europe/Rome:20261102T090000,20261103T090000\r\nSUMMARY:X\r\n");
            Assert.Equal("FREQ=DAILY;COUNT=5", e.RRule);
            Assert.Equal(2, e.ExDatesLocal.Count);
            Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), e.ExDatesLocal[0]);
        }

        [Fact]
        public void Garbage_and_events_without_start_are_skipped()
        {
            Assert.Empty(ICal.Parse("garbage", Rome));
            Assert.Empty(ICal.Parse(null, Rome));
            Assert.Empty(ICal.Parse(Wrap("SUMMARY:No start\r\n"), Rome));
        }

        [Fact]
        public void Cancelled_events_are_skipped()
        {
            Assert.Empty(ICal.Parse(Wrap("DTSTART:20261110T090000\r\nSTATUS:CANCELLED\r\nSUMMARY:X\r\n"), Rome));
        }

        [Theory]
        [InlineData("https://x/y.ics", true)]
        [InlineData("HTTPS://x/y.ics", true)]
        [InlineData("webcal://x/y.ics", true)]
        [InlineData("  webcal://x/y.ics ", true)]
        [InlineData("http://x/y.ics", false)]
        [InlineData("ftp://x/y.ics", false)]
        [InlineData("x/y.ics", false)]
        [InlineData("https://", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Only_https_and_webcal_links_are_secure(string url, bool secure)
        {
            Assert.Equal(secure, ICal.IsSecureUrl(url));
        }

        [Fact]
        public void Webcal_url_is_rewritten_to_https()
        {
            Assert.Equal("https://x/y.ics", ICal.NormalizeUrl("webcal://x/y.ics"));
            Assert.Equal("https://x/y.ics", ICal.NormalizeUrl("  https://x/y.ics "));
        }

        [Fact]
        public void IanaZones_resolves_common_names_and_unknown_is_null()
        {
            Assert.NotNull(IanaZones.Find("Europe/Berlin"));
            Assert.NotNull(IanaZones.Find("Asia/Tokyo"));
            Assert.Equal(TimeSpan.Zero, IanaZones.Find("UTC").BaseUtcOffset);
            Assert.Null(IanaZones.Find("Mars/Olympus_Mons"));
        }
    }
}
