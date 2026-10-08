using System.Diagnostics;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Infrastructure.Persistence;

public sealed class ProfileFileManager : IProfileFileManager
{
    private readonly LauncherDataPaths _paths;
    private readonly IAppLogger _logger;

    public ProfileFileManager(LauncherDataPaths paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public string GetOrCreateDirectory(string profileId, ProfileDirectoryKind directoryKind)
    {
        Directory.CreateDirectory(_paths.InstancesDirectory);
        EnsureNotReparsePoint(_paths.InstancesDirectory);

        string instanceDirectory = _paths.GetInstanceDirectory(profileId);
        Directory.CreateDirectory(instanceDirectory);
        EnsureNotReparsePoint(instanceDirectory);

        string directory = directoryKind switch
        {
            ProfileDirectoryKind.Game => instanceDirectory,
            ProfileDirectoryKind.Mods => ResolveChild(instanceDirectory, "mods"),
            ProfileDirectoryKind.ResourcePacks => ResolveChild(instanceDirectory, "resourcepacks"),
            ProfileDirectoryKind.ShaderPacks => ResolveChild(instanceDirectory, "shaderpacks"),
            _ => throw new ArgumentOutOfRangeException(nameof(directoryKind)),
        };

        Directory.CreateDirectory(directory);
        EnsureNotReparsePoint(directory);
        return directory;
    }

    public void OpenDirectory(string profileId, ProfileDirectoryKind directoryKind)
    {
        string directory = GetOrCreateDirectory(profileId, directoryKind);
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true,
            Verb = "open",
        });
        _logger.Info($"Opened profile directory: profile={profileId}, kind={directoryKind}, path={directory}.");
    }

    private static string ResolveChild(string instanceDirectory, string directoryName)
    {
        string root = Path.GetFullPath(instanceDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string child = Path.GetFullPath(Path.Combine(root, directoryName));
        if (!child.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved profile directory is outside the instance root.");
        }

        return child;
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException($"Profile directory cannot be a file-system link: {path}");
        }
    }
}
