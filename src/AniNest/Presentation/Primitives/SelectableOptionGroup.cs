using System.Windows;
using System.Windows.Controls;

namespace AniNest.Presentation.Primitives;

public class SelectableOptionGroup : ContentControl
{
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SelectableOptionGroup),
            new PropertyMetadata(-1));

    public static readonly DependencyProperty HighlightStyleProperty =
        DependencyProperty.Register(
            nameof(HighlightStyle),
            typeof(Style),
            typeof(SelectableOptionGroup),
            new PropertyMetadata(null));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public Style? HighlightStyle
    {
        get => (Style?)GetValue(HighlightStyleProperty);
        set => SetValue(HighlightStyleProperty, value);
    }
}
