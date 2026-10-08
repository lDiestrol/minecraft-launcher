using Launcher.Core.Models;

namespace Launcher.App.Services;

public interface IManagedPackRemovalConfirmation
{
    bool Confirm(GameProfile profile);
}
