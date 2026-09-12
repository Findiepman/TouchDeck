using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Types a piece of text into whatever has focus. Any character works, including ones no key
/// on the keyboard produces, because the text is injected as unicode rather than as keys.
/// <code>
/// { "type": "text", "value": "Thanks for the follow!" }
/// </code>
/// </summary>
public sealed class TextAction : IAction
{
    /// <inheritdoc />
    public string Type => "text";

    /// <inheritdoc />
    public string Title => "Type text";

    /// <inheritdoc />
    public string Description => "Types a piece of text into whatever has focus.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("value", ActionParameterKind.MultilineText, "The text to type."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        ctx.Service<IInputInjector>().TypeText(ctx.RequireString("value"));
        return Task.CompletedTask;
    }
}
