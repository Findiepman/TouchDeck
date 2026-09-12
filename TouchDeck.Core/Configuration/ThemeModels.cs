namespace TouchDeck.Core.Configuration;

/// <summary>Root model for <c>themes.json</c>.</summary>
public sealed record ThemeFile
{
    /// <summary>Themes by name. Profiles and buttons reference these names.</summary>
    public IReadOnlyDictionary<string, Theme> Themes { get; init; } =
        new Dictionary<string, Theme>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Where a button's label sits relative to its icon.</summary>
public enum LabelPosition
{
    Top,
    Bottom,
    Center,
    None,
}

/// <summary>Easing curves available to press and transition animations.</summary>
public enum Easing
{
    Linear,
    SineOut,
    QuadOut,
    CubicIn,
    CubicOut,
    CubicInOut,
    BackOut,
}

/// <summary>How one page replaces another.</summary>
public enum TransitionStyleKind
{
    None,
    Fade,
    Slide,
}

/// <summary>
/// A named theme. Every member is optional; unset members fall back to the theme this one
/// is merged onto, and ultimately to <see cref="Defaults"/>.
/// </summary>
public sealed record Theme
{
    /// <summary>Name of another theme to inherit from before applying this one.</summary>
    public string? Inherits { get; init; }

    public string? Background { get; init; }

    public string? BackgroundImage { get; init; }

    /// <summary>Space between cells, in device independent units.</summary>
    public double? Gap { get; init; }

    /// <summary>Space between the panel edge and the grid, in device independent units.</summary>
    public double? Padding { get; init; }

    public ButtonStyle? Button { get; init; }

    public PressStyle? Press { get; init; }

    public TransitionStyle? Transition { get; init; }

    /// <summary>The ultimate fallback, used when nothing else supplies a value.</summary>
    public static Theme Defaults { get; } = new()
    {
        Background = "#0B0D10",
        BackgroundImage = null,
        Gap = 8,
        Padding = 12,
        Button = ButtonStyle.Defaults,
        Press = PressStyle.Defaults,
        Transition = TransitionStyle.Defaults,
    };

    /// <summary>Returns a theme where this theme's set members override <paramref name="baseTheme"/>.</summary>
    public Theme MergedOnto(Theme baseTheme) => new()
    {
        Inherits = Inherits ?? baseTheme.Inherits,
        Background = Background ?? baseTheme.Background,
        BackgroundImage = BackgroundImage ?? baseTheme.BackgroundImage,
        Gap = Gap ?? baseTheme.Gap,
        Padding = Padding ?? baseTheme.Padding,
        Button = ButtonStyle.Merge(baseTheme.Button, Button),
        Press = PressStyle.Merge(baseTheme.Press, Press),
        Transition = TransitionStyle.Merge(baseTheme.Transition, Transition),
    };

    /// <summary>Collapses this theme onto the built in defaults, producing fully populated values.</summary>
    public ResolvedTheme Resolve()
    {
        var merged = MergedOnto(Defaults);
        return new ResolvedTheme(
            merged.Background ?? Defaults.Background!,
            merged.BackgroundImage,
            merged.Gap ?? Defaults.Gap!.Value,
            merged.Padding ?? Defaults.Padding!.Value,
            (ButtonStyle.Merge(ButtonStyle.Defaults, merged.Button) ?? ButtonStyle.Defaults).Resolve(),
            (PressStyle.Merge(PressStyle.Defaults, merged.Press) ?? PressStyle.Defaults).Resolve(),
            (TransitionStyle.Merge(TransitionStyle.Defaults, merged.Transition) ?? TransitionStyle.Defaults).Resolve());
    }
}

/// <summary>
/// The appearance of a button. Used both as the theme wide default and as a per button
/// override, which is why every member is optional.
/// </summary>
public sealed record ButtonStyle
{
    public string? Background { get; init; }

    public string? BackgroundPressed { get; init; }

    public string? BackgroundDisabled { get; init; }

    public string? Border { get; init; }

    public double? BorderWidth { get; init; }

    public double? CornerRadius { get; init; }

    public string? TextColour { get; init; }

    public string? TextColourPressed { get; init; }

    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    /// <summary>A WPF font weight name, for example <c>SemiBold</c>.</summary>
    public string? FontWeight { get; init; }

    public LabelPosition? LabelPosition { get; init; }

    public double? IconSize { get; init; }

    public string? IconColour { get; init; }

    /// <summary>Space between the button edge and its content.</summary>
    public double? Padding { get; init; }

    public static ButtonStyle Defaults { get; } = new()
    {
        Background = "#171A1F",
        BackgroundPressed = "#252A32",
        BackgroundDisabled = "#101216",
        Border = "#22262D",
        BorderWidth = 1,
        CornerRadius = 12,
        TextColour = "#E8EAED",
        TextColourPressed = "#FFFFFF",
        FontFamily = "Segoe UI Variable Display, Segoe UI",
        FontSize = 13,
        FontWeight = "SemiBold",
        LabelPosition = Configuration.LabelPosition.Bottom,
        IconSize = 40,
        IconColour = "#CFD4DC",
        Padding = 8,
    };

    /// <summary>Overlays <paramref name="over"/> onto <paramref name="under"/>, member by member.</summary>
    public static ButtonStyle? Merge(ButtonStyle? under, ButtonStyle? over)
    {
        if (under is null)
        {
            return over;
        }

        if (over is null)
        {
            return under;
        }

        return new ButtonStyle
        {
            Background = over.Background ?? under.Background,
            BackgroundPressed = over.BackgroundPressed ?? under.BackgroundPressed,
            BackgroundDisabled = over.BackgroundDisabled ?? under.BackgroundDisabled,
            Border = over.Border ?? under.Border,
            BorderWidth = over.BorderWidth ?? under.BorderWidth,
            CornerRadius = over.CornerRadius ?? under.CornerRadius,
            TextColour = over.TextColour ?? under.TextColour,
            TextColourPressed = over.TextColourPressed ?? under.TextColourPressed,
            FontFamily = over.FontFamily ?? under.FontFamily,
            FontSize = over.FontSize ?? under.FontSize,
            FontWeight = over.FontWeight ?? under.FontWeight,
            LabelPosition = over.LabelPosition ?? under.LabelPosition,
            IconSize = over.IconSize ?? under.IconSize,
            IconColour = over.IconColour ?? under.IconColour,
            Padding = over.Padding ?? under.Padding,
        };
    }

    /// <summary>Collapses onto <see cref="Defaults"/> so no member is left unset.</summary>
    public ResolvedButtonStyle Resolve()
    {
        var m = Merge(Defaults, this) ?? Defaults;
        return new ResolvedButtonStyle(
            m.Background ?? Defaults.Background!,
            m.BackgroundPressed ?? Defaults.BackgroundPressed!,
            m.BackgroundDisabled ?? Defaults.BackgroundDisabled!,
            m.Border ?? Defaults.Border!,
            m.BorderWidth ?? Defaults.BorderWidth!.Value,
            m.CornerRadius ?? Defaults.CornerRadius!.Value,
            m.TextColour ?? Defaults.TextColour!,
            m.TextColourPressed ?? Defaults.TextColourPressed!,
            m.FontFamily ?? Defaults.FontFamily!,
            m.FontSize ?? Defaults.FontSize!.Value,
            m.FontWeight ?? Defaults.FontWeight!,
            m.LabelPosition ?? Defaults.LabelPosition!.Value,
            m.IconSize ?? Defaults.IconSize!.Value,
            m.IconColour ?? Defaults.IconColour!,
            m.Padding ?? Defaults.Padding!.Value);
    }
}

/// <summary>The press animation.</summary>
public sealed record PressStyle
{
    /// <summary>Scale factor applied while held. A value of 1 disables the scale.</summary>
    public double? Scale { get; init; }

    public int? DurationMs { get; init; }

    public Easing? Easing { get; init; }

    public static PressStyle Defaults { get; } = new()
    {
        Scale = 0.94,
        DurationMs = 70,
        Easing = Configuration.Easing.CubicOut,
    };

    /// <summary>Overlays <paramref name="over"/> onto <paramref name="under"/>, member by member.</summary>
    public static PressStyle? Merge(PressStyle? under, PressStyle? over)
    {
        if (under is null)
        {
            return over;
        }

        if (over is null)
        {
            return under;
        }

        return new PressStyle
        {
            Scale = over.Scale ?? under.Scale,
            DurationMs = over.DurationMs ?? under.DurationMs,
            Easing = over.Easing ?? under.Easing,
        };
    }

    /// <summary>Collapses onto <see cref="Defaults"/> so no member is left unset.</summary>
    public ResolvedPressStyle Resolve() => new(
        Scale ?? Defaults.Scale!.Value,
        DurationMs ?? Defaults.DurationMs!.Value,
        Easing ?? Defaults.Easing!.Value);
}

/// <summary>The page change animation.</summary>
public sealed record TransitionStyle
{
    public int? PageChangeMs { get; init; }

    public TransitionStyleKind? Style { get; init; }

    /// <summary>Overlays <paramref name="over"/> onto <paramref name="under"/>, member by member.</summary>
    public static TransitionStyle? Merge(TransitionStyle? under, TransitionStyle? over)
    {
        if (under is null)
        {
            return over;
        }

        if (over is null)
        {
            return under;
        }

        return new TransitionStyle
        {
            PageChangeMs = over.PageChangeMs ?? under.PageChangeMs,
            Style = over.Style ?? under.Style,
        };
    }

    public static TransitionStyle Defaults { get; } = new()
    {
        PageChangeMs = 140,
        Style = TransitionStyleKind.Fade,
    };

    /// <summary>Collapses onto <see cref="Defaults"/> so no member is left unset.</summary>
    public ResolvedTransitionStyle Resolve() => new(
        PageChangeMs ?? Defaults.PageChangeMs!.Value,
        Style ?? Defaults.Style!.Value);
}

/// <summary>A theme with every value supplied. This is what the rendering layer consumes.</summary>
public sealed record ResolvedTheme(
    string Background,
    string? BackgroundImage,
    double Gap,
    double Padding,
    ResolvedButtonStyle Button,
    ResolvedPressStyle Press,
    ResolvedTransitionStyle Transition);

/// <summary>A button style with every value supplied.</summary>
public sealed record ResolvedButtonStyle(
    string Background,
    string BackgroundPressed,
    string BackgroundDisabled,
    string Border,
    double BorderWidth,
    double CornerRadius,
    string TextColour,
    string TextColourPressed,
    string FontFamily,
    double FontSize,
    string FontWeight,
    LabelPosition LabelPosition,
    double IconSize,
    string IconColour,
    double Padding)
{
    /// <summary>Applies a button's own overrides on top of an already resolved style.</summary>
    /// <param name="overrides">The button's style block, or null for no overrides.</param>
    public ResolvedButtonStyle With(ButtonStyle? overrides) => overrides is null
        ? this
        : new ResolvedButtonStyle(
            overrides.Background ?? Background,
            overrides.BackgroundPressed ?? BackgroundPressed,
            overrides.BackgroundDisabled ?? BackgroundDisabled,
            overrides.Border ?? Border,
            overrides.BorderWidth ?? BorderWidth,
            overrides.CornerRadius ?? CornerRadius,
            overrides.TextColour ?? TextColour,
            overrides.TextColourPressed ?? TextColourPressed,
            overrides.FontFamily ?? FontFamily,
            overrides.FontSize ?? FontSize,
            overrides.FontWeight ?? FontWeight,
            overrides.LabelPosition ?? LabelPosition,
            overrides.IconSize ?? IconSize,
            overrides.IconColour ?? IconColour,
            overrides.Padding ?? Padding);
}

/// <summary>A press animation with every value supplied.</summary>
public sealed record ResolvedPressStyle(double Scale, int DurationMs, Easing Easing);

/// <summary>A page transition with every value supplied.</summary>
public sealed record ResolvedTransitionStyle(int PageChangeMs, TransitionStyleKind Style);
