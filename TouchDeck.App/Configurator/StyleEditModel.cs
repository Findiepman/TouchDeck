using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>
/// An editable button style. Every member is optional here exactly as it is in the config,
/// so leaving a box empty means "inherit" rather than "blank".
/// </summary>
public sealed class StyleEditModel : ObservableObject
{
    private string? _background;
    private string? _backgroundPressed;
    private string? _backgroundDisabled;
    private string? _border;
    private double? _borderWidth;
    private double? _cornerRadius;
    private string? _textColour;
    private string? _textColourPressed;
    private string? _fontFamily;
    private double? _fontSize;
    private string? _fontWeight;
    private LabelPosition? _labelPosition;
    private double? _iconSize;
    private string? _iconColour;
    private double? _padding;

    /// <summary>Creates an empty style, meaning "inherit everything".</summary>
    public StyleEditModel()
    {
    }

    /// <summary>Creates an editable copy of a style.</summary>
    /// <param name="style">The style to copy, or null for an empty one.</param>
    public StyleEditModel(ButtonStyle? style)
    {
        if (style is null)
        {
            return;
        }

        _background = style.Background;
        _backgroundPressed = style.BackgroundPressed;
        _backgroundDisabled = style.BackgroundDisabled;
        _border = style.Border;
        _borderWidth = style.BorderWidth;
        _cornerRadius = style.CornerRadius;
        _textColour = style.TextColour;
        _textColourPressed = style.TextColourPressed;
        _fontFamily = style.FontFamily;
        _fontSize = style.FontSize;
        _fontWeight = style.FontWeight;
        _labelPosition = style.LabelPosition;
        _iconSize = style.IconSize;
        _iconColour = style.IconColour;
        _padding = style.Padding;
    }

    /// <summary>Every label position, for the dropdown.</summary>
    public static IReadOnlyList<Core.Configuration.LabelPosition?> LabelPositions { get; } = new Core.Configuration.LabelPosition?[]
    {
        null,
        Core.Configuration.LabelPosition.Top,
        Core.Configuration.LabelPosition.Center,
        Core.Configuration.LabelPosition.Bottom,
        Core.Configuration.LabelPosition.None,
    };

    /// <summary>Font weights offered in the dropdown.</summary>
    public static IReadOnlyList<string?> FontWeights { get; } =
        new string?[] { null, "Thin", "Light", "Normal", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" };

    public string? Background
    {
        get => _background;
        set => Set(ref _background, Normalise(value));
    }

    public string? BackgroundPressed
    {
        get => _backgroundPressed;
        set => Set(ref _backgroundPressed, Normalise(value));
    }

    public string? BackgroundDisabled
    {
        get => _backgroundDisabled;
        set => Set(ref _backgroundDisabled, Normalise(value));
    }

    public string? Border
    {
        get => _border;
        set => Set(ref _border, Normalise(value));
    }

    public double? BorderWidth
    {
        get => _borderWidth;
        set => Set(ref _borderWidth, value);
    }

    public double? CornerRadius
    {
        get => _cornerRadius;
        set => Set(ref _cornerRadius, value);
    }

    public string? TextColour
    {
        get => _textColour;
        set => Set(ref _textColour, Normalise(value));
    }

    public string? TextColourPressed
    {
        get => _textColourPressed;
        set => Set(ref _textColourPressed, Normalise(value));
    }

    public string? FontFamily
    {
        get => _fontFamily;
        set => Set(ref _fontFamily, Normalise(value));
    }

    public double? FontSize
    {
        get => _fontSize;
        set => Set(ref _fontSize, value);
    }

    public string? FontWeight
    {
        get => _fontWeight;
        set => Set(ref _fontWeight, Normalise(value));
    }

    public LabelPosition? LabelPosition
    {
        get => _labelPosition;
        set => Set(ref _labelPosition, value);
    }

    public double? IconSize
    {
        get => _iconSize;
        set => Set(ref _iconSize, value);
    }

    public string? IconColour
    {
        get => _iconColour;
        set => Set(ref _iconColour, Normalise(value));
    }

    public double? Padding
    {
        get => _padding;
        set => Set(ref _padding, value);
    }

    /// <summary>True when nothing at all is overridden, in which case no style block is written.</summary>
    public bool IsEmpty => ToConfig() is null;

    /// <summary>Builds the config record, or null when nothing is set.</summary>
    public ButtonStyle? ToConfig()
    {
        var style = new ButtonStyle
        {
            Background = _background,
            BackgroundPressed = _backgroundPressed,
            BackgroundDisabled = _backgroundDisabled,
            Border = _border,
            BorderWidth = _borderWidth,
            CornerRadius = _cornerRadius,
            TextColour = _textColour,
            TextColourPressed = _textColourPressed,
            FontFamily = _fontFamily,
            FontSize = _fontSize,
            FontWeight = _fontWeight,
            LabelPosition = _labelPosition,
            IconSize = _iconSize,
            IconColour = _iconColour,
            Padding = _padding,
        };

        return style == new ButtonStyle() ? null : style;
    }

    /// <summary>Clears every override.</summary>
    public void Clear()
    {
        Background = null;
        BackgroundPressed = null;
        BackgroundDisabled = null;
        Border = null;
        BorderWidth = null;
        CornerRadius = null;
        TextColour = null;
        TextColourPressed = null;
        FontFamily = null;
        FontSize = null;
        FontWeight = null;
        LabelPosition = null;
        IconSize = null;
        IconColour = null;
        Padding = null;
    }

    /// <summary>Treats an empty or whitespace box as "not set".</summary>
    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
