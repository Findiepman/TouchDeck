using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Starts a program, document or shell target.
/// <code>
/// { "type": "launch", "path": "notepad.exe", "args": "notes.txt", "workingDir": "C:\\Temp" }
/// </code>
/// </summary>
public sealed class LaunchAction : IAction
{
    /// <inheritdoc />
    public string Type => "launch";

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var request = new LaunchRequest(
            ctx.RequireString("path"),
            ctx.Action.GetString("args"),
            ctx.Action.GetString("workingDir"));

        try
        {
            ctx.Service<IProcessLauncher>().Launch(request);
        }
        catch (InvalidOperationException ex)
        {
            throw new ActionException(ex.Message, ex);
        }

        return Task.CompletedTask;
    }
}
