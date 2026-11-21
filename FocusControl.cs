// Webcam Focus Control Utility - manual focus for UVC webcams via DirectShow IAMCameraControl.
// Build: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:FocusControl.exe FocusControl.cs
// CLI:   FocusControl.exe --list            (print cameras and focus ranges)
//        FocusControl.exe --set <index> <value|auto>
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;

namespace WebcamFocus
{
    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ICreateDevEnum
    {
        [PreserveSig] int CreateClassEnumerator([In] ref Guid clsidDeviceClass, out IEnumMoniker ppEnumMoniker, int dwFlags);
    }

    [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyBag
    {
        [PreserveSig] int Read([MarshalAs(UnmanagedType.LPWStr)] string name, out object val, IntPtr errorLog);
        [PreserveSig] int Write([MarshalAs(UnmanagedType.LPWStr)] string name, ref object val);
    }

    [ComImport, Guid("C6E13370-30AC-11d0-A18C-00A0C9118956"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAMCameraControl
    {
        [PreserveSig] int GetRange(int prop, out int min, out int max, out int step, out int def, out int caps);
        [PreserveSig] int Set(int prop, int value, int flags);
        [PreserveSig] int Get(int prop, out int value, out int flags);
    }

    class Camera
    {
        public const int PropFocus = 6;
        public const int FlagAuto = 1, FlagManual = 2;

        public string Name;
        public IAMCameraControl Control;
        public bool HasFocus;
        public int Min, Max, Step, Default, Caps;

        public bool SupportsAuto { get { return (Caps & FlagAuto) != 0; } }
        public override string ToString() { return Name + (HasFocus ? "" : "  (no focus control)"); }

        public bool Read(out int value, out bool auto)
        {
            int flags;
            value = 0; auto = false;
            if (Control == null || Control.Get(PropFocus, out value, out flags) != 0) return false;
            auto = (flags & FlagAuto) != 0;
            return true;
        }

        public int SetManual(int value) { return Control.Set(PropFocus, value, FlagManual); }
        public int SetAuto() { return Control.Set(PropFocus, Default, FlagAuto); }

        static readonly Guid VideoInputCategory = new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
        static readonly Guid SystemDeviceEnum = new Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
        static readonly Guid IID_IBaseFilter = new Guid("56a86895-0ad4-11ce-b03a-0020af0ba770");

        public static List<Camera> Enumerate()
        {
            var list = new List<Camera>();
            var devEnum = (ICreateDevEnum)Activator.CreateInstance(Type.GetTypeFromCLSID(SystemDeviceEnum));
            IEnumMoniker em;
            Guid cat = VideoInputCategory;
            if (devEnum.CreateClassEnumerator(ref cat, out em, 0) != 0 || em == null) return list;

            var arr = new IMoniker[1];
            while (em.Next(1, arr, IntPtr.Zero) == 0)
            {
                var m = arr[0];
                var cam = new Camera { Name = "Camera " + (list.Count + 1) };
                try
                {
                    object bagObj; Guid bagId = typeof(IPropertyBag).GUID;
                    m.BindToStorage(null, null, ref bagId, out bagObj);
                    object name;
                    if (((IPropertyBag)bagObj).Read("FriendlyName", out name, IntPtr.Zero) == 0) cam.Name = (string)name;
                }
                catch { }
                try
                {
                    object filter; Guid fid = IID_IBaseFilter;
                    m.BindToObject(null, null, ref fid, out filter);
                    cam.Control = filter as IAMCameraControl;
                    if (cam.Control != null)
                        cam.HasFocus = cam.Control.GetRange(PropFocus, out cam.Min, out cam.Max, out cam.Step, out cam.Default, out cam.Caps) == 0
                                       && cam.Max > cam.Min;
                }
                catch { }
                if (cam.Step <= 0) cam.Step = 1;
                list.Add(cam);
            }
            return list;
        }
    }

    class MainForm : Form
    {
        ComboBox cameraBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
        CheckBox autoBox = new CheckBox { Text = "Autofocus", AutoSize = true };
        TrackBar slider = new TrackBar { Width = 330, TickStyle = TickStyle.None };
        Label valueLabel = new Label { AutoSize = true, Font = new Font("Segoe UI", 18f, FontStyle.Bold) };
        Label rangeLabel = new Label { AutoSize = true, ForeColor = SystemColors.GrayText };
        Label status = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(340, 0) };
        Button minus = new Button { Text = "−", Width = 40 }, plus = new Button { Text = "+", Width = 40 };
        Button reset = new Button { Text = "Default", Width = 70 }, cameraApp = new Button { Text = "Open Camera app", Width = 120 };
        List<Camera> cams;
        bool loading;

        Camera Cur { get { return cameraBox.SelectedItem as Camera; } }

        public MainForm()
        {
            Text = "Webcam Focus";
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            TopMost = true;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            KeyPreview = true;

            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(12), WrapContents = false };
            var row1 = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            row1.Controls.AddRange(new Control[] { valueLabel });
            var row2 = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            row2.Controls.AddRange(new Control[] { minus, plus, reset, cameraApp });
            var topMost = new CheckBox { Text = "Keep on top", AutoSize = true, Checked = true };
            topMost.CheckedChanged += (s, e) => TopMost = topMost.Checked;
            var row3 = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            row3.Controls.AddRange(new Control[] { autoBox, topMost });

            p.Controls.AddRange(new Control[] { cameraBox, row3, row1, slider, rangeLabel, row2, status });
            Controls.Add(p);

            cameraBox.SelectedIndexChanged += (s, e) => LoadCamera();
            autoBox.CheckedChanged += (s, e) => { if (!loading) ApplyAuto(); };
            slider.ValueChanged += (s, e) => { if (!loading) ApplyManual(slider.Value); };
            minus.Click += (s, e) => Nudge(-1);
            plus.Click += (s, e) => Nudge(+1);
            reset.Click += (s, e) => { if (Cur != null && Cur.HasFocus) SetSlider(Cur.Default, true); };
            cameraApp.Click += (s, e) => { try { System.Diagnostics.Process.Start("microsoft.windows.camera:"); } catch { } };
            KeyDown += (s, e) =>
            {
                if (ActiveControl == cameraBox) return;
                int mult = e.Shift ? 10 : 1;
                if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) { Nudge(-mult); e.Handled = true; }
                if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) { Nudge(+mult); e.Handled = true; }
            };

            cams = Camera.Enumerate();
            foreach (var c in cams) cameraBox.Items.Add(c);
            if (cams.Count == 0) { status.Text = "No cameras found."; EnableControls(false); return; }
            int pick = cams.FindIndex(c => c.HasFocus);
            cameraBox.SelectedIndex = pick >= 0 ? pick : 0;

            var poll = new Timer { Interval = 250 };
            poll.Tick += (s, e) => PollFocus();
            poll.Start();
        }

        void EnableControls(bool on)
        {
            slider.Enabled = minus.Enabled = plus.Enabled = reset.Enabled = on && !autoBox.Checked;
            autoBox.Enabled = on && Cur != null && Cur.SupportsAuto;
        }

        void LoadCamera()
        {
            var c = Cur;
            loading = true;
            try
            {
                if (c == null || !c.HasFocus)
                {
                    valueLabel.Text = "—"; rangeLabel.Text = "";
                    status.Text = "This camera doesn't expose a focus control (fixed-focus lens or driver doesn't support it).";
                    autoBox.Checked = false; EnableControls(false); return;
                }
                slider.Minimum = c.Min; slider.Maximum = c.Max;
                slider.SmallChange = c.Step; slider.LargeChange = Math.Max(c.Step, (c.Max - c.Min) / 20);
                int v; bool auto;
                if (!c.Read(out v, out auto)) v = c.Default;
                slider.Value = Math.Max(c.Min, Math.Min(c.Max, v));
                autoBox.Checked = auto;
                ShowValue(v, auto);
                rangeLabel.Text = string.Format("Range {0}–{1}, step {2}, default {3}.  Arrow keys nudge (Shift = ×10).", c.Min, c.Max, c.Step, c.Default);
                status.Text = auto ? "Autofocus is on — the level above follows it live. Untick to lock it there." : "Manual focus.";
                EnableControls(true);
            }
            finally { loading = false; }
        }

        void ShowValue(int v, bool auto)
        {
            valueLabel.Text = v + (auto ? "  (auto)" : "");
        }

        // While autofocus is on, poll the camera so the level and slider track where autofocus has focused.
        void PollFocus()
        {
            var c = Cur;
            if (c == null || !c.HasFocus || !autoBox.Checked) return;
            int v; bool auto;
            if (!c.Read(out v, out auto)) return;
            loading = true;
            try
            {
                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, v));
                ShowValue(v, true);
            }
            finally { loading = false; }
        }

        void ApplyAuto()
        {
            var c = Cur; if (c == null) return;
            if (autoBox.Checked)
            {
                int hr = c.SetAuto();
                status.Text = hr == 0 ? "Autofocus on — the level above follows it live." : string.Format("Camera refused the change (0x{0:X8}).", hr);
                EnableControls(true);
                PollFocus();
                return;
            }
            // Turning autofocus off: lock focus where autofocus currently has it, so the picture doesn't jump.
            int v; bool auto;
            if (c.Read(out v, out auto)) SetSliderSilently(v);
            EnableControls(true);
            int hr2 = c.SetManual(slider.Value);
            ShowValue(slider.Value, false);
            status.Text = hr2 == 0 ? string.Format("Manual focus, locked at {0} (where autofocus was). Adjust from here.", slider.Value)
                                   : string.Format("Camera refused the change (0x{0:X8}).", hr2);
        }

        void SetSliderSilently(int v)
        {
            loading = true;
            try { slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, v)); }
            finally { loading = false; }
        }

        void ApplyManual(int v)
        {
            var c = Cur; if (c == null || autoBox.Checked) return;
            int hr = c.SetManual(v);
            ShowValue(v, false);
            status.Text = hr == 0 ? "Manual focus." : string.Format("Camera refused the change (0x{0:X8}).", hr);
        }

        void Nudge(int steps)
        {
            var c = Cur; if (c == null || !c.HasFocus || autoBox.Checked) return;
            SetSlider(slider.Value + steps * c.Step, false);
        }

        void SetSlider(int v, bool force)
        {
            v = Math.Max(slider.Minimum, Math.Min(slider.Maximum, v));
            if (slider.Value == v) { if (force) ApplyManual(v); } else slider.Value = v;
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "--list" || args[0] == "--set" || args[0] == "--probe"))
            {
                AttachConsole(-1);
                var cams = Camera.Enumerate();
                if (args[0] == "--probe")
                {
                    string[] names = { "Pan", "Tilt", "Roll", "Zoom", "Exposure", "Iris", "Focus" };
                    foreach (var c in cams)
                    {
                        Console.WriteLine(c.Name);
                        if (c.Control == null) continue;
                        for (int p = 0; p < names.Length; p++)
                        {
                            int mn, mx, st, df, cp, v, fl;
                            int rhr = c.Control.GetRange(p, out mn, out mx, out st, out df, out cp);
                            int hr2 = c.Control.Get(p, out v, out fl);
                            Console.WriteLine("  {0,-8} hr={1:X8} range={2}..{3} step={4} def={5} caps={6} cur={7} flags={8}", names[p], rhr, mn, mx, st, df, cp, hr2 == 0 ? v.ToString() : "n/a", fl);
                        }
                    }
                    return 0;
                }
                if (args[0] == "--list")
                {
                    for (int i = 0; i < cams.Count; i++)
                    {
                        var c = cams[i]; int v; bool a;
                        string cur = c.Read(out v, out a) ? (v + (a ? " (auto)" : " (manual)")) : "n/a";
                        Console.WriteLine("[{0}] {1}: focus={2} range={3}..{4} step={5} default={6} caps={7} current={8}",
                            i, c.Name, c.HasFocus, c.Min, c.Max, c.Step, c.Default, c.Caps, cur);
                    }
                    return 0;
                }
                if (args.Length < 3) { Console.WriteLine("usage: --set <index> <value|auto>"); return 2; }
                var cam = cams[int.Parse(args[1])];
                int hr = args[2] == "auto" ? cam.SetAuto() : cam.SetManual(int.Parse(args[2]));
                Console.WriteLine("hr=0x{0:X8}", hr);
                return hr == 0 ? 0 : 1;
            }
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
            return 0;
        }

        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
    }
}
