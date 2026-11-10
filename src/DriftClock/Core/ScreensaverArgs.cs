using System;

namespace DriftClock
{
    public enum SaverMode { Settings, Fullscreen, Preview }

    public sealed class ScreensaverArgs
    {
        public SaverMode Mode { get; private set; }
        public IntPtr Handle { get; private set; }

        public static ScreensaverArgs Parse(string[] args)
        {
            var r = new ScreensaverArgs { Mode = SaverMode.Settings, Handle = IntPtr.Zero };
            if (args == null || args.Length == 0) return r;

            string first = args[0].Trim();
            if (first.Length < 2 || (first[0] != '/' && first[0] != '-')) return r;

            char flag = char.ToLowerInvariant(first[1]);
            string inline = first.Length > 3 && first[2] == ':' ? first.Substring(3) : null;
            string handleText = inline ?? (args.Length > 1 ? args[1] : null);

            long h;
            bool hasHandle = handleText != null && long.TryParse(handleText.Trim(), out h) && h != 0;
            if (hasHandle) r.Handle = (IntPtr)long.Parse(handleText.Trim());

            switch (flag)
            {
                case 's': r.Mode = SaverMode.Fullscreen; break;
                case 'p': r.Mode = hasHandle ? SaverMode.Preview : SaverMode.Settings; break;
                default: r.Mode = SaverMode.Settings; break;
            }
            if (r.Mode == SaverMode.Fullscreen) r.Handle = IntPtr.Zero;
            return r;
        }
    }
}
