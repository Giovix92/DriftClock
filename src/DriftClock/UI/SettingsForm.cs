using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DriftClock
{
    internal sealed class Win32Owner : IWin32Window
    {
        public Win32Owner(IntPtr handle) { Handle = handle; }
        public IntPtr Handle { get; private set; }
    }

    internal sealed class SettingsForm : Form
    {
        private readonly Settings _settings;
        private string _locName;
        private double? _lat, _lon;

        private readonly TextBox _search = new TextBox();
        private readonly Button _searchBtn = new Button();
        private readonly ListBox _results = new ListBox();
        private readonly Label _chosen = new Label();
        private readonly Button _clear = new Button();
        private readonly CheckBox _weather = new CheckBox(), _battery = new CheckBox(), _month = new CheckBox(),
                                  _events = new CheckBox(), _12h = new CheckBox(), _fahrenheit = new CheckBox();
        private readonly TextBox _ical = new TextBox();
        private readonly TrackBar _night = new TrackBar();
        private readonly Label _nightValue = new Label();
        private readonly DateTimePicker _nightFrom = new DateTimePicker(), _nightTo = new DateTimePicker();
        private readonly Button _ok = new Button(), _cancel = new Button();

        public SettingsForm(Settings settings)
        {
            _settings = settings;
            _locName = settings.LocationName;
            _lat = settings.Latitude;
            _lon = settings.Longitude;

            Text = "DriftClock settings";
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(424, 468);

            Add(Lbl("Location (for weather and sunset/sunrise)", 12, 10, 398));
            Place(_search, 12, 32, 290, 24);
            Place(_searchBtn, 310, 31, 100, 26);
            _searchBtn.Text = "Search";
            _searchBtn.Click += (s, e) => RunSearch();
            _search.Enter += (s, e) => AcceptButton = _searchBtn;
            _search.Leave += (s, e) => AcceptButton = _ok;
            Place(_results, 12, 64, 398, 72);
            _results.SelectedIndexChanged += (s, e) => PickResult();
            Place(_chosen, 12, 142, 330, 20);
            Place(_clear, 346, 138, 64, 24);
            _clear.Text = "Clear";
            _clear.Click += (s, e) => { _locName = null; _lat = _lon = null; ShowChosen(); };

            Check(_weather, "Show weather", 12, 172);
            Check(_battery, "Show battery (if any)", 220, 172);
            Check(_month, "Show month calendar", 12, 196);
            Check(_12h, "12-hour clock", 220, 196);
            Check(_events, "Show calendar events", 12, 220);
            Check(_fahrenheit, "Fahrenheit (°F)", 220, 220);
            _events.CheckedChanged += (s, e) => _ical.Enabled = _events.Checked;

            Add(Lbl("Calendar link (private iCal / webcal address)", 12, 252, 398));
            Place(_ical, 12, 272, 398, 24);

            Add(Lbl("Brightness at night", 12, 306, 200));
            _night.Minimum = 10;
            _night.Maximum = 100;
            _night.TickFrequency = 10;
            Place(_night, 12, 326, 340, 40);
            Place(_nightValue, 360, 332, 50, 20);
            _night.Scroll += (s, e) => _nightValue.Text = _night.Value + "%";

            Add(Lbl("Night hours when sunrise/sunset isn't available:", 12, 376, 398));
            foreach (var p in new[] { _nightFrom, _nightTo })
            {
                p.Format = DateTimePickerFormat.Custom;
                p.CustomFormat = "HH:mm";
                p.ShowUpDown = true;
            }
            Place(_nightFrom, 12, 398, 70, 24);
            Add(Lbl("to", 92, 402, 28));
            Place(_nightTo, 124, 398, 70, 24);

            Place(_ok, 254, 430, 75, 28);
            _ok.Text = "OK";
            _ok.Click += (s, e) => Save();
            Place(_cancel, 335, 430, 75, 28);
            _cancel.Text = "Cancel";
            _cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = _ok;
            CancelButton = _cancel;

            LoadValues();
        }

        private void LoadValues()
        {
            _weather.Checked = _settings.ShowWeather;
            _battery.Checked = _settings.ShowBattery;
            _month.Checked = _settings.ShowMonthGrid;
            _events.Checked = _settings.ShowEvents;
            _12h.Checked = _settings.Use12Hour;
            _fahrenheit.Checked = _settings.UseFahrenheit;
            _ical.Text = _settings.ICalUrl ?? "";
            _ical.Enabled = _events.Checked;
            _night.Value = Math.Max(10, Math.Min(100, _settings.NightBrightnessPercent));
            _nightValue.Text = _night.Value + "%";
            _nightFrom.Value = DateTime.Today + _settings.ManualNightStart;
            _nightTo.Value = DateTime.Today + _settings.ManualNightEnd;
            ShowChosen();
        }

        private void ShowChosen()
        {
            _chosen.Text = string.IsNullOrEmpty(_locName) || !_lat.HasValue
                ? "No location selected"
                : "Selected: " + _locName;
        }

        private void RunSearch()
        {
            string q = _search.Text;
            if (string.IsNullOrWhiteSpace(q)) return;
            _searchBtn.Enabled = false;
            _results.Items.Clear();
            _chosen.Text = "Searching…";
            Task.Run(() =>
            {
                List<GeoResult> found = Weather.Search(q);
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)(() =>
                {
                    _searchBtn.Enabled = true;
                    foreach (GeoResult g in found) _results.Items.Add(g);
                    if (found.Count == 0) _chosen.Text = "No results (or you are offline)";
                    else ShowChosen();
                }));
            });
        }

        private void PickResult()
        {
            var g = _results.SelectedItem as GeoResult;
            if (g == null) return;
            _locName = g.Name;
            _lat = g.Latitude;
            _lon = g.Longitude;
            ShowChosen();
        }

        private void Save()
        {
            string url = _ical.Text.Trim();
            if (_events.Checked && url.Length > 0 &&
                !(url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                  url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                  url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "The calendar link should start with https:// or webcal://", "DriftClock",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _settings.LocationName = _locName;
            _settings.Latitude = _lat;
            _settings.Longitude = _lon;
            _settings.ShowWeather = _weather.Checked;
            _settings.ShowBattery = _battery.Checked;
            _settings.ShowMonthGrid = _month.Checked;
            _settings.ShowEvents = _events.Checked;
            _settings.Use12Hour = _12h.Checked;
            _settings.UseFahrenheit = _fahrenheit.Checked;
            _settings.ICalUrl = url.Length == 0 ? null : url;
            _settings.NightBrightnessPercent = _night.Value;
            _settings.ManualNightStart = _nightFrom.Value.TimeOfDay;
            _settings.ManualNightEnd = _nightTo.Value.TimeOfDay;

            try
            {
                _settings.Save(Settings.DefaultPath);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save settings: " + ex.Message, "DriftClock",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ---- layout helpers ----------------------------------------------------------------------------------

        private void Add(Control c) { Controls.Add(c); }

        private void Place(Control c, int x, int y, int w, int h)
        {
            c.SetBounds(x, y, w, h);
            Controls.Add(c);
        }

        private static Label Lbl(string text, int x, int y, int w)
        {
            return new Label { Text = text, Left = x, Top = y, Width = w, Height = 20, AutoSize = false };
        }

        private void Check(CheckBox c, string text, int x, int y)
        {
            c.Text = text;
            Place(c, x, y, 190, 22);
        }
    }
}
