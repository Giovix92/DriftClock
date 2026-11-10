using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DriftClock
{
    /// <summary>
    /// Live thumbnail drawn inside the Windows "Screen Saver Settings" dialog (/p mode). A plain Control is used
    /// (not a Form) because it can be created directly as a child of a window owned by another process.
    /// </summary>
    internal sealed class PreviewWindow : Control
    {
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_CLIPSIBLINGS = 0x04000000;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);

        private readonly IntPtr _parent;
        private readonly DataService _data;
        private readonly Settings _settings;
        private readonly ClockRenderer _renderer = new ClockRenderer(0.15f);
        private readonly Timer _timer = new Timer { Interval = 1000 };

        public PreviewWindow(IntPtr parent, DataService data, Settings settings)
        {
            _parent = parent;
            _data = data;
            _settings = settings;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Black;
            _timer.Tick += (s, a) =>
            {
                if (!IsWindow(_parent)) { Application.Exit(); return; }
                Fit();
                Invalidate();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style = WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS;
                cp.ExStyle = 0;
                cp.Parent = _parent;
                RECT r;
                if (GetClientRect(_parent, out r))
                {
                    cp.X = 0;
                    cp.Y = 0;
                    cp.Width = Math.Max(1, r.Right - r.Left);
                    cp.Height = Math.Max(1, r.Bottom - r.Top);
                }
                return cp;
            }
        }

        /// <summary>Creates the child window and starts the refresh timer.</summary>
        public void Attach()
        {
            CreateControl();
            if (!IsHandleCreated) { var h = Handle; }
            Visible = true;
            Fit();
            _timer.Start();
        }

        private void Fit()
        {
            RECT r;
            if (GetClientRect(_parent, out r))
                Bounds = new Rectangle(0, 0, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);
            _renderer.Draw(e.Graphics, ClientRectangle, DateTime.Now, _data.Current, _settings, Point.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _renderer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
