using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Serilog;
using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Rendering;

/// <summary>
/// Turns the strings and enums in a theme into the WPF objects that draw them. Anything
/// unparsable falls back rather than throwing, because a typo in a colour must not take the
/// deck down.
/// </summary>
public static class StyleTranslator
{
    private static readonly ConcurrentDictionary<string, Brush> BrushCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, FontFamily> FontCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Colour used when a colour string cannot be understood.</summary>
    private static readonly Brush UnparsableColour = CreateFrozen(Color.FromRgb(0xFF, 0x00, 0xFF));

    /// <summary>Logger used to report unparsable values once they are hit.</summary>
    public static ILogger Logger { get; set; } = Serilog.Core.Logger.None;

    /// <summary>Parses a hex or named colour into a frozen brush.</summary>
    /// <param name="colour">A value such as <c>#171A1F</c>, <c>#CC171A1F</c> or <c>Red</c>.</param>
    public static Brush Brush(string colour) => BrushCache.GetOrAdd(colour, Parse);

    /// <summary>Parses a comma separated font family list.</summary>
    /// <param name="families">A value such as <c>Segoe UI Variable Display, Segoe UI</c>.</param>
    public static FontFamily Font(string families) =>
        FontCache.GetOrAdd(families, name => new FontFamily(name));

    /// <summary>Parses a font weight name, falling back to normal.</summary>
    /// <param name="weight">A value such as <c>SemiBold</c>.</param>
    public static FontWeight Weight(string weight)
    {
        try
        {
            var converted = new FontWeightConverter().ConvertFromString(weight);
            if (converted is FontWeight parsed)
            {
                return parsed;
            }
        }
        catch (FormatException)
        {
            // Falls through to the warning below.
        }

        Logger.Warning("\"{Weight}\" is not a font weight. Using Normal.", weight);
        return FontWeights.Normal;
    }

    /// <summary>Builds the easing function for an easing name.</summary>
    /// <param name="easing">The curve to use.</param>
    public static IEasingFunction? Easing(Core.Configuration.Easing easing) => easing switch
    {
        Core.Configuration.Easing.Linear => null,
        Core.Configuration.Easing.SineOut => new SineEase { EasingMode = EasingMode.EaseOut },
        Core.Configuration.Easing.QuadOut => new QuadraticEase { EasingMode = EasingMode.EaseOut },
        Core.Configuration.Easing.CubicIn => new CubicEase { EasingMode = EasingMode.EaseIn },
        Core.Configuration.Easing.CubicOut => new CubicEase { EasingMode = EasingMode.EaseOut },
        Core.Configuration.Easing.CubicInOut => new CubicEase { EasingMode = EasingMode.EaseInOut },
        Core.Configuration.Easing.BackOut => new BackEase { EasingMode = EasingMode.EaseOut },
        _ => null,
    };

    /// <summary>Maps a label position onto vertical alignment within the button.</summary>
    /// <param name="position">Where the label should sit.</param>
    public static VerticalAlignment LabelAlignment(LabelPosition position) => position switch
    {
        LabelPosition.Top => VerticalAlignment.Top,
        LabelPosition.Center => VerticalAlignment.Center,
        _ => VerticalAlignment.Bottom,
    };

    private static Brush Parse(string colour)
    {
        var text = colour.Trim();

        try
        {
            if (text.StartsWith('#'))
            {
                return CreateFrozen(ParseHex(text));
            }

            var converted = ColorConverter.ConvertFromString(text);
            if (converted is Color named)
            {
                return CreateFrozen(named);
            }
        }
        catch (FormatException)
        {
            // Falls through to the warning below.
        }

        Logger.Warning("\"{Colour}\" is not a colour. Using magenta so it is obvious on screen.", colour);
        return UnparsableColour;
    }

    /// <summary>Accepts #RGB, #ARGB, #RRGGBB and #AARRGGBB, the last being hex with alpha.</summary>
    private static Color ParseHex(string text)
    {
        var digits = text[1..];

        static byte Nibble(char c) => (byte)(Convert.ToByte(c.ToString(), 16) * 17);

        switch (digits.Length)
        {
            case 3:
                return Color.FromRgb(Nibble(digits[0]), Nibble(digits[1]), Nibble(digits[2]));
            case 4:
                return Color.FromArgb(Nibble(digits[0]), Nibble(digits[1]), Nibble(digits[2]), Nibble(digits[3]));
            case 6:
                return Color.FromRgb(HexByte(digits, 0), HexByte(digits, 2), HexByte(digits, 4));
            case 8:
                return Color.FromArgb(HexByte(digits, 0), HexByte(digits, 2), HexByte(digits, 4), HexByte(digits, 6));
            default:
                throw new FormatException($"\"{text}\" has {digits.Length} hex digits; 3, 4, 6 or 8 are allowed.");
        }
    }

    private static byte HexByte(string digits, int offset) =>
        byte.Parse(digits.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static Brush CreateFrozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
