using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Flips a remembered value between true and false. Anything not yet set counts as false,
/// so the first press turns it on.
/// <code>
/// { "type": "toggleVariable", "name": "streaming" }
/// </code>
/// </summary>
public sealed class ToggleVariableAction : IAction
{
    /// <inheritdoc />
    public string Type => "toggleVariable";

    /// <inheritdoc />
    public string Title => "Flip a value";

    /// <inheritdoc />
    public string Description => "Turns a remembered value on, or off if it was on.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("name", ActionParameterKind.Text, "Which value to flip."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var name = VariableName.Of(ctx.RequireString("name"));
        var now = ctx.Variables.Toggle(name);

        ctx.Logger.Debug("{Name} is now {Value}", name, now);
        return Task.CompletedTask;
    }
}
