using System.Text.Json;
using System.Text.Json.Serialization;

namespace TouchDeck.Core.Configuration;

/// <summary>A profile: one grid, one theme and a set of pages.</summary>
public sealed record Profile
{
    /// <summary>Stable identifier, referenced by <c>switchProfile</c> and by the tray menu.</summary>
    public string Id { get; init; } = "";

    /// <summary>Human readable name. Falls back to <see cref="Id"/> when unset.</summary>
    public string? Name { get; init; }

    /// <summary>Name of a theme in <c>themes.json</c>.</summary>
    public string? Theme { get; init; }

    public GridConfig Grid { get; init; } = new();

    public AutoSwitchConfig? AutoSwitch { get; init; }

    public IReadOnlyList<Page> Pages { get; init; } = Array.Empty<Page>();

    /// <summary>Absolute path of the file this profile was loaded from. Not part of the JSON.</summary>
    [JsonIgnore]
    public string SourceFile { get; init; } = "";

    /// <summary>Name shown to the user.</summary>
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

/// <summary>Grid geometry for a profile.</summary>
public sealed record GridConfig
{
    public int Columns { get; init; } = 5;

    public int Rows { get; init; } = 3;

    /// <summary>
    /// Cell width divided by cell height. Defaults to square cells. Ignored when
    /// <see cref="Fill"/> is set.
    /// </summary>
    public double? CellAspect { get; init; }

    /// <summary>Stretch cells to fill the panel instead of honouring <see cref="CellAspect"/>.</summary>
    public bool Fill { get; init; }
}

/// <summary>Rules that make a profile activate itself when a matching window comes forward.</summary>
public sealed record AutoSwitchConfig
{
    public bool Enabled { get; init; }

    /// <summary>Higher wins when several profiles match.</summary>
    public int Priority { get; init; }

    public MatchConfig? Match { get; init; }
}

/// <summary>How a foreground window is matched.</summary>
public sealed record MatchConfig
{
    /// <summary>Executable name, compared case insensitively, for example <c>obs64.exe</c>.</summary>
    public string? ProcessName { get; init; }

    /// <summary>Regular expression matched against the window title.</summary>
    public string? TitleRegex { get; init; }
}

/// <summary>One screen of buttons within a profile.</summary>
public sealed record Page
{
    public string Id { get; init; } = "";

    public string? Name { get; init; }

    /// <summary>Folders are reachable only through <c>openFolder</c> and are skipped by swipe navigation.</summary>
    public bool IsFolder { get; init; }

    /// <summary>Optional button that returns to the page a folder was opened from.</summary>
    public ButtonConfig? BackButton { get; init; }

    public IReadOnlyList<ButtonConfig> Buttons { get; init; } = Array.Empty<ButtonConfig>();
}

/// <summary>A single button. Only the position and one action slot are required.</summary>
public sealed record ButtonConfig
{
    public int Col { get; init; }

    public int Row { get; init; }

    public int ColSpan { get; init; } = 1;

    public int RowSpan { get; init; } = 1;

    /// <summary>Label text. Supports <c>{{...}}</c> interpolation.</summary>
    public string? Label { get; init; }

    public LabelPosition? LabelPosition { get; init; }

    public IconConfig? Icon { get; init; }

    /// <summary>Any subset of the theme button block, overriding it for this button.</summary>
    public ButtonStyle? Style { get; init; }

    /// <summary>Fired on press.</summary>
    public ActionConfig? Action { get; init; }

    /// <summary>Fired on release. Pairs with <see cref="Action"/> for push to talk buttons.</summary>
    public ActionConfig? ReleaseAction { get; init; }

    /// <summary>Fired once the press passes <c>behaviour.longPressMs</c>.</summary>
    public ActionConfig? LongPressAction { get; init; }

    /// <summary>Fired on a second press inside <c>behaviour.doubleTapMs</c>.</summary>
    public ActionConfig? DoubleTapAction { get; init; }

    /// <summary>Binds appearance to a live provider.</summary>
    public StateConfig? State { get; init; }

    /// <summary>Expression deciding whether the button is drawn at all.</summary>
    public string? VisibleWhen { get; init; }

    /// <summary>Expression deciding whether the button can be pressed.</summary>
    public string? EnabledWhen { get; init; }

    /// <summary>Shows an inline confirmation before firing.</summary>
    public ConfirmConfig? Confirm { get; init; }

    /// <summary>Hold to repeat behaviour.</summary>
    public RepeatConfig? Repeat { get; init; }
}

/// <summary>What an icon is drawn from.</summary>
public enum IconKind
{
    None,

    /// <summary>A png or svg relative to the icons folder, or an absolute path.</summary>
    File,

    /// <summary>A Segoe Fluent Icons glyph, given as a character or a hex code point.</summary>
    Glyph,

    /// <summary>Literal text drawn in place of an icon.</summary>
    Text,
}

/// <summary>A button icon.</summary>
public sealed record IconConfig
{
    public IconKind Type { get; init; } = IconKind.None;

    /// <summary>Path, glyph or text, depending on <see cref="Type"/>. Supports interpolation.</summary>
    public string? Value { get; init; }

    public double? Size { get; init; }

    public string? Colour { get; init; }
}

/// <summary>Binds a button's appearance to a live provider value.</summary>
public sealed record StateConfig
{
    /// <summary>Provider key, for example <c>obs.inputMuted</c>.</summary>
    public string Provider { get; init; } = "";

    /// <summary>Arguments passed to the provider.</summary>
    public IReadOnlyDictionary<string, JsonElement> Args { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Appearance per stringified provider value.</summary>
    public IReadOnlyDictionary<string, ButtonStateVisual> Map { get; init; } =
        new Dictionary<string, ButtonStateVisual>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Appearance applied when a provider reports a particular value.</summary>
public sealed record ButtonStateVisual
{
    public string? Label { get; init; }

    public IconConfig? Icon { get; init; }

    public ButtonStyle? Style { get; init; }
}

/// <summary>An inline confirmation prompt.</summary>
public sealed record ConfirmConfig
{
    public string Message { get; init; } = "Are you sure?";
}

/// <summary>Hold to repeat settings.</summary>
public sealed record RepeatConfig
{
    public bool Enabled { get; init; }

    /// <summary>Delay before repeating starts.</summary>
    public int DelayMs { get; init; } = 400;

    /// <summary>Interval between repeats.</summary>
    public int IntervalMs { get; init; } = 100;
}
