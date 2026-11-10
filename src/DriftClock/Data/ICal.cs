using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DriftClock
{
    public sealed class CalEvent
    {
        public string Summary;
        public DateTime StartLocal;
        public DateTime EndLocal;
        public bool AllDay;
        public string RRule;
        public List<DateTime> ExDatesLocal = new List<DateTime>();

        // Series time zone, so recurrences keep the right wall-clock/UTC time across DST changes.
        public TimeZoneInfo SourceZone;     // null = floating or all-day (expand in local time)
        public DateTime StartSource;        // DTSTART as wall-clock time in SourceZone
        public TimeZoneInfo LocalZone;      // the zone StartLocal etc. are expressed in

        // Moved / cancelled single instances of a series.
        public string Uid;
        public DateTime? RecurrenceIdLocal;
        public bool Cancelled;
    }

    public static class ICal
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] DateTimeFormats = { "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm", "yyyyMMdd" };

        public static string NormalizeUrl(string url)
        {
            if (url == null) return null;
            url = url.Trim();
            if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url.Substring(9);
            return url;
        }

        public static List<CalEvent> Parse(string ics, TimeZoneInfo local)
        {
            var result = new List<CalEvent>();
            if (string.IsNullOrEmpty(ics)) return result;
            if (local == null) local = TimeZoneInfo.Local;

            List<string> block = null;
            int nested = 0; // depth inside VALARM and other sub-components of a VEVENT
            foreach (string line in Unfold(ics))
            {
                if (block == null)
                {
                    if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) { block = new List<string>(); nested = 0; }
                    continue;
                }
                if (line.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase)) { nested++; continue; }
                if (line.StartsWith("END:", StringComparison.OrdinalIgnoreCase))
                {
                    if (nested > 0) { nested--; continue; }
                    try
                    {
                        CalEvent e = ParseEvent(block, local);
                        if (e != null) result.Add(e);
                    }
                    catch { /* skip malformed event */ }
                    block = null;
                    continue;
                }
                if (nested == 0) block.Add(line);
            }

            // A moved or cancelled instance replaces that occurrence of its series.
            var series = new Dictionary<string, List<CalEvent>>();
            foreach (CalEvent e in result.Where(x => x.RRule != null && x.Uid != null))
            {
                List<CalEvent> list;
                if (!series.TryGetValue(e.Uid, out list)) series[e.Uid] = list = new List<CalEvent>();
                list.Add(e);
            }
            foreach (CalEvent o in result.Where(x => x.RecurrenceIdLocal.HasValue && x.Uid != null))
            {
                List<CalEvent> masters;
                if (series.TryGetValue(o.Uid, out masters))
                    foreach (CalEvent m in masters) m.ExDatesLocal.Add(o.RecurrenceIdLocal.Value);
            }
            result.RemoveAll(e => e.Cancelled);
            return result;
        }

        private static List<string> Unfold(string ics)
        {
            var lines = new List<string>();
            foreach (string raw in ics.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t') && lines.Count > 0)
                    lines[lines.Count - 1] += raw.Substring(1);
                else
                    lines.Add(raw);
            }
            return lines;
        }

        private sealed class Prop
        {
            public string Name;
            public Dictionary<string, string> Params = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string Value;
        }

        private static Prop SplitLine(string line)
        {
            int colon = -1;
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') quoted = !quoted;
                else if (line[i] == ':' && !quoted) { colon = i; break; }
            }
            if (colon <= 0) return null;
            string[] head = line.Substring(0, colon).Split(';');
            var p = new Prop { Name = head[0].Trim().ToUpperInvariant(), Value = line.Substring(colon + 1) };
            for (int i = 1; i < head.Length; i++)
            {
                int eq = head[i].IndexOf('=');
                if (eq > 0) p.Params[head[i].Substring(0, eq).Trim()] = head[i].Substring(eq + 1).Trim().Trim('"');
            }
            return p;
        }

        private sealed class Parsed
        {
            public DateTime Local;
            public DateTime Source;
            public TimeZoneInfo Zone; // null = floating or all-day
            public bool AllDay;
        }

        private static CalEvent ParseEvent(List<string> block, TimeZoneInfo local)
        {
            var e = new CalEvent { LocalZone = local };
            Prop start = null, end = null, recurrenceId = null;
            foreach (string line in block)
            {
                Prop p = SplitLine(line);
                if (p == null) continue;
                switch (p.Name)
                {
                    case "SUMMARY": e.Summary = Unescape(p.Value); break;
                    case "UID": e.Uid = p.Value.Trim(); break;
                    case "DTSTART": start = p; break;
                    case "DTEND": end = p; break;
                    case "RECURRENCE-ID": recurrenceId = p; break;
                    case "RRULE": e.RRule = p.Value.Trim(); break;
                    case "STATUS":
                        if (p.Value.Trim().Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) e.Cancelled = true;
                        break;
                    case "EXDATE":
                        foreach (string part in p.Value.Split(','))
                        {
                            Parsed x = ParseTime(part.Trim(), p.Params, local);
                            if (x != null) e.ExDatesLocal.Add(x.Local);
                        }
                        break;
                }
            }
            if (start == null) return null;

            Parsed s = ParseTime(start.Value.Trim(), start.Params, local);
            if (s == null) return null;
            e.StartLocal = s.Local;
            e.AllDay = s.AllDay;
            e.SourceZone = s.Zone;
            e.StartSource = s.Source;

            Parsed en = end == null ? null : ParseTime(end.Value.Trim(), end.Params, local);
            e.EndLocal = en != null ? en.Local : (s.AllDay ? e.StartLocal.AddDays(1) : e.StartLocal);

            if (recurrenceId != null)
            {
                Parsed r = ParseTime(recurrenceId.Value.Trim(), recurrenceId.Params, local);
                if (r != null) e.RecurrenceIdLocal = r.Local;
            }
            if (e.Summary == null) e.Summary = "";
            return e;
        }

        private static Parsed ParseTime(string value, Dictionary<string, string> prm, TimeZoneInfo local)
        {
            string valueType;
            bool allDay = (prm.TryGetValue("VALUE", out valueType) && valueType.Equals("DATE", StringComparison.OrdinalIgnoreCase))
                          || value.Length == 8;
            bool utc = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
            if (utc) value = value.Substring(0, value.Length - 1);

            DateTime dt;
            if (!DateTime.TryParseExact(value, DateTimeFormats, Inv, DateTimeStyles.None, out dt)) return null;
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
            if (allDay) return new Parsed { Local = dt.Date, Source = dt.Date, AllDay = true };

            if (utc)
                return new Parsed { Local = TimeZoneInfo.ConvertTimeFromUtc(dt, local), Source = dt, Zone = TimeZoneInfo.Utc };

            string tzid;
            if (prm.TryGetValue("TZID", out tzid))
            {
                TimeZoneInfo zone = IanaZones.Find(tzid);
                if (zone != null)
                    return new Parsed { Local = Recurrence.ToLocal(dt, zone, local), Source = dt, Zone = zone };
            }
            return new Parsed { Local = dt, Source = dt }; // floating
        }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    sb.Append(n == 'n' || n == 'N' ? ' ' : n);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
