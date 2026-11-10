using System.Drawing;
using Xunit;

namespace DriftClock.Tests
{
    public class ScreenLayoutTests
    {
        private static readonly Rectangle Main = new Rectangle(0, 0, 1920, 1080);
        private static readonly Rectangle Side = new Rectangle(1920, 0, 2560, 1440);

        [Fact]
        public void Identical_layouts_do_not_differ()
        {
            Assert.False(ScreenLayout.Differs(new[] { Main, Side }, new[] { Main, Side }));
        }

        [Fact]
        public void Enumeration_order_does_not_matter()
        {
            Assert.False(ScreenLayout.Differs(new[] { Main, Side }, new[] { Side, Main }));
        }

        [Fact]
        public void Resolution_change_differs()
        {
            Assert.True(ScreenLayout.Differs(new[] { Main }, new[] { new Rectangle(0, 0, 1280, 720) }));
        }

        [Fact]
        public void Monitor_added_or_removed_differs()
        {
            Assert.True(ScreenLayout.Differs(new[] { Main }, new[] { Main, Side }));
            Assert.True(ScreenLayout.Differs(new[] { Main, Side }, new[] { Main }));
        }

        [Fact]
        public void Monitor_moved_differs()
        {
            Assert.True(ScreenLayout.Differs(new[] { Main, Side }, new[] { Main, new Rectangle(-2560, 0, 2560, 1440) }));
        }

        [Fact]
        public void Null_inputs_are_treated_as_empty()
        {
            Assert.False(ScreenLayout.Differs(null, new Rectangle[0]));
            Assert.True(ScreenLayout.Differs(null, new[] { Main }));
        }
    }
}
