using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Turns the scroll wheel wherever the pointer is.
/// <code>
/// { "type": "scroll", "direction": "down", "amount": 3 }
/// </code>
/// </summary>
public sealed class ScrollAction : IAction
{
    /// <inheritdoc />
    public string Type => "scroll";

    /// <inheritdoc />
    public string Title => "Scroll";

    /// <inheritdoc />
    public string Description => "Turns the scroll wheel wherever the pointer is.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.OneOf("direction", "Which way to scroll.", false, ActionContext.ChoiceList<ScrollDirection>()),
        ActionParameter.Optional("amount", ActionParameterKind.Number, "How many notches.", "1"),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        ctx.Service<IInputInjector>().Scroll(
            ctx.Action.GetInt32("amount", 1),
            ctx.RequireOneOf("direction", ScrollDirection.Down));

        return Task.CompletedTask;
    }
}
