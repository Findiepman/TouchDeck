using TouchDeck.Core.Actions;
using TouchDeck.Core.Variables;

namespace TouchDeck.Actions;

/// <summary>
/// Remembers a value. Other buttons can show it in a label or test it in a condition.
/// <code>
/// { "type": "setVariable", "name": "mode", "value": "quiet", "scope": "persistent" }
/// </code>
/// </summary>
public sealed class SetVariableAction : IAction
{
    /// <inheritdoc />
    public string Type => "setVariable";

    /// <inheritdoc />
    public string Title => "Remember a value";

    /// <inheritdoc />
    public string Description => "Stores a value that other buttons can show or test.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("name", ActionParameterKind.Text, "What to call it."),
        ActionParameter.Optional("value", ActionParameterKind.Text, "What to set it to. Leave empty to forget it."),
        ActionParameter.OneOf("scope", "Whether it survives a restart.", false, ActionContext.ChoiceList<VariableScope>()),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        ctx.Variables.Set(
            VariableName.Of(ctx.RequireString("name")),
            ctx.Action.GetString("value"),
            ctx.RequireOneOf("scope", VariableScope.Session));

        return Task.CompletedTask;
    }
}
