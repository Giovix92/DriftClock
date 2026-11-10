using System;
using System.Collections.Generic;
using System.Drawing;

namespace DriftClock
{
    /// <summary>Deterministic pixel-shift path: a Lissajous walk where consecutive points differ by at least 2 px.</summary>
    public static class DriftPath
    {
        private const int Length = 2000;
        private static readonly Dictionary<int, Point[]> Cache = new Dictionary<int, Point[]>();

        public static Point OffsetFor(long step, int range)
        {
            if (range <= 0) return Point.Empty;
            Point[] path;
            lock (Cache)
            {
                if (!Cache.TryGetValue(range, out path)) Cache[range] = path = Build(range);
            }
            long idx = step % Length;
            if (idx < 0) idx += Length;
            return path[idx];
        }

        private static Point[] Build(int range)
        {
            var pts = new Point[Length];
            for (int k = 0; k < Length; k++)
            {
                int x = (int)Math.Round(range * Math.Sin(2 * Math.PI * k / 37.0));
                int y = (int)Math.Round(range * Math.Sin(2 * Math.PI * k / 53.0 + Math.PI / 3));
                if (k > 0)
                {
                    Point p = pts[k - 1];
                    if (Math.Abs(x - p.X) + Math.Abs(y - p.Y) < 2)
                        x = p.X + 2 <= range ? p.X + 2 : p.X - 2;
                }
                pts[k] = new Point(x, y);
            }
            return pts;
        }
    }
}
