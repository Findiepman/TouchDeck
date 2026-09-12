using TouchDeck.Core.Configuration;
using Xunit;

namespace TouchDeck.Tests;

public class ThemeMergeTests
{
    [Fact]
    public void AnEmptyThemeResolvesToTheBuiltInDefaults()
    {
        var resolved = new Theme().Resolve();

        Assert.Equal(Theme.Defaults.Background, resolved.Background);
        Assert.Equal(ButtonStyle.Defaults.CornerRadius, resolved.Button.CornerRadius);
        Assert.Equal(PressStyle.Defaults.Scale, resolved.Press.Scale);
        Assert.Equal(TransitionStyle.Defaults.PageChangeMs, resolved.Transition.PageChangeMs);
    }

    [Fact]
    public void StatedValuesWinAndTheRestAreInherited()
    {
        var theme = new Theme
        {
            Background = "#123456",
            Button = new ButtonStyle { CornerRadius = 2 },
        };

        var resolved = theme.Resolve();

        Assert.Equal("#123456", resolved.Background);
        Assert.Equal(2, resolved.Button.CornerRadius);
        Assert.Equal(ButtonStyle.Defaults.Background, resolved.Button.Background);
        Assert.Equal(Theme.Defaults.Gap, resolved.Gap);
    }

    [Fact]
    public void ButtonStyleOverridesOnlyTheMembersItStates()
    {
        var resolved = new Theme
        {
            Button = new ButtonStyle { Background = "#111111", TextColour = "#222222" },
        }.Resolve();

        var overridden = resolved.Button.With(new ButtonStyle { Background = "#5A1D1D" });

        Assert.Equal("#5A1D1D", overridden.Background);
        Assert.Equal("#222222", overridden.TextColour);
        Assert.Equal(resolved.Button.FontSize, overridden.FontSize);
    }

    [Fact]
    public void NoOverridesLeavesTheStyleAlone()
    {
        var resolved = new Theme().Resolve();

        Assert.Same(resolved.Button, resolved.Button.With(null));
    }

    [Fact]
    public void InheritanceFollowsTheChain()
    {
        var configuration = new DeckConfiguration
        {
            Themes = new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase)
            {
                ["base"] = new() { Background = "#000000", Gap = 4, Button = new ButtonStyle { FontSize = 20 } },
                ["derived"] = new() { Inherits = "base", Background = "#FFFFFF" },
            },
        };

        var resolved = configuration.ResolveTheme("derived");

        Assert.Equal("#FFFFFF", resolved.Background);
        Assert.Equal(4, resolved.Gap);
        Assert.Equal(20, resolved.Button.FontSize);
    }

    [Fact]
    public void AnInheritanceCycleStillResolves()
    {
        var configuration = new DeckConfiguration
        {
            Themes = new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = new() { Inherits = "b", Gap = 1 },
                ["b"] = new() { Inherits = "a", Padding = 2 },
            },
        };

        var resolved = configuration.ResolveTheme("a");

        Assert.Equal(1, resolved.Gap);
        Assert.Equal(2, resolved.Padding);
    }

    [Fact]
    public void AnUnknownThemeNameFallsBackToTheDefaults()
    {
        var resolved = new DeckConfiguration().ResolveTheme("nothing-called-this");

        Assert.Equal(Theme.Defaults.Background, resolved.Background);
    }
}
