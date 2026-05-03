using System;
using System.Globalization;
using System.Windows.Data;

namespace LocalPlayer.View.Converters;

public class LanguageMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is string code && values[1] is string current)
            return code == current;
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
