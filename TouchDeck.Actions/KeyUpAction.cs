using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Releases keys that <c>keyDown</c> is holding.
/// <code>
/// { "type": "keyUp", "keys": "f13" }
/// </code>
/// </summary>
public sealed class KeyUpAction : IAction
{
    /// <inheritdoc />
    public string Type => "keyUp";

    /// <inheritdoc />
    public string Title => "Release keys";

    /// <inheritdoc />
    public string Description => "Lets go of keys that a hold action is holding.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("keys", ActionParameterKind.Keys, "The keys to let go of."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        ctx.Service<IInputInjector>().ReleaseCombo(KeyComboReader.Read(ctx, "keys"));
        return Task.CompletedTask;
    }
}
