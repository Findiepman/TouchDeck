using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Clicks, holds or releases a mouse button, optionally moving the pointer first.
/// <code>
/// { "type": "mouse", "button": "left", "action": "click", "x": 400, "y": 300 }
/// { "type": "mouse", "button": "right", "action": "click", "x": 20, "y": 0, "relative": true }
/// </code>
/// </summary>
public sealed class MouseAction : IAction
{
    /// <inheritdoc />
    public string Type => "mouse";

    /// <inheritdoc />
    public string Title => "Click the mouse";

    /// <inheritdoc />
    public string Description => "Clicks, holds or releases a mouse button, moving there first if asked.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.OneOf("button", "Which button.", false, ActionContext.ChoiceList<MouseButton>()),
        ActionParameter.OneOf("action", "Click, or hold and release separately.", false, ActionContext.ChoiceList<PressAction>()),
        ActionParameter.Optional("x", ActionParameterKind.Number, "Where to move to first. Leave both empty to click where the pointer already is."),
        ActionParameter.Optional("y", ActionParameterKind.Number, "Where to move to first."),
        ActionParameter.Optional("relative", ActionParameterKind.Boolean, "Move by this much rather than to this position.", "false"),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var injector = ctx.Service<IInputInjector>();

        var hasX = ctx.Action.TryGetParameter("x", out _);
        var hasY = ctx.Action.TryGetParameter("y", out _);

        if (hasX || hasY)
        {
            injector.MoveMouse(
                ctx.Action.GetInt32("x"),
                ctx.Action.GetInt32("y"),
                ctx.Action.GetBoolean("relative"));
        }

        injector.MouseButton(
            ctx.RequireOneOf("button", MouseButton.Left),
            ctx.RequireOneOf("action", PressAction.Click));

        return Task.CompletedTask;
    }
}
