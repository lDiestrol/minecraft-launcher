using System.Windows;

namespace Launcher.App.Dialogs;

public partial class ManagedPackRemovalDialog : Window
{
    public ManagedPackRemovalDialog(string profileName)
    {
        InitializeComponent();
        ProfileNameText.Text = profileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Delete_Click(object sender, RoutedEventArgs e) => DialogResult = true;

}
