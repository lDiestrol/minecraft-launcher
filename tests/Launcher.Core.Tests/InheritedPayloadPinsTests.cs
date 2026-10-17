using System.Runtime.InteropServices;
using Launcher.InstallHelper;
using Microsoft.Win32.SafeHandles;

namespace Launcher.Core.Tests;

public sealed class InheritedPayloadPinsTests
{
    [Fact]
    public void ReadPinRemainsOpenButIsNoLongerInheritable()
    {
        if (!OperatingSystem.IsWindows()) return;
        string path = Path.Combine(Path.GetTempPath(), "LauncherPinTest-" + Guid.NewGuid().ToString("N"));
        using (FileStream created = new(path, FileMode.CreateNew, FileAccess.Write)) { }
        try
        {
            using FileStream original = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Assert.True(DuplicateHandle(GetCurrentProcess(), original.SafeFileHandle, GetCurrentProcess(), out SafeFileHandle pin, 0, true, 2));
            using (pin)
            {
                InheritedPayloadPins.RevokeFurtherInheritance(pin.DangerousGetHandle().ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
                Assert.True(InheritedPayloadPins.GetHandleInformation(pin.DangerousGetHandle(), out uint flags));
                Assert.Equal(0U, flags & 1);
                Assert.Equal(0, RandomAccess.GetLength(pin));
            }
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("1,1")]
    [InlineData("invalid")]
    public void InvalidHandleMetadataCannotAuthorizeHelperStartup(string? value) =>
        Assert.Throws<InvalidDataException>(() => InheritedPayloadPins.RevokeFurtherInheritance(value));

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(IntPtr sourceProcess, SafeFileHandle source, IntPtr targetProcess,
        out SafeFileHandle duplicate, uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);
}
