using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace Launcher.App;

public partial class MainWindow : Window
{
    private const double CompactContentBreakpoint = 740;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        bool isMaximized = WindowState == WindowState.Maximized;
        MaximizeRestoreButton.Content = isMaximized ? "❐" : "□";
        AutomationProperties.SetName(
            MaximizeRestoreButton,
            isMaximized ? "Восстановить окно" : "Развернуть окно");
        WindowFrame.BorderThickness = isMaximized ? new Thickness(0) : new Thickness(1);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        Point screenPoint = PointToScreen(e.GetPosition(this));
        SystemCommands.ShowSystemMenu(this, screenPoint);
        e.Handled = true;
    }

    private void ApplyResponsiveLayout()
    {
        if (!IsInitialized)
        {
            return;
        }

        double availableContentWidth = Math.Max(0, ActualWidth - 218 - 60);
        bool useCompactLayout = availableContentWidth < CompactContentBreakpoint;

        ApplyResponsiveGrid(
            DashboardLayout,
            ProfileHero,
            DashboardSidebar,
            useCompactLayout,
            new GridLength(2, GridUnitType.Star),
            new GridLength(1, GridUnitType.Star),
            360,
            270,
            18);

        ApplyResponsiveGrid(
            SettingsLayout,
            SettingsPrimary,
            SettingsSecondary,
            useCompactLayout,
            new GridLength(1, GridUnitType.Star),
            new GridLength(1, GridUnitType.Star),
            300,
            300,
            16);
    }

    private static void ApplyResponsiveGrid(
        Grid layout,
        FrameworkElement primary,
        FrameworkElement secondary,
        bool useCompactLayout,
        GridLength primaryWidth,
        GridLength secondaryWidth,
        double primaryMinimumWidth,
        double secondaryMinimumWidth,
        double gutter)
    {
        if (useCompactLayout)
        {
            layout.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            layout.ColumnDefinitions[0].MinWidth = 0;
            layout.ColumnDefinitions[1].Width = new GridLength(0);
            layout.ColumnDefinitions[2].Width = new GridLength(0);
            layout.ColumnDefinitions[2].MinWidth = 0;

            layout.RowDefinitions[0].Height = GridLength.Auto;
            layout.RowDefinitions[1].Height = new GridLength(gutter);
            layout.RowDefinitions[2].Height = GridLength.Auto;

            Grid.SetRow(primary, 0);
            Grid.SetColumn(primary, 0);
            Grid.SetRow(secondary, 2);
            Grid.SetColumn(secondary, 0);
            return;
        }

        layout.ColumnDefinitions[0].Width = primaryWidth;
        layout.ColumnDefinitions[0].MinWidth = primaryMinimumWidth;
        layout.ColumnDefinitions[1].Width = new GridLength(gutter);
        layout.ColumnDefinitions[2].Width = secondaryWidth;
        layout.ColumnDefinitions[2].MinWidth = secondaryMinimumWidth;

        layout.RowDefinitions[0].Height = GridLength.Auto;
        layout.RowDefinitions[1].Height = new GridLength(0);
        layout.RowDefinitions[2].Height = new GridLength(0);

        Grid.SetRow(primary, 0);
        Grid.SetColumn(primary, 0);
        Grid.SetRow(secondary, 0);
        Grid.SetColumn(secondary, 2);
    }
}
