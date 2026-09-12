using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Runs several actions one after another, which is how one button does a whole routine.
/// <code>
/// { "type": "sequence", "stopOnError": true, "steps": [
///     { "type": "obs", "command": "setScene", "scene": "Live" },
///     { "type": "delay", "ms": 200 },
///     { "type": "obs", "command": "startStream" }
/// ] }
/// </code>
/// </summary>
public sealed class SequenceAction : IAction
{
    /// <inheritdoc />
    public string Type => "sequence";

    /// <inheritdoc />
    public string Title => "Do several things";

    /// <inheritdoc />
    public string Description => "Runs a list of actions one after another.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Optional("stopOnError", ActionParameterKind.Boolean, "Give up when a step fails.", "true"),
    };

    /// <inheritdoc />
    public async Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var steps = ctx.Action.GetActions("steps");

        if (steps.Count == 0)
        {
            throw new ActionException("This sequence has no steps in it, so pressing it does nothing.");
        }

        var stopOnError = ctx.Action.GetBoolean("stopOnError", true);

        for (var i = 0; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var ran = await ctx.RunAsync(steps[i], ct).ConfigureAwait(false);

            if (ran || !stopOnError)
            {
                continue;
            }

            // The step itself has already reported what went wrong, so this only says where.
            ctx.Logger.Warning(
                "Step {Number} of {Total} failed, so the rest of the sequence was skipped.",
                i + 1,
                steps.Count);
            return;
        }
    }
}
