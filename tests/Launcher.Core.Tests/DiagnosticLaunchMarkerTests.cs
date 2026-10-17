using Launcher.InstallerDiagnosticHost;

namespace Launcher.Core.Tests;

public sealed class DiagnosticLaunchMarkerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LauncherMarkerTests-" + Guid.NewGuid().ToString("N"));
    private string Marker(DiagnosticLaunchMarker value) => Path.Combine(directory, DiagnosticLaunchMarker.FileName(value.LaunchId));
    public DiagnosticLaunchMarkerTests() => Directory.CreateDirectory(directory);

    private static DiagnosticLaunchMarker Expected() => new(1, Guid.NewGuid().ToString("N"), 1234, DateTime.UtcNow.Ticks);

    [Fact]
    public async Task SlowMainRequiresFreshMarkerNotProcessCreation()
    {
        DiagnosticLaunchMarker expected = Expected();
        Task waiting = DiagnosticLaunchMarker.WaitAsync(Marker(expected), expected, TimeSpan.FromSeconds(5));
        Assert.False(waiting.IsCompleted);
        expected.WriteAtomic(Marker(expected));
        await waiting;
    }

    [Fact]
    public async Task ReaderOfPreviousLaunchDoesNotBlockNewPublication()
    {
        DiagnosticLaunchMarker previous = Expected(), next = Expected();
        previous.WriteAtomic(Marker(previous));
        using (FileStream snapshot = new(Marker(previous), FileMode.Open, FileAccess.Read, FileShare.Read))
        using (StreamReader reader = new(snapshot))
        {
            next.WriteAtomic(Marker(next));
            Assert.Contains(previous.LaunchId, await reader.ReadToEndAsync());
            Assert.Contains(next.LaunchId, await File.ReadAllTextAsync(Marker(next)));
        }
        await DiagnosticLaunchMarker.WaitAsync(Marker(next), next, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DuplicateLaunchCannotOverwritePreviousConfirmation()
    {
        DiagnosticLaunchMarker expected = Expected();
        expected.WriteAtomic(Marker(expected));
        string original = await File.ReadAllTextAsync(Marker(expected));
        Assert.Throws<IOException>(() => (expected with { Pid = 4321 }).WriteAtomic(Marker(expected)));
        Assert.Equal(original, await File.ReadAllTextAsync(Marker(expected)));
    }

    [Fact]
    public async Task OldMarkerCannotConfirmMissingNewApplication()
    {
        DiagnosticLaunchMarker previous = Expected(), next = Expected();
        previous.WriteAtomic(Marker(previous));
        await Assert.ThrowsAsync<TimeoutException>(() => DiagnosticLaunchMarker.WaitAsync(Marker(next), next, TimeSpan.FromMilliseconds(60)));
        Assert.True(File.Exists(Marker(previous)));
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("retry")]
    [InlineData("reinstall")]
    public async Task EachCaseAcceptsOnlyItsOwnAtomicConfirmation(string scenario)
    {
        DiagnosticLaunchMarker previous = Expected(), next = Expected();
        previous.WriteAtomic(Marker(previous));
        Task waiting = DiagnosticLaunchMarker.WaitAsync(Marker(next), next, TimeSpan.FromSeconds(5));
        Assert.False(waiting.IsCompleted);
        next.WriteAtomic(Marker(next));
        await waiting;
        Assert.NotEqual(previous.LaunchId, next.LaunchId);
        Assert.False(string.IsNullOrEmpty(scenario));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    [Theory]
    [InlineData(2, 1234, 42)]
    [InlineData(1, 4321, 42)]
    [InlineData(1, 1234, 43)]
    [InlineData(1, 0, 42)]
    public void VersionPidAndStartTimeMustMatch(int version, int pid, long ticks)
    {
        DiagnosticLaunchMarker expected = Expected() with { StartUtcTicks = 42 };
        Assert.False(new DiagnosticLaunchMarker(version, expected.LaunchId, pid, ticks).Matches(expected));
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task InvalidMarkerNeverAuthorizesIntentCleanup(string text)
    {
        DiagnosticLaunchMarker expected = Expected();
        string intent = Path.Combine(directory, "installation-intent.json");
        await File.WriteAllTextAsync(intent, "pending intent sentinel");
        await File.WriteAllTextAsync(Marker(expected), text);
        await Assert.ThrowsAsync<TimeoutException>(() => DiagnosticLaunchMarker.WaitAsync(Marker(expected), expected, TimeSpan.FromMilliseconds(60)));
        Assert.Equal("pending intent sentinel", await File.ReadAllTextAsync(intent));
        Assert.Equal(text, await File.ReadAllTextAsync(Marker(expected)));
    }

    public void Dispose() => Directory.Delete(directory, true); // Only the unique test-owned directory.
}
