using System;
using System.Collections.Generic;

namespace DriftClock
{
    /// <summary>Maps IANA time zone names (used by Google/Outlook iCal feeds) to Windows zones; .NET Framework cannot do this itself.</summary>
    public static class IanaZones
    {
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Europe/London", "GMT Standard Time"}, {"Europe/Dublin", "GMT Standard Time"}, {"Europe/Lisbon", "GMT Standard Time"},
            {"Europe/Paris", "Romance Standard Time"}, {"Europe/Madrid", "Romance Standard Time"},
            {"Europe/Brussels", "Romance Standard Time"}, {"Europe/Copenhagen", "Romance Standard Time"},
            {"Europe/Berlin", "W. Europe Standard Time"}, {"Europe/Rome", "W. Europe Standard Time"},
            {"Europe/Amsterdam", "W. Europe Standard Time"}, {"Europe/Vienna", "W. Europe Standard Time"},
            {"Europe/Zurich", "W. Europe Standard Time"}, {"Europe/Stockholm", "W. Europe Standard Time"},
            {"Europe/Oslo", "W. Europe Standard Time"}, {"Europe/Luxembourg", "W. Europe Standard Time"},
            {"Europe/Prague", "Central Europe Standard Time"}, {"Europe/Budapest", "Central Europe Standard Time"},
            {"Europe/Belgrade", "Central Europe Standard Time"}, {"Europe/Warsaw", "Central European Standard Time"},
            {"Europe/Zagreb", "Central European Standard Time"},
            {"Europe/Athens", "GTB Standard Time"}, {"Europe/Bucharest", "GTB Standard Time"},
            {"Europe/Helsinki", "FLE Standard Time"}, {"Europe/Kiev", "FLE Standard Time"}, {"Europe/Kyiv", "FLE Standard Time"},
            {"Europe/Sofia", "FLE Standard Time"}, {"Europe/Riga", "FLE Standard Time"},
            {"Europe/Tallinn", "FLE Standard Time"}, {"Europe/Vilnius", "FLE Standard Time"},
            {"Europe/Istanbul", "Turkey Standard Time"}, {"Europe/Moscow", "Russian Standard Time"},
            {"America/New_York", "Eastern Standard Time"}, {"America/Toronto", "Eastern Standard Time"},
            {"America/Detroit", "Eastern Standard Time"}, {"America/Chicago", "Central Standard Time"},
            {"America/Denver", "Mountain Standard Time"}, {"America/Phoenix", "US Mountain Standard Time"},
            {"America/Los_Angeles", "Pacific Standard Time"}, {"America/Vancouver", "Pacific Standard Time"},
            {"America/Anchorage", "Alaskan Standard Time"}, {"America/Halifax", "Atlantic Standard Time"},
            {"America/Mexico_City", "Central Standard Time (Mexico)"}, {"America/Sao_Paulo", "E. South America Standard Time"},
            {"America/Argentina/Buenos_Aires", "Argentina Standard Time"}, {"America/Buenos_Aires", "Argentina Standard Time"},
            {"America/Bogota", "SA Pacific Standard Time"}, {"America/Santiago", "Pacific SA Standard Time"},
            {"Pacific/Honolulu", "Hawaiian Standard Time"}, {"Pacific/Auckland", "New Zealand Standard Time"},
            {"Asia/Tokyo", "Tokyo Standard Time"}, {"Asia/Seoul", "Korea Standard Time"},
            {"Asia/Shanghai", "China Standard Time"}, {"Asia/Hong_Kong", "China Standard Time"},
            {"Asia/Singapore", "Singapore Standard Time"}, {"Asia/Manila", "Singapore Standard Time"},
            {"Asia/Taipei", "Taipei Standard Time"}, {"Asia/Kolkata", "India Standard Time"}, {"Asia/Calcutta", "India Standard Time"},
            {"Asia/Dubai", "Arabian Standard Time"}, {"Asia/Riyadh", "Arab Standard Time"}, {"Asia/Tehran", "Iran Standard Time"},
            {"Asia/Jerusalem", "Israel Standard Time"}, {"Asia/Bangkok", "SE Asia Standard Time"},
            {"Asia/Jakarta", "SE Asia Standard Time"}, {"Asia/Karachi", "Pakistan Standard Time"},
            {"Asia/Dhaka", "Bangladesh Standard Time"},
            {"Australia/Sydney", "AUS Eastern Standard Time"}, {"Australia/Melbourne", "AUS Eastern Standard Time"},
            {"Australia/Brisbane", "E. Australia Standard Time"}, {"Australia/Perth", "W. Australia Standard Time"},
            {"Australia/Adelaide", "Cen. Australia Standard Time"},
            {"Africa/Johannesburg", "South Africa Standard Time"}, {"Africa/Cairo", "Egypt Standard Time"},
            {"Africa/Lagos", "W. Central Africa Standard Time"}, {"Africa/Nairobi", "E. Africa Standard Time"},
        };

        public static TimeZoneInfo Find(string tzid)
        {
            if (string.IsNullOrWhiteSpace(tzid)) return null;
            tzid = tzid.Trim().Trim('"');
            if (tzid.Equals("UTC", StringComparison.OrdinalIgnoreCase) ||
                tzid.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase) ||
                tzid.Equals("GMT", StringComparison.OrdinalIgnoreCase)) return TimeZoneInfo.Utc;
            try { return TimeZoneInfo.FindSystemTimeZoneById(tzid); } catch { }
            string win;
            if (Map.TryGetValue(tzid, out win))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(win); } catch { }
            }
            return null;
        }
    }
}
