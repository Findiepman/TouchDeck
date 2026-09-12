namespace TouchDeck.Core.Actions;

/// <summary>
/// What kind of value a parameter holds. The editor picks its input control from this, so a
/// new action type gets a sensible form without anyone writing one.
/// </summary>
public enum ActionParameterKind
{
    /// <summary>A single line of text.</summary>
    Text,

    /// <summary>Several lines of text, such as a script.</summary>
    MultilineText,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A yes or no.</summary>
    Boolean,

    /// <summary>A key combination, offered with a combo recorder.</summary>
    Keys,

    /// <summary>A path to a file, offered with a browse button.</summary>
    FilePath,

    /// <summary>A path to a folder, offered with a browse button.</summary>
    FolderPath,

    /// <summary>One of a fixed set of values, offered as a dropdown.</summary>
    Choice,
}

/// <summary>
/// One parameter an action accepts, described well enough for an editor to render an input
/// for it and for a human to understand what it does.
/// </summary>
/// <param name="Name">The property name as written in JSON.</param>
/// <param name="Kind">What sort of value it holds.</param>
/// <param name="Description">What the parameter does, in one line.</param>
public sealed record ActionParameter(string Name, ActionParameterKind Kind, string Description)
{
    /// <summary>Whether the action refuses to run without it.</summary>
    public bool Required { get; init; }

    /// <summary>The value used when the parameter is left out, written as it would be in JSON.</summary>
    public string? Default { get; init; }

    /// <summary>The allowed values, for <see cref="ActionParameterKind.Choice"/>.</summary>
    public IReadOnlyList<string> Choices { get; init; } = Array.Empty<string>();

    /// <summary>Creates a required parameter.</summary>
    /// <param name="name">The property name as written in JSON.</param>
    /// <param name="kind">What sort of value it holds.</param>
    /// <param name="description">What the parameter does.</param>
    public static ActionParameter Require(string name, ActionParameterKind kind, string description) =>
        new(name, kind, description) { Required = true };

    /// <summary>Creates an optional parameter.</summary>
    /// <param name="name">The property name as written in JSON.</param>
    /// <param name="kind">What sort of value it holds.</param>
    /// <param name="description">What the parameter does.</param>
    /// <param name="fallback">The value used when it is left out.</param>
    public static ActionParameter Optional(
        string name,
        ActionParameterKind kind,
        string description,
        string? fallback = null) =>
        new(name, kind, description) { Default = fallback };

    /// <summary>Creates a parameter limited to a fixed set of values.</summary>
    /// <param name="name">The property name as written in JSON.</param>
    /// <param name="description">What the parameter does.</param>
    /// <param name="required">Whether the action refuses to run without it.</param>
    /// <param name="choices">The allowed values.</param>
    public static ActionParameter OneOf(
        string name,
        string description,
        bool required,
        params string[] choices) =>
        new(name, ActionParameterKind.Choice, description) { Required = required, Choices = choices };
}
