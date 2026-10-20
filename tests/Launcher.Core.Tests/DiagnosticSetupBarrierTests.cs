using Launcher.InstallerDiagnosticHost;

namespace Launcher.Core.Tests;

public sealed class DiagnosticSetupBarrierTests
{
    [Fact]
    public void MissingVariablesDoNotEnableTheGate()
    {
        Assert.False(DiagnosticSetupBarrier.IsRequested(null, null));
        Assert.True(DiagnosticSetupBarrier.IsRequested("C:\\temp", null));
        Assert.True(DiagnosticSetupBarrier.IsRequested(null, Guid.NewGuid().ToString("N")));
    }

    [Theory]
    [InlineData(null, "123")]
    [InlineData("C:\\Temp", null)]
    [InlineData("C:\\Temp", "not-a-guid")]
    [InlineData("relative", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData(@"\\\\server\\share", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void InvalidConfigurationIsRejected(string? root, string? id)
    {
        Assert.ThrowsAny<Exception>(() => DiagnosticSetupBarrier.Validate(root, id));
    }

    [Fact]
    public async Task ContinueSignalCreatesDistinctDurableReceipts()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = Path.Combine(Path.GetTempPath(), "diagnostic-setup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string id = Guid.NewGuid().ToString("N");
        try
        {
            Task waiting = Task.Run(() => DiagnosticSetupBarrier.Run(root, id, TimeSpan.FromSeconds(5)));
            string ready = Path.Combine(root, "setup-hook-ready-" + id + ".json");
            try
            {
                bool found = false;
                for (int i = 0; i < 200; i++)
                {
                    if (File.Exists(ready)) { found = true; break; }
                    await Task.Delay(10);
                }
                Assert.True(found, "Hook never published ready receipt");
                using EventWaitHandle controller = EventWaitHandle.OpenExisting(@"Local\MinecraftLauncher.SetupHook." + id);
                Assert.True(controller.Set());
                await waiting.WaitAsync(TimeSpan.FromSeconds(6));
                Assert.True(File.Exists(Path.Combine(root, "setup-hook-continued-" + id + ".json")));
                Assert.False(File.Exists(Path.Combine(root, "setup-hook-timeout-" + id + ".json")));
                Assert.Throws<IOException>(() => DiagnosticSetupBarrier.Run(root, id, TimeSpan.FromMilliseconds(10)));
            }
            finally { await waiting.WaitAsync(TimeSpan.FromSeconds(6)); }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void TimeoutIsBoundedAndLeavesDistinctReceipt()
    {
        if (!OperatingSystem.IsWindows()) return;
        string root = Path.Combine(Path.GetTempPath(), "diagnostic-setup-timeout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string id = Guid.NewGuid().ToString("N");
        try
        {
            Assert.Throws<TimeoutException>(() => DiagnosticSetupBarrier.Run(root, id, TimeSpan.FromMilliseconds(20)));
            Assert.True(File.Exists(Path.Combine(root, "setup-hook-ready-" + id + ".json")));
            Assert.True(File.Exists(Path.Combine(root, "setup-hook-timeout-" + id + ".json")));
            Assert.False(File.Exists(Path.Combine(root, "setup-hook-continued-" + id + ".json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
