using System;
using System.Globalization;
using System.Windows.Data;

namespace LocalPlayer.Presentation.Converters;

public class RateToOffsetConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is float rate)
        {
            return rate switch
            {
                2.0f => 0.0,
                1.5f => 34.0,
                1.25f => 68.0,
                1.0f => 102.0,
                0.75f => 136.0,
                0.5f => 170.0,
                _ => 0.0
            };
        }
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

