using System.IO;
using TouchDeck.App.Configurator;
using TouchDeck.App.Rendering;
using TouchDeck.Core.Configuration;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// Icons, from the path rules up. Nothing here builds a visual: that needs a UI thread, and
/// what is worth pinning down is which values are accepted and what they turn into.
/// </summary>
public sealed class IconTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "touchdeck-tests", Guid.NewGuid().ToString("N"));

    private ConfigPaths Paths => new(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void ARelativeIconPathIsReadFromTheIconsFolder()
    {
        var resolved = IconFile.Resolve(Paths.IconsDirectory, "discord.png");

        Assert.Equal(Path.Combine(Paths.IconsDirectory, "discord.png"), resolved);
    }

    [Fact]
    public void AnAbsoluteIconPathIsLeftAlone()
    {
        var resolved = IconFile.Resolve(Paths.IconsDirectory, @"C:\Windows\System32\shell32.dll");

        Assert.Equal(@"C:\Windows\System32\shell32.dll", resolved);
    }

    [Fact]
    public void AnIconPathExpandsEnvironmentVariables()
    {
        var resolved = IconFile.Resolve(Paths.IconsDirectory, "%WINDIR%\\explorer.exe");

        Assert.Equal(
            Path.Combine(Environment.GetEnvironmentVariable("WINDIR")!, "explorer.exe"),
            resolved);
    }

    [Fact]
    public void AnEmptyIconPathResolvesToNothing()
    {
        Assert.Null(IconFile.Resolve(Paths.IconsDirectory, "   "));
    }

    [Fact]
    public void SvgIsRecognisedButNotSupported()
    {
        Assert.True(IconFile.IsSvg("logo.svg"));
        Assert.False(IconFile.IsSupported("logo.svg"));
        Assert.True(IconFile.IsSupported("logo.PNG"));
        Assert.False(IconFile.IsSupported("logo.psd"));
    }

    [Fact]
    public void APathWithAPlaceholderIsLeftForTheDeckToWorkOut()
    {
        Assert.True(IconFile.IsInterpolated("{{var.theme}}/mic.png"));
        Assert.False(IconFile.IsInterpolated("mic.png"));
    }

    [Theory]
    [InlineData("E713")]
    [InlineData("e713")]
    [InlineData("0xE713")]
    [InlineData("U+E713")]
    [InlineData("\\uE713")]
    public void AGlyphCanBeGivenAsACodePointInAnyOfTheUsualForms(string value)
    {
        Assert.Equal("\uE713", IconFactory.ParseGlyph(value));
    }

    [Fact]
    public void AGlyphCanBeTheCharacterItself()
    {
        Assert.Equal("\uE713", IconFactory.ParseGlyph("\uE713"));
    }

    [Fact]
    public void AShortValueIsALetterRatherThanACodePoint()
    {
        // "A" is a hex digit, so the bare form deliberately needs four of them.
        Assert.Equal("A", IconFactory.ParseGlyph("A"));
        Assert.Equal("Go", IconFactory.ParseGlyph("Go"));
    }

    [Fact]
    public void APrefixedGlyphThatIsNotHexIsRejectedRatherThanDrawnLiterally()
    {
        Assert.Null(IconFactory.ParseGlyph("0xZZZZ"));
        Assert.Null(IconFactory.ParseGlyph("U+"));
    }

    [Fact]
    public void AGlyphCodePointInTheSurrogateRangeIsRejected()
    {
        Assert.Null(IconFactory.ParseGlyph("D800"));
    }

    [Fact]
    public void TurningAnIconOffDropsItsSizeAndColourToo()
    {
        var model = new IconEditModel(new IconConfig
        {
            Type = IconKind.Glyph,
            Value = "E713",
            Size = 30,
            Colour = "#FF0000",
        })
        {
            Type = IconKind.None,
        };

        Assert.Null(model.ToConfig());
    }

    [Fact]
    public void AnIconKeepsItsValuesThroughTheEditor()
    {
        var model = new IconEditModel(new IconConfig
        {
            Type = IconKind.File,
            Value = "discord.png",
            Size = 44,
            Colour = "#CFD4DC",
        });

        Assert.True(model.HasIcon);
        Assert.True(model.IsFile);

        var written = model.ToConfig()!;

        Assert.Equal(IconKind.File, written.Type);
        Assert.Equal("discord.png", written.Value);
        Assert.Equal(44, written.Size);
        Assert.Equal("#CFD4DC", written.Colour);
    }

    [Fact]
    public void AButtonKeepsItsIconThroughTheEditor()
    {
        var button = new ButtonConfig
        {
            Col = 1,
            Row = 2,
            Label = "Mute",
            Icon = new IconConfig { Type = IconKind.Glyph, Value = "E74F" },
        };

        var written = new ButtonEditModel(EditModelTestsRegistry, button).ToConfig();

        Assert.Equal(IconKind.Glyph, written.Icon!.Type);
        Assert.Equal("E74F", written.Icon.Value);
    }

    [Fact]
    public void AThemeKeepsItsBackgroundImageThroughTheEditor()
    {
        var theme = new Theme { Background = "#0B0D10", BackgroundImage = "wallpaper.png" };

        var written = new ThemeEditModel("dark", theme).ToConfig();

        Assert.Equal("wallpaper.png", written.BackgroundImage);
    }

    [Fact]
    public void AMissingIconFileIsAWarningRatherThanAnError()
    {
        var messages = Validate(new IconConfig { Type = IconKind.File, Value = "nowhere.png" });

        var warning = Assert.Single(messages);
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
        Assert.Contains("no icon at", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIconFileThatExistsPassesValidation()
    {
        Directory.CreateDirectory(Paths.IconsDirectory);
        File.WriteAllBytes(Path.Combine(Paths.IconsDirectory, "there.png"), new byte[] { 0 });

        Assert.Empty(Validate(new IconConfig { Type = IconKind.File, Value = "there.png" }));
    }

    [Fact]
    public void AnSvgIconSaysSoRatherThanJustSayingItIsMissing()
    {
        Directory.CreateDirectory(Paths.IconsDirectory);
        File.WriteAllText(Path.Combine(Paths.IconsDirectory, "logo.svg"), "<svg/>");

        var warning = Assert.Single(Validate(new IconConfig { Type = IconKind.File, Value = "logo.svg" }));

        Assert.Contains("Svg icons are not supported yet", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIconWithNoValueIsAWarning()
    {
        var warning = Assert.Single(Validate(new IconConfig { Type = IconKind.Glyph }));

        Assert.Contains("needs a value", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIconPathWithAPlaceholderIsNotCheckedUpFront()
    {
        Assert.Empty(Validate(new IconConfig { Type = IconKind.File, Value = "{{var.mode}}.png" }));
    }

    [Fact]
    public void NoIconIsNotAProblem()
    {
        Assert.Empty(Validate(null));
        Assert.Empty(Validate(new IconConfig { Type = IconKind.None }));
    }

    private static Core.Actions.ActionRegistry EditModelTestsRegistry =>
        Core.Actions.ActionRegistry.Scan(Serilog.Core.Logger.None, typeof(Actions.HotkeyAction).Assembly);

    /// <summary>Runs the validator over one button carrying <paramref name="icon"/>, and returns only what it said about the icon.</summary>
    private IReadOnlyList<ValidationMessage> Validate(IconConfig? icon)
    {
        var profile = new Profile
        {
            Id = "test",
            SourceFile = Path.Combine(Paths.ProfilesDirectory, "test.json"),
            Grid = new GridConfig { Columns = 1, Rows = 1 },
            Pages = new[]
            {
                new Page
                {
                    Id = "main",
                    Buttons = new[]
                    {
                        new ButtonConfig
                        {
                            Col = 0,
                            Row = 0,
                            Icon = icon,
                            Action = new ActionConfig { Type = "hotkey" },
                        },
                    },
                },
            },
        };

        var configuration = new DeckConfiguration { Profiles = new[] { profile } };

        return new ConfigValidator(Paths, new[] { "hotkey" })
            .Validate(configuration)
            .Where(m => m.Path.Contains("icon", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
