using System.Windows;
using Launcher.App.Dialogs;
using Launcher.Core.Models;

namespace Launcher.App.Services;

public sealed class ManagedPackRemovalConfirmation : IManagedPackRemovalConfirmation
{
    public bool Confirm(GameProfile profile)
    {
        ManagedPackRemovalDialog dialog = new(profile.Name)
        {
            Owner = Application.Current.MainWindow,
        };
        return dialog.ShowDialog() == true;
    }
}
