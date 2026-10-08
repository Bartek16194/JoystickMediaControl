using System.Runtime.InteropServices;
using System.Diagnostics;

namespace JoystickMediaControl;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (args.Contains("--self-test")) { SelfTest.Run(); return; }
        if (args.Contains("--teamspeak-ui-test")) {
            ApplicationConfiguration.Initialize();
            using var form = new TeamSpeakForm(TeamSpeak.Defaults());
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            string folder = Environment.GetEnvironmentVariable("JOYSTICK_MEDIA_TEST_DIR") ?? Path.GetTempPath();
            bitmap.Save(Path.Combine(folder, "teamspeak-preview.png")); return;
        }
        if (args.Contains("--ui-test")) {
            ApplicationConfiguration.Initialize();
            using var form = new MainForm();
            form.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            string folder = Environment.GetEnvironmentVariable("JOYSTICK_MEDIA_TEST_DIR") ?? Path.GetTempPath();
            bitmap.Save(Path.Combine(folder, "ui-preview.png"));
            return;
        }
        using var mutex = new Mutex(true, "Local\\OrionMedia.SingleInstance", out bool first);
        if (!first) { MessageBox.Show("JoystickMediaControl is already running. Open it from the system tray."); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
internal sealed class MainForm : Form
{
    Settings settings = new();
    Dictionary<IntPtr, Controller> devices = new();
    readonly Dictionary<(string, ButtonId), long> lastFired = new();
    readonly HotkeyRouter hotkeys = new(new WindowsKeyOutput());
    readonly ButtonId testPttButton = new(0, 0, 0);
    long testPttUntil;
    readonly ComboBox deviceBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 490 };
    readonly ComboBox actionBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, DropDownWidth = 330 };
    readonly ComboBox buttonBox = new() { Name = "buttonBox", AccessibleName = "Controller button", DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, DropDownHeight = 320 };
    readonly Button apply = Btn("Apply binding", (_, _) => { });
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.FromArgb(24, 31, 44), BorderStyle = BorderStyle.None };
    readonly Label status = new() { AutoSize = true, ForeColor = Color.FromArgb(89, 222, 178), MaximumSize = new Size(850, 0) };
    readonly Label monitor = new() { AutoSize = true, ForeColor = Color.Silver };
    readonly CheckBox enabled = new() { Text = "Enable bindings", AutoSize = true };
    readonly CheckBox hidden = new() { Text = "Start minimized", AutoSize = true };
    readonly CheckBox startup = new() { Text = "Start with Windows", AutoSize = true };
    readonly NumericUpDown debounce = new() { Minimum = 0, Maximum = 2000, Increment = 20, Width = 80 };
    readonly Button learn = new() { Text = "Detect button", AutoSize = true };
    readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Text = "JoystickMediaControl", Visible = true };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 250 };
    string? learningDevice, learningAction;
    long learningDeadline;
    bool closing, ready, fillingGrid;
    static readonly Color Background = Color.FromArgb(16, 22, 33);
    public MainForm()
    {
        Text = "JoystickMediaControl"; Size = new Size(1020, 740); MinimumSize = new Size(940, 660);
        StartPosition = FormStartPosition.CenterScreen; BackColor = Background; ForeColor = Color.FromArgb(228, 234, 245); Font = new Font("Segoe UI", 10);
        string? loadError = null;
        try {
            if (File.Exists(Settings.FilePath)) settings = Settings.Read(Settings.FilePath);
            else if (File.Exists(Settings.LegacyFilePath)) settings = Settings.Read(Settings.LegacyFilePath);
        } catch (Exception ex) { loadError = "Could not load settings: " + ex.Message; }
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), ColumnCount = 1, RowCount = 9 };
        for (int i = 0; i < 9; i++) layout.RowStyles.Add(new RowStyle(i == 5 ? SizeType.Percent : SizeType.AutoSize, i == 5 ? 100 : 0));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "JoystickMediaControl", Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 18) }, 0, 0);
        layout.Controls.Add(new Label { Text = "Controller", AutoSize = true, ForeColor = Color.Silver, Margin = new Padding(0) }, 0, 1);
        deviceBox.AccessibleName = "Controller"; actionBox.AccessibleName = "Media action";
        var deviceRow = Row(); deviceRow.Controls.Add(deviceBox); deviceRow.Controls.Add(Btn("Refresh", (_, _) => RefreshDevices())); layout.Controls.Add(deviceRow, 0, 2);
        actionBox.Items.AddRange(Media.Actions); actionBox.SelectedIndex = 0;
        actionBox.SelectionChangeCommitted += (_, _) => {
            if (ready && !configuring && actionBox.SelectedItem is string action && !settings.IsTeamSpeakReady(action)) EnsureTeamSpeak(action);
        };
        foreach (var combo in new[] { deviceBox, actionBox, buttonBox }) {
            combo.BackColor = Color.FromArgb(32, 42, 58); combo.ForeColor = Color.White;
            combo.DrawMode = DrawMode.OwnerDrawFixed; combo.ItemHeight = 24;
            combo.DrawItem += (_, e) => {
                e.DrawBackground(); if (e.Index < 0) return;
                TextRenderer.DrawText(e.Graphics, combo.Items[e.Index].ToString(), e.Font, e.Bounds, e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
        }
        learn.FlatStyle = FlatStyle.Flat; learn.BackColor = Color.FromArgb(29, 115, 89); learn.ForeColor = Color.White; learn.Padding = new Padding(8, 4, 8, 4);
        var bindRow = Row(); bindRow.Controls.Add(new Label { Text = "Action", AutoSize = true, Padding = new Padding(0, 6, 10, 0) }); bindRow.Controls.Add(actionBox); bindRow.Controls.Add(learn); bindRow.Controls.Add(Btn("Cancel", (_, _) => CancelLearn())); bindRow.Controls.Add(Btn("Test action", (_, _) => TestAction())); layout.Controls.Add(bindRow, 0, 3);
        var manualRow = Row(); manualRow.Controls.Add(new Label { Text = "Button", AutoSize = true, Padding = new Padding(0, 6, 8, 0) }); manualRow.Controls.Add(buttonBox); manualRow.Controls.Add(apply);
        var inputArea = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty }; inputArea.Controls.Add(manualRow, 0, 0);
        monitor.Text = "Last input: none"; monitor.Margin = new Padding(0, 5, 0, 12); inputArea.Controls.Add(monitor, 0, 1); layout.Controls.Add(inputArea, 0, 4);
        apply.Click += (_, _) => {
            if (deviceBox.SelectedItem is not Controller d || buttonBox.SelectedItem is not ButtonId b) { SetStatus("Select a controller and button first."); return; }
            CancelLearn(); Assign(d, b, (string)actionBox.SelectedItem!);
        };
        grid.Columns.Add("device", "Controller"); grid.Columns.Add("button", "HID button"); grid.Columns.Add("action", "Action"); grid.Columns.Add("connected", "Connection");
        grid.Columns[0].FillWeight = 130; grid.Columns[3].FillWeight = 70;
        grid.DefaultCellStyle.BackColor = Color.FromArgb(24, 31, 44); grid.DefaultCellStyle.ForeColor = ForeColor; grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(32, 95, 82); grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 42, 58); grid.ColumnHeadersDefaultCellStyle.ForeColor = ForeColor; grid.ColumnHeadersHeight = 38; grid.RowTemplate.Height = 34;
        layout.Controls.Add(grid, 0, 5);
        var buttons = Row(); buttons.Controls.Add(Btn("Remove selected", (_, _) => RemoveSelected())); buttons.Controls.Add(Btn("Export profile", (_, _) => Export())); buttons.Controls.Add(Btn("Import profile", (_, _) => Import())); buttons.Controls.Add(Btn("TeamSpeak settings", (_, _) => ConfigureTeamSpeak())); buttons.Controls.Add(Btn("Minimize to tray", (_, _) => Hide())); layout.Controls.Add(buttons, 0, 6);
        var options = Row(); options.Controls.Add(enabled); options.Controls.Add(hidden); options.Controls.Add(startup); options.Controls.Add(new Label { Text = "Debounce (ms):", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }); options.Controls.Add(debounce); layout.Controls.Add(options, 0, 7);
        var footer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        status.Margin = new Padding(0, 12, 0, 4); footer.Controls.Add(status, 0, 0);
        var links = Row(); links.Controls.Add(Link("GitHub · Bartek16194", "https://github.com/Bartek16194")); links.Controls.Add(Link("Discord", "https://discord.gg/WbChuSCGHQ")); footer.Controls.Add(links, 0, 1); layout.Controls.Add(footer, 0, 8);
        enabled.Checked = settings.Enabled; hidden.Checked = settings.StartHidden; debounce.Value = settings.Debounce;
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) startup.Checked = key?.GetValue("JoystickMediaControl") != null || key?.GetValue("OrionMedia") != null;
        enabled.CheckedChanged += (_, _) => { hotkeys.ReleaseAll(); settings.Enabled = enabled.Checked; CancelLearn(); Save(); tray.Text = enabled.Checked ? "JoystickMediaControl • enabled" : "JoystickMediaControl • disabled"; };
        hidden.CheckedChanged += (_, _) => { settings.StartHidden = hidden.Checked; Save(); };
        debounce.ValueChanged += (_, _) => { settings.Debounce = (int)debounce.Value; Save(); };
        startup.CheckedChanged += (_, _) => {
            try { using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); if (startup.Checked) key.SetValue("JoystickMediaControl", "\"" + Environment.ProcessPath + "\""); else key.DeleteValue("JoystickMediaControl", false); key.DeleteValue("OrionMedia", false); SetStatus("Windows startup setting updated."); }
            catch (Exception ex) { MessageBox.Show("Could not change startup: " + ex.Message); }
        };
        learn.Click += (_, _) => {
            if (deviceBox.SelectedItem is not Controller d) { SetStatus("Connect a controller and click Refresh."); return; }
            if (!EnsureTeamSpeak((string)actionBox.SelectedItem!)) return;
            hotkeys.ReleaseAll();
            learningDevice = d.Path; learningAction = (string)actionBox.SelectedItem!; learningDeadline = Environment.TickCount64 + 15000;
            learn.Enabled = false; SetStatus("Release and press a controller button within 15 seconds. Bindings are paused.");
        };
        deviceBox.SelectedIndexChanged += (_, _) => { CancelLearn(); PopulateButtons(); };
        grid.SelectionChanged += (_, _) => {
            if (fillingGrid) return;
            if (grid.SelectedRows.Count == 0 || grid.SelectedRows[0].Tag is not Binding b) return;
            var d = devices.Values.FirstOrDefault(d => d.Path == b.Device);
            if (d != null) { deviceBox.SelectedItem = d; buttonBox.SelectedItem = b.Button; }
            actionBox.SelectedItem = b.Action;
        };
        var menu = new ContextMenuStrip(); menu.Items.Add("Open", null, (_, _) => ShowWindow()); menu.Items.Add("Toggle bindings", null, (_, _) => enabled.Checked = !enabled.Checked); menu.Items.Add("Exit", null, (_, _) => { closing = true; Close(); }); tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => ShowWindow();
        timer.Tick += (_, _) => {
            if (learningDevice != null && Environment.TickCount64 >= learningDeadline) CancelLearn("Timed out. Click Detect button to try again.");
            if (testPttUntil > 0 && Environment.TickCount64 >= testPttUntil) { hotkeys.Release("test", testPttButton); testPttUntil = 0; SetStatus("PTT test finished; hotkey released."); }
        }; timer.Start();
        ready = true;
        Shown += (_, _) => { RefreshDevices(); if (loadError != null) SetStatus(loadError); if (settings.StartHidden) Hide(); NotifyUnconfiguredBindings(); };
        tray.BalloonTipClicked += (_, _) => { ShowWindow(); if (pendingSetupAction != null) EnsureTeamSpeak(pendingSetupAction); };
        FormClosing += (_, e) => { if (!closing && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } else hotkeys.ReleaseAll(); };
        FormClosed += (_, _) => { hotkeys.ReleaseAll(); timer.Dispose(); tray.Dispose(); };
    }
    static FlowLayoutPanel Row() => new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 5, 0, 5), Margin = Padding.Empty };
    static Button Btn(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(32, 42, 58), ForeColor = Color.White, Padding = new Padding(8, 4, 8, 4), Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = Color.FromArgb(60, 75, 95); b.Click += click; return b;
    }
    static LinkLabel Link(string text, string url)
    {
        var label = new LinkLabel { Text = text, AutoSize = true, LinkColor = Color.FromArgb(89, 222, 178), ActiveLinkColor = Color.White, VisitedLinkColor = Color.FromArgb(89, 222, 178), Margin = new Padding(0, 0, 20, 0) };
        label.LinkClicked += (_, _) => { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show("Could not open link: " + ex.Message); } };
        return label;
    }
    void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void SetStatus(string text) => status.Text = text;
    void PopulateButtons()
    {
        buttonBox.Items.Clear();
        if (deviceBox.SelectedItem is Controller d) buttonBox.Items.AddRange(d.Buttons);
        buttonBox.Enabled = apply.Enabled = buttonBox.Items.Count > 0;
        if (buttonBox.Items.Count > 0) buttonBox.SelectedIndex = 0;
    }
    void Assign(Controller device, ButtonId button, string action)
    {
        if (!EnsureTeamSpeak(action)) return;
        hotkeys.Release(device.Path, button);
        var binding = settings.Assign(device, button, action);
        lastFired.Remove((device.Path, button)); Save(); FillGrid();
        foreach (DataGridViewRow row in grid.Rows) if (ReferenceEquals(row.Tag, binding)) { grid.ClearSelection(); row.Selected = true; break; }
        buttonBox.SelectedItem = button; SetStatus($"Saved: {button} → {action}");
    }
    void Save() { if (!ready) return; try { settings.Write(Settings.FilePath); } catch (Exception ex) { MessageBox.Show("Could not save settings: " + ex.Message); } }
    void CancelLearn(string? message = null) { bool wasLearning = learningDevice != null; learningDevice = learningAction = null; learn.Enabled = true; if (wasLearning || message != null) SetStatus(message ?? "Button detection cancelled."); }
    void RefreshDevices()
    {
        hotkeys.ReleaseAll();
        CancelLearn(); string? selected = (deviceBox.SelectedItem as Controller)?.Path;
        try {
            devices = Controller.Scan(); lastFired.Clear(); deviceBox.Items.Clear(); deviceBox.Items.AddRange(devices.Values.ToArray());
            deviceBox.SelectedItem = devices.Values.FirstOrDefault(d => d.Path == selected) ?? devices.Values.FirstOrDefault();
            FillGrid(); SetStatus(devices.Count == 0 ? "No HID controller found. Connect a controller and click Refresh." : $"Controllers found: {devices.Count}. Settings are saved automatically.");
        } catch (Exception ex) { SetStatus("Could not scan controllers: " + ex.Message); }
    }
    void FillGrid()
    {
        fillingGrid = true;
        try {
            grid.Rows.Clear();
            foreach (var b in settings.Bindings) { int i = grid.Rows.Add(b.DeviceName, b.Button, b.Action, devices.Values.Any(d => d.Path == b.Device) ? "Connected" : "Disconnected"); grid.Rows[i].Tag = b; }
        } finally { fillingGrid = false; }
    }
    void RemoveSelected() { if (grid.SelectedRows.Count == 0) return; var b = (Binding)grid.SelectedRows[0].Tag!; hotkeys.Release(b.Device, b.Button); settings.Bindings.Remove(b); Save(); FillGrid(); }
    void ConfigureTeamSpeak(string? focusAction = null)
    {
        CancelLearn(); hotkeys.ReleaseAll();
        using var dialog = new TeamSpeakForm(settings.TeamSpeakHotkeys, settings.TeamSpeakConfirmedHotkeys, focusAction);
        // Pause joystick actions while configuring keyboard hotkeys.
        configuring = true;
        try { if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Result != null) { settings.TeamSpeakHotkeys = dialog.Result; settings.TeamSpeakConfirmedHotkeys = dialog.Confirmed ?? new(); notifiedSetup = false; Save(); SetStatus("TeamSpeak setup saved. Only confirmed actions are enabled."); } }
        finally { configuring = false; }
    }
    bool EnsureTeamSpeak(string action)
    {
        if (settings.IsTeamSpeakReady(action)) return true;
        ConfigureTeamSpeak(action);
        if (settings.IsTeamSpeakReady(action)) return true;
        SetStatus("TeamSpeak setup is incomplete for this action. Open TeamSpeak settings to finish."); return false;
    }
    void NotifyTeamSpeak(string action)
    {
        pendingSetupAction = action;
        SetStatus("TeamSpeak setup needed. Click TeamSpeak settings to configure this action.");
        if (notifiedSetup) return;
        notifiedSetup = true; tray.ShowBalloonTip(6000, "TeamSpeak setup needed", "Configure the matching TS3 hotkey before using this action. Click here to open setup.", ToolTipIcon.Info);
    }
    void NotifyUnconfiguredBindings()
    {
        var binding = settings.Bindings.FirstOrDefault(b => !settings.IsTeamSpeakReady(b.Action));
        if (binding != null) NotifyTeamSpeak(binding.Action);
    }
    void TestAction()
    {
        CancelLearn(); string action = (string)actionBox.SelectedItem!;
        if (!EnsureTeamSpeak(action)) return;
        if (action == TeamSpeak.PushToTalk) {
            string key = settings.TeamSpeakHotkeys[action];
            if (hotkeys.Hold("test", testPttButton, key)) { testPttUntil = Environment.TickCount64 + 1000; SetStatus($"PTT test: holding {key} for 1 second."); }
            else SetStatus("Windows blocked the PTT hotkey.");
        } else SetStatus(SendAction(action));
    }
    string SendAction(string action)
    {
        if (!TeamSpeak.Actions.Contains(action)) return Media.Send(action);
        string key = settings.TeamSpeakHotkeys[action];
        return hotkeys.Tap(key) ? $"Sent {key}: {action}." : "Hotkey was blocked or overlaps a held PTT key.";
    }
    void Export()
    {
        using var dlg = new SaveFileDialog { Filter = "JoystickMediaControl profile (*.json)|*.json", FileName = "joystick-media-profile.json" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try { settings.Write(dlg.FileName); SetStatus("Profile exported."); } catch (Exception ex) { MessageBox.Show(ex.Message); }
    }
    void Import()
    {
        using var dlg = new OpenFileDialog { Filter = "JoystickMediaControl profile (*.json)|*.json" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try { var s = Settings.Read(dlg.FileName); hotkeys.ReleaseAll(); CancelLearn(); settings = s; enabled.Checked = s.Enabled; hidden.Checked = s.StartHidden; debounce.Value = s.Debounce; lastFired.Clear(); Save(); FillGrid(); SetStatus("Profile imported. Previous bindings replaced."); } catch (Exception ex) { MessageBox.Show("Could not import profile: " + ex.Message); }
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var regs = new ushort[] { 4, 5, 8 }.Select(u => new Native.Registration { Page = 1, Usage = u, Flags = 0x2100, Target = Handle }).ToArray();
        if (!Native.RegisterRawInputDevices(regs, (uint)regs.Length, (uint)Marshal.SizeOf<Native.Registration>())) MessageBox.Show("Could not register raw input: " + Marshal.GetLastWin32Error());
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0xFE && ready) RefreshDevices();
        if ((m.Msg == 0x218 && m.WParam.ToInt64() == 4) || m.Msg == 0x16) hotkeys.ReleaseAll();
        if (m.Msg == 0xFF && ready) {
            try { Receive(m.LParam); } catch (Exception ex) { SetStatus("HID report error: " + ex.Message); }
        }
        base.WndProc(ref m);
    }
    void Receive(IntPtr raw)
    {
        uint n = 0; uint header = (uint)(8 + IntPtr.Size * 2);
        if (Native.GetRawInputData(raw, 0x10000003, IntPtr.Zero, ref n, header) == uint.MaxValue || n < header + 8 || n > 1024 * 1024) return;
        var ptr = Marshal.AllocHGlobal((int)n);
        try {
            if (Native.GetRawInputData(raw, 0x10000003, ptr, ref n, header) == uint.MaxValue) return;
            if (Marshal.ReadInt32(ptr) != 2 || !devices.TryGetValue(Marshal.ReadIntPtr(ptr, 8), out var d)) return;
            int length = Marshal.ReadInt32(ptr, (int)header), count = Marshal.ReadInt32(ptr, (int)header + 4);
            if (length <= 0 || count < 0 || (long)length * count > n - header - 8) return;
            for (int i = 0; i < count; i++) {
                var report = new byte[length]; Marshal.Copy(IntPtr.Add(ptr, (int)header + 8 + i * length), report, 0, length);
                foreach (var change in d.ParseChanges(report)) {
                    if (change.Pressed) Pressed(d, change.Button);
                    else if (hotkeys.Release(d.Path, change.Button)) SetStatus("PTT released.");
                }
            }
        } finally { Marshal.FreeHGlobal(ptr); }
    }
    void Pressed(Controller device, ButtonId button)
    {
        monitor.Text = $"Last input: {device.Name} • {button}";
        if (learningDevice != null) {
            if (device.Path != learningDevice) return;
            string action = learningAction!; CancelLearn(); Assign(device, button, action);
            // Prevent another edge in this report from triggering media during learning.
            suppressUntil = Environment.TickCount64 + 300; return;
        }
        if (!settings.Enabled || configuring || Environment.TickCount64 < suppressUntil) return;
        var bind = settings.Bindings.FirstOrDefault(x => x.Device == device.Path && x.Button == button);
        if (bind == null) return;
        if (!settings.IsTeamSpeakReady(bind.Action)) { NotifyTeamSpeak(bind.Action); return; }
        if (bind.Action == TeamSpeak.PushToTalk) {
            // PTT is edge-driven: never suppress a fresh press because of media debounce.
            string hotkey = settings.TeamSpeakHotkeys[bind.Action];
            SetStatus(hotkeys.Hold(device.Path, button, hotkey) ? $"PTT held: {hotkey}" : "Windows blocked the PTT hotkey."); return;
        }
        var key = (device.Path, button); long now = Environment.TickCount64;
        if (lastFired.TryGetValue(key, out long last) && now - last < settings.Debounce) return;
        lastFired[key] = now; SetStatus(SendAction(bind.Action));
    }
    long suppressUntil;
    bool configuring;
    bool notifiedSetup;
    string? pendingSetupAction;
}
