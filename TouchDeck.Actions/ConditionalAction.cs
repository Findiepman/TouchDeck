using TouchDeck.Core.Actions;
using TouchDeck.Core.Expressions;

namespace TouchDeck.Actions;

/// <summary>
/// Runs one action or another depending on a condition, so one button can do two jobs.
/// <code>
/// { "type": "conditional",
///   "if": "var.mode == \"quiet\"",
///   "then": { "type": "audio", "operation": "unmute" },
///   "else": { "type": "audio", "operation": "mute" } }
/// </code>
/// </summary>
public sealed class ConditionalAction : IAction
{
    /// <inheritdoc />
    public string Type => "conditional";

    /// <inheritdoc />
    public string Title => "Choose between two things";

    /// <inheritdoc />
    public string Description => "Runs one action or another, depending on a condition.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("if", ActionParameterKind.Text, "The condition, such as var.mode == \"quiet\"."),
    };

    /// <inheritdoc />
    public async Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var written = ctx.RequireString("if");

        if (!Expression.TryParse(written, out var condition, out var error))
        {
            throw new ActionException($"\"{written}\" is not a condition this deck understands. {error}");
        }

        var holds = condition.Evaluate(ctx.Values);
        var branch = ctx.Action.GetAction(holds ? "then" : "else");

        ctx.Logger.Debug("Condition {Condition} was {Result}", condition, holds);

        if (branch is null)
        {
            // Having only a "then" is perfectly reasonable; the other way round does nothing.
            return;
        }

        await ctx.RunAsync(branch, ct).ConfigureAwait(false);
    }
}
