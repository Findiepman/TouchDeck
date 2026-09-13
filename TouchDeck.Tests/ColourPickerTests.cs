using System.Windows.Media;
using TouchDeck.App.Configurator;
using TouchDeck.App.Rendering;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// The arithmetic behind the config center's colour picker. The picker itself needs a UI
/// thread; what is worth pinning down is that a colour survives the trip through the square
/// and the hue strip, and comes back out spelled the way config spells one.
/// </summary>
public sealed class ColourPickerTests
{
    [Theory]
    [InlineData("#FF3D7F")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#46B96B")]
    [InlineData("#4C9AFF")]
    [InlineData("#8A93A5")]
    [InlineData("#D9A441")]
    public void AColourSurvivesBeingTakenApartAndPutBackTogether(string hex)
    {
        Assert.True(StyleTranslator.TryColour(hex, out var colour));

        var (hue, saturation, brightness) = ColourPickerBox.ToHsv(colour);

        Assert.Equal(colour, ColourPickerBox.FromHsv(hue, saturation, brightness));
    }

    [Fact]
    public void EachSixthOfTheHueStripIsThePrimaryOrSecondaryItLooksLike()
    {
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), ColourPickerBox.FromHsv(0, 1, 1));
        Assert.Equal(Color.FromRgb(0xFF, 0xFF, 0x00), ColourPickerBox.FromHsv(60, 1, 1));
        Assert.Equal(Color.FromRgb(0x00, 0xFF, 0x00), ColourPickerBox.FromHsv(120, 1, 1));
        Assert.Equal(Color.FromRgb(0x00, 0xFF, 0xFF), ColourPickerBox.FromHsv(180, 1, 1));
        Assert.Equal(Color.FromRgb(0x00, 0x00, 0xFF), ColourPickerBox.FromHsv(240, 1, 1));
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0xFF), ColourPickerBox.FromHsv(300, 1, 1));
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), ColourPickerBox.FromHsv(360, 1, 1));
    }

    [Fact]
    public void TheCornersOfTheSquareAreWhiteAndBlackWhateverTheHue()
    {
        Assert.Equal(Colors.White, ColourPickerBox.FromHsv(210, 0, 1));
        Assert.Equal(Colors.Black, ColourPickerBox.FromHsv(210, 1, 0));
        Assert.Equal(Colors.Black, ColourPickerBox.FromHsv(210, 0, 0));
    }

    [Fact]
    public void AHueOutsideTheStripWrapsRatherThanClamping() =>
        Assert.Equal(ColourPickerBox.FromHsv(30, 1, 1), ColourPickerBox.FromHsv(390, 1, 1));

    [Fact]
    public void AnOrdinaryColourIsWrittenAsSixDigits() =>
        Assert.Equal("#FF3D7F", ColourPickerBox.Format(Color.FromRgb(0xFF, 0x3D, 0x7F)));

    [Fact]
    public void AColourThatIsNotFullyOpaqueKeepsItsAlphaByte() =>
        Assert.Equal("#80FF3D7F", ColourPickerBox.Format(Color.FromArgb(0x80, 0xFF, 0x3D, 0x7F)));

    [Theory]
    [InlineData("#FFF")]
    [InlineData("#FFFFFF")]
    [InlineData("#CCFFFFFF")]
    [InlineData("White")]
    [InlineData("  #FFFFFF  ")]
    public void EverySpellingOfAColourIsUnderstood(string text) =>
        Assert.True(StyleTranslator.TryColour(text, out _));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("#")]
    [InlineData("#FF3D7")]
    [InlineData("#GGGGGG")]
    [InlineData("not a colour")]
    public void AHalfTypedColourIsRefusedRatherThanTurnedIntoMagenta(string? text) =>
        Assert.False(StyleTranslator.TryColour(text, out _));
}
