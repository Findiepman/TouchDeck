using TouchDeck.Core.Configuration;

namespace TouchDeck.App.Configurator;

/// <summary>An editable named theme.</summary>
public sealed class ThemeEditModel : ObservableObject
{
    private string _name;
    private string? _inherits;
    private string? _background;
    private double? _gap;
    private double? _padding;
    private double? _pressScale;
    private int? _pressDurationMs;
    private Easing? _pressEasing;
    private int? _pageChangeMs;
    private TransitionStyleKind? _transitionStyle;

    /// <summary>Creates an editable copy of a theme.</summary>
    /// <param name="name">The theme's name, which is its key in themes.json.</param>
    /// <param name="theme">The theme as configured.</param>
    public ThemeEditModel(string name, Theme theme)
    {
        _name = name;
        _inherits = theme.Inherits;
        _background = theme.Background;
        _gap = theme.Gap;
        _padding = theme.Padding;
        _pressScale = theme.Press?.Scale;
        _pressDurationMs = theme.Press?.DurationMs;
        _pressEasing = theme.Press?.Easing;
        _pageChangeMs = theme.Transition?.PageChangeMs;
        _transitionStyle = theme.Transition?.Style;

        Button = new StyleEditModel(theme.Button);
        Adopt(Button);
    }

    /// <summary>The theme wide button appearance.</summary>
    public StyleEditModel Button { get; }

    /// <summary>Every easing curve, for the dropdown.</summary>
    public static IReadOnlyList<Easing?> Easings { get; } =
        new Easing?[] { null }.Concat(Enum.GetValues<Easing>().Cast<Easing?>()).ToArray();

    /// <summary>Every transition style, for the dropdown.</summary>
    public static IReadOnlyList<TransitionStyleKind?> TransitionStyles { get; } =
        new TransitionStyleKind?[] { null }.Concat(Enum.GetValues<TransitionStyleKind>().Cast<TransitionStyleKind?>()).ToArray();

    /// <summary>The theme's name, referenced by profiles.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
            {
                Raise(nameof(DisplayName));
            }
        }
    }

    /// <summary>Another theme to take unset values from.</summary>
    public string? Inherits
    {
        get => _inherits;
        set => Set(ref _inherits, string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>The colour behind the whole grid.</summary>
    public string? Background
    {
        get => _background;
        set => Set(ref _background, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    /// <summary>Space between cells.</summary>
    public double? Gap
    {
        get => _gap;
        set => Set(ref _gap, value);
    }

    /// <summary>Space between the grid and the screen edge.</summary>
    public double? Padding
    {
        get => _padding;
        set => Set(ref _padding, value);
    }

    /// <summary>How far a button shrinks while held. 1 disables the shrink.</summary>
    public double? PressScale
    {
        get => _pressScale;
        set => Set(ref _pressScale, value);
    }

    /// <summary>How long the press animation takes.</summary>
    public int? PressDurationMs
    {
        get => _pressDurationMs;
        set => Set(ref _pressDurationMs, value);
    }

    /// <summary>The curve the press animation follows.</summary>
    public Easing? PressEasing
    {
        get => _pressEasing;
        set => Set(ref _pressEasing, value);
    }

    /// <summary>How long a page change takes.</summary>
    public int? PageChangeMs
    {
        get => _pageChangeMs;
        set => Set(ref _pageChangeMs, value);
    }

    /// <summary>How one page replaces another.</summary>
    public TransitionStyleKind? TransitionStyle
    {
        get => _transitionStyle;
        set => Set(ref _transitionStyle, value);
    }

    /// <summary>What to show in the tree.</summary>
    public string DisplayName => _name;

    /// <summary>Builds the config record.</summary>
    public Theme ToConfig()
    {
        var press = new PressStyle
        {
            Scale = _pressScale,
            DurationMs = _pressDurationMs,
            Easing = _pressEasing,
        };

        var transition = new TransitionStyle
        {
            PageChangeMs = _pageChangeMs,
            Style = _transitionStyle,
        };

        return new Theme
        {
            Inherits = _inherits,
            Background = _background,
            Gap = _gap,
            Padding = _padding,
            Button = Button.ToConfig(),
            Press = press == new PressStyle() ? null : press,
            Transition = transition == new TransitionStyle() ? null : transition,
        };
    }
}
