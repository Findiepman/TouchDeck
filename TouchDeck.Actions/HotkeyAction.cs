using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;
using TouchDeck.Core.Input;

namespace TouchDeck.Actions;

/// <summary>
/// Sends a key combination to whatever has focus.
/// <code>
/// { "type": "hotkey", "keys": "ctrl+shift+m", "repeat": 1 }
/// </code>
/// </summary>
public sealed class HotkeyAction : IAction
{
    /// <inheritdoc />
    public string Type => "hotkey";

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var keys = ctx.RequireString("keys");

        if (!HotkeyParser.TryParse(keys, out var combo, out var error))
        {
            throw new ActionException($"\"{keys}\" is not a valid key combination. {error}");
        }

        var repeat = Math.Max(1, ctx.Action.GetInt32("repeat", 1));
        var injector = ctx.Service<IInputInjector>();

        for (var i = 0; i < repeat; i++)
        {
            ct.ThrowIfCancellationRequested();
            injector.SendCombo(combo);
        }

        ctx.Logger.Debug("Sent {Combo} {Repeat} time(s)", combo, repeat);
        return Task.CompletedTask;
    }
}
