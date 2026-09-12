using TouchDeck.Core.Abstractions;
using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Focuses, minimises, maximises or closes another application's window.
/// <code>
/// { "type": "window", "operation": "focus", "processName": "obs64.exe" }
/// { "type": "window", "operation": "close", "titleRegex": "^Untitled" }
/// </code>
/// </summary>
public sealed class WindowAction : IAction
{
    /// <inheritdoc />
    public string Type => "window";

    /// <inheritdoc />
    public string Title => "Control a window";

    /// <inheritdoc />
    public string Description => "Focuses, minimises, maximises or closes another application's window.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.OneOf("operation", "What to do with it.", true, ActionContext.ChoiceList<WindowOperation>()),
        ActionParameter.Optional("processName", ActionParameterKind.Text, "Which program, such as obs64.exe."),
        ActionParameter.Optional("titleRegex", ActionParameterKind.Text, "A pattern the window title must match."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var processName = ctx.Action.GetString("processName");
        var titleRegex = ctx.Action.GetString("titleRegex");

        if (processName is null && titleRegex is null)
        {
            throw new ActionException(
                "This button needs to know which window it means. Give it a processName, a titleRegex, or both.");
        }

        var operation = ctx.RequireOneOf<WindowOperation>("operation");
        var found = ctx.Service<IWindowManager>().Apply(operation, new WindowMatch(processName, titleRegex));

        if (!found)
        {
            throw new ActionException(
                $"No open window matched {Describe(processName, titleRegex)}, so there was nothing to {operation.ToString().ToLowerInvariant()}.");
        }

        return Task.CompletedTask;
    }

    private static string Describe(string? processName, string? titleRegex) =>
        (processName, titleRegex) switch
        {
            ({ } name, null) => $"\"{name}\"",
            (null, { } title) => $"a title like \"{title}\"",
            ({ } name, { } title) => $"\"{name}\" with a title like \"{title}\"",
            _ => "that description",
        };
}
