using FluentAssertions;
using LocalPlayer.View.Converters;
using Xunit;

namespace LocalPlayer.Tests.Converters;

public class IsSpeedMatchConverterTests
{
    private readonly IsSpeedMatchConverter _converter = new();

    [Fact]
    public void Convert_EqualValues_ReturnsTrue()
    {
        var result = _converter.Convert(new object[] { 1.5f, 1.5f }, null!, null!, null!);
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_DifferentValues_ReturnsFalse()
    {
        var result = _converter.Convert(new object[] { 1.0f, 2.0f }, null!, null!, null!);
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WithinEpsilon_Different_ReturnsFalse()
    {
        var result = _converter.Convert(new object[] { 1.0f, 1.001f }, null!, null!, null!);
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_ZeroValues_ReturnsTrue()
    {
        var result = _converter.Convert(new object[] { 0f, 0f }, null!, null!, null!);
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WrongType_ReturnsFalse()
    {
        var result = _converter.Convert(new object[] { "str", 1.0f }, null!, null!, null!);
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_SingleElement_ReturnsFalse()
    {
        var result = _converter.Convert(new object[] { 1.0f }, null!, null!, null!);
        result.Should().Be(false);
    }
}
