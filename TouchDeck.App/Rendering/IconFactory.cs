using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;
using TouchDeck.Core.Configuration;

// Aliased rather than imported: System.Windows.Shapes.Path collides with System.IO.Path.
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TouchDeck.App.Rendering;

/// <summary>
/// Turns an <see cref="IconConfig"/> into something WPF can draw. A bad path or an
/// unreadable image produces nothing and a single warning rather than an exception, on the
/// same principle as <see cref="StyleTranslator"/>: one typo must not take the deck down.
/// </summary>
public sealed class IconFactory
{
    /// <summary>
    /// Segoe Fluent Icons ships with Windows 11, Segoe MDL2 Assets with Windows 10. Listing
    /// both means a glyph still draws on the older font, where most code points match.
    /// </summary>
    private const string GlyphFontFamily = "Segoe Fluent Icons, Segoe MDL2 Assets";

    /// <summary>Decoded images, keyed by path and write time so replacing a file is picked up.</summary>
    private readonly ConcurrentDictionary<string, BitmapSource?> _images = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Keys already complained about, so a broken icon warns once rather than per render.</summary>
    private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _iconsDirectory;
    private readonly ILogger _logger;

    /// <summary>Creates a factory reading relative paths from the icons folder.</summary>
    /// <param name="iconsDirectory">Root for icon paths that are not absolute.</param>
    /// <param name="logger">Where unreadable icons are reported.</param>
    public IconFactory(string iconsDirectory, ILogger logger)
    {
        _iconsDirectory = iconsDirectory;
        _logger = logger.ForContext<IconFactory>();
    }

    /// <summary>
    /// Builds the visual for an icon, or null when there is nothing to draw. Null covers
    /// both "no icon was configured" and "the configured icon could not be loaded", because
    /// a button with a missing icon should still show its label.
    /// </summary>
    /// <param name="icon">The icon as configured, or null.</param>
    /// <param name="style">The resolved style, supplying the default size and colour.</param>
    public FrameworkElement? Create(IconConfig? icon, ResolvedButtonStyle style)
    {
        if (icon is null || icon.Type == IconKind.None || string.IsNullOrWhiteSpace(icon.Value))
        {
            return null;
        }

        var size = Math.Max(1, icon.Size ?? style.IconSize);

        var element = icon.Type switch
        {
            IconKind.File => CreateFile(icon, size),
            IconKind.Glyph => CreateGlyph(icon, style, size),
            IconKind.Text => CreateText(icon, style, size),
            _ => null,
        };

        if (element is not null)
        {
            element.IsHitTestVisible = false;
        }

        return element;
    }

    /// <summary>
    /// Builds the brush for a theme's background image, or null when there is none or it
    /// cannot be read. It is drawn over the background colour rather than instead of it, so
    /// a transparent image still sits on the theme's own background.
    /// </summary>
    /// <param name="path">The configured path, relative to the icons folder or absolute.</param>
    public Brush? Background(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || ResolvePath(path) is not { } full)
        {
            return null;
        }

        if (IconFile.IsSvg(full))
        {
            WarnOnce(full, "\"{Path}\" is an svg, which is not supported yet. Use a png.", full);
            return null;
        }

        if (Load(full) is not { } source)
        {
            return null;
        }

        var brush = new ImageBrush(source)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
        };

        brush.Freeze();
        return brush;
    }

    /// <summary>Loads a bitmap, returning null and warning once when it cannot be read.</summary>
    /// <param name="path">An absolute path to an image file.</param>
    public BitmapSource? Load(string path)
    {
        string key;

        try
        {
            // The write time is part of the key so that replacing an icon on disk and
            // reloading the config shows the new one instead of the cached old one.
            key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            key = path;
        }

        return _images.GetOrAdd(key, _ => Decode(path));
    }

    private BitmapSource? Decode(string path)
    {
        if (!File.Exists(path))
        {
            WarnOnce(path, "There is no file at \"{Path}\", so the icon is not drawn.", path);
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();

            // OnLoad reads the whole file now and closes it, so the icon is not left locked
            // and the user can overwrite it while the deck is running.
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (e is NotSupportedException or IOException or UnauthorizedAccessException
                                      or ArgumentException or UriFormatException)
        {
            WarnOnce(path, "\"{Path}\" could not be read as an image: {Reason}", path, e.Message);
            return null;
        }
    }

    private FrameworkElement? CreateFile(IconConfig icon, double size)
    {
        var path = ResolvePath(icon.Value!);
        if (path is null)
        {
            return null;
        }

        if (IconFile.IsSvg(path))
        {
            WarnOnce(
                path,
                "\"{Path}\" is an svg, which is not supported yet. Use a png, or a glyph.",
                path);
            return null;
        }

        if (!IconFile.IsSupported(path))
        {
            WarnOnce(
                path,
                "\"{Path}\" is not an image TouchDeck can read. Supported: {Extensions}.",
                path,
                string.Join(", ", IconFile.SupportedExtensions));
            return null;
        }

        if (Load(path) is not { } source)
        {
            return null;
        }

        // A file icon keeps its own colours unless the button asks for a tint. The theme
        // wide iconColour exists for glyphs; applying it here would flatten every app logo
        // on the deck into one grey silhouette.
        if (icon.Colour is { Length: > 0 } tint)
        {
            return Tinted(source, tint, size);
        }

        return new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            MaxWidth = size,
            MaxHeight = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>Paints one colour through the image's alpha, which is what tinting means here.</summary>
    private static FrameworkElement Tinted(BitmapSource source, string colour, double size)
    {
        var mask = new ImageBrush(source) { Stretch = Stretch.Uniform };
        mask.Freeze();

        return new Rectangle
        {
            Fill = StyleTranslator.Brush(colour),
            OpacityMask = mask,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private FrameworkElement? CreateGlyph(IconConfig icon, ResolvedButtonStyle style, double size)
    {
        if (ParseGlyph(icon.Value!) is not { } glyph)
        {
            WarnOnce(
                icon.Value!,
                "\"{Value}\" is not a glyph. Give the character itself, or a code point such as E713.",
                icon.Value!);
            return null;
        }

        return new TextBlock
        {
            Text = glyph,
            FontFamily = StyleTranslator.Font(GlyphFontFamily),
            FontSize = size,
            Foreground = StyleTranslator.Brush(icon.Colour ?? style.IconColour),
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static FrameworkElement CreateText(IconConfig icon, ResolvedButtonStyle style, double size) =>
        new TextBlock
        {
            Text = icon.Value!,
            FontFamily = StyleTranslator.Font(style.FontFamily),
            FontSize = size,
            FontWeight = StyleTranslator.Weight(style.FontWeight),
            Foreground = StyleTranslator.Brush(icon.Colour ?? style.IconColour),
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

    /// <summary>
    /// Accepts the glyph itself, or a code point written as <c>E713</c>, <c>0xE713</c>,
    /// <c>U+E713</c> or the escaped form. A bare hex form needs four digits, so a one character
    /// value like <c>A</c> is still treated as the letter rather than as a code point.
    /// </summary>
    /// <param name="value">The configured glyph value.</param>
    public static string? ParseGlyph(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var digits = text;
        var prefixed = true;

        if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            digits.StartsWith(@"\u", StringComparison.OrdinalIgnoreCase))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
        {
            digits = digits[2..];
        }
        else
        {
            prefixed = false;
        }

        var looksHex = digits.Length is >= 4 and <= 6 && digits.All(Uri.IsHexDigit);

        if (!prefixed && !looksHex)
        {
            return text;
        }

        // It was meant as a code point, so a value that is not one is a mistake worth
        // reporting rather than drawing literally.
        if (int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint) &&
            codePoint is > 0 and <= 0x10FFFF &&
            codePoint is < 0xD800 or > 0xDFFF)
        {
            return char.ConvertFromUtf32(codePoint);
        }

        return null;
    }

    /// <summary>Turns a configured path into an absolute one, complaining once if it cannot.</summary>
    private string? ResolvePath(string value)
    {
        if (IconFile.Resolve(_iconsDirectory, value) is { } path)
        {
            return path;
        }

        WarnOnce(value, "\"{Value}\" is not a usable icon path.", value);
        return null;
    }

    private void WarnOnce(string key, string template, params object?[] arguments)
    {
        lock (_warned)
        {
            if (!_warned.Add(key))
            {
                return;
            }
        }

        _logger.Warning(template, arguments);
    }
}
