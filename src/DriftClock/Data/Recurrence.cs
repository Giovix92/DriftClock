using System;
using System.Collections.Generic;
using System.Globalization;

namespace DriftClock
{
    /// <summary>Expands a master CalEvent with a basic RRULE into concrete occurrences inside a window.</summary>
    public static class Recurrence
    {
        private const int MaxIterations = 50000;
        private static readonly string[] Days = { "MO", "TU", "WE", "TH", "FR", "SA", "SU" };

        public static IEnumerable<CalEvent> Expand(CalEvent master, DateTime windowStart, DateTime windowEnd)
        {
            var result = new List<CalEvent>();
            if (master == null) return result;
            TimeSpan duration = master.EndLocal - master.StartLocal;

            if (string.IsNullOrWhiteSpace(master.RRule))
            {
                if (Overlaps(master.StartLocal, master.EndLocal, windowStart, windowEnd)) result.Add(master);
                return result;
            }

            Rule rule = ParseRule(master.RRule);
            if (rule == null) return result;

            int produced = 0, iterations = 0;
            foreach (DateTime start in Candidates(master.StartLocal, rule))
            {
                if (++iterations > MaxIterations) break;
                if (start >= windowEnd) break;
                if (rule.Until.HasValue && start > rule.Until.Value) break;
                if (rule.Count.HasValue && produced >= rule.Count.Value) break;
                produced++;

                DateTime end = start + duration;
                if (!Overlaps(start, end, windowStart, windowEnd)) continue;
                if (master.ExDatesLocal != null && master.ExDatesLocal.Contains(start)) continue;

                result.Add(new CalEvent
                {
                    Summary = master.Summary, StartLocal = start, EndLocal = end,
                    AllDay = master.AllDay, RRule = null, ExDatesLocal = new List<DateTime>()
                });
            }
            return result;
        }

        private static bool Overlaps(DateTime start, DateTime end, DateTime winStart, DateTime winEnd)
        {
            if (start >= winEnd) return false;
            return end > winStart || (end == start && start >= winStart);
        }

        private sealed class Rule
        {
            public string Freq;
            public int Interval = 1;
            public int? Count;
            public DateTime? Until;
            public List<int> ByDay; // 0 = Monday .. 6 = Sunday
        }

        private static Rule ParseRule(string text)
        {
            try
            {
                var r = new Rule();
                foreach (string part in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) return null;
                    string key = part.Substring(0, eq).Trim().ToUpperInvariant();
                    string val = part.Substring(eq + 1).Trim().ToUpperInvariant();
                    switch (key)
                    {
                        case "FREQ":
                            if (val != "DAILY" && val != "WEEKLY" && val != "MONTHLY" && val != "YEARLY") return null;
                            r.Freq = val;
                            break;
                        case "INTERVAL":
                            int iv;
                            if (!int.TryParse(val, out iv) || iv < 1) return null;
                            r.Interval = iv;
                            break;
                        case "COUNT":
                            int c;
                            if (!int.TryParse(val, out c) || c < 1) return null;
                            r.Count = c;
                            break;
                        case "UNTIL":
                            r.Until = ParseUntil(val);
                            if (!r.Until.HasValue) return null;
                            break;
                        case "WKST":
                            break;
                        case "BYDAY":
                            r.ByDay = new List<int>();
                            foreach (string d in val.Split(','))
                            {
                                int idx = Array.IndexOf(Days, d.Trim());
                                if (idx < 0) return null; // numeric prefixes (1MO) unsupported
                                if (!r.ByDay.Contains(idx)) r.ByDay.Add(idx);
                            }
                            r.ByDay.Sort();
                            break;
                        default:
                            return null; // BYSETPOS, BYMONTHDAY, ... unsupported
                    }
                }
                if (r.Freq == null) return null;
                if (r.ByDay != null && r.Freq != "WEEKLY") return null;
                return r;
            }
            catch { return null; }
        }

        private static DateTime? ParseUntil(string v)
        {
            DateTime dt;
            var inv = CultureInfo.InvariantCulture;
            if (v.EndsWith("Z") &&
                DateTime.TryParseExact(v.TrimEnd('Z'), "yyyyMMdd'T'HHmmss", inv, DateTimeStyles.None, out dt))
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), TimeZoneInfo.Local);
            if (DateTime.TryParseExact(v, "yyyyMMdd'T'HHmmss", inv, DateTimeStyles.None, out dt)) return dt;
            if (DateTime.TryParseExact(v, "yyyyMMdd", inv, DateTimeStyles.None, out dt)) return dt.Date.AddDays(1).AddTicks(-1);
            return null;
        }

        private static IEnumerable<DateTime> Candidates(DateTime start, Rule r)
        {
            TimeSpan tod = start.TimeOfDay;
            switch (r.Freq)
            {
                case "DAILY":
                    for (int k = 0; ; k++) yield return start.AddDays((double)k * r.Interval);

                case "WEEKLY":
                    if (r.ByDay == null || r.ByDay.Count == 0)
                    {
                        for (int k = 0; ; k++) yield return start.AddDays(7.0 * k * r.Interval);
                    }
                    else
                    {
                        DateTime monday = start.Date.AddDays(-(((int)start.DayOfWeek + 6) % 7));
                        for (int w = 0; ; w++)
                        {
                            DateTime weekStart = monday.AddDays(7.0 * w * r.Interval);
                            foreach (int d in r.ByDay)
                            {
                                DateTime cand = weekStart.AddDays(d) + tod;
                                if (cand >= start) yield return cand;
                            }
                        }
                    }
                    break;

                case "MONTHLY":
                    for (int k = 0; ; k++)
                    {
                        int months = start.Month - 1 + k * r.Interval;
                        int year = start.Year + months / 12, month = months % 12 + 1;
                        if (year > 9998) yield break;
                        if (start.Day <= DateTime.DaysInMonth(year, month))
                            yield return new DateTime(year, month, start.Day) + tod;
                    }

                case "YEARLY":
                    for (int k = 0; ; k++)
                    {
                        int year = start.Year + k * r.Interval;
                        if (year > 9998) yield break;
                        if (start.Day <= DateTime.DaysInMonth(year, start.Month))
                            yield return new DateTime(year, start.Month, start.Day) + tod;
                    }
            }
        }
    }
}
