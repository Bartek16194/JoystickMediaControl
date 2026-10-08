using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace JoystickMediaControl;

internal static class Native
{
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    public const int Success = 0x00110000;
    [StructLayout(LayoutKind.Sequential)] public struct DeviceEntry { public IntPtr Handle; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] public struct Registration { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] public struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Key;
        [FieldOffset(10)] public ushort Scan;
        [FieldOffset(12)] public uint Flags;
    }
    [DllImport("user32.dll", SetLastError = true)] public static extern uint GetRawInputDeviceList([Out] DeviceEntry[]? list, ref uint count, uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)] public static extern uint DeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint GetRawInputData(IntPtr handle, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(byte[] preparsed, [Out] byte[] caps);
    [DllImport("hid.dll")] public static extern int HidP_GetButtonCaps(int reportType, [Out] byte[] caps, ref ushort count, byte[] preparsed);
    [DllImport("hid.dll")] public static extern int HidP_GetUsages(int reportType, ushort page, ushort collection, [Out] ushort[] usages, ref uint count, byte[] preparsed, byte[] report, uint length);
    [DllImport("hid.dll")] public static extern int HidP_InitializeReportForID(int reportType, byte id, byte[] preparsed, [Out] byte[] report, uint length);
    [DllImport("hid.dll")] public static extern int HidP_SetUsages(int reportType, ushort page, ushort collection, ushort[] usages, ref uint count, byte[] preparsed, [In, Out] byte[] report, uint length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetProductString(SafeFileHandle handle, [Out] byte[] text, uint length);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")] public static extern uint MapVirtualKey(uint code, uint mapType);
    public delegate bool WindowCallback(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] public static extern bool EnumWindows(WindowCallback callback, IntPtr data);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    public static byte[] InfoBytes(IntPtr device, uint command)
    {
        uint n = 0;
        if (DeviceInfo(device, command, IntPtr.Zero, ref n) == uint.MaxValue || n == 0) return Array.Empty<byte>();
        int bytes = checked((int)n * (command == 0x20000007 ? 2 : 1));
        var ptr = Marshal.AllocHGlobal(bytes + 2);
        try {
            if (DeviceInfo(device, command, ptr, ref n) == uint.MaxValue) return Array.Empty<byte>();
            var result = new byte[bytes]; Marshal.Copy(ptr, result, 0, bytes); return result;
        } finally { Marshal.FreeHGlobal(ptr); }
    }
    public static string Product(string path)
    {
        using var h = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        var text = new byte[512];
        return !h.IsInvalid && HidD_GetProductString(h, text, 512) ? Encoding.Unicode.GetString(text).TrimEnd('\0') : "HID controller";
    }
}
