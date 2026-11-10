using System.Drawing;
using Xunit;

namespace DriftClock.Tests
{
    public class ExitDetectorTests
    {
        [Fact]
        public void First_move_never_exits()
        {
            var d = new ExitDetector();
            Assert.False(d.OnMouseMove(new Point(500, 400)));
        }

        [Fact]
        public void Small_jitter_is_ignored()
        {
            var d = new ExitDetector();
            d.OnMouseMove(new Point(500, 400));
            Assert.False(d.OnMouseMove(new Point(505, 403)));
            Assert.False(d.OnMouseMove(new Point(508, 400)));
        }

        [Fact]
        public void Move_beyond_threshold_exits()
        {
            var d = new ExitDetector();
            d.OnMouseMove(new Point(500, 400));
            Assert.True(d.OnMouseMove(new Point(509, 400)));
        }

        [Fact]
        public void Diagonal_distance_counts()
        {
            var d = new ExitDetector();
            d.OnMouseMove(new Point(0, 0));
            Assert.True(d.OnMouseMove(new Point(7, 7))); // ~9.9 px
        }

        [Fact]
        public void Key_or_click_exits()
        {
            Assert.True(new ExitDetector().OnKeyOrClick());
        }
    }
}
