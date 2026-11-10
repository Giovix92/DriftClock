using System;
using Xunit;

namespace DriftClock.Tests
{
    public class ScreensaverArgsTests
    {
        [Theory]
        [InlineData(new string[0], SaverMode.Settings, 0)]
        [InlineData(new[] { "/c" }, SaverMode.Settings, 0)]
        [InlineData(new[] { "/C:1234" }, SaverMode.Settings, 1234)]
        [InlineData(new[] { "/s" }, SaverMode.Fullscreen, 0)]
        [InlineData(new[] { "-S" }, SaverMode.Fullscreen, 0)]
        [InlineData(new[] { "/p", "5678" }, SaverMode.Preview, 5678)]
        [InlineData(new[] { "/P:5678" }, SaverMode.Preview, 5678)]
        [InlineData(new[] { "/x" }, SaverMode.Settings, 0)]
        public void Parse_handles_variants(string[] a, SaverMode mode, long h)
        {
            var r = ScreensaverArgs.Parse(a);
            Assert.Equal(mode, r.Mode);
            Assert.Equal((IntPtr)h, r.Handle);
        }

        [Fact]
        public void Preview_without_handle_falls_back_to_settings()
        {
            Assert.Equal(SaverMode.Settings, ScreensaverArgs.Parse(new[] { "/p" }).Mode);
        }
    }
}
