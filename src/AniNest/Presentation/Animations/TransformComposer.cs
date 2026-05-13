using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;

namespace AniNest.Presentation.Animations;

public static class TransformComposer
{
    private static readonly DependencyProperty PrimaryScaleTransformProperty =
        DependencyProperty.RegisterAttached(
            "PrimaryScaleTransform",
            typeof(ScaleTransform),
            typeof(TransformComposer),
            new PropertyMetadata(null));

    private static readonly DependencyProperty LayoutTranslateTransformProperty =
        DependencyProperty.RegisterAttached(
            "LayoutTranslateTransform",
            typeof(TranslateTransform),
            typeof(TransformComposer),
            new PropertyMetadata(null));

    public static ScaleTransform EnsurePrimaryScaleTransform(UIElement element)
    {
        if (TryGetStoredTransform(element, PrimaryScaleTransformProperty, out ScaleTransform? existing))
            return existing!;

        switch (element.RenderTransform)
        {
            case ScaleTransform directScale when !directScale.IsFrozen:
                element.SetValue(PrimaryScaleTransformProperty, directScale);
                return directScale;

            case ScaleTransform directScale:
            {
                var clonedScale = directScale.CloneCurrentValue();
                element.RenderTransform = clonedScale;
                element.SetValue(PrimaryScaleTransformProperty, clonedScale);
                return clonedScale;
            }

            case TransformGroup group:
            {
                var mutableGroup = group.IsFrozen ? group.CloneCurrentValue() : group;
                var groupScale = mutableGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (groupScale == null)
                {
                    groupScale = new ScaleTransform(1, 1);
                    mutableGroup.Children.Insert(0, groupScale);
                }

                if (!ReferenceEquals(mutableGroup, group))
                    element.RenderTransform = mutableGroup;

                element.SetValue(PrimaryScaleTransformProperty, groupScale);
                return groupScale;
            }

            case null:
            case MatrixTransform matrixTransform when ReferenceEquals(matrixTransform, Transform.Identity):
            {
                var createdScale = new ScaleTransform(1, 1);
                element.RenderTransform = createdScale;
                element.SetValue(PrimaryScaleTransformProperty, createdScale);
                return createdScale;
            }

            default:
            {
                var group = new TransformGroup();
                var createdScale = new ScaleTransform(1, 1);
                group.Children.Add(createdScale);
                group.Children.Add(element.RenderTransform);
                element.RenderTransform = group;
                element.SetValue(PrimaryScaleTransformProperty, createdScale);
                return createdScale;
            }
        }
    }

    public static TranslateTransform EnsureLayoutTranslateTransform(UIElement element)
    {
        if (TryGetStoredTransform(element, LayoutTranslateTransformProperty, out TranslateTransform? existing))
            return existing!;

        switch (element.RenderTransform)
        {
            case TranslateTransform directTranslate when !directTranslate.IsFrozen:
                element.SetValue(LayoutTranslateTransformProperty, directTranslate);
                return directTranslate;

            case TranslateTransform directTranslate:
            {
                var clonedTranslate = directTranslate.CloneCurrentValue();
                element.RenderTransform = clonedTranslate;
                element.SetValue(LayoutTranslateTransformProperty, clonedTranslate);
                return clonedTranslate;
            }

            case TransformGroup group:
            {
                var mutableGroup = group.IsFrozen ? group.CloneCurrentValue() : group;
                var translate = new TranslateTransform();
                mutableGroup.Children.Add(translate);

                if (!ReferenceEquals(mutableGroup, group))
                    element.RenderTransform = mutableGroup;

                element.SetValue(LayoutTranslateTransformProperty, translate);
                return translate;
            }

            case null:
            case MatrixTransform matrixTransform when ReferenceEquals(matrixTransform, Transform.Identity):
            {
                var createdTranslate = new TranslateTransform();
                element.RenderTransform = createdTranslate;
                element.SetValue(LayoutTranslateTransformProperty, createdTranslate);
                return createdTranslate;
            }

            default:
            {
                var group = new TransformGroup();
                var translate = new TranslateTransform();
                group.Children.Add(element.RenderTransform);
                group.Children.Add(translate);
                element.RenderTransform = group;
                element.SetValue(LayoutTranslateTransformProperty, translate);
                return translate;
            }
        }
    }

    public static bool TryGetPrimaryScaleTransform(UIElement element, [NotNullWhen(true)] out ScaleTransform? transform)
        => TryGetStoredTransform(element, PrimaryScaleTransformProperty, out transform);

    public static bool TryGetLayoutTranslateTransform(UIElement element, [NotNullWhen(true)] out TranslateTransform? transform)
        => TryGetStoredTransform(element, LayoutTranslateTransformProperty, out transform);

    private static bool TryGetStoredTransform<TTransform>(
        UIElement element,
        DependencyProperty property,
        [NotNullWhen(true)]
        out TTransform? transform)
        where TTransform : Transform
    {
        if (element.ReadLocalValue(property) is TTransform stored && ContainsTransform(element.RenderTransform, stored))
        {
            transform = stored;
            return true;
        }

        transform = null;
        element.ClearValue(property);
        return false;
    }

    private static bool ContainsTransform(Transform? root, Transform target)
    {
        if (root == null)
            return false;

        if (ReferenceEquals(root, target))
            return true;

        if (root is not TransformGroup group)
            return false;

        for (int i = 0; i < group.Children.Count; i++)
        {
            if (ContainsTransform(group.Children[i], target))
                return true;
        }

        return false;
    }
}
