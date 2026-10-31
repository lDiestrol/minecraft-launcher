using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class GuardianLifecycleHost
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args[0] == "guardian-lifecycle-exit")
        {
            Console.Out.WriteLine("root-output");
            Console.Error.WriteLine("root-error");
            return 37;
        }

        if (args[0] == "guardian-lifecycle-child")
        {
            using Process self = Process.GetCurrentProcess();
            string temporary = args[1] + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new
            {
                PID = self.Id,
                BirthFILETIME = self.StartTime.ToUniversalTime().ToFileTimeUtc(),
                ImagePath = Environment.ProcessPath,
                RootPID = int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture),
            }));
            File.Move(temporary, args[1]);
            long began = Stopwatch.GetTimestamp();
            while (!File.Exists(args[2]) && Stopwatch.GetElapsedTime(began) < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(10);
            }

            return File.Exists(args[2]) ? 0 : 98;
        }

        // Keep the root output EOF independent from the live child's lifetime.
        foreach (int kind in new[] { -11, -12 })
        {
            IntPtr handle = GetStdHandle(kind);
            if (handle != IntPtr.Zero && handle != new IntPtr(-1) && !SetHandleInformation(handle, 1, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        ProcessStartInfo start = new(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("guardian-lifecycle-child");
        start.ArgumentList.Add(args[1]);
        start.ArgumentList.Add(args[2]);
        start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using Process child = Process.Start(start)!;
        string spawnTemporary = args[1] + ".spawn.tmp";
        await File.WriteAllTextAsync(spawnTemporary, JsonSerializer.Serialize(new
        {
            PID = child.Id,
            BirthFILETIME = child.StartTime.ToUniversalTime().ToFileTimeUtc(),
            ImagePath = Environment.ProcessPath,
            RootPID = Environment.ProcessId,
        }));
        File.Move(spawnTemporary, args[1] + ".spawn.json");
        long beganWaiting = Stopwatch.GetTimestamp();
        while (!File.Exists(args[1]) && Stopwatch.GetElapsedTime(beganWaiting) < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(10);
        }

        Console.Out.WriteLine("root-output");
        Console.Error.WriteLine("root-error");
        return File.Exists(args[1]) ? 37 : 99;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int kind);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
}
