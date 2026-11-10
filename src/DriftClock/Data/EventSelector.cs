using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DriftClock
{
    public sealed class EventLine
    {
        public string Text;
    }

    public static class EventSelector
    {
        private const int MaxLines = 4;
        private const int MinTodayBeforeTomorrow = 3;
        private const int MaxSummary = 40;

        public static List<EventLine> Select(IEnumerable<CalEvent> masters, DateTime nowLocal, bool use12h, CultureInfo culture)
        {
            var lines = new List<EventLine>();
            if (masters == null) return lines;
            if (culture == null) culture = CultureInfo.CurrentCulture;

            DateTime today = nowLocal.Date, tomorrow = today.AddDays(1), dayAfter = today.AddDays(2);
            var occurrences = new List<CalEvent>();
            foreach (CalEvent m in masters)
                occurrences.AddRange(Recurrence.Expand(m, today, dayAfter));

            // Today: all-day events covering today, plus timed events that have not ended yet and start before midnight.
            Func<CalEvent, bool> isToday = o => o.AllDay
                ? o.StartLocal.Date <= today && o.EndLocal > today
                : o.StartLocal < tomorrow && o.EndLocal > nowLocal;
            var todayList = Order(occurrences.Where(isToday)).ToList();

            foreach (CalEvent o in todayList.Take(MaxLines))
                lines.Add(new EventLine { Text = Format(o, false, use12h, culture) });

            if (todayList.Count < MinTodayBeforeTomorrow)
            {
                Func<CalEvent, bool> isTomorrow = o => !todayList.Contains(o) && (o.AllDay
                    ? o.StartLocal.Date <= tomorrow && o.EndLocal > tomorrow
                    : o.StartLocal >= tomorrow && o.StartLocal < dayAfter);
                foreach (CalEvent o in Order(occurrences.Where(isTomorrow)))
                {
                    if (lines.Count >= MaxLines) break;
                    lines.Add(new EventLine { Text = Format(o, true, use12h, culture) });
                }
            }
            return lines;
        }

        private static IEnumerable<CalEvent> Order(IEnumerable<CalEvent> events)
        {
            return events.OrderBy(o => o.AllDay ? 0 : 1).ThenBy(o => o.StartLocal);
        }

        private static string Format(CalEvent e, bool withDay, bool use12h, CultureInfo culture)
        {
            string title = (e.Summary ?? "").Trim();
            if (title.Length > MaxSummary) title = title.Substring(0, MaxSummary - 1) + "…";
            string when = e.AllDay ? "All day" : e.StartLocal.ToString(use12h ? "h:mm tt" : "HH:mm", culture);
            string day = withDay ? culture.DateTimeFormat.GetAbbreviatedDayName(e.StartLocal.DayOfWeek) + " " : "";
            return (day + when + " " + title).TrimEnd();
        }
    }
}
