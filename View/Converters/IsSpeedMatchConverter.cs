using System;
using System.Globalization;
using System.Windows.Data;

namespace LocalPlayer.View.Converters;

public class IsSpeedMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is float option && values[1] is float current)
            return Math.Abs(option - current) < 0.001f;
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
