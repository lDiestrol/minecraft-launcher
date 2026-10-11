using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Launcher.Infrastructure.Installation;

internal sealed class WindowsInstallationJob : IDisposable
{
    private readonly SafeFileHandle _job;
    public string Name { get; }

    internal static string NameFor(string token)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (!Guid.TryParseExact(token, "N", out _)) throw new InvalidDataException("Invalid installer token.");
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return $"Global\\MinecraftLauncher.Install.{identity.User!.Value}.{token}";
    }

    internal uint ActiveProcesses => Query(Name) ?? throw new InvalidDataException("Owned installer job disappeared.");

    internal void Terminate() => Check(TerminateJobObject(_job, 1223));

    public WindowsInstallationJob(string token)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User!.Value;
        Name = NameFor(token);
        Check(ConvertStringSecurityDescriptorToSecurityDescriptor($"O:{sid}D:P(A;;GA;;;SY)(A;;GA;;;{sid})", 1, out IntPtr descriptor, out _));
        try
        {
            SecurityAttributes security = new() { Size = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            _job = CreateJobObject(ref security, Name);
            int error = Marshal.GetLastWin32Error();
            if (_job.IsInvalid || error == 183)
            {
                _job.Dispose();
                throw new Win32Exception(error);
            }
            Limits limits = new();
            limits.Basic.Flags = 0x2000; // Installer tree only: KILL_ON_JOB_CLOSE; never a game job.
            try { Check(SetInformationJobObject(_job, 9, ref limits, Marshal.SizeOf<Limits>())); }
            catch { _job.Dispose(); throw; }
        }
        finally { LocalFree(descriptor); }
    }

    public WindowsInstallationProcess Start(ProcessStartInfo start)
    {
        Check(DuplicateHandle(GetCurrentProcess(), _job.DangerousGetHandle(), GetCurrentProcess(), out SafeFileHandle witness, 4, true, 0));
        using (witness)
        {
            nuint size = 0;
            InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            IntPtr attributes = Marshal.AllocHGlobal(checked((int)size));
            IntPtr jobList = Marshal.AllocHGlobal(IntPtr.Size);
            IntPtr handles = Marshal.AllocHGlobal(IntPtr.Size);
            IntPtr environment = IntPtr.Zero;
            bool initialized = false;
            try
            {
                Check(InitializeProcThreadAttributeList(attributes, 2, 0, ref size));
                initialized = true;
                Marshal.WriteIntPtr(jobList, _job.DangerousGetHandle());
                Marshal.WriteIntPtr(handles, witness.DangerousGetHandle());
                Check(UpdateProcThreadAttribute(attributes, 0, 0x2000D, jobList, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
                Check(UpdateProcThreadAttribute(attributes, 0, 0x20002, handles, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
                StartupEx startup = new() { Info = new() { Size = Marshal.SizeOf<StartupEx>() }, Attributes = attributes };
                string variables = string.Join('\0', start.Environment.Where(pair => pair.Value is not null).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
                environment = Marshal.StringToHGlobalUni(variables);
                StringBuilder command = new(Quote(start.FileName) + " " + string.Join(' ', start.ArgumentList.Select(Quote)));
                Check(CreateProcess(start.FileName, command, IntPtr.Zero, IntPtr.Zero, true, 0x80000 | 0x400 | 0x08000000, environment, start.WorkingDirectory, ref startup, out ProcessInformation process));
                SafeFileHandle nativeProcess = new(process.Process, true);
                using SafeFileHandle thread = new(process.Thread, true);
                return new WindowsInstallationProcess(nativeProcess, checked((int)process.Id));
            }
            finally
            {
                if (initialized) DeleteProcThreadAttributeList(attributes);
                Marshal.FreeHGlobal(attributes); Marshal.FreeHGlobal(jobList); Marshal.FreeHGlobal(handles); Marshal.FreeHGlobal(environment);
                GC.KeepAlive(_job);
            }
        }
    }

    public static uint? Query(string name)
    {
        using SafeFileHandle job = OpenJobObject(4, false, name);
        if (job.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 2) return null;
            throw new Win32Exception(error);
        }
        Check(QueryInformationJobObject(job, 1, out Accounting accounting, Marshal.SizeOf<Accounting>(), IntPtr.Zero));
        return accounting.Active;
    }

    internal static bool ContainsCurrentProcess(string name)
    {
        using SafeFileHandle job = OpenJobObject(4, false, name);
        if (job.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        Check(IsProcessInJob(GetCurrentProcess(), job, out bool inside));
        return inside;
    }

    private static string Quote(string argument) => Launcher.Infrastructure.Game.AtomicJobProcess.Quote(argument);
    private static void Check(bool ok) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    public void Dispose() => _job.Dispose();

    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Size; public IntPtr Descriptor; public int Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits { public long ProcessTime, JobTime; public uint Flags; public nuint Min, Max; public uint ActiveLimit; public nuint Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong Read, Write, Other, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct Limits { public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcess, PeakJob; }
    [StructLayout(LayoutKind.Sequential)] private struct Accounting { public long User, Kernel, PeriodUser, PeriodKernel; public uint Faults, Total, Active, Terminated; }
    [StructLayout(LayoutKind.Sequential)] private struct Startup { public int Size; public IntPtr Reserved, Desktop, Title; public uint X, Y, XS, YS, XC, YC, Fill, Flags; public ushort Show, ReservedBytes; public IntPtr ReservedPointer, Input, Output, Error; }
    [StructLayout(LayoutKind.Sequential)] private struct StartupEx { public Startup Info; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process, Thread; public uint Id, ThreadId; }
    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(ref SecurityAttributes attributes, string name);
    [DllImport("kernel32.dll", EntryPoint = "OpenJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle OpenJobObject(uint access, bool inherit, string name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(SafeFileHandle handle, int info, ref Limits limits, int length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(SafeFileHandle handle, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool IsProcessInJob(IntPtr process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool inside);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool QueryInformationJobObject(SafeFileHandle handle, int info, out Accounting accounting, int length, IntPtr returned);
    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string value, int revision, out IntPtr descriptor, out uint length);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr source, IntPtr targetProcess, out SafeFileHandle target, uint access, bool inherit, uint options);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string app, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string directory, ref StartupEx startup, out ProcessInformation process);
}

internal sealed class WindowsInstallationProcess(SafeFileHandle handle, int id) : IDisposable
{
    public int Id { get; } = id;
    public bool HasExited => WaitForSingleObject(handle, 0) switch
    {
        0 => true,
        258 => false,
        _ => throw new Win32Exception(Marshal.GetLastWin32Error()),
    };
    public int ExitCode
    {
        get
        {
            if (!HasExited || !GetExitCodeProcess(handle, out uint code)) throw new InvalidOperationException("Installer exit code is unavailable.");
            return unchecked((int)code);
        }
    }
    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        while (!HasExited) await Task.Delay(25, cancellationToken).ConfigureAwait(false);
    }
    public void Dispose() => handle.Dispose();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeFileHandle process, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(SafeFileHandle process, out uint exitCode);
}
