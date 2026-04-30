using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace LocalPlayer.Converters;

public class KeyDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Key key) return "";

        if (key == Key.None) return "(未绑定)";

        return key.ToString()
            .Replace("Left", "←").Replace("Right", "→")
            .Replace("Up", "↑").Replace("Down", "↓")
            .Replace("Space", "空格").Replace("Escape", "Esc")
            .Replace("Return", "Enter")
            .Replace("PageUp", "PgUp").Replace("PageDown", "PgDn")
            .Replace("OemComma", ",").Replace("OemPeriod", ".")
            .Replace("OemMinus", "-").Replace("OemPlus", "=")
            .Replace("OemQuestion", "/").Replace("OemSemicolon", ";")
            .Replace("OemQuotes", "\"").Replace("OemOpenBrackets", "[")
            .Replace("OemCloseBrackets", "]").Replace("OemPipe", "\\")
            .Replace("OemTilde", "~")
            .Replace("D0", "0").Replace("D1", "1").Replace("D2", "2")
            .Replace("D3", "3").Replace("D4", "4").Replace("D5", "5")
            .Replace("D6", "6").Replace("D7", "7").Replace("D8", "8")
            .Replace("D9", "9")
            .Replace("NumPad0", "Num0").Replace("NumPad1", "Num1")
            .Replace("NumPad2", "Num2").Replace("NumPad3", "Num3")
            .Replace("NumPad4", "Num4").Replace("NumPad5", "Num5")
            .Replace("NumPad6", "Num6").Replace("NumPad7", "Num7")
            .Replace("NumPad8", "Num8").Replace("NumPad9", "Num9")
            .Replace("Add", "+").Replace("Subtract", "-")
            .Replace("Multiply", "*").Replace("Divide", "/")
            .Replace("Decimal", ".");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
