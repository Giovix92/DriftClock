using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DriftClock
{
    public sealed class Settings
    {
        public string LocationName { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool Use12Hour { get; set; }
        public bool UseFahrenheit { get; set; }
        public bool ShowWeather { get; set; } = true;
        public bool ShowBattery { get; set; } = true;
        public bool ShowMonthGrid { get; set; }
        public bool ShowEvents { get; set; }
        public string ICalUrl { get; set; }
        public int NightBrightnessPercent { get; set; } = 35;
        public TimeSpan ManualNightStart { get; set; } = TimeSpan.FromHours(23);
        public TimeSpan ManualNightEnd { get; set; } = TimeSpan.FromHours(7);

        public static string DefaultPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "DriftClock", "settings.ini");
            }
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static Settings Load(string path)
        {
            var s = new Settings();
            Dictionary<string, string> kv;
            try { kv = Read(path); } catch { return s; }

            string v;
            if (kv.TryGetValue("LocationName", out v)) s.LocationName = v;
            double d;
            if (kv.TryGetValue("Latitude", out v) && double.TryParse(v, NumberStyles.Float, Inv, out d)) s.Latitude = d;
            if (kv.TryGetValue("Longitude", out v) && double.TryParse(v, NumberStyles.Float, Inv, out d)) s.Longitude = d;
            s.Use12Hour = Bool(kv, "Use12Hour", s.Use12Hour);
            s.UseFahrenheit = Bool(kv, "UseFahrenheit", s.UseFahrenheit);
            s.ShowWeather = Bool(kv, "ShowWeather", s.ShowWeather);
            s.ShowBattery = Bool(kv, "ShowBattery", s.ShowBattery);
            s.ShowMonthGrid = Bool(kv, "ShowMonthGrid", s.ShowMonthGrid);
            s.ShowEvents = Bool(kv, "ShowEvents", s.ShowEvents);
            int n;
            if (kv.TryGetValue("NightBrightnessPercent", out v) && int.TryParse(v, NumberStyles.Integer, Inv, out n))
                s.NightBrightnessPercent = Math.Max(10, Math.Min(100, n));
            s.ManualNightStart = Time(kv, "ManualNightStart", s.ManualNightStart);
            s.ManualNightEnd = Time(kv, "ManualNightEnd", s.ManualNightEnd);
            if (kv.TryGetValue("ICalUrlProtected", out v) && !string.IsNullOrEmpty(v))
            {
                try
                {
                    s.ICalUrl = Encoding.UTF8.GetString(
                        ProtectedData.Unprotect(Convert.FromBase64String(v), null, DataProtectionScope.CurrentUser));
                }
                catch { s.ICalUrl = null; }
            }
            return s;
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var sb = new StringBuilder();
            if (LocationName != null) sb.AppendLine("LocationName=" + LocationName.Replace("\r", " ").Replace("\n", " "));
            if (Latitude.HasValue) sb.AppendLine("Latitude=" + Latitude.Value.ToString("R", Inv));
            if (Longitude.HasValue) sb.AppendLine("Longitude=" + Longitude.Value.ToString("R", Inv));
            sb.AppendLine("Use12Hour=" + Use12Hour);
            sb.AppendLine("UseFahrenheit=" + UseFahrenheit);
            sb.AppendLine("ShowWeather=" + ShowWeather);
            sb.AppendLine("ShowBattery=" + ShowBattery);
            sb.AppendLine("ShowMonthGrid=" + ShowMonthGrid);
            sb.AppendLine("ShowEvents=" + ShowEvents);
            sb.AppendLine("NightBrightnessPercent=" + Math.Max(10, Math.Min(100, NightBrightnessPercent)).ToString(Inv));
            sb.AppendLine("ManualNightStart=" + ManualNightStart.ToString(@"hh\:mm", Inv));
            sb.AppendLine("ManualNightEnd=" + ManualNightEnd.ToString(@"hh\:mm", Inv));
            if (!string.IsNullOrWhiteSpace(ICalUrl))
            {
                byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(ICalUrl.Trim()), null, DataProtectionScope.CurrentUser);
                sb.AppendLine("ICalUrlProtected=" + Convert.ToBase64String(enc));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static Dictionary<string, string> Read(string path)
        {
            var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(path))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                kv[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
            return kv;
        }

        private static bool Bool(Dictionary<string, string> kv, string key, bool def)
        {
            string v; bool b;
            return kv.TryGetValue(key, out v) && bool.TryParse(v, out b) ? b : def;
        }

        private static TimeSpan Time(Dictionary<string, string> kv, string key, TimeSpan def)
        {
            string v; TimeSpan t;
            return kv.TryGetValue(key, out v) && TimeSpan.TryParseExact(v, @"hh\:mm", Inv, out t) ? t : def;
        }
    }
}
