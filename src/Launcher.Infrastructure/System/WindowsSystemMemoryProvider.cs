using System.ComponentModel;
using System.Runtime.InteropServices;
using Launcher.Core.Services;

namespace Launcher.Infrastructure.System;

public sealed class WindowsSystemMemoryProvider : ISystemMemoryProvider
{
    private const long BytesPerMegabyte = 1024 * 1024;

    public long GetTotalPhysicalMemoryMb()
    {
        MemoryStatus status = new()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatus>(),
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return checked((long)(status.TotalPhysical / BytesPerMegabyte));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
