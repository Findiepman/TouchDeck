using TouchDeck.Core.Actions;

namespace TouchDeck.Actions;

/// <summary>
/// Opens a folder page, remembering the page you came from so a back button can return.
/// <code>
/// { "type": "openFolder", "page": "scenes" }
/// </code>
/// </summary>
public sealed class OpenFolderAction : IAction
{
    /// <inheritdoc />
    public string Type => "openFolder";

    /// <inheritdoc />
    public string Title => "Open a folder";

    /// <inheritdoc />
    public string Description => "Opens a folder page and remembers where to come back to.";

    /// <inheritdoc />
    public IReadOnlyList<ActionParameter> Parameters { get; } = new[]
    {
        ActionParameter.Require("page", ActionParameterKind.Text, "The id of the folder page to open."),
    };

    /// <inheritdoc />
    public Task ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        ctx.Deck.OpenFolder(ctx.RequireString("page"));
        return Task.CompletedTask;
    }
}
