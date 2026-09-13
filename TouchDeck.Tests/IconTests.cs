using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    public void TheGlyphGridOffersFarMoreThanAHandfulAndEveryOneOfThemDraws()
    {
        var common = IconPickerBox.CommonGlyphs;
        var all = IconPickerBox.FontGlyphs;

        Assert.NotEmpty(common);
        Assert.All(common, code => Assert.NotNull(IconFactory.ParseGlyph(code)));
        Assert.All(all, code => Assert.NotNull(IconFactory.ParseGlyph(code)));

        // The list is read out of the font rather than written by hand, so on a machine with
        // an icon font there is no reason for it to be short. Without one it is empty, and
        // the curated list stands on its own.
        Assert.True(all.Count == 0 || all.Count > 500, $"{all.Count} glyphs found.");
        Assert.Equal(common.Distinct().Count(), common.Count);
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
        });

        Assert.True(model.HasIcon);
        Assert.True(model.IsFile);

        var written = model.ToConfig()!;

        Assert.Equal(IconKind.File, written.Type);
        Assert.Equal("discord.png", written.Value);
        Assert.Equal(44, written.Size);
    }

    [Fact]
    public void AnImageHasNoColourOfItsOwnToSet()
    {
        var model = new IconEditModel(new IconConfig
        {
            Type = IconKind.File,
            Value = "discord.png",
            Colour = "#CFD4DC",
        });

        Assert.False(model.HasColour);
        Assert.Null(model.Colour);
        Assert.Null(model.ToConfig()!.Colour);
    }

    [Fact]
    public void SwitchingAGlyphToAnImageDropsTheColourItWasDrawnIn()
    {
        var model = new IconEditModel(new IconConfig
        {
            Type = IconKind.Glyph,
            Value = "E713",
            Colour = "#FF3D7F",
        });

        Assert.True(model.HasColour);

        model.Type = IconKind.File;

        Assert.False(model.HasColour);
        Assert.Null(model.ToConfig()!.Colour);
    }

    [Fact]
    public void AColourOnAnImageIsReportedRatherThanIgnoredInSilence()
    {
        Directory.CreateDirectory(Paths.IconsDirectory);
        File.WriteAllBytes(Path.Combine(Paths.IconsDirectory, "logo.png"), Array.Empty<byte>());

        var messages = Validate(new IconConfig
        {
            Type = IconKind.File,
            Value = "logo.png",
            Colour = "#46B96B",
        });

        Assert.Contains(messages, m => m.Path.EndsWith(".icon.colour", StringComparison.Ordinal));
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

    [Fact]
    public void AFlatBackgroundIsTakenOffAnUploadedLogo()
    {
        var source = Picture(32, 32, (x, y) =>
            Math.Sqrt(((x - 15.5) * (x - 15.5)) + ((y - 15.5) * (y - 15.5))) <= 11
                ? (0x5A, 0x3C, 0xE6, 0xFF)
                : (0xFF, 0xFF, 0xFF, 0xFF));

        var cut = IconImage.Prepare(source);

        Assert.NotNull(cut);

        // Trimmed to the circle, so the corners of what is left are the circle's own corners.
        Assert.Equal(22, cut!.PixelWidth);
        Assert.Equal(22, cut.PixelHeight);
        Assert.Equal(0, AlphaAt(cut, 0, 0));
        Assert.Equal(255, AlphaAt(cut, 11, 11));
    }

    [Fact]
    public void TheEmptyMarginRoundAnIconIsTrimmedAway()
    {
        // Already cut out, but sitting in the middle of a much larger transparent canvas,
        // which is what makes an icon draw smaller than the size it was given.
        var source = Picture(64, 64, (x, y) =>
            x is >= 28 and < 36 && y is >= 28 and < 36
                ? (0x20, 0x40, 0x60, 0xFF)
                : (0, 0, 0, 0));

        var trimmed = IconImage.Prepare(source);

        Assert.NotNull(trimmed);
        Assert.Equal(8, trimmed!.PixelWidth);
        Assert.Equal(8, trimmed.PixelHeight);
    }

    [Fact]
    public void AnIconThatAlreadyFillsItsCanvasIsLeftAlone()
    {
        var source = Picture(32, 32, (x, y) => (0x20, 0x40, 0x60, 0xFF));

        Assert.Null(IconImage.Prepare(source));
    }

    [Fact]
    public void AnAreaEnclosedByTheLogoIsKept()
    {
        // A ring on white: the hole in the middle is the background colour but is not the
        // background, because you cannot reach it from the edge.
        var source = Picture(32, 32, (x, y) =>
        {
            var d = Math.Sqrt(((x - 15.5) * (x - 15.5)) + ((y - 15.5) * (y - 15.5)));
            return d is >= 8 and <= 13 ? (0x5A, 0x3C, 0xE6, 0xFF) : (0xFF, 0xFF, 0xFF, 0xFF);
        });

        var cut = IconImage.Prepare(source);

        Assert.NotNull(cut);

        // The hole in the middle of the ring is kept: it is the background colour, but you
        // cannot reach it from the edge, so it is part of the picture.
        Assert.Equal(255, AlphaAt(cut!, cut.PixelWidth / 2, cut.PixelHeight / 2));
        Assert.Equal(0, AlphaAt(cut, 0, 0));
    }

    [Fact]
    public void AnImageThatIsAlreadyCutOutHasNoBackgroundTakenOff()
    {
        // Half transparent, half solid, with the solid half reaching every edge it can, so
        // there is neither a background to remove nor a margin to trim.
        var source = Picture(32, 32, (x, y) => x < 16 ? (0, 0, 0, 0) : (0x20, 0x40, 0x60, 0xFF));

        var prepared = IconImage.Prepare(source);

        Assert.NotNull(prepared);
        Assert.Equal(16, prepared!.PixelWidth);
        Assert.Equal(32, prepared.PixelHeight);
    }

    [Fact]
    public void APictureWithNoFlatBackgroundIsLeftAlone()
    {
        var source = Picture(32, 32, (x, y) => ((byte)(x * 8), (byte)(y * 8), (byte)(x + y), (byte)255));

        Assert.Null(IconImage.Prepare(source));
    }

    [Fact]
    public void AnImageThatIsAllBackgroundIsLeftAlone()
    {
        var source = Picture(32, 32, (x, y) => (0xFF, 0xFF, 0xFF, 0xFF));

        Assert.Null(IconImage.Prepare(source));
    }

    /// <summary>Builds a test image from a function over its pixels, in BGRA order.</summary>
    private static BitmapSource Picture(int width, int height, Func<int, int, (int B, int G, int R, int A)> pixel)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (b, g, r, a) = pixel(x, y);
                var offset = (y * stride) + (x * 4);
                pixels[offset] = (byte)b;
                pixels[offset + 1] = (byte)g;
                pixels[offset + 2] = (byte)r;
                pixels[offset + 3] = (byte)a;
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
    }

    private static int AlphaAt(BitmapSource image, int x, int y)
    {
        var pixel = new byte[4];
        image.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel[3];
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
