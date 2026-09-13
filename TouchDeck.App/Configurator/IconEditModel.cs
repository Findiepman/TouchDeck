using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>
/// An editable button icon. Size and colour are optional here as they are in the config, so
/// an empty box means "follow the theme" rather than zero.
/// </summary>
public sealed class IconEditModel : ObservableObject
{
    private IconKind _type;
    private string? _value;
    private double? _size;
    private string? _colour;

    /// <summary>Creates an editable copy of an icon.</summary>
    /// <param name="icon">The icon to copy, or null for no icon.</param>
    public IconEditModel(IconConfig? icon)
    {
        if (icon is null)
        {
            return;
        }

        _type = icon.Type;
        _value = icon.Value;
        _size = icon.Size;
        _colour = icon.Type == IconKind.File ? null : icon.Colour;
    }

    /// <summary>Every icon kind, for the dropdown.</summary>
    public static IReadOnlyList<IconKind> Types { get; } = new[]
    {
        IconKind.None,
        IconKind.File,
        IconKind.Glyph,
        IconKind.Text,
    };

    /// <summary>Where the icon is drawn from.</summary>
    public IconKind Type
    {
        get => _type;
        set
        {
            if (Set(ref _type, value))
            {
                // An image draws in its own colours, so a colour left over from when this was
                // a glyph would have nothing to do and no field to show it in.
                if (value == IconKind.File)
                {
                    Colour = null;
                }

                RaiseQuiet(nameof(HasIcon));
                RaiseQuiet(nameof(IsFile));
                RaiseQuiet(nameof(HasColour));
                RaiseQuiet(nameof(ValueLabel));
                RaiseQuiet(nameof(ValueHint));
            }
        }
    }

    /// <summary>Path, glyph or text, depending on <see cref="Type"/>.</summary>
    public string? Value
    {
        get => _value;
        set => Set(ref _value, value);
    }

    /// <summary>Icon size, or null to follow the theme.</summary>
    public double? Size
    {
        get => _size;
        set => Set(ref _size, value);
    }

    /// <summary>
    /// Icon colour, or null to follow the theme. A glyph or a piece of text is drawn in this
    /// colour; an image has colours of its own and is left alone.
    /// </summary>
    public string? Colour
    {
        get => _colour;
        set => Set(ref _colour, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    /// <summary>True when there is an icon at all, which is what shows the rest of the fields.</summary>
    public bool HasIcon => _type != IconKind.None;

    /// <summary>True when the icon comes from a file, which is what shows the browse button.</summary>
    public bool IsFile => _type == IconKind.File;

    /// <summary>
    /// True when a colour means anything here, which is everything but an image. There is no
    /// colour field on an image, because an image already has its own.
    /// </summary>
    public bool HasColour => _type is IconKind.Glyph or IconKind.Text;

    /// <summary>What to call the value box, which differs per kind.</summary>
    public string ValueLabel => _type switch
    {
        IconKind.File => "Image file",
        IconKind.Glyph => "Glyph",
        IconKind.Text => "Text",
        _ => "Value",
    };

    /// <summary>A line of help under the value box.</summary>
    public string ValueHint => _type switch
    {
        IconKind.File => "A png in the icons folder, or a full path. It keeps its own colours. " +
                         "Svg is not supported yet.",
        IconKind.Glyph => "A Segoe Fluent Icons glyph, or its code point such as E713.",
        IconKind.Text => "Drawn at icon size, for one or two characters.",
        _ => string.Empty,
    };

    /// <summary>
    /// Builds the config record, or null when there is no icon. Dropping size and colour
    /// along with the icon keeps a switched off icon from leaving orphaned keys in the file.
    /// </summary>
    public IconConfig? ToConfig()
    {
        if (_type == IconKind.None)
        {
            return null;
        }

        return new IconConfig
        {
            Type = _type,
            Value = string.IsNullOrWhiteSpace(_value) ? null : _value,
            Size = _size,
            Colour = HasColour ? _colour : null,
        };
    }
}
