using FluentAssertions;
using LocalPlayer.Presentation.Converters;
using Xunit;

namespace LocalPlayer.Tests.Converters;

public class InverseBoolConverterTests
{
    private readonly InverseBoolConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsFalse()
    {
        _converter.Convert(true, null!, null!, null!).Should().Be(false);
    }

    [Fact]
    public void Convert_False_ReturnsTrue()
    {
        _converter.Convert(false, null!, null!, null!).Should().Be(true);
    }

    [Fact]
    public void Convert_NonBool_ReturnsFalse()
    {
        _converter.Convert("string", null!, null!, null!).Should().Be(false);
    }

    [Fact]
    public void ConvertBack_True_ReturnsFalse()
    {
        _converter.ConvertBack(true, null!, null!, null!).Should().Be(false);
    }

    [Fact]
    public void ConvertBack_False_ReturnsTrue()
    {
        _converter.ConvertBack(false, null!, null!, null!).Should().Be(true);
    }
}
