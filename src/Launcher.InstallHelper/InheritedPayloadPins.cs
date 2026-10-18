using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Launcher.InstallHelper;

internal static class InheritedPayloadPins
{
    internal const string EnvironmentKey = "LAUNCHER_BOOTSTRAPPER_READ_PINS";

    internal static void RevokeFurtherInheritance(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded) || encoded.Length > 32768) throw new InvalidDataException("Missing payload protection handles.");
        string[] values = encoded.Split(',');
        if (values.Length > 2048 || values.Distinct(StringComparer.Ordinal).Count() != values.Length) throw new InvalidDataException("Invalid payload protection handle list.");
        foreach (string value in values)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) || number <= 0)
                throw new InvalidDataException("Invalid payload protection handle.");
            IntPtr pin = new(number);
            if (!GetHandleInformation(pin, out uint flags) || (flags & 1) == 0 || GetFileType(pin) != 1 || !SetHandleInformation(pin, 1, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot retain non-propagating payload protection.");
        }
        // Keep the OS handles open until helper exit, but never propagate them into Setup/Launcher/Minecraft.
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetHandleInformation(IntPtr handle, out uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr handle);
}
