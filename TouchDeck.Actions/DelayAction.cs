using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Waits. Only useful inside a sequence, to give the thing before it a moment to catch up.
/// <code>
/// { "type": "delay", "ms": 200 }
/// </code>
/// </summary>
public sealed class DelayAction : IAction
{
    /// <inheritdoc />
    public string Type => "delay";

    /// <inheritdoc />
    public string Title => "Wait";

    /// <inheritdoc />
    public string Description => "Pauses inside a sequence, to let the step before it catch up.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Optional("ms", ActionParameterKind.Number, "How long to wait, in milliseconds.", "200"),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct) =>
        Task.Delay(Math.Max(0, ctx.Action.GetInt32("ms", 200)), ct);
}
