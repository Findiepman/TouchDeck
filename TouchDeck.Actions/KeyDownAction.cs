using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Input;

namespace TouchDeck.Actions;

/// <summary>
/// Presses keys and leaves them down. Pair it with <c>keyUp</c> on the same button's release
/// to get a push to talk key.
/// <code>
/// { "type": "keyDown", "keys": "f13" }
/// </code>
/// </summary>
public sealed class KeyDownAction : IAction
{
    /// <inheritdoc />
    public string Type => "keyDown";

    /// <inheritdoc />
    public string Title => "Hold keys down";

    /// <inheritdoc />
    public string Description => "Presses keys and keeps them held, for push to talk.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("keys", ActionParameterKind.Keys, "The keys to hold down."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        ctx.Service<IInputInjector>().HoldCombo(KeyComboReader.Read(ctx, "keys"));
        return Task.CompletedTask;
    }
}

/// <summary>Reads a key combination parameter, complaining usefully when it is not one.</summary>
internal static class KeyComboReader
{
    /// <summary>Parses a combination from an action parameter.</summary>
    /// <param name="ctx">The action being run.</param>
    /// <param name="name">The parameter holding the combination.</param>
    public static KeyCombo Read(ActionContext ctx, string name)
    {
        var written = ctx.RequireString(name);

        return HotkeyParser.TryParse(written, out var combo, out var error)
            ? combo
            : throw new ActionException($"\"{written}\" is not a valid key combination. {error}");
    }
}
