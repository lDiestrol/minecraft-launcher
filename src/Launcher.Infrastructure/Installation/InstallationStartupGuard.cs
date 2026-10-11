using System.ComponentModel;
using System.Text.Json;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Installation;

public static class InstallationStartupGuard
{
    public static bool TryAcquireVelopackStartup(string[] args, out IDisposable? lease)
    {
        lease = null;
        StorageRootResolver resolver;
        try { resolver = new StorageRootResolver(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or StorageRootException) { return false; }
        return TryAcquireVelopackStartup(args, resolver, out lease);
    }

    internal static bool TryInitializeVelopack(string[] args, StorageRootResolver resolver, Action initialize)
    {
        if (!TryAcquireVelopackStartup(args, resolver, out IDisposable? lease)) return false;
        using (lease) initialize(); // SDK failures are not swallowed as a storage error.
        return true;
    }

    private static bool TryAcquireVelopackStartup(string[] args, StorageRootResolver resolver, out IDisposable? lease)
    {
        lease = null;
        try
        {
            // Only the authenticated install hook runs under its owner's exclusive lease.
            // Ordinary startup must hold a shared lease THROUGH the SDK callback, not just check intent.
            InstallationIntent? state = InstallationIntentStore.Read(resolver.ControlDirectory);
            if (state is not null)
            {
                if (!IsOwnedInstallHook(args, state)) return false;
            }
            else
            {
                lease = resolver.AcquireStartupLease(); // Rechecks pending state under the lease.
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                   JsonException or Win32Exception or StorageRootException or InvalidDataException)
        {
            lease?.Dispose();
            return false;
        }

        return true;
    }

    internal static bool CanInitializeVelopack(string[] args, string control)
    {
        try
        {
            InstallationIntent? state = InstallationIntentStore.Read(control);
            if (state is null) return true;
            return IsOwnedInstallHook(args, state);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                   JsonException or Win32Exception or StorageRootException or InvalidDataException)
        {
            return false;
        }
    }

    private static bool IsOwnedInstallHook(string[] args, InstallationIntent state) =>
        args.Length == 2 && args[0] == "--veloapp-install" &&
        WindowsInstallationJob.ContainsCurrentProcess(WindowsInstallationJob.NameFor(state.Token));
}
