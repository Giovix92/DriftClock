using System;

namespace DriftClock
{
    public static class NightSchedule
    {
        public static bool IsNight(DateTime nowLocal, DateTime? sunriseLocal, DateTime? sunsetLocal,
                                   TimeSpan manualStart, TimeSpan manualEnd)
        {
            TimeSpan tod = nowLocal.TimeOfDay;
            if (sunriseLocal.HasValue && sunsetLocal.HasValue)
            {
                TimeSpan rise = sunriseLocal.Value.TimeOfDay, set = sunsetLocal.Value.TimeOfDay;
                return rise <= set ? (tod < rise || tod >= set) : (tod >= set && tod < rise);
            }
            if (manualStart == manualEnd) return false;
            return manualStart > manualEnd
                ? (tod >= manualStart || tod < manualEnd)
                : (tod >= manualStart && tod < manualEnd);
        }
    }
}
