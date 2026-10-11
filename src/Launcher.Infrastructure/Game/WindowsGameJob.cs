using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Launcher.Infrastructure.Game;

/// <summary>A per-user job; the primary JVM inherits a QUERY witness to retain its name.</summary>
internal sealed class WindowsGameJob : IDisposable
{
    internal const int WitnessContractVersion = 1;
    internal SafeFileHandle Handle { get; }
    public string Name { get; }

    public static string GetName(string token)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Atomic game jobs require Windows 10 or later.");
        }

        if (!Guid.TryParseExact(token, "N", out _))
        {
            throw new InvalidDataException("Invalid game job token.");
        }

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return $"Global\\MinecraftLauncher.Game.{identity.User!.Value}.{token}";
    }

    public WindowsGameJob(string token)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Atomic game jobs require Windows 10 or later.");
        }

        Name = GetName(token);
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User!.Value;
        // Do not grant other users access, and do not inherit a permissive default DACL.
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(
            $"O:{sid}D:P(A;;GA;;;SY)(A;;GA;;;{sid})", 1, out IntPtr descriptor, out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            SecurityAttributes attributes = new() { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            Handle = CreateJobObject(ref attributes, Name);
            int error = Marshal.GetLastWin32Error();
            if (Handle.IsInvalid || error == 183)
            {
                Handle.Dispose();
                throw new Win32Exception(error, "Cannot create an exclusive game job (name collision or access denied).");
            }

            // Intentionally no KILL_ON_JOB_CLOSE or BREAKAWAY flags.
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    public uint ActiveProcesses => Query(Handle);

    internal SafeFileHandle CreateInheritableQueryWitness()
    {
        if (!DuplicateHandle(GetCurrentProcess(), Handle.DangerousGetHandle(), GetCurrentProcess(),
            out SafeFileHandle witness, 4, true, 0))
        {
            int error = Marshal.GetLastWin32Error();
            witness.Dispose();
            throw new Win32Exception(error, "Cannot create the game job QUERY witness.");
        }

        return witness;
    }

    // null means missing NAME, not an empty process tree. Only the versioned primary-JVM
    // witness contract permits callers to use this as evidence that the main JVM exited.
    public static uint? GetActiveProcesses(string name)
    {
        using SafeFileHandle job = OpenJobObject(4, false, name);
        if (job.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 2)
            {
                return null;
            }

            // All errors except ERROR_FILE_NOT_FOUND fail closed.
            throw new Win32Exception(error, "Cannot verify game job state.");
        }

        return Query(job);
    }

    private static uint Query(SafeFileHandle job)
    {
        if (!QueryInformationJobObject(job, 1, out Accounting information, Marshal.SizeOf<Accounting>(), IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return information.ActiveProcesses;
    }

    public void Dispose() => Handle.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr Descriptor;
        public int Inherit;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Accounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(ref SecurityAttributes attributes, string name);
    [DllImport("kernel32.dll", EntryPoint = "OpenJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle OpenJobObject(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int informationClass,
        out Accounting information, int length, IntPtr returnLength);
    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, int revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr source, IntPtr targetProcess,
        out SafeFileHandle target, uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);
}
