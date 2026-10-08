namespace JoystickMediaControl;

internal static class TeamSpeak
{
    public static string Guide(string action) => action == PushToTalk ? "Capture > Push-To-Talk / Hotkeys > Push-to-Talk" : action.Replace("TS3: ", "");
    public static string OpenOptions()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("ts3client_win64").Concat(System.Diagnostics.Process.GetProcessesByName("ts3client_win32")).ToArray();
        try {
            var client = processes.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            if (client == null) return "Open the TeamSpeak 3 desktop client first, then click Open TS3 options again.";
            var window = client.MainWindowHandle; Native.ShowWindow(window, 9);
            if (!Native.SetForegroundWindow(window)) return "Switch to TS3 and press Alt+P to open its options.";
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out uint pid);
            if (pid != client.Id) return "Switch to TS3 and press Alt+P to open its options.";
            var output = new WindowsKeyOutput(); bool alt = output.Send(0x12, true);
            bool key = alt && output.Send(0x50, true);
            if (key) output.Send(0x50, false);
            if (alt) output.Send(0x12, false);
            return key ? "TS3 options requested. Select Hotkeys and add the corresponding action." : "Could not send Alt+P. Open Tools > Options in TS3.";
        } catch (Exception ex) { return "Could not open TS3 options: " + ex.Message; }
        finally { foreach (var process in processes) process.Dispose(); }
    }
    public const string PushToTalk = "TS3: Push-to-talk (hold)";
    public static readonly string[] Actions = { PushToTalk, "TS3: Toggle microphone mute", "TS3: Mute microphone", "TS3: Unmute microphone", "TS3: Toggle speakers mute", "TS3: Mute speakers", "TS3: Unmute speakers" };
    public static readonly string[] Hotkeys = Enumerable.Range(13, 12).Select(n => $"F{n}")
        .Concat(Enumerable.Range(1, 12).Select(n => $"Ctrl+Shift+F{n}")).ToArray();
    public static Dictionary<string, string> Defaults() => Actions.Select((a, i) => (a, key: $"F{13 + i}")).ToDictionary(x => x.a, x => x.key);
    public static ushort[] Keys(string hotkey)
    {
        if (!Hotkeys.Contains(hotkey)) throw new ArgumentException("Invalid TeamSpeak hotkey.");
        bool modifiers = hotkey.StartsWith("Ctrl+Shift+");
        int n = int.Parse(hotkey[(hotkey.LastIndexOf('F') + 1)..]);
        ushort key = (ushort)(0x70 + n - 1);
        return modifiers ? new ushort[] { 0xA2, 0xA0, key } : new[] { key };
    }
}

internal interface IKeyOutput { bool Send(ushort key, bool down); }
internal sealed class WindowsKeyOutput : IKeyOutput
{
    public bool Send(ushort key, bool down) => Native.SendInput(1, new[] { new Native.Input { Type = 1, Key = key, Flags = down ? 0u : 2u } }, 40) == 1;
}

// One source per physical button, with reference counts for shared PTT keys/modifiers.
internal sealed class HotkeyRouter
{
    readonly IKeyOutput output;
    readonly Dictionary<(string Device, ButtonId Button), ushort[]> held = new();
    readonly Dictionary<ushort, int> counts = new();
    public int HeldCount => held.Count;
    public HotkeyRouter(IKeyOutput output) => this.output = output;
    public bool Hold(string device, ButtonId button, string hotkey)
    {
        var source = (device, button);
        if (held.ContainsKey(source)) return true;
        var keys = TeamSpeak.Keys(hotkey); var acquired = new List<ushort>();
        foreach (ushort key in keys) {
            counts.TryGetValue(key, out int count);
            if (count == 0 && !output.Send(key, true)) { ReleaseKeys(acquired); return false; }
            counts[key] = count + 1; acquired.Add(key);
        }
        held[source] = keys; return true;
    }
    void ReleaseKeys(IEnumerable<ushort> keys)
    {
        foreach (ushort key in keys.Reverse()) {
            if (!counts.TryGetValue(key, out int count)) continue;
            if (count > 1) counts[key] = count - 1;
            else { output.Send(key, false); counts.Remove(key); }
        }
    }
    public bool Release(string device, ButtonId button)
    {
        if (!held.Remove((device, button), out var keys)) return false;
        ReleaseKeys(keys); return true;
    }
    public void ReleaseAll()
    {
        foreach (var source in held.Keys.ToArray()) Release(source.Device, source.Button);
    }
    public bool Tap(string hotkey)
    {
        // Avoid emitting a key-up for a key that another controller is holding.
        var keys = TeamSpeak.Keys(hotkey);
        if (keys.Any(counts.ContainsKey)) return false;
        var pressed = new List<ushort>(); bool success = true;
        foreach (var key in keys) { if (!output.Send(key, true)) { success = false; break; } pressed.Add(key); }
        foreach (var key in pressed.AsEnumerable().Reverse()) success = output.Send(key, false) && success;
        return success;
    }
}

internal sealed class TeamSpeakForm : Form
{
    readonly Dictionary<string, ComboBox> boxes = new();
    readonly Dictionary<string, CheckBox> checks = new();
    readonly System.Windows.Forms.Timer sendTimer = new() { Interval = 3000 };
    readonly Label status = new() { AutoSize = true, MaximumSize = new Size(620, 0) };
    string? pending;
    bool pendingHold, releasing;
    readonly HotkeyRouter testRouter = new(new WindowsKeyOutput());
    readonly ButtonId testSource = new(0, 0, 0);
    public Dictionary<string, string>? Result { get; private set; }
    public Dictionary<string, string>? Confirmed { get; private set; }
    public TeamSpeakForm(Dictionary<string, string> current, Dictionary<string, string>? confirmed = null, string? focusAction = null)
    {
        Text = "TeamSpeak 3 setup"; Size = new Size(940, 700); MinimumSize = Size; StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(16, 22, 33); ForeColor = Color.White; Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 4, RowCount = 12 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        Controls.Add(layout);
        var info = new Label { AutoSize = true, MaximumSize = new Size(870, 0), Text = "One-time setup: TS3 needs matching hotkeys before joystick controls can work.\n1. Open TS3 options, select Hotkeys and add the action shown below.\n2. Start recording the key in TS3. Use Send in 3s and return to the TS3 capture dialog.\n3. Save the hotkey in TS3. For PTT, also select Push-To-Talk in Capture.\n4. Use Send in 3s again to test it in TS3, tick Configured in TS3, then save this setup.\nPTT tests hold the key for 2 seconds; other actions send a tap.\nOnly configure the actions you plan to use. The checkbox records your confirmation.", Margin = new Padding(0, 0, 0, 10) };
        layout.Controls.Add(info, 0, 0); layout.SetColumnSpan(info, 4);
        var openRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var open = MakeButton("Open TS3 options"); open.Click += (_, _) => status.Text = TeamSpeak.OpenOptions(); openRow.Controls.Add(open);
        var guide = new Label { AutoSize = true, MaximumSize = new Size(620, 0), Padding = new Padding(6, 5, 0, 0), Text = focusAction == null ? "Choose the corresponding action in the TS3 hotkey list." : "Set up: " + TeamSpeak.Guide(focusAction) }; openRow.Controls.Add(guide); layout.Controls.Add(openRow, 0, 1); layout.SetColumnSpan(openRow, 4);
        int row = 2;
        foreach (string action in TeamSpeak.Actions) {
            layout.Controls.Add(new Label { Text = action.Replace("TS3: ", ""), AutoSize = true, Padding = new Padding(0, 7, 0, 0), ForeColor = action == focusAction ? Color.FromArgb(89, 222, 178) : Color.White }, 0, row);
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, ForeColor = Color.Black, BackColor = Color.White, AccessibleName = action + " hotkey" }; box.Items.AddRange(TeamSpeak.Hotkeys); box.SelectedItem = current[action]; boxes[action] = box; layout.Controls.Add(box, 1, row);
            var send = MakeButton("Send in 3s"); send.Click += (_, _) => { testRouter.ReleaseAll(); releasing = false; pendingHold = action == TeamSpeak.PushToTalk; pending = (string)box.SelectedItem!; sendTimer.Stop(); sendTimer.Interval = 3000; sendTimer.Start(); guide.Text = "TS3 action: " + TeamSpeak.Guide(action); status.Text = $"Focus TS3. Sending {pending} in 3 seconds..."; }; layout.Controls.Add(send, 2, row);
            var check = new CheckBox { Text = "Configured in TS3", AutoSize = true, Checked = confirmed != null && confirmed.TryGetValue(action, out var value) && value == current[action], Padding = new Padding(0, 5, 0, 0) }; checks[action] = check;
            box.SelectedIndexChanged += (_, _) => check.Checked = false;
            layout.Controls.Add(check, 3, row++);
        }
        layout.Controls.Add(status, 0, row); layout.SetColumnSpan(status, 4); row++;
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var save = MakeButton("Save setup"); var cancel = MakeButton("Set up later"); cancel.DialogResult = DialogResult.Cancel;
        save.Click += (_, _) => {
            var result = boxes.ToDictionary(x => x.Key, x => (string)x.Value.SelectedItem!);
            if (result.Values.Distinct().Count() != result.Count) { status.Text = "Each TS3 action needs a different hotkey."; return; }
            Result = result; Confirmed = checks.Where(x => x.Value.Checked).ToDictionary(x => x.Key, x => result[x.Key]); DialogResult = DialogResult.OK; Close();
        };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, row); layout.SetColumnSpan(buttons, 4); CancelButton = cancel;
        sendTimer.Tick += (_, _) => {
            sendTimer.Stop();
            if (releasing) { testRouter.ReleaseAll(); releasing = false; status.Text = "PTT test finished; hotkey released."; pending = null; return; }
            if (pending == null) return;
            if (pendingHold) {
                if (testRouter.Hold("setup-test", testSource, pending)) { releasing = true; status.Text = $"Holding {pending} for 2 seconds. Check PTT in TS3."; sendTimer.Interval = 2000; sendTimer.Start(); }
                else { status.Text = "Windows blocked the hotkey."; pending = null; }
            } else { status.Text = testRouter.Tap(pending) ? $"Sent {pending}. Check the action in TS3." : "Windows blocked the hotkey."; pending = null; }
        };
        FormClosed += (_, _) => { testRouter.ReleaseAll(); sendTimer.Dispose(); };
    }
    static Button MakeButton(string text) => new() { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(32, 42, 58), ForeColor = Color.White };
}
