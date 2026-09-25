using System.Windows;
using System.Windows.Media;

namespace Launcher.App.Controls;

public static class ButtonAssist
{
    public static readonly DependencyProperty HoverBackgroundProperty = DependencyProperty.RegisterAttached(
        "HoverBackground",
        typeof(Brush),
        typeof(ButtonAssist),
        new FrameworkPropertyMetadata(Brushes.Transparent));

    public static Brush GetHoverBackground(DependencyObject element) =>
        (Brush)element.GetValue(HoverBackgroundProperty);

    public static void SetHoverBackground(DependencyObject element, Brush value) =>
        element.SetValue(HoverBackgroundProperty, value);
}
