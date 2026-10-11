using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace StorageRoot.JobPrototype;

// Self-contained Win32 prototype: no reference to Launcher.Infrastructure or its guardian.
internal sealed class NativeJob : IDisposable
{
    public string Name { get; }
    private readonly SafeFileHandle _job;
    public SafeFileHandle Witness { get; }
    private readonly SafeFileHandle _canary;
    public long CanaryHandle => _canary.DangerousGetHandle().ToInt64();
    public long FullJobHandle => _job.DangerousGetHandle().ToInt64();

    public NativeJob(string name, string directory)
    {
        Name = name;
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string sid = identity.User!.Value;
        Check(ConvertStringSecurityDescriptorToSecurityDescriptor($"O:{sid}D:P(A;;GA;;;SY)(A;;GA;;;{sid})",
            1, out IntPtr descriptor, out _));
        try
        {
            SecurityAttributes security = new() { Size = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
            _job = CreateJobObject(ref security, name);
            int error = Marshal.GetLastWin32Error();
            if (_job.IsInvalid || error == 183)
            {
                _job.Dispose();
                throw new Win32Exception(error, "Exclusive prototype job creation failed.");
            }

            // QUERY only (4), inheritable, and listed explicitly at atomic process creation.
            Check(DuplicateHandle(GetCurrentProcess(), _job.DangerousGetHandle(), GetCurrentProcess(),
                out SafeFileHandle witness, 4, true, 0));
            Witness = witness;
            SecurityAttributes inheritable = new() { Size = Marshal.SizeOf<SecurityAttributes>(), Inherit = 1 };
            _canary = CreateFile(Path.Combine(directory, "canary.txt"), 0x40000000, 3, ref inheritable, 4, 0, IntPtr.Zero);
            if (_canary.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    public CreatedProcess Start(string java, IReadOnlyList<string> arguments, string directory,
        IReadOnlyDictionary<string, string?> variables)
    {
        using SafeFileHandle input = OpenStdio("NUL", true);
        using SafeFileHandle output = OpenStdio(Path.Combine(directory, "stdout.txt"), false);
        using SafeFileHandle error = OpenStdio(Path.Combine(directory, "stderr.txt"), false);
        IntPtr list = IntPtr.Zero, handles = IntPtr.Zero, jobs = IntPtr.Zero, environment = IntPtr.Zero;
        bool initialized = false;
        try
        {
            nuint size = 0;
            InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            list = Marshal.AllocHGlobal(checked((int)size));
            Check(InitializeProcThreadAttributeList(list, 2, 0, ref size));
            initialized = true;
            handles = Marshal.AllocHGlobal(IntPtr.Size * 4);
            IntPtr[] allowed = [input.DangerousGetHandle(), output.DangerousGetHandle(), error.DangerousGetHandle(), Witness.DangerousGetHandle()];
            for (int index = 0; index < allowed.Length; index++)
            {
                Marshal.WriteIntPtr(handles, index * IntPtr.Size, allowed[index]);
            }

            jobs = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(jobs, _job.DangerousGetHandle());
            Check(UpdateProcThreadAttribute(list, 0, 0x00020002, handles, (nuint)(4 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero));
            Check(UpdateProcThreadAttribute(list, 0, 0x0002000D, jobs, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
            StartupInfoEx startup = new()
            {
                Info = new StartupInfo
                {
                    Size = Marshal.SizeOf<StartupInfoEx>(),
                    Flags = 0x100,
                    Input = allowed[0],
                    Output = allowed[1],
                    Error = allowed[2]
                },
                Attributes = list,
            };
            string block = string.Join('\0', variables.Where(pair => pair.Value is not null)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
            environment = Marshal.StringToHGlobalUni(block);
            StringBuilder command = new(string.Join(' ', new[] { java }.Concat(arguments).Select(Quote)));
            Check(CreateProcess(java, command, IntPtr.Zero, IntPtr.Zero, true, 0x08080400,
                environment, directory, ref startup, out ProcessInformation information));
            // This dedicated owner creates no other processes during the inheritance window.
            Check(SetHandleInformation(Witness, 1, 0));
            using SafeFileHandle thread = new(information.Thread, true);
            using SafeFileHandle process = new(information.Process, true);
            Check(GetProcessTimes(process, out long created, out _, out _, out _));
            return new CreatedProcess((int)information.ProcessId, DateTime.FromFileTimeUtc(created).Ticks,
                Witness.DangerousGetHandle().ToInt64(), _canary.DangerousGetHandle().ToInt64(), _job.DangerousGetHandle().ToInt64());
        }
        finally
        {
            if (initialized)
            {
                DeleteProcThreadAttributeList(list);
            }

            Marshal.FreeHGlobal(list);
            Marshal.FreeHGlobal(handles);
            Marshal.FreeHGlobal(jobs);
            Marshal.FreeHGlobal(environment);
        }
    }

    public static uint? Count(string name)
    {
        using SafeFileHandle handle = OpenJobObject(4, false, name);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 2)
            {
                return null; // Missing name is never silently equated with zero processes.
            }

            throw new Win32Exception(error);
        }

        Check(QueryInformationJobObject(handle, 1, out Accounting accounting, Marshal.SizeOf<Accounting>(), IntPtr.Zero));
        return accounting.Active;
    }

    public static Snapshot? Inspect(string name)
    {
        using SafeFileHandle job = OpenJobObject(4, false, name);
        if (job.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 2)
            {
                return null;
            }

            throw new Win32Exception(error);
        }

        Check(QueryInformationJobObject(job, 1, out Accounting accounting, Marshal.SizeOf<Accounting>(), IntPtr.Zero));
        int capacity = 16;
        while (true)
        {
            int size = 8 + capacity * IntPtr.Size;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!QueryBuffer(job, 3, buffer, size, IntPtr.Zero))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 234)
                    {
                        capacity *= 2;
                        continue;
                    }

                    throw new Win32Exception(error);
                }

                int length = Marshal.ReadInt32(buffer, 4);
                List<Member> members = [];
                for (int index = 0; index < length; index++)
                {
                    int pid = checked((int)Marshal.ReadIntPtr(buffer, 8 + index * IntPtr.Size));
                    using Process process = Process.GetProcessById(pid);
                    Check(IsProcessInJob(process.Handle, job, out bool inJob));
                    members.Add(new Member(pid, process.ProcessName, process.SessionId, inJob));
                }

                IntPtr limits = Marshal.AllocHGlobal(144);
                try
                {
                    Check(QueryBuffer(job, 9, limits, 144, IntPtr.Zero));
                    uint flags = unchecked((uint)Marshal.ReadInt32(limits, 16));
                    return new Snapshot(accounting.Active, flags, members.ToArray());
                }
                finally
                {
                    Marshal.FreeHGlobal(limits);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    public static bool InAnyJob(int pid)
    {
        using Process process = Process.GetProcessById(pid);
        Check(IsProcessInAnyJob(process.Handle, IntPtr.Zero, out bool inJob));
        return inJob;
    }

    public static bool InheritedSameObject(int ownerPid, int childPid, long value)
    {
        using SafeFileHandle owner = OpenProcess(0x40, false, ownerPid);
        using SafeFileHandle child = OpenProcess(0x40, false, childPid);
        Check(DuplicateHandle(owner.DangerousGetHandle(), new IntPtr(value), GetCurrentProcess(),
            out SafeFileHandle expected, 0, false, 2));
        using (expected)
        {
            if (!DuplicateHandle(child.DangerousGetHandle(), new IntPtr(value), GetCurrentProcess(),
                out SafeFileHandle actual, 0, false, 2))
            {
                actual.Dispose();
                return false;
            }

            using (actual)
            {
                return CompareObjectHandles(expected, actual);
            }
        }
    }

    public string SecurityDescriptor()
    {
        GetKernelObjectSecurity(_job, 7, null, 0, out uint size);
        byte[] descriptor = new byte[size];
        Check(GetKernelObjectSecurity(_job, 7, descriptor, size, out _));
        return new System.Security.AccessControl.RawSecurityDescriptor(descriptor, 0)
            .GetSddlForm(System.Security.AccessControl.AccessControlSections.All);
    }

    public void SetActiveProcessLimit(uint count)
    {
        IntPtr limits = Marshal.AllocHGlobal(144);
        try
        {
            Marshal.Copy(new byte[144], 0, limits, 144);
            Marshal.WriteInt32(limits, 16, 8); // ACTIVE_PROCESS only; never kill or breakaway.
            Marshal.WriteInt32(limits, 40, checked((int)count));
            Check(SetInformationJobObject(_job, 9, limits, 144));
        }
        finally
        {
            Marshal.FreeHGlobal(limits);
        }
    }

    public void DenyQuery()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        Check(ConvertStringSecurityDescriptorToSecurityDescriptor(
            $"D:P(D;;0x4;;;{identity.User!.Value})(A;;GA;;;SY)(A;;GA;;;{identity.User.Value})",
            1, out IntPtr descriptor, out _));
        try
        {
            Check(SetKernelObjectSecurity(_job, 4, descriptor));
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    public static uint? InheritedJobAccess(int pid, long handleValue, string name)
    {
        using SafeFileHandle process = OpenProcess(0x40, false, pid);
        if (process.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (!DuplicateHandle(process.DangerousGetHandle(), new IntPtr(handleValue), GetCurrentProcess(),
            out SafeFileHandle duplicate, 0, false, 2))
        {
            duplicate.Dispose();
            return null;
        }

        using (duplicate)
        using (SafeFileHandle expected = OpenJobObject(4, false, name))
        {
            if (expected.IsInvalid || !CompareObjectHandles(duplicate, expected))
            {
                return null;
            }

            IntPtr basic = Marshal.AllocHGlobal(56);
            try
            {
                int status = NtQueryObject(duplicate, 0, basic, 56, out _);
                if (status != 0)
                {
                    throw new InvalidOperationException($"NtQueryObject failed: 0x{status:x8}");
                }

                return unchecked((uint)Marshal.ReadInt32(basic, 4));
            }
            finally
            {
                Marshal.FreeHGlobal(basic);
            }
        }
    }

    private static SafeFileHandle OpenStdio(string path, bool input)
    {
        SecurityAttributes security = new() { Size = Marshal.SizeOf<SecurityAttributes>(), Inherit = 1 };
        SafeFileHandle handle = CreateFile(path, input ? 0x80000000u : 0x40000000u, 3, ref security,
            input ? 3u : 2u, 0, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return handle;
    }

    private static string Quote(string value)
    {
        StringBuilder result = new("\"");
        int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }

            result.Append('\\', character == '"' ? 2 * slashes + 1 : slashes).Append(character);
            slashes = 0;
        }

        return result.Append('\\', 2 * slashes).Append('"').ToString();
    }

    private static void Check(bool success)
    {
        if (!success)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Dispose()
    {
        _canary.Dispose();
        Witness.Dispose();
        _job.Dispose();
    }

    internal sealed record CreatedProcess(int Pid, long StartTicks, long WitnessHandle, long CanaryHandle, long FullJobHandle);
    internal sealed record Member(int Pid, string Process, int Session, bool InJob);
    internal sealed record Snapshot(uint ActiveProcesses, uint LimitFlags, Member[] Members);
    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { public int Size; public IntPtr Descriptor; public int Inherit; }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
        public ushort Show, ReservedBytes;
        public IntPtr ReservedPointer, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo Info; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Accounting
    {
        public long User, Kernel, PeriodUser, PeriodKernel;
        public uint Faults, Total, Active, Terminated;
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObject(ref SecurityAttributes security, string name);
    [DllImport("kernel32.dll", EntryPoint = "OpenJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle OpenJobObject(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int informationClass, out Accounting accounting, int size, IntPtr returned);
    [DllImport("kernel32.dll", EntryPoint = "QueryInformationJobObject", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryBuffer(SafeFileHandle job, int informationClass, IntPtr data, int size, IntPtr returned);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, SafeFileHandle? job, [MarshalAs(UnmanagedType.Bool)] out bool result);
    [DllImport("kernel32.dll", EntryPoint = "IsProcessInJob", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInAnyJob(IntPtr process, IntPtr job, [MarshalAs(UnmanagedType.Bool)] out bool result);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKernelObjectSecurity(SafeFileHandle handle, uint information, byte[]? descriptor, uint size, out uint needed);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKernelObjectSecurity(SafeFileHandle handle, uint information, IntPtr descriptor);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, IntPtr data, int size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, int revision, out IntPtr descriptor, out uint size);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr source, IntPtr targetProcess, out SafeFileHandle target,
        uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);
    [DllImport("kernelbase.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CompareObjectHandles(SafeFileHandle first, SafeFileHandle second);
    [DllImport("ntdll.dll")]
    private static extern int NtQueryObject(SafeFileHandle handle, int informationClass, IntPtr information, int size, out int returned);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, ref SecurityAttributes security,
        uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment, string directory,
        ref StartupInfoEx startup, out ProcessInformation information);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeFileHandle process, out long created, out long exited, out long kernel, out long user);
}
