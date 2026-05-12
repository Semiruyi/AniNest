using System.Windows;
using System.Windows.Media;

namespace AniNest.Presentation.Behaviors;

public static class MenuVisualBehavior
{
    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.RegisterAttached(
            "SummaryText",
            typeof(string),
            typeof(MenuVisualBehavior),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.RegisterAttached(
            "IconData",
            typeof(Geometry),
            typeof(MenuVisualBehavior),
            new PropertyMetadata(null));

    public static void SetSummaryText(DependencyObject element, string value)
        => element.SetValue(SummaryTextProperty, value);

    public static string GetSummaryText(DependencyObject element)
        => (string)element.GetValue(SummaryTextProperty);

    public static void SetIconData(DependencyObject element, Geometry value)
        => element.SetValue(IconDataProperty, value);

    public static Geometry GetIconData(DependencyObject element)
        => (Geometry)element.GetValue(IconDataProperty);
}
