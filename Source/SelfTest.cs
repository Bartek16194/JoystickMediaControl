using System.Runtime.InteropServices;

namespace JoystickMediaControl;
internal static class SelfTest
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
        var e = new EdgeTracker();
        Check(!e.Update(1, 0, new ushort[] { 2 }).Any(), "Held button on startup");
        Check(!e.Update(1, 0, new ushort[] { 2 }).Any(), "Held button does not repeat");
        Check(!e.Update(1, 0, Array.Empty<ushort>()).Any(), "Release does not fire");
        Check(e.Update(1, 0, new ushort[] { 2 }).Single().Usage == 2, "Press after release");
        Check(!e.Update(2, 0, Array.Empty<ushort>()).Any(), "Independent report baseline");
        Check(e.Update(2, 0, new ushort[] { 128 }).Single().Usage == 128, "More than 32 buttons");
        Check(!e.Update(1, 0, new ushort[] { 2 }).Any(), "Other report does not reset held button");
        Check(Marshal.SizeOf<Native.Input>() == 40 && Marshal.SizeOf<Native.Registration>() == 16 && Marshal.SizeOf<Native.DeviceEntry>() == 16, "x64 native ABI");
        var virtualController = new Controller { Path = "test-controller", Name = "Test controller" };
        virtualController.Groups.Add((1, 0, new HashSet<ushort> { 1, 128 }));
        virtualController.Groups.Add((2, 3, new HashSet<ushort> { 1 }));
        Check(virtualController.Buttons.Length == 3, "Manual list includes distinct report/collection buttons");
        Check(virtualController.Buttons.Contains(new ButtonId(1, 0, 128)), "Manual list includes high button numbers");
        var assignments = new Settings();
        assignments.Assign(virtualController, new ButtonId(1, 0, 128), "Next track");
        Check(assignments.Bindings.Single().Action == "Next track", "Manual assignment creates binding");
        assignments.Assign(virtualController, new ButtonId(1, 0, 128), "Previous track");
        Check(assignments.Bindings.Count == 1 && assignments.Bindings[0].Action == "Previous track", "Manual assignment edits without duplicates");
        assignments.Assign(virtualController, new ButtonId(2, 3, 1), "Mute");
        Check(assignments.Bindings.Count == 2, "Manual assignment preserves other buttons");
        bool invalidButton = false; try { assignments.Assign(virtualController, new ButtonId(1, 0, 999), "Mute"); } catch (ArgumentException) { invalidButton = true; }
        Check(invalidButton && assignments.Bindings.Count == 2, "Invalid manual button rejected without changing bindings");
        var transitions = new EdgeTracker();
        Check(!transitions.UpdateChanges(1, 0, Array.Empty<ushort>()).Any(), "Transition baseline");
        Check(transitions.UpdateChanges(1, 0, new ushort[] { 4 }).Single() == new ButtonChange(new ButtonId(1, 0, 4), true), "PTT press transition");
        Check(!transitions.UpdateChanges(1, 0, new ushort[] { 4 }).Any(), "PTT held report does not repeat");
        Check(transitions.UpdateChanges(1, 0, Array.Empty<ushort>()).Single() == new ButtonChange(new ButtonId(1, 0, 4), false), "PTT release transition");
        Check(transitions.UpdateChanges(1, 0, new ushort[] { 4 }).Single().Pressed, "Rapid PTT repress is not discarded");
        var output = new RecordingOutput(); var router = new HotkeyRouter(output); var ptt = new ButtonId(1, 0, 4);
        Check(router.Hold("A", ptt, "F13") && output.Events.SequenceEqual(new[] { ((ushort)0x7C, true) }), "PTT emits key down");
        router.Hold("A", ptt, "F13"); Check(output.Events.Count == 1 && router.HeldCount == 1, "Repeated PTT does not emit extra down");
        router.Hold("B", ptt, "F13"); Check(output.Events.Count == 1 && router.HeldCount == 2, "Shared PTT key uses references");
        router.Release("A", ptt); Check(output.Events.Count == 1, "First PTT release preserves second controller");
        router.Release("B", ptt); Check(output.Events.Last() == ((ushort)0x7C, false) && router.HeldCount == 0, "Final PTT release emits key up");
        output.Events.Clear(); router.Hold("A", ptt, "Ctrl+Shift+F1");
        Check(output.Events.Select(x => x.Key).SequenceEqual(new ushort[] { 0xA2, 0xA0, 0x70 }), "Chord modifiers press before primary key");
        router.ReleaseAll(); Check(output.Events.Skip(3).Select(x => x.Key).SequenceEqual(new ushort[] { 0x70, 0xA0, 0xA2 }) && router.HeldCount == 0, "ReleaseAll releases key before modifiers");
        output.Events.Clear(); router.Hold("A", ptt, "F13");
        Check(!router.Tap("F13") && output.Events.Count == 1, "Tap cannot release an active PTT key");
        router.ReleaseAll(); output.Events.Clear();
        Check(router.Tap("F14") && output.Events.SequenceEqual(new[] { ((ushort)0x7D, true), ((ushort)0x7D, false) }), "Microphone toggle emits one key tap");
        output.Events.Clear(); output.FailKey = 0x70;
        Check(!router.Hold("A", ptt, "Ctrl+Shift+F1") && router.HeldCount == 0, "Failed PTT does not remain held");
        Check(output.Events.SequenceEqual(new[] { ((ushort)0xA2, true), ((ushort)0xA0, true), ((ushort)0xA0, false), ((ushort)0xA2, false) }), "Failed PTT rolls back acquired modifiers");
        output.FailKey = null;
        router.Hold("A", ptt, "F13"); router.ReleaseAll(); router.ReleaseAll();
        Check(router.HeldCount == 0, "Disconnect/disable/import/exit cleanup is idempotent");
        Check(TeamSpeak.Defaults().Values.Distinct().Count() == 7, "Distinct default TS3 hotkeys");
        Check(TeamSpeak.Keys("F24").Single() == 0x87, "F24 virtual key");
        var onboarding = new Settings();
        Check(onboarding.IsTeamSpeakReady("Next track"), "Media action bypasses TS3 setup");
        Check(!onboarding.IsTeamSpeakReady(TeamSpeak.PushToTalk), "Fresh TS3 action requires setup");
        onboarding.TeamSpeakConfirmedHotkeys[TeamSpeak.PushToTalk] = "F13";
        Check(onboarding.IsTeamSpeakReady(TeamSpeak.PushToTalk), "User confirmation enables matching hotkey");
        Check(!onboarding.IsTeamSpeakReady("TS3: Toggle microphone mute"), "Confirmation is per action");
        onboarding.TeamSpeakHotkeys[TeamSpeak.PushToTalk] = "F24";
        Check(!onboarding.IsTeamSpeakReady(TeamSpeak.PushToTalk), "Changing hotkey requires setup again");
        string testDir = Environment.GetEnvironmentVariable("JOYSTICK_MEDIA_TEST_DIR") ?? Path.GetTempPath();
        Directory.CreateDirectory(testDir); string file = Path.Combine(testDir, "joystick-media-test-profile.json");
        try {
            var s = new Settings { Bindings = new() { new Binding { Device = "test-hid", Button = new(3, 4, 128), Action = "Next track" } } };
            s.Write(file); var round = Settings.Read(file);
            Check(round.Bindings.Single().Button == new ButtonId(3, 4, 128), "Settings roundtrip");
            Check(round.TeamSpeakHotkeys[TeamSpeak.PushToTalk] == "F13", "TS3 defaults roundtrip");
            s.TeamSpeakHotkeys[TeamSpeak.PushToTalk] = "Ctrl+Shift+F8"; s.Write(file);
            Check(Settings.Read(file).TeamSpeakHotkeys[TeamSpeak.PushToTalk] == "Ctrl+Shift+F8", "Custom TS3 hotkey persists");
            s.TeamSpeakHotkeys = TeamSpeak.Defaults();
            s.Bindings[0].Action = TeamSpeak.PushToTalk; s.Write(file);
            Check(Settings.Read(file).Bindings[0].Action == TeamSpeak.PushToTalk, "TS3 binding profile roundtrip");
            s.TeamSpeakHotkeys[TeamSpeak.PushToTalk] = "F14"; s.Write(file);
            bool duplicateHotkey = false; try { Settings.Read(file); } catch (InvalidDataException) { duplicateHotkey = true; }
            Check(duplicateHotkey, "Duplicate TS3 hotkeys rejected");
            s.TeamSpeakHotkeys = TeamSpeak.Defaults();
            s.Write(file);
            var oldProfile = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!; oldProfile.AsObject().Remove("TeamSpeakHotkeys"); File.WriteAllText(file, oldProfile.ToJsonString());
            Check(Settings.Read(file).TeamSpeakHotkeys.Count == 7, "Existing profiles migrate with TS3 defaults");
            s.TeamSpeakConfirmedHotkeys[TeamSpeak.PushToTalk] = "F13"; s.Write(file);
            Check(Settings.Read(file).IsTeamSpeakReady(TeamSpeak.PushToTalk), "Setup confirmation persists");
            s.TeamSpeakHotkeys[TeamSpeak.PushToTalk] = "F24"; s.Write(file);
            Check(!Settings.Read(file).IsTeamSpeakReady(TeamSpeak.PushToTalk), "Profile drops stale confirmation");
            s.TeamSpeakHotkeys = TeamSpeak.Defaults(); s.TeamSpeakConfirmedHotkeys.Clear();
            foreach (string legacy in new[] { "Play / Pauza", "Następny utwór", "Poprzedni utwór", "Głośniej", "Ciszej", "Wycisz", "Pauza (Spotify)" }) {
                s.Bindings[0].Action = legacy; s.Write(file);
                Check(Media.Actions.Contains(Settings.Read(file).Bindings[0].Action), "Legacy action migration: " + legacy);
            }
            s.Bindings[0].Action = "Next track";
            s.Bindings.Add(s.Bindings[0]); s.Write(file);
            bool rejected = false; try { Settings.Read(file); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Duplicate profile rejected");
            var scanned = Controller.Scan(); Check(scanned.Values.All(d => d.Groups.Count > 0), "Native enumeration");
            foreach (var d in scanned.Values) {
                var caps = new byte[64]; Check(Native.HidP_GetCaps(d.Preparsed, caps) == Native.Success, "Live device caps");
                var g = d.Groups.First(); var report = new byte[BitConverter.ToUInt16(caps, 4)];
                Check(Native.HidP_InitializeReportForID(0, g.Report, d.Preparsed, report, (uint)report.Length) == Native.Success, "Initialize synthetic report using live descriptor");
                Check(!d.Parse(report).Any(), "Live descriptor baseline");
                ushort button = g.Allowed.Max(); uint count = 1;
                Check(Native.HidP_SetUsages(0, 9, g.Collection, new[] { button }, ref count, d.Preparsed, report, (uint)report.Length) == Native.Success, "Set synthetic button");
                Check(d.Parse(report).Contains(new ButtonId(g.Report, g.Collection, button)), "Decode synthetic edge with real descriptor");
                Check(!d.Parse(report).Any(), "Synthetic held input does not repeat");
                Check(Native.HidP_InitializeReportForID(0, g.Report, d.Preparsed, report, (uint)report.Length) == Native.Success, "Clear synthetic held button");
                Check(d.ParseChanges(report).Contains(new ButtonChange(new ButtonId(g.Report, g.Collection, button), false)), "Decode release with real descriptor");
            }
            File.WriteAllText(Path.Combine(testDir, "test-results.txt"), $"PASS: {checks} checks. HID controllers found: {scanned.Count}.\n" + string.Join("\n", scanned.Values.Select(d => $"{d.Name}: {d.Groups.Sum(g => g.Allowed.Count)} buttons")) + "\nPhysical button presses, media playback and TS3 client behavior not tested.\n");
        } finally { if (File.Exists(file)) File.Delete(file); }
    }
    sealed class RecordingOutput : IKeyOutput
    {
        public List<(ushort Key, bool Down)> Events { get; } = new();
        public ushort? FailKey { get; set; }
        public bool Send(ushort key, bool down) { if (down && key == FailKey) return false; Events.Add((key, down)); return true; }
    }
}
