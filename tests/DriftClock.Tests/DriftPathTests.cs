using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;

namespace DriftClock.Tests
{
    public class DriftPathTests
    {
        [Fact]
        public void Offsets_stay_in_range()
        {
            for (long i = 0; i < 10000; i++)
            {
                var o = DriftPath.OffsetFor(i, 16);
                Assert.InRange(o.X, -16, 16);
                Assert.InRange(o.Y, -16, 16);
            }
        }

        [Fact]
        public void Consecutive_offsets_differ_by_at_least_2px()
        {
            for (long i = 0; i < 1000; i++)
            {
                var a = DriftPath.OffsetFor(i, 16);
                var b = DriftPath.OffsetFor(i + 1, 16);
                Assert.True(Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) >= 2, "step " + i);
            }
        }

        [Fact]
        public void Path_covers_box()
        {
            var seen = new HashSet<Point>();
            int minX = 99, maxX = -99, minY = 99, maxY = -99;
            for (long i = 0; i < 2000; i++)
            {
                var o = DriftPath.OffsetFor(i, 16);
                seen.Add(o);
                minX = Math.Min(minX, o.X); maxX = Math.Max(maxX, o.X);
                minY = Math.Min(minY, o.Y); maxY = Math.Max(maxY, o.Y);
            }
            Assert.True(seen.Count >= 200, "distinct " + seen.Count);
            Assert.True(minX <= -14 && maxX >= 14 && minY <= -14 && maxY >= 14);
        }

        [Fact]
        public void Negative_steps_and_small_ranges_are_safe()
        {
            var o = DriftPath.OffsetFor(-5, 4);
            Assert.InRange(o.X, -4, 4);
            Assert.Equal(Point.Empty, DriftPath.OffsetFor(10, 0));
        }
    }
}
