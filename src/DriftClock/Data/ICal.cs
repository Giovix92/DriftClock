using System;
using System.Collections.Generic;
using System.Globalization;
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

            List<string> lines = Unfold(ics);
            List<string> block = null;
            foreach (string line in lines)
            {
                if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) block = new List<string>();
                else if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (block != null)
                    {
                        try
                        {
                            CalEvent e = ParseEvent(block, local);
                            if (e != null) result.Add(e);
                        }
                        catch { /* skip malformed event */ }
                    }
                    block = null;
                }
                else if (block != null) block.Add(line);
            }
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

        private static CalEvent ParseEvent(List<string> block, TimeZoneInfo local)
        {
            var e = new CalEvent();
            Prop start = null, end = null;
            foreach (string line in block)
            {
                Prop p = SplitLine(line);
                if (p == null) continue;
                switch (p.Name)
                {
                    case "SUMMARY": e.Summary = Unescape(p.Value); break;
                    case "DTSTART": start = p; break;
                    case "DTEND": end = p; break;
                    case "RRULE": e.RRule = p.Value.Trim(); break;
                    case "STATUS":
                        if (p.Value.Trim().Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)) return null;
                        break;
                    case "EXDATE":
                        foreach (string part in p.Value.Split(','))
                        {
                            bool d;
                            DateTime? x = ParseTime(part.Trim(), p.Params, local, out d);
                            if (x.HasValue) e.ExDatesLocal.Add(x.Value);
                        }
                        break;
                }
            }
            if (start == null) return null;

            bool allDay;
            DateTime? s = ParseTime(start.Value.Trim(), start.Params, local, out allDay);
            if (!s.HasValue) return null;
            e.StartLocal = s.Value;
            e.AllDay = allDay;

            DateTime? en = null;
            if (end != null)
            {
                bool ignored;
                en = ParseTime(end.Value.Trim(), end.Params, local, out ignored);
            }
            e.EndLocal = en ?? (allDay ? e.StartLocal.AddDays(1) : e.StartLocal);
            if (e.Summary == null) e.Summary = "";
            return e;
        }

        private static DateTime? ParseTime(string value, Dictionary<string, string> prm, TimeZoneInfo local, out bool allDay)
        {
            string valueType;
            allDay = (prm.TryGetValue("VALUE", out valueType) && valueType.Equals("DATE", StringComparison.OrdinalIgnoreCase))
                     || value.Length == 8;
            bool utc = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
            if (utc) value = value.Substring(0, value.Length - 1);

            DateTime dt;
            if (!DateTime.TryParseExact(value, DateTimeFormats, Inv, DateTimeStyles.None, out dt)) return null;
            if (allDay) return dt.Date;

            if (utc) return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), local);

            string tzid;
            if (prm.TryGetValue("TZID", out tzid))
            {
                TimeZoneInfo zone = IanaZones.Find(tzid);
                if (zone != null)
                {
                    try { return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), zone, local); }
                    catch { return dt; }
                }
            }
            return dt; // floating
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
