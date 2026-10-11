using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Launcher.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace Launcher.Infrastructure.Game;

/// <summary>Creates the child atomically in its job; there is deliberately no unprotected fallback.</summary>
internal sealed class AtomicJobProcess : IDisposable
{
    private SafeFileHandle? _process;
    private readonly List<StreamReader> _readers = [];
    private readonly List<Task> _output = [];
    public int Id { get; private set; }
    public DateTime StartTimeUtc { get; private set; }
    public bool HasExited => _process is not null && WaitForSingleObject(_process, 0) == 0;
    public int ExitCode
    {
        get
        {
            if (_process is null || !HasExited || !GetExitCodeProcess(_process, out uint code))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Game exit code is unavailable.");
            }

            return unchecked((int)code);
        }
    }

    public void Start(ProcessStartInfo start, WindowsGameJob job, IAppLogger logger)
    {
        if (_process is not null || start.UseShellExecute || start.RedirectStandardInput)
        {
            throw new InvalidOperationException("Unsupported guarded process configuration.");
        }

        List<SafeFileHandle> childHandles = [];
        IntPtr attributes = IntPtr.Zero, jobList = IntPtr.Zero, handleList = IntPtr.Zero, environment = IntPtr.Zero;
        bool initialized = false;
        try
        {
            StartupInfoEx startup = new();
            startup.Info.Size = Marshal.SizeOf<StartupInfoEx>();
            startup.Info.Flags = 0x100; // STARTF_USESTDHANDLES
            startup.Info.Input = CreateStandardHandle(-10, false, childHandles, null);
            startup.Info.Output = CreateStandardHandle(-11, start.RedirectStandardOutput, childHandles,
                start.StandardOutputEncoding ?? Console.OutputEncoding);
            startup.Info.Error = CreateStandardHandle(-12, start.RedirectStandardError, childHandles,
                start.StandardErrorEncoding ?? Console.OutputEncoding);
            childHandles.Add(job.CreateInheritableQueryWitness());

            nuint size = 0;
            InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            attributes = Marshal.AllocHGlobal(checked((int)size));
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            initialized = true;
            jobList = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(jobList, job.Handle.DangerousGetHandle());
            handleList = Marshal.AllocHGlobal(IntPtr.Size * childHandles.Count);
            for (int index = 0; index < childHandles.Count; index++)
            {
                Marshal.WriteIntPtr(handleList, index * IntPtr.Size, childHandles[index].DangerousGetHandle());
            }

            // Dedicated guardian creates no other processes in this inheritance window.
            // Only stdio + QUERY witness are inherited, never the full-access job handle.
            // The JVM retains its witness until exit; descendants inside the job may inherit it.
            SetAttribute(attributes, 0x0002000D, jobList, IntPtr.Size);
            SetAttribute(attributes, 0x00020002, handleList, IntPtr.Size * childHandles.Count);
            startup.Attributes = attributes;
            string variables = string.Join('\0', start.Environment.Where(pair => pair.Value is not null)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
            environment = Marshal.StringToHGlobalUni(variables);
            string arguments = start.ArgumentList.Count == 0 ? start.Arguments : string.Join(' ', start.ArgumentList.Select(Quote));
            StringBuilder command = new(Quote(start.FileName) + (arguments.Length == 0 ? "" : " " + arguments));
            uint flags = 0x00080000 | 0x00000400 | (start.CreateNoWindow ? 0x08000000u : 0);
            if (!CreateProcess(start.FileName, command, IntPtr.Zero, IntPtr.Zero, true, flags,
                environment, start.WorkingDirectory, ref startup, out ProcessInformation information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Atomic job assignment failed; Minecraft was not launched.");
            }

            _process = new SafeFileHandle(information.Process, true);
            using SafeFileHandle thread = new(information.Thread, true);
            Id = checked((int)information.ProcessId);
            if (!GetProcessTimes(_process, out long created, out _, out _, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            StartTimeUtc = DateTime.FromFileTimeUtc(created);
            for (int index = 0; index < _readers.Count; index++)
            {
                StreamReader reader = _readers[index];
                bool error = start.RedirectStandardError && (!start.RedirectStandardOutput || index == 1);
                _output.Add(PumpAsync(reader, error, logger));
            }
        }
        finally
        {
            foreach (SafeFileHandle handle in childHandles)
            {
                handle.Dispose();
            }

            if (initialized)
            {
                DeleteProcThreadAttributeList(attributes);
            }

            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(jobList);
            Marshal.FreeHGlobal(handleList);
            Marshal.FreeHGlobal(environment);
            GC.KeepAlive(job);
        }
    }

    public async Task WaitForExitAsync()
    {
        SafeFileHandle process = _process ?? throw new InvalidOperationException("Game was not started.");
        await Task.Run(() =>
        {
            if (WaitForSingleObject(process, uint.MaxValue) != 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        });
        await Task.WhenAll(_output);
    }

    private IntPtr CreateStandardHandle(int kind, bool redirect, List<SafeFileHandle> handles, Encoding? encoding)
    {
        SecurityAttributes security = new() { Length = Marshal.SizeOf<SecurityAttributes>(), Inherit = 1 };
        SafeFileHandle child;
        if (redirect)
        {
            if (!CreatePipe(out SafeFileHandle reader, out child, ref security, 0))
            {
                reader.Dispose();
                child.Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!SetHandleInformation(reader, 1, 0))
            {
                reader.Dispose();
                child.Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            _readers.Add(new StreamReader(new FileStream(reader, FileAccess.Read), encoding!, true));
        }
        else
        {
            IntPtr standard = GetStdHandle(kind);
            if (standard != IntPtr.Zero && standard != new IntPtr(-1))
            {
                if (!DuplicateHandle(GetCurrentProcess(), standard, GetCurrentProcess(), out child, 0, true, 2))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            else
            {
                child = CreateFile("NUL", kind == -10 ? 0x80000000u : 0x40000000u, 3, ref security, 3, 0, IntPtr.Zero);
                if (child.IsInvalid)
                {
                    child.Dispose();
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        handles.Add(child);
        return child.DangerousGetHandle();
    }

    private static async Task PumpAsync(StreamReader reader, bool error, IAppLogger logger)
    {
        // Anonymous pipe reads are synchronous underneath: keep them off the caller/dispatcher.
        await Task.Run(async () =>
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                if (error)
                {
                    logger.Error($"[Minecraft] {line}");
                }
                else
                {
                    logger.Info($"[Minecraft] {line}");
                }
            }
        });
    }

    internal static string Quote(string value)
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

            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }

        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    private static void SetAttribute(IntPtr list, nuint attribute, IntPtr value, int bytes)
    {
        if (!UpdateProcThreadAttribute(list, 0, attribute, value, (nuint)bytes, IntPtr.Zero, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public void Dispose()
    {
        foreach (StreamReader reader in _readers)
        {
            reader.Dispose();
        }

        _process?.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; }
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returnSize);
    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity,
        IntPtr threadSecurity, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment,
        string directory, ref StartupInfoEx startup, out ProcessInformation information);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeFileHandle handle, uint timeout);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeFileHandle process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeFileHandle process, out long created, out long exited, out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(out SafeFileHandle reader, out SafeFileHandle writer, ref SecurityAttributes security, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int kind);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr source, IntPtr targetProcess,
        out SafeFileHandle target, uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, ref SecurityAttributes security,
        uint disposition, uint flags, IntPtr template);
}
