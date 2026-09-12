using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Runs a snippet of AutoHotkey v2, which is the last resort for anything the deck has no
/// action for. Needs AutoHotkey installed.
/// <code>
/// { "type": "ahk", "script": "WinMove 0, 0, 1920, 1080, \"A\"" }
/// </code>
/// </summary>
public sealed class AutoHotkeyAction : IAction
{
    /// <inheritdoc />
    public string Type => "ahk";

    /// <inheritdoc />
    public string Title => "Run AutoHotkey";

    /// <inheritdoc />
    public string Description => "Runs an AutoHotkey v2 snippet, for anything else at all.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("script", ActionParameterKind.MultilineText, "The AutoHotkey v2 script to run."),
    };

    /// <inheritdoc />
    public async Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var runner = ctx.Service<IScriptRunner>();

        if (!runner.IsAutoHotkeyInstalled)
        {
            throw new ActionException(
                "AutoHotkey is not installed, so this button cannot run its script. " +
                "Install AutoHotkey v2 from autohotkey.com and try again.");
        }

        try
        {
            await runner.RunAutoHotkeyAsync(ctx.RequireString("script"), ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new ActionException(ex.Message, ex);
        }
    }
}
