using System.Windows.Input;
using FluentAssertions;
using LocalPlayer.Presentation.Converters;
using Xunit;

namespace LocalPlayer.Tests.ViewModel;

public class KeyDisplayConverterTests
{
    [Theory]
    [InlineData(Key.Space, "\u7a7a\u683c")]
    [InlineData(Key.Left, "\u2190")]
    [InlineData(Key.Right, "\u2192")]
    [InlineData(Key.Up, "\u2191")]
    [InlineData(Key.Down, "\u2193")]
    [InlineData(Key.Escape, "Esc")]
    [InlineData(Key.Enter, "Enter")]
    [InlineData(Key.A, "A")]
    [InlineData(Key.D1, "1")]
    [InlineData(Key.D9, "9")]
    [InlineData(Key.OemComma, ",")]
    [InlineData(Key.OemPeriod, ".")]
    [InlineData(Key.None, "(\u672a\u7ed1\u5b9a)")]
    [InlineData(Key.NumPad0, "Num0")]
    [InlineData(Key.NumPad9, "Num9")]
    [InlineData(Key.Add, "+")]
    [InlineData(Key.Subtract, "-")]
    public void Format_ReturnsExpected(Key key, string expected)
    {
        KeyDisplayConverter.Format(key).Should().Be(expected);
    }

    [Fact]
    public void Convert_Key_DelegatesToFormat()
    {
        var converter = new KeyDisplayConverter();
        converter.Convert(Key.Space, null!, null!, null!).Should().Be("\u7a7a\u683c");
    }

    [Fact]
    public void Convert_NonKey_ReturnsEmpty()
    {
        var converter = new KeyDisplayConverter();
        converter.Convert("not a key", null!, null!, null!).Should().Be("");
    }
}
