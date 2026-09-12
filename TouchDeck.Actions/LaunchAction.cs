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
    public string Title => "Launch program";

    /// <inheritdoc />
    public string Description => "Starts a program, document or shell target.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("path", ActionParameterKind.FilePath, "What to start, such as notepad.exe."),
        ActionParameter.Optional("args", ActionParameterKind.Text, "Command line arguments."),
        ActionParameter.Optional("workingDir", ActionParameterKind.FolderPath, "Folder to start it in."),
    };

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
