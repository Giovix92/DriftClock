using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Text;

namespace DriftClock
{
    /// <summary>Draws layout C: huge clock + date in the centre, small dim extras in the four corners.</summary>
    public sealed class ClockRenderer : IDisposable
    {
        private const int ClockGray = 200;      // ~#C8C8C8
        private const int ExtraGray = 112;      // ~#707070
        private const int HighlightGray = 48;   // ~#303030
        private const float DriftRange = 16f;

        private static readonly string ClockFamily = PickFamily("Segoe UI Variable Display Light", "Segoe UI Light", "Segoe UI");
        private const string TextFamily = "Segoe UI";
        private const string SymbolFamily = "Segoe UI Symbol";

        private readonly float _dpi;
        private readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();
        private readonly StringFormat _fmt = new StringFormat(StringFormat.GenericTypographic);
        private readonly StringFormat _center = new StringFormat(StringFormat.GenericTypographic);

        public ClockRenderer(float dpiScale)
        {
            _dpi = dpiScale <= 0 ? 1f : dpiScale;
            _fmt.FormatFlags |= StringFormatFlags.NoWrap;
            _center.FormatFlags |= StringFormatFlags.NoWrap;
            _center.Alignment = StringAlignment.Center;
            _center.LineAlignment = StringAlignment.Center;
        }

        public void Dispose()
        {
            foreach (Font f in _fonts.Values) f.Dispose();
            _fonts.Clear();
            _fmt.Dispose();
            _center.Dispose();
        }

        /// <summary>Changes only when the drawn output would change (minute, offset, night, extras content).</summary>
        public string RenderKey(DateTime now, ScreenState st, Point offset)
        {
            var sb = new StringBuilder();
            sb.Append(now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
            sb.Append('|').Append(offset.X).Append(',').Append(offset.Y);
            if (st == null) return sb.ToString();
            sb.Append('|').Append(st.IsNight ? 'N' : 'D');
            if (st.Weather != null)
                sb.Append("|W").Append(Math.Round(st.Weather.CurrentTemp)).Append(',').Append(st.Weather.WeatherCode)
                  .Append(',').Append(Math.Round(st.Weather.High)).Append(',').Append(Math.Round(st.Weather.Low));
            if (st.BatteryPercent.HasValue) sb.Append("|B").Append(st.BatteryPercent.Value).Append(st.Charging ? "+" : "-");
            if (st.Events != null)
                foreach (EventLine e in st.Events) sb.Append("|E").Append(e.Text);
            return sb.ToString();
        }

        public void Draw(Graphics g, Rectangle b, DateTime now, ScreenState st, Settings s, Point offset)
        {
            if (st == null) st = new ScreenState();
            CultureInfo culture = CultureInfo.CurrentCulture;
            float f = st.IsNight ? Math.Max(10, Math.Min(100, s.NightBrightnessPercent)) / 100f : 1f;

            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            GraphicsState saved = g.Save();
            try
            {
                g.TranslateTransform(offset.X, offset.Y);
                float margin = Math.Max(b.Width * 0.03f, 2f * DriftRange * _dpi);
                float ep = Math.Max(6f, b.Height * 0.024f); // extras font size in px

                DrawClock(g, b, now, s, culture, Gray(ClockGray, f), Gray(ExtraGray, f), ep);

                Color extra = Gray(ExtraGray, f);
                if (s.ShowWeather && st.Weather != null) DrawWeather(g, b, margin, ep, st.Weather, s, extra);
                if (s.ShowBattery && st.BatteryPercent.HasValue) DrawBattery(g, b, margin, ep, st, extra);
                if (st.Events != null && st.Events.Count > 0) DrawEvents(g, b, margin, ep, st.Events, extra);
                if (s.ShowMonthGrid) DrawMonth(g, b, margin, ep, now, culture, f, extra);
            }
            finally { g.Restore(saved); }
        }

        // ---- centre ------------------------------------------------------------------------------------------

        private void DrawClock(Graphics g, Rectangle b, DateTime now, Settings s, CultureInfo culture,
                               Color clockColor, Color dimColor, float ep)
        {
            string time = now.ToString(s.Use12Hour ? "h\\:mm" : "HH\\:mm", CultureInfo.InvariantCulture);
            string suffix = null;
            if (s.Use12Hour)
            {
                suffix = now.Hour < 12 ? culture.DateTimeFormat.AMDesignator : culture.DateTimeFormat.PMDesignator;
                if (string.IsNullOrEmpty(suffix)) suffix = now.Hour < 12 ? "AM" : "PM";
            }

            float px = b.Height * 0.40f;
            Font big = GetFont(ClockFamily, px);
            SizeF ts = Measure(g, time, big);
            float sufPx = px * 0.16f;
            float sufW = suffix == null ? 0 : Measure(g, suffix, GetFont(TextFamily, sufPx)).Width + sufPx * 0.4f;
            while (ts.Width + sufW > b.Width * 0.92f && px > 8)
            {
                px *= 0.92f;
                big = GetFont(ClockFamily, px);
                ts = Measure(g, time, big);
                sufPx = px * 0.16f;
                sufW = suffix == null ? 0 : Measure(g, suffix, GetFont(TextFamily, sufPx)).Width + sufPx * 0.4f;
            }

            Font dateFont = GetFont(TextFamily, Math.Max(ep, b.Height * 0.04f));
            string date = now.ToString("dddd, d MMMM", culture);
            SizeF ds = Measure(g, date, dateFont);

            float gap = ts.Height * -0.06f;
            float totalH = ts.Height + gap + ds.Height;
            float top = b.Top + (b.Height - totalH) / 2f;
            float left = b.Left + (b.Width - (ts.Width + sufW)) / 2f;

            using (var cb = new SolidBrush(clockColor))
            using (var db = new SolidBrush(dimColor))
            {
                g.DrawString(time, big, cb, left, top, _fmt);
                if (suffix != null)
                {
                    Font sf = GetFont(TextFamily, sufPx);
                    float sy = top + ts.Height * 0.62f;
                    g.DrawString(suffix, sf, db, left + ts.Width + sufPx * 0.4f, sy, _fmt);
                }
                g.DrawString(date, dateFont, cb, b.Left + (b.Width - ds.Width) / 2f, top + ts.Height + gap, _fmt);
            }
        }

        // ---- corners -----------------------------------------------------------------------------------------

        private void DrawWeather(Graphics g, Rectangle b, float margin, float ep, WeatherInfo w, Settings s, Color c)
        {
            Font text = GetFont(TextFamily, ep);
            Font sym = GetFont(SymbolFamily, ep * 1.25f);
            string glyph = Weather.GlyphFor(w.WeatherCode);
            string body = string.Format(CultureInfo.InvariantCulture, "{0}°   ↑{1}° ↓{2}°",
                Whole(w.CurrentTemp), Whole(w.High), Whole(w.Low));
            if (!string.IsNullOrWhiteSpace(s.LocationName)) body += "   " + s.LocationName;

            float x = b.Left + margin, y = b.Top + margin;
            using (var br = new SolidBrush(c))
            {
                g.DrawString(glyph, sym, br, x, y - ep * 0.1f, _fmt);
                float gw = Measure(g, glyph, sym).Width + ep * 0.5f;
                g.DrawString(body, text, br, x + gw, y, _fmt);
            }
        }

        private void DrawBattery(Graphics g, Rectangle b, float margin, float ep, ScreenState st, Color c)
        {
            Font text = GetFont(TextFamily, ep);
            Font sym = GetFont(SymbolFamily, ep * 1.1f);
            string body = st.BatteryPercent.Value.ToString(CultureInfo.InvariantCulture) + "%";
            float tw = Measure(g, body, text).Width;
            string bolt = "⚡";
            float bw = st.Charging ? Measure(g, bolt, sym).Width + ep * 0.2f : 0f;
            float x = b.Right - margin - tw - bw, y = b.Top + margin;
            using (var br = new SolidBrush(c))
            {
                if (st.Charging) g.DrawString(bolt, sym, br, x, y, _fmt);
                g.DrawString(body, text, br, x + bw, y, _fmt);
            }
        }

        private void DrawEvents(Graphics g, Rectangle b, float margin, float ep, List<EventLine> events, Color c)
        {
            Font text = GetFont(TextFamily, ep);
            float lineH = ep * 1.5f;
            float y = b.Bottom - margin - events.Count * lineH;
            using (var br = new SolidBrush(c))
                foreach (EventLine e in events)
                {
                    g.DrawString(e.Text, text, br, b.Left + margin, y, _fmt);
                    y += lineH;
                }
        }

        private void DrawMonth(Graphics g, Rectangle b, float margin, float ep, DateTime now, CultureInfo culture, float f, Color c)
        {
            float gp = ep * 0.9f;
            Font font = GetFont(TextFamily, gp);
            float cellW = gp * 2.2f, lineH = gp * 1.55f;

            DateTime first = new DateTime(now.Year, now.Month, 1);
            DayOfWeek firstDow = culture.DateTimeFormat.FirstDayOfWeek;
            int lead = ((int)first.DayOfWeek - (int)firstDow + 7) % 7;
            int days = DateTime.DaysInMonth(now.Year, now.Month);
            int rows = (lead + days + 6) / 7;

            float gridW = cellW * 7, gridH = lineH * (rows + 2);
            float x0 = b.Right - margin - gridW, y0 = b.Bottom - margin - gridH;

            using (var br = new SolidBrush(c))
            using (var todayBg = new SolidBrush(Gray(HighlightGray, f)))
            using (var todayFg = new SolidBrush(Gray(ClockGray, f)))
            {
                g.DrawString(now.ToString("MMMM yyyy", culture).ToUpper(culture), font, br, x0, y0, _fmt);
                for (int i = 0; i < 7; i++)
                {
                    var dow = (DayOfWeek)(((int)firstDow + i) % 7);
                    string name = culture.DateTimeFormat.GetAbbreviatedDayName(dow);
                    string initial = string.IsNullOrEmpty(name) ? "" : name.Substring(0, 1).ToUpper(culture);
                    g.DrawString(initial, font, br, new RectangleF(x0 + i * cellW, y0 + lineH, cellW, lineH), _center);
                }
                for (int d = 1; d <= days; d++)
                {
                    int slot = lead + d - 1;
                    var cell = new RectangleF(x0 + (slot % 7) * cellW, y0 + (2 + slot / 7) * lineH, cellW, lineH);
                    bool today = d == now.Day;
                    if (today) FillRounded(g, todayBg, cell, lineH * 0.3f);
                    g.DrawString(d.ToString(CultureInfo.InvariantCulture), font, today ? todayFg : br, cell, _center);
                }
            }
        }

        // ---- helpers -----------------------------------------------------------------------------------------

        private static int Whole(double v)
        {
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        private static Color Gray(int v, float f)
        {
            int n = Math.Max(0, Math.Min(255, (int)(v * f + 0.5f)));
            return Color.FromArgb(n, n, n);
        }

        private SizeF Measure(Graphics g, string text, Font font)
        {
            return g.MeasureString(text, font, PointF.Empty, _fmt);
        }

        private Font GetFont(string family, float px)
        {
            string key = family + "|" + Math.Round(px, 1).ToString(CultureInfo.InvariantCulture);
            Font font;
            if (!_fonts.TryGetValue(key, out font))
                _fonts[key] = font = new Font(family, Math.Max(1f, px), FontStyle.Regular, GraphicsUnit.Pixel);
            return font;
        }

        private static void FillRounded(Graphics g, Brush brush, RectangleF r, float radius)
        {
            float d = radius * 2;
            using (var path = new GraphicsPath())
            {
                path.AddArc(r.Left, r.Top, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }

        private static string PickFamily(params string[] candidates)
        {
            try
            {
                using (var fc = new InstalledFontCollection())
                    foreach (string want in candidates)
                        foreach (FontFamily ff in fc.Families)
                            if (string.Equals(ff.Name, want, StringComparison.OrdinalIgnoreCase)) return ff.Name;
            }
            catch { }
            return candidates[candidates.Length - 1];
        }
    }
}
