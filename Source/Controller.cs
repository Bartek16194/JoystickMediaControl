using System.Runtime.InteropServices;
using System.Text;

namespace JoystickMediaControl;

public record ButtonId(byte Report, ushort Collection, ushort Usage)
{
    public override string ToString() => $"Button {Usage}" + (Collection != 0 || Report != 0 ? $" (report {Report}, collection {Collection})" : "");
}
public class EdgeTracker
{
    private readonly Dictionary<(byte, ushort), HashSet<ushort>> states = new();
    public IEnumerable<ButtonId> Update(byte report, ushort collection, IEnumerable<ushort> pressed)
        => UpdateChanges(report, collection, pressed).Where(x => x.Pressed).Select(x => x.Button);
    public IEnumerable<ButtonChange> UpdateChanges(byte report, ushort collection, IEnumerable<ushort> pressed)
    {
        var key = (report, collection); var next = pressed.ToHashSet();
        bool known = states.TryGetValue(key, out var previous);
        states[key] = next;
        // The initial state is a baseline: switches held during startup must not fire.
        if (!known) return Array.Empty<ButtonChange>();
        return previous!.Except(next).Select(x => new ButtonChange(new ButtonId(report, collection, x), false))
            .Concat(next.Except(previous!).Select(x => new ButtonChange(new ButtonId(report, collection, x), true))).ToArray();
    }
}
public record ButtonChange(ButtonId Button, bool Pressed);
internal sealed class Controller
{
    public IntPtr Handle { get; init; }
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public byte[] Preparsed { get; init; } = Array.Empty<byte>();
    public List<(byte Report, ushort Collection, HashSet<ushort> Allowed)> Groups { get; } = new();
    public EdgeTracker Edges { get; } = new();
    readonly ushort[] usageBuffer = new ushort[4096];
    public ButtonId[] Buttons => Groups.SelectMany(g => g.Allowed.Select(u => new ButtonId(g.Report, g.Collection, u)))
        .Distinct().OrderBy(b => b.Usage).ThenBy(b => b.Report).ThenBy(b => b.Collection).ToArray();
    public override string ToString() => $"{Name} • {Groups.Sum(x => x.Allowed.Count)} buttons • {Path.Split('#').ElementAtOrDefault(1)}";
    public static Dictionary<IntPtr, Controller> Scan()
    {
        uint count = 0; uint size = (uint)Marshal.SizeOf<Native.DeviceEntry>();
        if (Native.GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) throw new System.ComponentModel.Win32Exception();
        var list = new Native.DeviceEntry[count];
        if (Native.GetRawInputDeviceList(list, ref count, size) == uint.MaxValue) throw new System.ComponentModel.Win32Exception();
        var result = new Dictionary<IntPtr, Controller>();
        foreach (var entry in list.Take((int)count).Where(x => x.Type == 2)) {
            var pp = Native.InfoBytes(entry.Handle, 0x20000005); var caps = new byte[64];
            if (pp.Length == 0 || Native.HidP_GetCaps(pp, caps) != Native.Success) continue;
            ushort usage = BitConverter.ToUInt16(caps, 0), page = BitConverter.ToUInt16(caps, 2);
            if (page != 1 || (usage != 4 && usage != 5 && usage != 8)) continue;
            ushort n = BitConverter.ToUInt16(caps, 46);
            var buttonCaps = new byte[n * 72];
            if (n == 0 || Native.HidP_GetButtonCaps(0, buttonCaps, ref n, pp) != Native.Success) continue;
            string path = Encoding.Unicode.GetString(Native.InfoBytes(entry.Handle, 0x20000007)).TrimEnd('\0');
            var d = new Controller { Handle = entry.Handle, Path = path, Name = Native.Product(path), Preparsed = pp };
            for (int i = 0; i < n; i++) {
                int o = i * 72;
                if (BitConverter.ToUInt16(buttonCaps, o) != 9) continue;
                byte report = buttonCaps[o + 2]; ushort collection = BitConverter.ToUInt16(buttonCaps, o + 6);
                var group = d.Groups.FirstOrDefault(x => x.Report == report && x.Collection == collection);
                if (group.Allowed == null) { group = (report, collection, new HashSet<ushort>()); d.Groups.Add(group); }
                int first = BitConverter.ToUInt16(buttonCaps, o + 56);
                int last = buttonCaps[o + 12] != 0 ? BitConverter.ToUInt16(buttonCaps, o + 58) : first;
                for (int u = first; u <= last; u++) group.Allowed.Add((ushort)u);
            }
            if (d.Groups.Count > 0) result[entry.Handle] = d;
        }
        return result;
    }
    public IEnumerable<ButtonId> Parse(byte[] report)
        => ParseChanges(report).Where(x => x.Pressed).Select(x => x.Button);
    public IEnumerable<ButtonChange> ParseChanges(byte[] report)
    {
        var edges = new List<ButtonChange>();
        foreach (var group in Groups.Where(g => g.Report == 0 || g.Report == report[0])) {
            var usages = usageBuffer; uint n = (uint)usages.Length;
            int status = Native.HidP_GetUsages(0, 9, group.Collection, usages, ref n, Preparsed, report, (uint)report.Length);
            if (status != Native.Success) continue;
            edges.AddRange(Edges.UpdateChanges(group.Report, group.Collection, usages.Take((int)n).Where(group.Allowed.Contains)));
        }
        return edges;
    }
}
