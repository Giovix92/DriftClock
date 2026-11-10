using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Xunit;

namespace DriftClock.Tests
{
    public class MonthHeaderTests
    {
        private const int W = 1920, H = 1080;

        /// <summary>Horizontal [min,max] of lit pixels for each vertical band of consecutive lit rows in the bottom-right area.</summary>
        private static List<int[]> Bands()
        {
            var s = new Settings { ShowWeather = false, ShowBattery = false, ShowMonthGrid = true, ShowEvents = false };
            byte[] px;
            using (var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            using (var r = new ClockRenderer(1f))
            {
                g.Clear(Color.Black);
                r.Draw(g, new Rectangle(0, 0, W, H), new DateTime(2026, 11, 10, 14, 37, 0), new ScreenState(), s, Point.Empty);
                var data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                px = new byte[data.Stride * H];
                Marshal.Copy(data.Scan0, px, 0, px.Length);
                bmp.UnlockBits(data);
            }

            var bands = new List<int[]>();
            int[] cur = null;
            for (int y = 740; y < H; y++)
            {
                int min = int.MaxValue, max = -1;
                for (int x = 1300; x < W; x++)
                {
                    int i = (y * W + x) * 4;
                    if (px[i] > 20) { if (x < min) min = x; if (x > max) max = x; }
                }
                if (max >= 0)
                {
                    if (cur == null) { cur = new[] { min, max }; bands.Add(cur); }
                    else { cur[0] = Math.Min(cur[0], min); cur[1] = Math.Max(cur[1], max); }
                }
                else cur = null;
            }
            return bands;
        }

        [Fact]
        public void Month_title_is_centered_over_the_calendar_columns()
        {
            var bands = Bands();
            Assert.True(bands.Count >= 3, "expected title, weekday row and day rows; got " + bands.Count);
            double titleCenter = (bands[0][0] + bands[0][1]) / 2.0;
            double headerCenter = (bands[1][0] + bands[1][1]) / 2.0; // weekday initials span all 7 columns
            Assert.InRange(Math.Abs(titleCenter - headerCenter), 0, 6);
        }
    }
}
