using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

using WpfPoint = System.Windows.Point;

namespace AniNest.Presentation.Converters;

public class ProgressToPieConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int percent = value is int i ? i : 0;
        if (percent <= 0 || percent >= 100) return Geometry.Empty;

        double size = 20; // matches CheckIcon
        double r = size / 2;
        double cx = r, cy = r;
        double angle = percent / 100.0 * 360.0;

        // Start at 12 o'clock
        double startAngle = -90;
        double endAngle = startAngle + angle;

        var start = PolarToCartesian(cx, cy, r, endAngle);
        var end = PolarToCartesian(cx, cy, r, startAngle);
        bool largeArc = angle > 180;

        var figure = new PathFigure { StartPoint = new WpfPoint(cx, cy) }; // center
        figure.Segments.Add(new LineSegment(new WpfPoint(cx, cy - r), true)); // to 12 o'clock
        figure.Segments.Add(new ArcSegment(start, new System.Windows.Size(r, r), 0, largeArc,
            SweepDirection.Clockwise, true));
        figure.IsClosed = true;

        return new PathGeometry(new[] { figure });
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static WpfPoint PolarToCartesian(double cx, double cy, double r, double angleDeg)
    {
        double rad = angleDeg * Math.PI / 180.0;
        return new WpfPoint(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }
}

public class ProgressToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int percent = value is int i ? i : 0;
        return percent > 0 && percent < 100 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double percent = value switch
        {
            double d => d,
            int i => i,
            float f => f,
            _ => 0
        };

        double totalWidth = parameter switch
        {
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            double d => d,
            int i => i,
            _ => 0
        };

        if (percent <= 0 || totalWidth <= 0)
            return 0d;

        if (percent >= 100)
            return totalWidth;

        return totalWidth * percent / 100.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

