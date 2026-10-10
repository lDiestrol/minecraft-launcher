using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using Launcher.App.Dialogs;

namespace Launcher.App.Tests;

public sealed class WindowChromeBehaviorTests
{
    [Fact]
    public void CaptionButtonsExecuteTheirWindowActions()
    {
        RunInSta(() =>
        {
            App application = new();
            application.InitializeComponent();

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
