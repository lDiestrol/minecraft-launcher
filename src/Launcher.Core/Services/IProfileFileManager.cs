using Launcher.Core.Models;

namespace Launcher.Core.Services;

public interface IProfileFileManager
{
    string GetOrCreateDirectory(string profileId, ProfileDirectoryKind directoryKind);

    void OpenDirectory(string profileId, ProfileDirectoryKind directoryKind);
}
