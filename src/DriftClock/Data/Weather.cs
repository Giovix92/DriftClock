using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace DriftClock
{
    public sealed class WeatherInfo
    {
        public double CurrentTemp;
        public int WeatherCode;
        public double High;
        public double Low;
        public DateTime? SunriseLocal;
        public DateTime? SunsetLocal;
        public DateTime FetchedUtc;
    }

    public sealed class GeoResult
    {
        public string Name;
        public string Admin;
        public string Country;
        public double Latitude;
        public double Longitude;

        public override string ToString()
        {
            var parts = new List<string> { Name };
            if (!string.IsNullOrEmpty(Admin)) parts.Add(Admin);
            if (!string.IsNullOrEmpty(Country)) parts.Add(Country);
            return string.Join(", ", parts);
        }
    }

    public static class Weather
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string ForecastUrl(double lat, double lon, bool fahrenheit)
        {
            return "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString("R", Inv) +
                   "&longitude=" + lon.ToString("R", Inv) +
                   "&current=temperature_2m,weather_code" +
                   "&daily=temperature_2m_max,temperature_2m_min,sunrise,sunset" +
                   "&timezone=auto&forecast_days=1" +
                   (fahrenheit ? "&temperature_unit=fahrenheit" : "");
        }

        public static WeatherInfo ParseForecast(string json, DateTime fetchedUtc)
        {
            try
            {
                var root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (root == null) return null;
                var cur = root["current"] as Dictionary<string, object>;
                var daily = root["daily"] as Dictionary<string, object>;
                if (cur == null || daily == null) return null;

                double offset = root.ContainsKey("utc_offset_seconds") ? Num(root["utc_offset_seconds"]) : 0;
                return new WeatherInfo
                {
                    CurrentTemp = Num(cur["temperature_2m"]),
                    WeatherCode = (int)Num(cur["weather_code"]),
                    High = Num(First(daily["temperature_2m_max"])),
                    Low = Num(First(daily["temperature_2m_min"])),
                    SunriseLocal = SunTime(First(daily.ContainsKey("sunrise") ? daily["sunrise"] : null), offset),
                    SunsetLocal = SunTime(First(daily.ContainsKey("sunset") ? daily["sunset"] : null), offset),
                    FetchedUtc = fetchedUtc
                };
            }
            catch { return null; }
        }

        public static List<GeoResult> ParseGeocode(string json)
        {
            var list = new List<GeoResult>();
            try
            {
                var root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                var results = root != null && root.ContainsKey("results") ? root["results"] as IList : null;
                if (results == null) return list;
                foreach (object o in results)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null || !d.ContainsKey("name") || !d.ContainsKey("latitude") || !d.ContainsKey("longitude")) continue;
                    list.Add(new GeoResult
                    {
                        Name = Convert.ToString(d["name"], Inv),
                        Admin = d.ContainsKey("admin1") ? Convert.ToString(d["admin1"], Inv) : null,
                        Country = d.ContainsKey("country") ? Convert.ToString(d["country"], Inv) : null,
                        Latitude = Num(d["latitude"]),
                        Longitude = Num(d["longitude"])
                    });
                }
            }
            catch { list.Clear(); }
            return list;
        }

        public static WeatherInfo Fetch(double lat, double lon, bool fahrenheit)
        {
            string body = HttpGet(ForecastUrl(lat, lon, fahrenheit));
            return body == null ? null : ParseForecast(body, DateTime.UtcNow);
        }

        public static List<GeoResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<GeoResult>();
            string url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(query.Trim()) +
                         "&count=8&language=en&format=json";
            string body = HttpGet(url);
            return body == null ? new List<GeoResult>() : ParseGeocode(body);
        }

        public static string GlyphFor(int code)
        {
            if (code == 0) return "☀";                       // clear
            if (code <= 2) return "⛅";                       // partly cloudy
            if (code == 3) return "☁";                       // overcast
            if (code == 45 || code == 48) return "≈";        // fog
            if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return "☔"; // rain
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) return "❄";   // snow
            if (code >= 95) return "⛈";                      // thunderstorm
            return "☁";
        }

        /// <summary>GET a URL as text with a short timeout; returns null on any failure.</summary>
        public static string HttpGet(string url)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;
                req.UserAgent = "DriftClock";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return rd.ReadToEnd();
            }
            catch { return null; }
        }

        private static object First(object arr)
        {
            var l = arr as IList;
            return l != null && l.Count > 0 ? l[0] : null;
        }

        private static double Num(object o)
        {
            return Convert.ToDouble(o, Inv);
        }

        private static DateTime? SunTime(object o, double offsetSeconds)
        {
            string s = o as string;
            DateTime dt;
            if (s == null || !DateTime.TryParse(s, Inv, DateTimeStyles.None, out dt)) return null;
            var utc = DateTime.SpecifyKind(dt.AddSeconds(-offsetSeconds), DateTimeKind.Utc);
            return utc.ToLocalTime();
        }
    }
}
