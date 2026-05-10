using System.Windows;

namespace AniNest.Presentation.Animations;

public static class SelectableOptionState
{
    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.RegisterAttached(
            "IsSelected",
            typeof(bool),
            typeof(SelectableOptionState),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetIsSelected(DependencyObject obj) => (bool)obj.GetValue(IsSelectedProperty);

    public static void SetIsSelected(DependencyObject obj, bool value) => obj.SetValue(IsSelectedProperty, value);
}
