using System.Windows.Input;
using FluentAssertions;
using LocalPlayer.ViewModel;
using Xunit;

namespace LocalPlayer.Tests.ViewModel;

public class KeyDisplayConverterTests
{
    [Theory]
    [InlineData(Key.Space, "空格")]
    [InlineData(Key.Left, "←")]
    [InlineData(Key.Right, "→")]
    [InlineData(Key.Up, "↑")]
    [InlineData(Key.Down, "↓")]
    [InlineData(Key.Escape, "Esc")]
    [InlineData(Key.Enter, "Enter")]
    [InlineData(Key.A, "A")]
    [InlineData(Key.D1, "1")]
    [InlineData(Key.D9, "9")]
    [InlineData(Key.OemComma, ",")]
    [InlineData(Key.OemPeriod, ".")]
    [InlineData(Key.None, "(未绑定)")]
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
        converter.Convert(Key.Space, null!, null!, null!).Should().Be("空格");
    }

    [Fact]
    public void Convert_NonKey_ReturnsEmpty()
    {
        var converter = new KeyDisplayConverter();
        converter.Convert("not a key", null!, null!, null!).Should().Be("");
    }
}
