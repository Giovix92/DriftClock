using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DriftClock
{
    /// <summary>Compares monitor layouts so the saver can close when displays are added, removed, moved or resized.</summary>
    public static class ScreenLayout
    {
        public static bool Differs(IEnumerable<Rectangle> before, IEnumerable<Rectangle> after)
        {
            var a = Sort(before);
            var b = Sort(after);
            return !a.SequenceEqual(b);
        }

        private static List<Rectangle> Sort(IEnumerable<Rectangle> rects)
        {
            return (rects ?? Enumerable.Empty<Rectangle>())
                .OrderBy(r => r.X).ThenBy(r => r.Y).ThenBy(r => r.Width).ThenBy(r => r.Height)
                .ToList();
        }
    }
}
