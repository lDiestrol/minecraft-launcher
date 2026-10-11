using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Launcher.App.Dialogs;
using Launcher.App.ViewModels;
using Xunit.Abstractions;

namespace Launcher.App.Tests;

public sealed class WindowChromeBehaviorTests(ITestOutputHelper output)
{
    [Fact]
    public void CaptionButtonsExecuteTheirWindowActions()
    {
        RunInSta(() =>
        {
            // Load production resources without starting networking or writing real settings.
            Application application = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/MinecraftLauncher;component/Themes/DarkGaming.xaml", UriKind.Relative),
            });
            application.Resources.Add("BooleanToVisibilityConverter", new BooleanToVisibilityConverter());

            AssertBrush(application, "TextBrush", "#EAF3F7");
            AssertBrush(application, "SecondaryTextBrush", "#B5C9D4");
            AssertBrush(application, "MutedBrush", "#91AAB9");
            AssertBrush(application, "AccentBrush", "#28D4C5");
            AssertBrush(application, "DangerTextBrush", "#FF929E");

            Style textBlockStyle = Assert.IsType<Style>(application.FindResource(typeof(TextBlock)));
            Setter foregroundSetter = Assert.Single(
                textBlockStyle.Setters.OfType<Setter>(),
                setter => setter.Property == TextBlock.ForegroundProperty);
            Assert.Equal(
                ((SolidColorBrush)application.FindResource("TextBrush")).Color,
                Assert.IsType<SolidColorBrush>(foregroundSetter.Value).Color);

            ManagedPackRemovalDialog removalDialog = new("Test profile");
            Assert.Equal(
                ((SolidColorBrush)application.FindResource("TextBrush")).Color,
                Assert.IsType<SolidColorBrush>(removalDialog.Foreground).Color);

            MainWindow window = new()
            {
                ShowInTaskbar = false,
            };

            window.Show();

            AssertLaunchButtonsLayout(window, output);

            Button minimizeButton = FindButton(window, "Свернуть окно");
            Button maximizeButton = FindButton(window, "Развернуть окно");
            Button closeButton = FindButton(window, "Закрыть окно");

            Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(minimizeButton));
            Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(maximizeButton));
            Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(closeButton));

            Assert.True(minimizeButton.IsEnabled);
            Assert.True(maximizeButton.IsEnabled);
            Assert.True(closeButton.IsEnabled);
            Assert.True(minimizeButton.Focusable);
            Assert.True(maximizeButton.Focusable);
            Assert.True(closeButton.Focusable);
            AssertCaptionButtonSize(minimizeButton);
            AssertCaptionButtonSize(maximizeButton);
            AssertCaptionButtonSize(closeButton);

            InvokeClick(minimizeButton);
            Assert.Equal(WindowState.Minimized, window.WindowState);

            window.WindowState = WindowState.Normal;
            InvokeClick(maximizeButton);
            Assert.Equal(WindowState.Maximized, window.WindowState);

            InvokeClick(maximizeButton);
            Assert.Equal(WindowState.Normal, window.WindowState);

            bool wasClosed = false;
            window.Closed += (_, _) => wasClosed = true;
            InvokeClick(closeButton);
            Assert.True(wasClosed);

            application.Shutdown();
        });
    }

    private static void AssertCaptionButtonSize(Button button)
    {
        Assert.Equal(46, button.Width);
        Assert.Equal(41, button.Height);
        Assert.True(button.UseLayoutRounding);

        DpiScale dpi = VisualTreeHelper.GetDpi(button);
        // WPF rounds physical pixels using midpoint-to-even; layout remains in DIP.
        double expectedWidth = Math.Round(46 * dpi.DpiScaleX, MidpointRounding.ToEven) / dpi.DpiScaleX;
        double expectedHeight = Math.Round(41 * dpi.DpiScaleY, MidpointRounding.ToEven) / dpi.DpiScaleY;

        // Allow only one representable double step, not a pixel-sized layout tolerance.
        Assert.Equal(expectedWidth, button.ActualWidth, tolerance: Math.BitIncrement(expectedWidth) - expectedWidth);
        Assert.Equal(expectedHeight, button.ActualHeight, tolerance: Math.BitIncrement(expectedHeight) - expectedHeight);
    }

    private static void AssertLaunchButtonsLayout(MainWindow window, ITestOutputHelper output)
    {
        MainViewModelLaunchTests.Fixture fixture = new();
        MainViewModel viewModel = fixture.CreateViewModelAsync().GetAwaiter().GetResult();
        viewModel.DismissUpdateOfferCommand.Execute(null);
        window.Width = 900;
        window.Height = 650;
        window.DataContext = viewModel;
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        window.UpdateLayout();

        Button serverButton = FindButton(window, "Играть");
        Button singleplayerButton = FindButton(window, "Одиночная игра");
        Assert.Same(viewModel.PlayCommand, serverButton.Command);
        Assert.Same(viewModel.SingleplayerCommand, singleplayerButton.Command);
        Assert.True(serverButton.IsVisible);
        Assert.True(singleplayerButton.IsVisible);
        output.WriteLine($"Native window: {window.ActualWidth}x{window.ActualHeight} DIP; DPI X/Y={VisualTreeHelper.GetDpi(serverButton).DpiScaleX}/{VisualTreeHelper.GetDpi(serverButton).DpiScaleY}.");
        Assert.True(serverButton.Focusable);
        Assert.True(singleplayerButton.Focusable);
        Assert.True(KeyboardNavigation.GetIsTabStop(serverButton));
        Assert.True(KeyboardNavigation.GetIsTabStop(singleplayerButton));
        Assert.True(serverButton.Focus());
        Assert.True(serverButton.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
        Assert.Same(singleplayerButton, Keyboard.FocusedElement);
        AssertButtonsFitViewport(window, serverButton, singleplayerButton);

        // Exercise the actual dashboard at synthetic DPI without changing monitor settings.
        FrameworkElement content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
        content.DataContext = viewModel;
        window.Content = null;
        try
        {
            foreach (double scale in new[] { 1d, 1.25d, 1.5d, 1.75d, 2d })
            {
                VisualTreeHelper.SetRootDpi(content, new DpiScale(scale, scale));
                content.Measure(new Size(900, 650));
                content.Arrange(new Rect(0, 0, 900, 650));
                content.UpdateLayout();
                Assert.Equal(scale, VisualTreeHelper.GetDpi(serverButton).DpiScaleX);
                Assert.Equal(scale, VisualTreeHelper.GetDpi(singleplayerButton).DpiScaleY);
                AssertButtonsFitViewport(content, serverButton, singleplayerButton);
                output.WriteLine($"Synthetic dashboard DPI {scale * 100}%: both buttons fit viewport.");
            }
        }
        finally
        {
            window.Content = content;
            content.ClearValue(FrameworkElement.DataContextProperty);
            window.UpdateLayout();
        }
    }

    private static void AssertButtonsFitViewport(Visual root, Button serverButton, Button singleplayerButton)
    {
        Assert.Equal(Visibility.Visible, serverButton.Visibility);
        Assert.Equal(Visibility.Visible, singleplayerButton.Visibility);
        Assert.True(serverButton.ActualWidth > 200);
        Assert.True(singleplayerButton.ActualWidth > 200);
        Assert.True(serverButton.ActualHeight >= 65);
        Assert.True(singleplayerButton.ActualHeight >= 43);
        ScrollViewer viewport = FindAncestor<ScrollViewer>(singleplayerButton);
        Rect serverBounds = serverButton.TransformToAncestor(viewport).TransformBounds(new Rect(serverButton.RenderSize));
        Rect singleplayerBounds = singleplayerButton.TransformToAncestor(viewport).TransformBounds(new Rect(singleplayerButton.RenderSize));
        Assert.True(serverBounds.Top >= 0);
        Assert.True(serverBounds.Left >= 0);
        Assert.True(singleplayerBounds.Left >= 0);
        Assert.True(serverBounds.Bottom <= singleplayerBounds.Top);
        Assert.True(singleplayerBounds.Right <= viewport.ActualWidth);
        Assert.True(singleplayerBounds.Bottom <= viewport.ActualHeight,
            $"Singleplayer button bottom {singleplayerBounds.Bottom} exceeds viewport height {viewport.ActualHeight} at DPI {VisualTreeHelper.GetDpi(root).DpiScaleY}.");
    }

    private static T FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T ancestor)
            {
                return ancestor;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        throw new InvalidOperationException($"Ancestor '{typeof(T).Name}' was not found.");
    }

    private static void AssertBrush(Application application, string key, string expected)
    {
        SolidColorBrush brush = Assert.IsType<SolidColorBrush>(application.FindResource(key));
        Color expectedColor = (Color)ColorConverter.ConvertFromString(expected)!;
        Assert.Equal(expectedColor, brush.Color);
    }

    private static Button FindButton(DependencyObject root, string automationName)
    {
        if (root is Button button && AutomationProperties.GetName(button) == automationName)
        {
            return button;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            Button? match = TryFindButton(VisualTreeHelper.GetChild(root, index), automationName);
            if (match is not null)
            {
                return match;
            }
        }

        throw new InvalidOperationException($"Button '{automationName}' was not found.");
    }

    private static Button? TryFindButton(DependencyObject root, string automationName)
    {
        if (root is Button button && AutomationProperties.GetName(button) == automationName)
        {
            return button;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            Button? match = TryFindButton(VisualTreeHelper.GetChild(root, index), automationName);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static void InvokeClick(Button button)
    {
        MethodInfo onClick = typeof(ButtonBase).GetMethod(
            "OnClick",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ButtonBase.OnClick was not found.");

        onClick.Invoke(button, null);
    }

    private static void RunInSta(Action action)
    {
        Exception? capturedException = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                capturedException = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            throw new AggregateException(capturedException);
        }
    }
}
