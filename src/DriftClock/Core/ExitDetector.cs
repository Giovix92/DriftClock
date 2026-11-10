using System;
using System.Drawing;

namespace DriftClock
{
    /// <summary>Decides when input should close the saver, ignoring the mouse jitter Windows sends at startup.</summary>
    public sealed class ExitDetector
    {
        private readonly int _threshold;
        private Point? _origin;

        public ExitDetector(int thresholdPx = 8)
        {
            _threshold = thresholdPx;
        }

        public bool OnMouseMove(Point screenPos)
        {
            if (!_origin.HasValue)
            {
                _origin = screenPos;
                return false;
            }
            double dx = screenPos.X - _origin.Value.X, dy = screenPos.Y - _origin.Value.Y;
            return Math.Sqrt(dx * dx + dy * dy) > _threshold;
        }

        public bool OnKeyOrClick()
        {
            return true;
        }
    }
}
