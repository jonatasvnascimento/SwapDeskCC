using SwapDeskCC.Core.Models;

namespace SwapDeskCC.Core.Tests;

public class ColorTextTests
{
    [Theory]
    [InlineData("#c96442", "#c96442")]
    [InlineData("#C96442", "#c96442")]
    [InlineData("#abc", "#aabbcc")]
    [InlineData("#c9644280", "#80c96442")]          // CSS RRGGBBAA → Avalonia AARRGGBB
    [InlineData("rgb(201, 100, 66)", "#c96442")]
    [InlineData("rgba(201, 100, 66, 1)", "#c96442")]
    [InlineData("rgba(201,100,66,0.5)", "#80c96442")]
    [InlineData("RGBA( 201 , 100 , 66 , 50% )", "#80c96442")]
    [InlineData("rgb(201 100 66 / 0.5)", "#80c96442")]
    [InlineData("rgba(0, 0, 0, 0)", "#00000000")]
    public void Parses_hex_and_rgba(string input, string expected)
    {
        Assert.True(ColorText.TryParseInput(input, out var stored));
        Assert.Equal(expected, stored);
    }

    [Theory]
    [InlineData("")]
    [InlineData("vermelho")]
    [InlineData("#12345")]
    [InlineData("rgba(300, 0, 0, 1)")]
    [InlineData("rgba(10, 10, 10, 1.5)")]
    [InlineData("rgb(10, 10)")]
    public void Rejects_invalid(string input) => Assert.False(ColorText.TryParseInput(input, out _));

    [Theory]
    [InlineData("#c96442", "rgba(201, 100, 66, 1)")]
    [InlineData("#80c96442", "rgba(201, 100, 66, 0.5)")]
    public void Formats_stored_as_rgba(string stored, string expected) =>
        Assert.Equal(expected, ColorText.ToRgba(stored));

    [Fact]
    public void Round_trips_through_rgba()
    {
        Assert.True(ColorText.TryParseInput(ColorText.ToRgba("#80c96442"), out var back));
        Assert.True(ColorText.SameColor("#80C96442", back));
    }
}
