using System.Text.Json;
using System.Diagnostics;

namespace JoystickMediaControl;
public class Binding
{
    public string Device { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public ButtonId Button { get; set; } = new(0, 0, 1);
    public string Action { get; set; } = "Play / Pause";
}
public class Settings
{
    public List<Binding> Bindings { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public bool StartHidden { get; set; }
    public int Debounce { get; set; } = 180;
    public Dictionary<string, string> TeamSpeakHotkeys { get; set; } = TeamSpeak.Defaults();
    public Dictionary<string, string> TeamSpeakConfirmedHotkeys { get; set; } = new();
    public bool IsTeamSpeakReady(string action) => !TeamSpeak.Actions.Contains(action) ||
        (TeamSpeakHotkeys.TryGetValue(action, out var key) && TeamSpeakConfirmedHotkeys.TryGetValue(action, out var confirmed) && key == confirmed);
    public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JoystickMediaControl");
    public static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    public static readonly string LegacyFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrionMedia", "settings.json");
    public static Settings Read(string path)
    {
        var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty profile.");
        if (s.Bindings != null) foreach (var b in s.Bindings.Where(b => b != null)) b.Action = Media.Normalize(b.Action);
        if (s.Bindings == null || s.Bindings.Any(b => b == null || b.Button == null || string.IsNullOrEmpty(b.Device) || !Media.Actions.Contains(b.Action))) throw new InvalidDataException("Invalid bindings in profile.");
        if (s.Bindings.GroupBy(b => (b.Device, b.Button)).Any(g => g.Count() > 1)) throw new InvalidDataException("Profile contains duplicate buttons.");
        if (s.TeamSpeakHotkeys == null || s.TeamSpeakHotkeys.Count != TeamSpeak.Actions.Length || TeamSpeak.Actions.Any(a => !s.TeamSpeakHotkeys.TryGetValue(a, out var key) || !TeamSpeak.Hotkeys.Contains(key)) || s.TeamSpeakHotkeys.Values.Distinct().Count() != s.TeamSpeakHotkeys.Count) throw new InvalidDataException("Invalid or duplicate TeamSpeak hotkeys.");
        s.TeamSpeakConfirmedHotkeys ??= new();
        foreach (var action in s.TeamSpeakConfirmedHotkeys.Keys.ToArray()) {
            if (!s.TeamSpeakHotkeys.TryGetValue(action, out var key) || key != s.TeamSpeakConfirmedHotkeys[action]) s.TeamSpeakConfirmedHotkeys.Remove(action);
        }
        s.Debounce = Math.Clamp(s.Debounce, 0, 2000); return s;
    }
    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, true);
    }
    internal Binding Assign(Controller device, ButtonId button, string action)
    {
        if (!device.Buttons.Contains(button)) throw new ArgumentException("This button is not available on the selected controller.");
        if (!Media.Actions.Contains(action)) throw new ArgumentException("Unknown action.");
        var binding = Bindings.FirstOrDefault(b => b.Device == device.Path && b.Button == button);
        if (binding == null) { binding = new Binding { Device = device.Path, DeviceName = device.Name, Button = button }; Bindings.Add(binding); }
        binding.Action = action; return binding;
    }
}
internal static class Media
{
    public static string Normalize(string action) => action switch {
        "Play / Pauza" => "Play / Pause", "Następny utwór" => "Next track", "Poprzedni utwór" => "Previous track",
        "Głośniej" => "Volume up", "Ciszej" => "Volume down", "Wycisz" => "Mute", "Pauza (Spotify)" => "Pause (Spotify)", _ => action
    };
    public static readonly string[] Actions = new[] { "Play / Pause", "Next track", "Previous track", "Stop", "Volume up", "Volume down", "Mute", "Play (Spotify)", "Pause (Spotify)" }.Concat(TeamSpeak.Actions).ToArray();
    public static string Send(string action)
    {
        if (action.EndsWith("(Spotify)")) {
            var ids = Process.GetProcessesByName("Spotify").Select(p => { using (p) return (uint)p.Id; }).ToHashSet();
            bool sent = false; int command = action.StartsWith("Play") ? 46 : 47;
            Native.EnumWindows((h, _) => {
                Native.GetWindowThreadProcessId(h, out uint id);
                if (!ids.Contains(id)) return true;
                if (Native.SendMessageTimeout(h, 0x319, h, new IntPtr(command << 16), 2, 100, out var handled) != IntPtr.Zero && handled != IntPtr.Zero) { sent = true; return false; }
                return true;
            }, IntPtr.Zero);
            return sent ? $"Sent: {action}" : "Spotify did not acknowledge the command. Try Play / Pause.";
        }
        var inputs = KeyInputs(action);
        return Native.SendInput(2, inputs, 40) == 2 ? $"Sent: {action}" : "Windows blocked the media command.";
    }
    internal static Native.Input[] KeyInputs(string action)
    {
        ushort key = action switch { "Play / Pause" => 0xB3, "Next track" => 0xB0, "Previous track" => 0xB1, "Stop" => 0xB2, "Volume up" => 0xAF, "Volume down" => 0xAE, "Mute" => 0xAD, _ => throw new ArgumentException("Unknown action.") };
        uint scan = Native.MapVirtualKey(key, 4); // MAPVK_VK_TO_VSC_EX preserves the E0 prefix.
        if ((scan & 0xFF) == 0) throw new InvalidOperationException("Windows could not map the media key.");
        uint flags = 0x8 | ((scan & 0xFF00) == 0xE000 ? 0x1u : 0u);
        // Send the physical scan code, including its extended flag, to keyboard listeners.
        return new[] {
            new Native.Input { Type = 1, Scan = (ushort)(scan & 0xFF), Flags = flags },
            new Native.Input { Type = 1, Scan = (ushort)(scan & 0xFF), Flags = flags | 0x2 }
        };
    }
}
