using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Variables;

namespace TouchDeck.Actions;

/// <summary>What a clipboard action does.</summary>
public enum ClipboardOperation
{
    /// <summary>Put a value on the clipboard.</summary>
    Set,

    /// <summary>Read the clipboard into a variable.</summary>
    Get,

    /// <summary>Empty the clipboard.</summary>
    Clear,
}

/// <summary>
/// Reads or writes the clipboard.
/// <code>
/// { "type": "clipboard", "operation": "set", "value": "https://twitch.tv/me" }
/// { "type": "clipboard", "operation": "get", "into": "lastCopied" }
/// </code>
/// </summary>
public sealed class ClipboardAction : IAction
{
    /// <inheritdoc />
    public string Type => "clipboard";

    /// <inheritdoc />
    public string Title => "Use the clipboard";

    /// <inheritdoc />
    public string Description => "Puts something on the clipboard, reads it, or empties it.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.OneOf("operation", "What to do.", true, ActionContext.ChoiceList<ClipboardOperation>()),
        ActionParameter.Optional("value", ActionParameterKind.MultilineText, "What to put there, when setting."),
        ActionParameter.Optional("into", ActionParameterKind.Text, "Name of a variable to read into, when getting.", "clipboard"),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var clipboard = ctx.Service<IClipboard>();

        switch (ctx.RequireOneOf<ClipboardOperation>("operation"))
        {
            case ClipboardOperation.Set:
                clipboard.SetText(ctx.RequireString("value"));
                break;

            case ClipboardOperation.Get:
                var into = VariableName.Of(ctx.Action.GetString("into", "clipboard"));
                ctx.Variables.Set(into, clipboard.GetText() ?? string.Empty, VariableScope.Session);
                break;

            case ClipboardOperation.Clear:
                clipboard.Clear();
                break;
        }

        return Task.CompletedTask;
    }
}
