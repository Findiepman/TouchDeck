using TouchDeck.Core.Input;
using Xunit;

namespace TouchDeck.Tests;

public class HotkeyParserTests
{
    [Theory]
    [InlineData("a", VirtualKey.A)]
    [InlineData("A", VirtualKey.A)]
    [InlineData("5", VirtualKey.D5)]
    [InlineData("f13", VirtualKey.F13)]
    [InlineData("F24", VirtualKey.F24)]
    [InlineData("esc", VirtualKey.Escape)]
    [InlineData("escape", VirtualKey.Escape)]
    [InlineData("numpad7", VirtualKey.NumPad7)]
    [InlineData("mediaplaypause", VirtualKey.MediaPlayPause)]
    [InlineData("volumeup", VirtualKey.VolumeUp)]
    [InlineData(",", VirtualKey.Comma)]
    public void ParsesASingleKey(string text, VirtualKey expected)
    {
        var combo = HotkeyParser.Parse(text);

        Assert.Empty(combo.Modifiers);
        Assert.Equal(expected, combo.Key);
    }

    [Fact]
    public void ParsesModifiersInWrittenOrder()
    {
        var combo = HotkeyParser.Parse("ctrl+shift+m");

        Assert.Equal(new[] { VirtualKey.LeftControl, VirtualKey.LeftShift }, combo.Modifiers);
        Assert.Equal(VirtualKey.M, combo.Key);
    }

    [Fact]
    public void ReleasesInReversePressOrder()
    {
        var combo = HotkeyParser.Parse("ctrl+alt+del");

        Assert.Equal(
            new[] { VirtualKey.LeftControl, VirtualKey.LeftAlt, VirtualKey.Delete },
            combo.PressOrder);
        Assert.Equal(
            new[] { VirtualKey.Delete, VirtualKey.LeftAlt, VirtualKey.LeftControl },
            combo.ReleaseOrder);
    }

    [Theory]
    [InlineData("CTRL+SHIFT+M")]
    [InlineData("  ctrl + shift + m ")]
    [InlineData("control+shift+m")]
    public void IgnoresCaseSpacingAndAliases(string text)
    {
        var combo = HotkeyParser.Parse(text);

        Assert.Equal(new[] { VirtualKey.LeftControl, VirtualKey.LeftShift }, combo.Modifiers);
        Assert.Equal(VirtualKey.M, combo.Key);
    }

    [Fact]
    public void TreatsATrailingPlusAsThePlusKey()
    {
        var combo = HotkeyParser.Parse("ctrl++");

        Assert.Equal(new[] { VirtualKey.LeftControl }, combo.Modifiers);
        Assert.Equal(VirtualKey.Plus, combo.Key);
    }

    [Fact]
    public void DistinguishesLeftAndRightModifiers()
    {
        var combo = HotkeyParser.Parse("ralt+rctrl+f1");

        Assert.Equal(new[] { VirtualKey.RightAlt, VirtualKey.RightControl }, combo.Modifiers);
        Assert.Equal(VirtualKey.F1, combo.Key);
    }

    [Fact]
    public void AcceptsAModifierOnlyCombo()
    {
        var combo = HotkeyParser.Parse("ctrl");

        Assert.Equal(new[] { VirtualKey.LeftControl }, combo.Modifiers);
        Assert.Null(combo.Key);
        Assert.False(combo.IsEmpty);
    }

    [Fact]
    public void DoesNotRepeatADuplicatedModifier()
    {
        var combo = HotkeyParser.Parse("ctrl+ctrl+a");

        Assert.Equal(new[] { VirtualKey.LeftControl }, combo.Modifiers);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("ctrl+notakey", "not a key name")]
    [InlineData("ctrl+a+b", "only have one non modifier key")]
    public void ReportsWhyItFailed(string text, string expectedFragment)
    {
        var parsed = HotkeyParser.TryParse(text, out var combo, out var error);

        Assert.False(parsed);
        Assert.Null(combo);
        Assert.Contains(expectedFragment, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RoundTripsThroughItsOwnSpelling()
    {
        var combo = HotkeyParser.Parse("ctrl+shift+m");

        Assert.Equal("ctrl+shift+m", combo.ToString());
        Assert.Equal(combo.PressOrder, HotkeyParser.Parse(combo.ToString()).PressOrder);
    }
}
